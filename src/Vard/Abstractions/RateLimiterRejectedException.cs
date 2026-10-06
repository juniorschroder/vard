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

        /// <summary>
        /// Inicializa uma nova instância de <see cref="RateLimiterRejectedException"/>.
        /// </summary>
        /// <param name="retryAfter">O tempo sugerido de espera antes de retentar a requisição.</param>
        /// <param name="permitLimit">O limite de permits configurado na política.</param>
        /// <param name="algorithmName">O algoritmo de rate limiting que efetuou a rejeição.</param>
        /// <param name="message">Mensagem de erro descritiva opcional.</param>
        /// <param name="innerException">Exceção interna opcional.</param>
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
