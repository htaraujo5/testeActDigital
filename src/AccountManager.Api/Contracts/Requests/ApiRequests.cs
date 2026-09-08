namespace AccountManager.Api.Contracts.Requests;

public sealed record MoneyRequest(decimal Amount);

public sealed record LoginRequest(string Username, string Password);
