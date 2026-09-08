using System.Text;
using AccountManager.Application.Interfaces.Auth;
using AccountManager.Application.Interfaces.Coordination;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Infrastructure.Auth;
using AccountManager.Infrastructure.Coordination;
using AccountManager.Infrastructure.Hosting;
using AccountManager.Infrastructure.Persistence.Read.Repositories;
using AccountManager.Infrastructure.Persistence.Write.Context;
using AccountManager.Infrastructure.Persistence.Write.Repositories;
using AccountManager.Infrastructure.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace AccountManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var writeCs = configuration.GetConnectionString("WriteDb")
            ?? throw new InvalidOperationException("Connection string 'WriteDb' is missing.");
        var readCs = configuration.GetConnectionString("ReadDb") ?? writeCs;
        var redisCs = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
        }

        services.AddDbContext<WriteDbContext>(options =>
            options.UseNpgsql(writeCs));

        services.AddSingleton<ICircuitBreakerRegistry, PollyCircuitBreakerRegistry>();
        services.AddScoped<IAccountWriteRepository, AccountWriteRepository>();
        services.AddScoped<IAccountReadRepository>(_ => new AccountReadRepository(readCs));
        services.AddScoped<IAuthService, JwtAuthService>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisCs);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<ICoordinationGate, RedisCoordinationGate>();
        services.AddHostedService<DatabaseInitializer>();
        services.AddHostedService<ReadReplicaHealthMonitor>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
