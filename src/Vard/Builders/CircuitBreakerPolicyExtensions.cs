using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão fluentes para configuração de políticas de Circuit Breaker.
    /// </summary>
    public static class CircuitBreakerPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de Circuit Breaker baseada em contagem consecutiva de falhas (Count-based).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="exceptionsAllowedBeforeBreaking">Número de falhas consecutivas permitidas antes de abrir o circuito.</param>
        /// <param name="durationOfBreak">Duração do tempo de repouso em que o circuito permanece aberto antes de permitir teste half-open.</param>
        /// <param name="onBreak">Callback síncrono disparado quando o circuito se abre.</param>
        /// <param name="onReset">Callback síncrono disparado quando o circuito é resetado para Closed.</param>
        /// <param name="onHalfOpen">Callback síncrono disparado quando o circuito transita para HalfOpen.</param>
        /// <param name="onBreakAsync">Callback assíncrono disparado quando o circuito se abre.</param>
        /// <param name="onResetAsync">Callback assíncrono disparado quando o circuito é resetado para Closed.</param>
        /// <param name="onHalfOpenAsync">Callback assíncrono disparado quando o circuito transita para HalfOpen.</param>
        /// <returns>Uma nova instância de <see cref="CircuitBreakerPolicy"/>.</returns>
        public static CircuitBreakerPolicy CircuitBreaker(
            this IPolicyBuilder builder,
            int exceptionsAllowedBeforeBreaking,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new CircuitBreakerPolicy(
                b.Condition,
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                onBreak,
                onBreakAsync,
                onReset,
                onResetAsync,
                onHalfOpen,
                onHalfOpenAsync);
        }

        /// <summary>
        /// Configura uma política avançada de Circuit Breaker baseada em taxa de falhas em janela deslizante (Rate-based).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="failureThreshold">Proporção de falhas (entre 0.0 e 1.0) necessária para romper o circuito.</param>
        /// <param name="samplingDuration">Duração da janela deslizante temporal de amostragem.</param>
        /// <param name="minimumThroughput">Taxa mínima de throughput dentro da janela para permitir avaliação da taxa de falhas.</param>
        /// <param name="durationOfBreak">Duração do tempo de repouso do circuito em estado aberto.</param>
        /// <param name="onBreak">Callback síncrono disparado quando o circuito se abre.</param>
        /// <param name="onReset">Callback síncrono disparado quando o circuito é resetado para Closed.</param>
        /// <param name="onHalfOpen">Callback síncrono disparado quando o circuito transita para HalfOpen.</param>
        /// <param name="onBreakAsync">Callback assíncrono disparado quando o circuito se abre.</param>
        /// <param name="onResetAsync">Callback assíncrono disparado quando o circuito é resetado para Closed.</param>
        /// <param name="onHalfOpenAsync">Callback assíncrono disparado quando o circuito transita para HalfOpen.</param>
        /// <returns>Uma nova instância de <see cref="AdvancedCircuitBreakerPolicy"/>.</returns>
        public static AdvancedCircuitBreakerPolicy AdvancedCircuitBreaker(
            this IPolicyBuilder builder,
            double failureThreshold,
            TimeSpan samplingDuration,
            int minimumThroughput,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new AdvancedCircuitBreakerPolicy(
                b.Condition,
                failureThreshold,
                samplingDuration,
                minimumThroughput,
                durationOfBreak,
                onBreak,
                onBreakAsync,
                onReset,
                onResetAsync,
                onHalfOpen,
                onHalfOpenAsync);
        }
    }
}
