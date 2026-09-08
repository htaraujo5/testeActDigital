using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace AccountManager.Integration.Tests;

[Collection("api")]
public sealed class AuthIntegrationTests
{
    private readonly ApiFactoryFixture _fixture;

    public AuthIntegrationTests(ApiFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Token_returns_jwt_for_valid_credentials()
    {
        using var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/token",
            new { username = "admin", password = "Admin@123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenDto>();
        body!.Username.Should().Be("admin");
        body.Role.Should().Be("Admin");
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Token_rejects_invalid_credentials()
    {
        using var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/token",
            new { username = "admin", password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_routes_require_bearer_token()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}/balance");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Operator_can_authenticate()
    {
        using var client = _fixture.CreateClient();
        var token = await _fixture.LoginAsync(client, "operator", "Operator@123");
        token.Should().NotBeNullOrWhiteSpace();
    }
}
