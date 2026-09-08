using AccountManager.Application.Interfaces.Resilience;
using Polly;
using Polly.CircuitBreaker;

namespace AccountManager.Infrastructure.Resilience;

public sealed class PollyCircuitBreakerRegistry : ICircuitBreakerRegistry
{
    private readonly Dictionary<CircuitTarget, ResiliencePipeline> _pipelines;
    private readonly Dictionary<CircuitTarget, CircuitBreakerStateProvider> _states;

    public PollyCircuitBreakerRegistry()
    {
        _pipelines = new Dictionary<CircuitTarget, ResiliencePipeline>();
        _states = new Dictionary<CircuitTarget, CircuitBreakerStateProvider>();

        foreach (CircuitTarget target in Enum.GetValues<CircuitTarget>())
        {
            var stateProvider = new CircuitBreakerStateProvider();
            _states[target] = stateProvider;

            var pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(20),
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    OnOpened = args =>
                    {
                        stateProvider.State = "open";
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        stateProvider.State = "closed";
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        stateProvider.State = "half-open";
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            _pipelines[target] = pipeline;
            stateProvider.State = "closed";
        }
    }

    public bool IsOpen(CircuitTarget target) =>
        string.Equals(_states[target].State, "open", StringComparison.OrdinalIgnoreCase);

    public string GetState(CircuitTarget target) => _states[target].State;

    public async Task<T> ExecuteAsync<T>(
        CircuitTarget target,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipelines[target].ExecuteAsync(
                async ct => await action(ct),
                cancellationToken);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException ex)
        {
            throw new Application.Interfaces.Resilience.BrokenCircuitException($"Circuit open for {target}: {ex.Message}");
        }
    }

    public async Task ExecuteAsync(
        CircuitTarget target,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync<object?>(target, async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
    }

    private sealed class CircuitBreakerStateProvider
    {
        public string State { get; set; } = "closed";
    }
}
