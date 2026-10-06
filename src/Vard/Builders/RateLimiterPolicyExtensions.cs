using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para configuração de políticas de Rate Limiter.
    /// </summary>
    public static class RateLimiterPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de rate limiting baseada no algoritmo Token Bucket.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxTokens">Capacidade máxima de armazenamento de tokens (burst capacity).</param>
        /// <param name="tokensPerSecond">Taxa contínua de reabastecimento de tokens por segundo.</param>
        /// <param name="maxWaitTime">Tempo limite máximo opcional para aguardar liberação de tokens antes de rejeitar a requisição.</param>
        /// <param name="onRateLimitExceeded">Callback síncrono opcional disparado quando o limite de taxa é excedido.</param>
        /// <param name="onRateLimitExceededAsync">Callback assíncrono opcional disparado quando o limite de taxa é excedido.</param>
        /// <returns>Uma nova instância de <see cref="TokenBucketRateLimiterPolicy"/>.</returns>
        public static TokenBucketRateLimiterPolicy TokenBucketRateLimiter(
            this IPolicyBuilder builder,
            int maxTokens,
            double tokensPerSecond,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new TokenBucketRateLimiterPolicy(
                maxTokens,
                tokensPerSecond,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);
        }

        /// <summary>
        /// Configura uma política de rate limiting baseada em Janela Deslizante Segmentada (Sliding Window Log aproximado com complexidade O(1)).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="permitLimit">Limite máximo de requisições/permits permitidos dentro da janela deslizante.</param>
        /// <param name="windowDuration">Duração temporal total da janela de controle.</param>
        /// <param name="segmentsPerWindow">Quantidade de subintervalos (buckets) nos quais a janela é particionada (padrão: 10).</param>
        /// <param name="maxWaitTime">Tempo limite máximo opcional para aguardar permissão antes de rejeitar.</param>
        /// <param name="onRateLimitExceeded">Callback síncrono opcional disparado em rejeições.</param>
        /// <param name="onRateLimitExceededAsync">Callback assíncrono opcional disparado em rejeições.</param>
        /// <returns>Uma nova instância de <see cref="SlidingWindowRateLimiterPolicy"/>.</returns>
        public static SlidingWindowRateLimiterPolicy SlidingWindowRateLimiter(
            this IPolicyBuilder builder,
            int permitLimit,
            TimeSpan windowDuration,
            int segmentsPerWindow = 10,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new SlidingWindowRateLimiterPolicy(
                permitLimit,
                windowDuration,
                segmentsPerWindow,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);
        }
    }
}
