using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Infrastructure.Persistence.Read.Repositories;
using AccountManager.Infrastructure.Persistence.Write.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
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

    private readonly Dictionary<string, string?> _previousEnv = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        var writeCs = _postgres.GetConnectionString();
        var redisCs = _redis.GetConnectionString();

        // Env vars beat appsettings.json. Required because WebApplication.CreateBuilder
        // reloads appsettings AFTER ConfigureAppConfiguration callbacks from WAF,
        // so in-memory config alone often loses to localhost:5432 in CI.
        SetEnv("ConnectionStrings__WriteDb", writeCs);
        SetEnv("ConnectionStrings__ReadDb", writeCs);
        SetEnv("ConnectionStrings__Redis", redisCs);
        SetEnv("ApiInstanceId", "test-api");
        SetEnv("EnableSwagger", "false");
        SetEnv("Jwt__Issuer", "AccountManager");
        SetEnv("Jwt__Audience", "AccountManager.Api");
        SetEnv("Jwt__SigningKey", "AccountManager_Dev_Signing_Key_ChangeInProd_32+");
        SetEnv("Jwt__ExpirationMinutes", "60");

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:WriteDb", writeCs);
                builder.UseSetting("ConnectionStrings:ReadDb", writeCs);
                builder.UseSetting("ConnectionStrings:Redis", redisCs);

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

                // Last-resort: force DI to use Testcontainers endpoints even if config races.
                builder.ConfigureTestServices(services =>
                {
                    RemoveAll(services, typeof(WriteDbContext));
                    RemoveAll(services, typeof(DbContextOptions<WriteDbContext>));
                    RemoveAll(services, typeof(IAccountReadRepository));
                    RemoveAll(services, typeof(IConnectionMultiplexer));

                    services.AddDbContext<WriteDbContext>(options => options.UseNpgsql(writeCs));
                    services.AddScoped<IAccountReadRepository>(_ => new AccountReadRepository(writeCs));
                    services.AddSingleton<IConnectionMultiplexer>(_ =>
                    {
                        var options = ConfigurationOptions.Parse(redisCs);
                        options.AbortOnConnectFail = false;
                        return ConnectionMultiplexer.Connect(options);
                    });
                });
            });

        using var client = Factory.CreateClient();
        await WaitUntilReadyAsync(client);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        RestoreEnv();
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

    private void SetEnv(string key, string value)
    {
        _previousEnv[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private void RestoreEnv()
    {
        foreach (var (key, value) in _previousEnv)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == serviceType)
            {
                services.RemoveAt(i);
            }
        }
    }

    private static async Task WaitUntilReadyAsync(HttpClient client)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 40; attempt++)
        {
            try
            {
                var live = await client.GetAsync("/health/live");
                if (live.IsSuccessStatusCode)
                {
                    var token = await client.PostAsJsonAsync(
                        "/api/v1/auth/token",
                        new { username = "admin", password = "Admin@123" });
                    if (token.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException(
            "API test host did not become ready in time.",
            lastError);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactoryFixture>;

public sealed record TokenDto(string AccessToken, string TokenType, int ExpiresInSeconds, string Username, string Role);
public sealed record MutationDto(bool IdempotentReplay, decimal Balance, string Outcome, string? ReasonCode);
public sealed record BalanceDto(decimal Balance, string Source);
public sealed record TransactionListDto(Guid AccountId, List<TransactionItemDto> Items, string Source);
public sealed record TransactionItemDto(Guid Id, string Type, decimal Amount, decimal BalanceAfter);
