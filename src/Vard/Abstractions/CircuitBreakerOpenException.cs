using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando uma execução é bloqueada porque o Circuit Breaker está aberto ou isolado.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Obtém o estado atual do circuito no momento do bloqueio (<see cref="CircuitState"/>).
        /// </summary>
        public CircuitState State { get; }

        /// <summary>
        /// Obtém o tempo restante antes que o circuito tente transitar para <see cref="CircuitState.HalfOpen"/>, ou <c>null</c> se isolado.
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        /// <summary>
        /// Obtém a última exceção que ocasionou a abertura do circuito, se disponível.
        /// </summary>
        public Exception? LastException { get; }

        /// <summary>
        /// Inicializa uma nova instância de <see cref="CircuitBreakerOpenException"/>.
        /// </summary>
        /// <param name="state">O estado do circuito no momento da rejeição.</param>
        /// <param name="retryAfter">O tempo estimado até que novas tentativas sejam avaliadas.</param>
        /// <param name="lastException">A última exceção que disparou o rompimento do circuito.</param>
        /// <param name="message">Mensagem descritiva opcional.</param>
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
