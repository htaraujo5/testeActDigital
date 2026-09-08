namespace AccountManager.Gateway.Dtos.Requests;

public sealed record GatewayLoginRequest(string Username, string Password);

public sealed record GatewayMoneyRequest(decimal Amount);
