namespace AccountManager.Application.Interfaces.Resilience;

public enum CircuitTarget
{
    DbWrite,
    DbRead,
    Redis
}

public interface ICircuitBreakerRegistry
{
    bool IsOpen(CircuitTarget target);
    Task<T> ExecuteAsync<T>(CircuitTarget target, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task ExecuteAsync(CircuitTarget target, Func<CancellationToken, Task> action, CancellationToken cancellationToken);
    string GetState(CircuitTarget target);
}

public sealed class BrokenCircuitException : Exception
{
    public BrokenCircuitException(string message) : base(message)
    {
    }
}
