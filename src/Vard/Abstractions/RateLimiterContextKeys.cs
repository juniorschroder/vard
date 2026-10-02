namespace Vard.Abstractions
{
    /// <summary>
    /// Chaves de contexto pré-definidas para controle de políticas de Rate Limiter.
    /// </summary>
    public static class RateLimiterContextKeys
    {
        /// <summary>
        /// Chave de contexto para especificar a quantidade de permits consumidos pela requisição (custo variável).
        /// O valor esperado é um <see cref="int"/> maior que zero.
        /// </summary>
        public const string Cost = "Vard.RateLimit.Cost";
    }
}
