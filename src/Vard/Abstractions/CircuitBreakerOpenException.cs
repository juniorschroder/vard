using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando uma execução é bloqueada porque o Circuit Breaker está aberto ou isolado.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitState State { get; }
        public TimeSpan? RetryAfter { get; }
        public Exception? LastException { get; }

        public CircuitBreakerOpenException(
            CircuitState state,
            TimeSpan? retryAfter = null,
            Exception? lastException = null,
            string? message = null)
            : base(message ?? $"The circuit is {state} and not accepting executions." + (retryAfter.HasValue ? $" Retry after {retryAfter.Value.TotalMilliseconds}ms." : string.Empty), lastException)
        {
            State = state;
            RetryAfter = retryAfter;
            LastException = lastException;
        }
    }
}
