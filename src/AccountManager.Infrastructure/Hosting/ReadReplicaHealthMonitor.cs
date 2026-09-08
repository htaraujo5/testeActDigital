using AccountManager.Application.Interfaces.Coordination;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AccountManager.Infrastructure.Hosting;

public sealed class ReadReplicaHealthMonitor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReadReplicaHealthMonitor> _logger;

    public ReadReplicaHealthMonitor(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<ReadReplicaHealthMonitor> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var readCs = _configuration.GetConnectionString("ReadDb")
            ?? _configuration.GetConnectionString("WriteDb")
            ?? throw new InvalidOperationException("ReadDb connection string is missing.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(readCs);
                await connection.OpenAsync(stoppingToken);
                await connection.ExecuteScalarAsync(new CommandDefinition("SELECT 1", cancellationToken: stoppingToken));

                using var scope = _services.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<ICoordinationGate>();
                await gate.SetReadReplicaHealthyAsync(true, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Read replica monitor failed; closing gate.");
                try
                {
                    using var scope = _services.CreateScope();
                    var gate = scope.ServiceProvider.GetRequiredService<ICoordinationGate>();
                    await gate.SetReadReplicaHealthyAsync(false, stoppingToken);
                }
                catch
                {
                    // ignored
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
