using AccountManager.Gateway.Clients;
using AccountManager.Gateway.Mapping;
using AccountManager.Gateway.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<UpstreamApiOptions>(builder.Configuration.GetSection(UpstreamApiOptions.SectionName));
builder.Services.AddSingleton<RoundRobinBaseAddressSelector>();
builder.Services.AddTransient<RoundRobinDelegatingHandler>();
builder.Services.AddAutoMapper(typeof(GatewayMappingProfile));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Account Manager Gateway (BFF)",
        Version = "v1",
        Description = "BFF com DTOs próprios, AutoMapper e RoundRobin para api-1/api-2."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe: Bearer {seu_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddHttpClient<IAccountApiClient, AccountApiClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        // Absolute URI is set by RoundRobinDelegatingHandler.
        client.BaseAddress = new Uri("http://upstream.invalid/");
    })
    .AddHttpMessageHandler<RoundRobinDelegatingHandler>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        return Task.CompletedTask;
    });
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "AccountManager.Gateway",
    mode = "BFF",
    message = "Controllers + AutoMapper + RoundRobin HttpClient"
}));

app.Run();
