using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Ponto de entrada estático da API Vard.
    /// </summary>
    public static class VardPolicy
    {
        /// <summary>
        /// Inicia a construção fluente de uma política interceptando exceções do tipo <typeparamref name="TException"/>.
        /// </summary>
        /// <typeparam name="TException">O tipo de exceção a ser interceptado.</typeparam>
        /// <returns>Uma instância de <see cref="IPolicyBuilder"/>.</returns>
        public static IPolicyBuilder Handle<TException>() where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle<TException>();
        }

        /// <summary>
        /// Inicia a construção fluente interceptando exceções do tipo <typeparamref name="TException"/> que satisfazem um predicado.
        /// </summary>
        /// <typeparam name="TException">O tipo de exceção a ser interceptado.</typeparam>
        /// <param name="predicate">Condição de filtro sobre a exceção.</param>
        /// <returns>Uma instância de <see cref="IPolicyBuilder"/>.</returns>
        public static IPolicyBuilder Handle<TException>(Func<TException, bool> predicate) where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle(predicate);
        }

        /// <summary>
        /// Inicia a construção fluente interceptando resultados que satisfazem um predicado de falha.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado retornado.</typeparam>
        /// <param name="predicate">Condição de filtro que define se o resultado representa uma falha.</param>
        /// <returns>Uma instância de <see cref="IPolicyBuilder"/>.</returns>
        public static IPolicyBuilder HandleResult<TResult>(Func<TResult, bool> predicate)
        {
            var builder = new PolicyBuilder();
            return builder.HandleResult(predicate);
        }

        /// <summary>
        /// Cria diretamente uma política de repetição padrão para qualquer exceção com quantidade fixa de tentativas.
        /// </summary>
        /// <param name="count">Número máximo de tentativas de repetição.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/>.</returns>
        public static RetryPolicy Retry(int count)
            => Handle<Exception>().Retry(count);

        /// <summary>
        /// Cria diretamente uma política de repetição com recuo programado (backoff) e jitter opcional para qualquer exceção.
        /// </summary>
        /// <param name="count">Número máximo de tentativas de repetição.</param>
        /// <param name="initialDelay">Atraso temporal inicial da primeira tentativa.</param>
        /// <param name="backoffType">Tipo de algoritmo de recuo temporal (<see cref="BackoffType"/>).</param>
        /// <param name="useJitter">Indica se variação pseudoaleatória (jitter) deve ser aplicada ao atraso.</param>
        /// <returns>Uma nova instância de <see cref="RetryPolicy"/>.</returns>
        public static RetryPolicy RetryWithBackoff(int count, TimeSpan initialDelay, BackoffType backoffType = BackoffType.Fixed, bool useJitter = false)
            => Handle<Exception>().RetryWithBackoff(count, initialDelay, backoffType, useJitter);

        /// <summary>
        /// Cria diretamente uma política de tempo limite com suporte a cancelamento cooperativo e estouro forçado.
        /// </summary>
        /// <param name="timeout">Duração máxima admitida para cada execução.</param>
        /// <param name="onTimeout">Callback síncrono opcional disparado em estouro de tempo.</param>
        /// <param name="onTimeoutAsync">Callback assíncrono opcional disparado em estouro de tempo.</param>
        /// <returns>Uma nova instância de <see cref="TimeoutPolicy"/>.</returns>
        public static TimeoutPolicy Timeout(
            TimeSpan timeout,
            Action<IDictionary<string, object>?, TimeSpan>? onTimeout = null,
            Func<IDictionary<string, object>?, TimeSpan, Task>? onTimeoutAsync = null)
            => new TimeoutPolicy(timeout, onTimeout, onTimeoutAsync);

        /// <summary>
        /// Cria diretamente uma política de contingência tipada retornando um valor substituto padrão para qualquer exceção.
        /// </summary>
        /// <typeparam name="TResult">O tipo do valor de retorno.</typeparam>
        /// <param name="fallbackValue">O valor constante a ser retornado em caso de falha.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy{TResult}"/>.</returns>
        public static FallbackPolicy<TResult> Fallback<TResult>(TResult fallbackValue)
            => Handle<Exception>().Fallback(fallbackValue);

        /// <summary>
        /// Cria diretamente uma política de contingência para ações sem retorno executando uma ação substituta.
        /// </summary>
        /// <param name="fallbackAction">Ação síncrona executada em substituição quando ocorrer qualquer exceção.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy"/>.</returns>
        public static FallbackPolicy Fallback(Action fallbackAction)
            => Handle<Exception>().Fallback(fallbackAction);

        /// <summary>
        /// Cria diretamente uma política de Circuit Breaker baseada em contagem de falhas consecutivas.
        /// </summary>
        /// <param name="exceptionsAllowedBeforeBreaking">Número de falhas consecutivas antes de romper o circuito.</param>
        /// <param name="durationOfBreak">Tempo de repouso com circuito aberto.</param>
        /// <param name="onBreak">Callback síncrono disparado na abertura.</param>
        /// <param name="onReset">Callback síncrono disparado no fechamento/reset.</param>
        /// <param name="onHalfOpen">Callback síncrono disparado ao testar estado half-open.</param>
        /// <param name="onBreakAsync">Callback assíncrono disparado na abertura.</param>
        /// <param name="onResetAsync">Callback assíncrono disparado no fechamento/reset.</param>
        /// <param name="onHalfOpenAsync">Callback assíncrono disparado ao testar estado half-open.</param>
        /// <returns>Uma nova instância de <see cref="CircuitBreakerPolicy"/>.</returns>
        public static CircuitBreakerPolicy CircuitBreaker(
            int exceptionsAllowedBeforeBreaking,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
            => Handle<Exception>().CircuitBreaker(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                onBreak,
                onReset,
                onHalfOpen,
                onBreakAsync,
                onResetAsync,
                onHalfOpenAsync);

        /// <summary>
        /// Cria diretamente uma política avançada de Circuit Breaker baseada em taxa de falhas em janela deslizante.
        /// </summary>
        /// <param name="failureThreshold">Proporção de falhas (0.0 a 1.0) para romper o circuito.</param>
        /// <param name="samplingDuration">Janela temporal deslizante de monitoramento.</param>
        /// <param name="minimumThroughput">Volume mínimo de requisições exigido na janela para avaliar a taxa.</param>
        /// <param name="durationOfBreak">Tempo de repouso com circuito aberto.</param>
        /// <param name="onBreak">Callback síncrono disparado na abertura.</param>
        /// <param name="onReset">Callback síncrono disparado no fechamento/reset.</param>
        /// <param name="onHalfOpen">Callback síncrono disparado ao testar estado half-open.</param>
        /// <param name="onBreakAsync">Callback assíncrono disparado na abertura.</param>
        /// <param name="onResetAsync">Callback assíncrono disparado no fechamento/reset.</param>
        /// <param name="onHalfOpenAsync">Callback assíncrono disparado ao testar estado half-open.</param>
        /// <returns>Uma nova instância de <see cref="AdvancedCircuitBreakerPolicy"/>.</returns>
        public static AdvancedCircuitBreakerPolicy AdvancedCircuitBreaker(
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
            => Handle<Exception>().AdvancedCircuitBreaker(
                failureThreshold,
                samplingDuration,
                minimumThroughput,
                durationOfBreak,
                onBreak,
                onReset,
                onHalfOpen,
                onBreakAsync,
                onResetAsync,
                onHalfOpenAsync);

        /// <summary>
        /// Cria diretamente uma política de isolamento Bulkhead controlando limites de concorrência e fila.
        /// </summary>
        /// <param name="maxParallelization">Número máximo de execuções simultâneas.</param>
        /// <param name="maxQueuedActions">Número máximo de chamadas enfileiradas aguardando vaga.</param>
        /// <param name="onBulkheadRejected">Callback síncrono disparado quando o bulkhead rejeita execução.</param>
        /// <param name="onBulkheadRejectedAsync">Callback assíncrono disparado quando o bulkhead rejeita execução.</param>
        /// <returns>Uma nova instância de <see cref="BulkheadPolicy"/>.</returns>
        public static BulkheadPolicy Bulkhead(
            int maxParallelization,
            int maxQueuedActions = 0,
            Action<IDictionary<string, object>?>? onBulkheadRejected = null,
            Func<IDictionary<string, object>?, Task>? onBulkheadRejectedAsync = null)
            => new BulkheadPolicy(maxParallelization, maxQueuedActions, onBulkheadRejected, onBulkheadRejectedAsync);

        /// <summary>
        /// Cria diretamente uma política de rate limiting via algoritmo Token Bucket.
        /// </summary>
        /// <param name="maxTokens">Capacidade total do bucket.</param>
        /// <param name="tokensPerSecond">Taxa de reabastecimento de tokens por segundo.</param>
        /// <param name="maxWaitTime">Tempo limite opcional de espera antes de rejeitar.</param>
        /// <param name="onRateLimitExceeded">Callback síncrono opcional ao exceder limite de taxa.</param>
        /// <param name="onRateLimitExceededAsync">Callback assíncrono opcional ao exceder limite de taxa.</param>
        /// <returns>Uma nova instância de <see cref="TokenBucketRateLimiterPolicy"/>.</returns>
        public static TokenBucketRateLimiterPolicy TokenBucket(
            int maxTokens,
            double tokensPerSecond,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
            => new TokenBucketRateLimiterPolicy(
                maxTokens,
                tokensPerSecond,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);

        /// <summary>
        /// Cria diretamente uma política de rate limiting via Janela Deslizante Segmentada O(1).
        /// </summary>
        /// <param name="permitLimit">Limite de requisições dentro da janela.</param>
        /// <param name="windowDuration">Duração temporal total da janela.</param>
        /// <param name="segmentsPerWindow">Número de partições internas da janela (padrão: 10).</param>
        /// <param name="maxWaitTime">Tempo limite opcional de espera antes de rejeitar.</param>
        /// <param name="onRateLimitExceeded">Callback síncrono opcional ao exceder limite.</param>
        /// <param name="onRateLimitExceededAsync">Callback assíncrono opcional ao exceder limite.</param>
        /// <returns>Uma nova instância de <see cref="SlidingWindowRateLimiterPolicy"/>.</returns>
        public static SlidingWindowRateLimiterPolicy SlidingWindow(
            int permitLimit,
            TimeSpan windowDuration,
            int segmentsPerWindow = 10,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
            => new SlidingWindowRateLimiterPolicy(
                permitLimit,
                windowDuration,
                segmentsPerWindow,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);

        /// <summary>
        /// Cria diretamente uma política de Hedging especulativo com atraso estático entre execuções.
        /// </summary>
        /// <param name="maxHedges">Número máximo de execuções simultâneas hedged.</param>
        /// <param name="hedgingDelay">Atraso temporal antes de despachar o próximo hedge.</param>
        /// <param name="onHedgingResult">Callback síncrono opcional disparado com resultado de qualquer hedge.</param>
        /// <param name="onHedgingResultAsync">Callback assíncrono opcional disparado com resultado de qualquer hedge.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy"/>.</returns>
        public static HedgingPolicy Hedging(
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
            => Handle<Exception>().Hedging(
                maxHedges,
                hedgingDelay,
                onHedgingResult,
                onHedgingResultAsync);

        /// <summary>
        /// Cria diretamente uma política de Hedging especulativo com atraso dinâmico calculado por tentativa.
        /// </summary>
        /// <param name="maxHedges">Número máximo de execuções simultâneas hedged.</param>
        /// <param name="hedgingDelayProvider">Função que calcula o tempo de espera dado o número do hedge.</param>
        /// <param name="onHedgingResult">Callback síncrono opcional disparado com resultado de qualquer hedge.</param>
        /// <param name="onHedgingResultAsync">Callback assíncrono opcional disparado com resultado de qualquer hedge.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy"/>.</returns>
        public static HedgingPolicy Hedging(
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
            => Handle<Exception>().Hedging(
                maxHedges,
                hedgingDelayProvider,
                onHedgingResult,
                onHedgingResultAsync);

        /// <summary>
        /// Compõe múltiplas políticas de resiliência em um pipeline outside-in.
        /// A primeira política é a mais externa e a última é a mais interna.
        /// </summary>
        public static PolicyWrap Wrap(params IPolicy[] policies)
            => PolicyWrapExtensions.Wrap(policies);
    }
}
