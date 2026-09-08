using AccountManager.Infrastructure.Persistence.Write.Context;
using AccountManager.Infrastructure.Persistence.Write.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccountManager.Infrastructure.Hosting;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceProvider services, ILogger<DatabaseInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var write = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await write.Database.EnsureCreatedAsync(cancellationToken);
                await EnsureUsersTableAsync(write, cancellationToken);
                await SeedUsersAsync(write, cancellationToken);
                _logger.LogInformation("Write database ensured successfully.");
                return;
            }
            catch (Exception ex) when (attempt < 20)
            {
                _logger.LogWarning(ex, "Waiting for write database (attempt {Attempt}/20)...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureUsersTableAsync(WriteDbContext write, CancellationToken cancellationToken)
    {
        await write.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS users (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Username" character varying(64) NOT NULL,
                "PasswordHash" text NOT NULL,
                "Role" character varying(32) NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_users_Username" ON users ("Username");
            """,
            cancellationToken);
    }

    private async Task SeedUsersAsync(WriteDbContext write, CancellationToken cancellationToken)
    {
        var hasher = new PasswordHasher<UserModel>();
        try
        {
            await EnsureUserAsync(write, hasher, "admin", "Admin@123", "Admin", cancellationToken);
            await EnsureUserAsync(write, hasher, "operator", "Operator@123", "Operator", cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Auth users seeded (admin/operator).");
        }
        catch (DbUpdateException ex)
        {
            write.ChangeTracker.Clear();
            _logger.LogInformation(ex, "Auth user seed skipped due to concurrent insert (already seeded).");
        }
    }

    private static async Task EnsureUserAsync(
        WriteDbContext write,
        PasswordHasher<UserModel> hasher,
        string username,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        var exists = await write.Users.AnyAsync(x => x.Username == username, cancellationToken);
        if (exists)
        {
            return;
        }

        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            Username = username,
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        write.Users.Add(user);
    }
}
