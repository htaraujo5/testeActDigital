using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AccountManager.Integration.Tests;

public sealed class ApiFactoryFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("account_manager")
        .WithUsername("account")
        .WithPassword("account")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        var writeCs = _postgres.GetConnectionString();
        var redisCs = _redis.GetConnectionString();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:WriteDb"] = writeCs,
                        ["ConnectionStrings:ReadDb"] = writeCs,
                        ["ConnectionStrings:Redis"] = redisCs,
                        ["ApiInstanceId"] = "test-api",
                        ["EnableSwagger"] = "false",
                        ["Jwt:Issuer"] = "AccountManager",
                        ["Jwt:Audience"] = "AccountManager.Api",
                        ["Jwt:SigningKey"] = "AccountManager_Dev_Signing_Key_ChangeInProd_32+",
                        ["Jwt:ExpirationMinutes"] = "60"
                    });
                });
            });

        // Allow hosted DB initializer/seed to finish.
        using var warmup = Factory.CreateClient();
        await Task.Delay(4000);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task<string> LoginAsync(HttpClient client, string username = "admin", string password = "Admin@123")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/token",
            new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenDto>();
        token!.AccessToken.Should().NotBeNullOrWhiteSpace();
        return token.AccessToken;
    }

    public async Task AuthenticateAsync(HttpClient client, string username = "admin", string password = "Admin@123")
    {
        var accessToken = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactoryFixture>;

public sealed record TokenDto(string AccessToken, string TokenType, int ExpiresInSeconds, string Username, string Role);
public sealed record MutationDto(bool IdempotentReplay, decimal Balance, string Outcome, string? ReasonCode);
public sealed record BalanceDto(decimal Balance, string Source);
public sealed record TransactionListDto(Guid AccountId, List<TransactionItemDto> Items, string Source);
public sealed record TransactionItemDto(Guid Id, string Type, decimal Amount, decimal BalanceAfter);
