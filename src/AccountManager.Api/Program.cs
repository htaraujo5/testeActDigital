using AccountManager.Application;
using AccountManager.Infrastructure;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApiInstanceId", ctx.Configuration["ApiInstanceId"] ?? Environment.MachineName)
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Account Manager API",
        Version = "v1",
        Description = "JWT auth. Demo users: admin/Admin@123 (Admin), operator/Operator@123 (Operator)."
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

var writeCs = builder.Configuration.GetConnectionString("WriteDb")!;
var readCs = builder.Configuration.GetConnectionString("ReadDb") ?? writeCs;
var redisCs = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddHealthChecks()
    .AddNpgSql(writeCs, name: "db-write")
    .AddNpgSql(readCs, name: "db-read")
    .AddRedis(redisCs, name: "redis");

var app = builder.Build();
var apiInstanceId = app.Configuration["ApiInstanceId"] ?? Environment.MachineName;

app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        context.Response.Headers["X-Api-Instance"] = apiInstanceId;
        return Task.CompletedTask;
    });

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    using (Serilog.Context.LogContext.PushProperty("ApiInstanceId", apiInstanceId))
    {
        context.Items["CorrelationId"] = correlationId;
        await next();
    }
});

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live", instance = apiInstanceId })).AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapMetrics().AllowAnonymous();

app.Run();

public partial class Program;
