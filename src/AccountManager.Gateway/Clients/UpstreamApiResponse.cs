namespace AccountManager.Gateway.Clients;

public sealed class UpstreamApiResponse<T>
{
    public required int StatusCode { get; init; }
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
    public T? Body { get; init; }
    public object? ErrorBody { get; init; }
    public string? ApiInstance { get; init; }
    public string? CorrelationId { get; init; }
}
