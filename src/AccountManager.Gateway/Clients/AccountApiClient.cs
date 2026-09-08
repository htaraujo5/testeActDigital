using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AccountManager.Gateway.Clients.Upstream;
using AccountManager.Gateway.Dtos.Requests;

namespace AccountManager.Gateway.Clients;

public interface IAccountApiClient
{
    Task<UpstreamApiResponse<UpstreamTokenResponse>> AuthenticateAsync(
        GatewayLoginRequest request,
        CancellationToken cancellationToken);

    Task<UpstreamApiResponse<UpstreamMoneyResponse>> CreditAsync(
        Guid accountId,
        GatewayMoneyRequest request,
        string? authorization,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<UpstreamApiResponse<UpstreamMoneyResponse>> DebitAsync(
        Guid accountId,
        GatewayMoneyRequest request,
        string? authorization,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<UpstreamApiResponse<UpstreamBalanceResponse>> GetBalanceAsync(
        Guid accountId,
        string? authorization,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<UpstreamApiResponse<UpstreamTransactionListResponse>> GetTransactionsAsync(
        Guid accountId,
        int take,
        string? authorization,
        string? correlationId,
        CancellationToken cancellationToken);
}

public sealed class AccountApiClient : IAccountApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public AccountApiClient(HttpClient http) => _http = http;

    public Task<UpstreamApiResponse<UpstreamTokenResponse>> AuthenticateAsync(
        GatewayLoginRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<UpstreamTokenResponse>(
            HttpMethod.Post,
            "api/v1/auth/token",
            request,
            authorization: null,
            idempotencyKey: null,
            correlationId: null,
            cancellationToken);

    public Task<UpstreamApiResponse<UpstreamMoneyResponse>> CreditAsync(
        Guid accountId,
        GatewayMoneyRequest request,
        string? authorization,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken) =>
        SendAsync<UpstreamMoneyResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{accountId}/credits",
            request,
            authorization,
            idempotencyKey,
            correlationId,
            cancellationToken);

    public Task<UpstreamApiResponse<UpstreamMoneyResponse>> DebitAsync(
        Guid accountId,
        GatewayMoneyRequest request,
        string? authorization,
        string idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken) =>
        SendAsync<UpstreamMoneyResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{accountId}/debits",
            request,
            authorization,
            idempotencyKey,
            correlationId,
            cancellationToken);

    public Task<UpstreamApiResponse<UpstreamBalanceResponse>> GetBalanceAsync(
        Guid accountId,
        string? authorization,
        string? correlationId,
        CancellationToken cancellationToken) =>
        SendAsync<UpstreamBalanceResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{accountId}/balance",
            body: null,
            authorization,
            idempotencyKey: null,
            correlationId,
            cancellationToken);

    public Task<UpstreamApiResponse<UpstreamTransactionListResponse>> GetTransactionsAsync(
        Guid accountId,
        int take,
        string? authorization,
        string? correlationId,
        CancellationToken cancellationToken) =>
        SendAsync<UpstreamTransactionListResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{accountId}/transactions?take={take}",
            body: null,
            authorization,
            idempotencyKey: null,
            correlationId,
            cancellationToken);

    private async Task<UpstreamApiResponse<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? authorization,
        string? idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            if (AuthenticationHeaderValue.TryParse(authorization, out var header))
            {
                request.Headers.Authorization = header;
            }
            else
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        T? payload = default;
        object? errorBody = null;

        if (!string.IsNullOrWhiteSpace(content))
        {
            if (response.IsSuccessStatusCode)
            {
                payload = JsonSerializer.Deserialize<T>(content, JsonOptions);
            }
            else
            {
                try
                {
                    errorBody = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                }
                catch (JsonException)
                {
                    errorBody = new { message = content };
                }
            }
        }

        response.Headers.TryGetValues("X-Api-Instance", out var instances);
        response.Headers.TryGetValues("X-Correlation-Id", out var correlations);

        return new UpstreamApiResponse<T>
        {
            StatusCode = (int)response.StatusCode,
            Body = payload,
            ErrorBody = errorBody,
            ApiInstance = instances?.FirstOrDefault(),
            CorrelationId = correlations?.FirstOrDefault()
        };
    }
}
