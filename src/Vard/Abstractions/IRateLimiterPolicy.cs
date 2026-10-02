namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato para políticas de controle de taxa de requisições (Rate Limiter).
    /// </summary>
    public interface IRateLimiterPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        /// <summary>
        /// Limite máximo de permits permitidos na janela ou capacidade do bucket.
        /// </summary>
        int PermitLimit { get; }

        /// <summary>
        /// Quantidade de permits disponíveis para consumo imediato.
        /// </summary>
        int AvailablePermits { get; }

        /// <summary>
        /// Nome do algoritmo de rate limiting empregado (ex: "TokenBucket", "SlidingWindow").
        /// </summary>
        string AlgorithmName { get; }
    }
}
