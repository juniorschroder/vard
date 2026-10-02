using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando uma requisição é rejeitada por exceder a taxa limite configurada.
    /// </summary>
    public class RateLimiterRejectedException : Exception
    {
        /// <summary>
        /// Tempo sugerido de espera antes de retentar a requisição.
        /// </summary>
        public TimeSpan RetryAfter { get; }

        /// <summary>
        /// Limite de permits configurado na política.
        /// </summary>
        public int PermitLimit { get; }

        /// <summary>
        /// Algoritmo do rate limiter que efetuou a rejeição.
        /// </summary>
        public string AlgorithmName { get; }

        public RateLimiterRejectedException(
            TimeSpan retryAfter,
            int permitLimit,
            string algorithmName,
            string? message = null,
            Exception? innerException = null)
            : base(message ?? $"The rate limit of {permitLimit} was exceeded for algorithm '{algorithmName}'. Retry after {retryAfter}.", innerException)
        {
            RetryAfter = retryAfter;
            PermitLimit = permitLimit;
            AlgorithmName = algorithmName;
        }
    }
}
