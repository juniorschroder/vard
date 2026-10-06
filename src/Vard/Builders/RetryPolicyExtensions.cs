using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Common;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão fluentes para configuração da política de repetição (<see cref="RetryPolicy"/>).
    /// </summary>
    public static class RetryPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de repetição com quantidade fixa de tentativas sem atraso.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="retryCount">O número máximo de repetições permitidas.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/>.</returns>
        public static RetryPolicy Retry(this IPolicyBuilder builder, int retryCount)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount);
        }

        /// <summary>
        /// Configura uma política de repetição com callback síncrono disparado a cada tentativa.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="retryCount">O número máximo de repetições permitidas.</param>
        /// <param name="onRetry">Callback disparado a cada repetição com a exceção ou resultado, número da tentativa, atraso e contexto.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/>.</returns>
        public static RetryPolicy Retry(
            this IPolicyBuilder builder,
            int retryCount,
            Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?> onRetry)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount, onRetrySync: onRetry);
        }

        /// <summary>
        /// Configura uma política de repetição com provedor customizado de atraso entre tentativas.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="retryCount">O número máximo de repetições permitidas.</param>
        /// <param name="sleepDurationProvider">Função que calcula o tempo de espera dado o número da tentativa atual.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/>.</returns>
        public static RetryPolicy Retry(
            this IPolicyBuilder builder,
            int retryCount,
            Func<int, TimeSpan> sleepDurationProvider)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount, sleepDurationProvider: (attempt, _) => sleepDurationProvider(attempt));
        }

        /// <summary>
        /// Configura uma política de repetição com cálculo automático de backoff e jitter opcional.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="retryCount">O número máximo de repetições permitidas.</param>
        /// <param name="initialDelay">Atraso base inicial antes da primeira repetição.</param>
        /// <param name="backoffType">O algoritmo de progressão do atraso (<see cref="BackoffType"/>).</param>
        /// <param name="useJitter">Se <c>true</c>, adiciona variação pseudoaleatória (Full Jitter) para evitar synchronized retry storm.</param>
        /// <param name="onRetry">Callback síncrono opcional a cada repetição.</param>
        /// <param name="onRetryAsync">Callback assíncrono opcional a cada repetição.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/> com backoff configurado.</returns>
        public static RetryPolicy RetryWithBackoff(
            this IPolicyBuilder builder,
            int retryCount,
            TimeSpan initialDelay,
            BackoffType backoffType = BackoffType.Fixed,
            bool useJitter = false,
            Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?>? onRetry = null,
            Func<Exception?, object?, int, TimeSpan, IDictionary<string, object>?, Task>? onRetryAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(
                b.Condition,
                retryCount,
                sleepDurationProvider: (attempt, _) => BackoffHelper.CalculateDelay(attempt, initialDelay, backoffType, useJitter),
                onRetrySync: onRetry,
                onRetryAsync: onRetryAsync);
        }
    }
}
