using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para configuração de política de Hedging no IPolicyBuilder.
    /// </summary>
    public static class HedgingPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de Hedging não-genérica com atraso estático entre execuções especulativas.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxHedges">Número máximo de chamadas paralelas simultâneas adicionais.</param>
        /// <param name="hedgingDelay">Atraso temporal antes de disparar o hedge concorrente subsequente.</param>
        /// <param name="onHedgingResult">Callback síncrono opcional disparado ao receber resultado hedged.</param>
        /// <param name="onHedgingResultAsync">Callback assíncrono opcional disparado ao receber resultado hedged.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy"/>.</returns>
        public static HedgingPolicy Hedging(
            this IPolicyBuilder builder,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy(
                b.Condition,
                maxHedges,
                hedgingDelay,
                onHedgingResult,
                onHedgingResultAsync);
        }

        /// <summary>
        /// Configura uma política de Hedging não-genérica com atraso dinâmico calculado por tentativa.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxHedges">Número máximo de chamadas paralelas simultâneas adicionais.</param>
        /// <param name="hedgingDelayProvider">Função que calcula o tempo de espera dado o número do hedge.</param>
        /// <param name="onHedgingResult">Callback síncrono opcional disparado ao receber resultado hedged.</param>
        /// <param name="onHedgingResultAsync">Callback assíncrono opcional disparado ao receber resultado hedged.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy"/>.</returns>
        public static HedgingPolicy Hedging(
            this IPolicyBuilder builder,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy(
                b.Condition,
                maxHedges,
                hedgingDelayProvider,
                onHedgingResult,
                onHedgingResultAsync);
        }

        /// <summary>
        /// Configura uma política tipada de Hedging com atraso estático entre tentativas.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado da ação.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxHedges">Número máximo de chamadas paralelas simultâneas adicionais.</param>
        /// <param name="hedgingDelay">Atraso temporal antes de disparar o hedge concorrente subsequente.</param>
        /// <param name="onHedgingResult">Callback tipado síncrono opcional.</param>
        /// <param name="onHedgingResultAsync">Callback tipado assíncrono opcional.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy{TResult}"/>.</returns>
        public static HedgingPolicy<TResult> Hedging<TResult>(
            this IPolicyBuilder builder,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy<TResult>(
                b.Condition,
                maxHedges,
                hedgingDelay,
                onHedgingResult,
                onHedgingResultAsync);
        }

        /// <summary>
        /// Configura uma política tipada de Hedging com atraso dinâmico calculado por tentativa.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado da ação.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxHedges">Número máximo de chamadas paralelas simultâneas adicionais.</param>
        /// <param name="hedgingDelayProvider">Função que calcula o tempo de espera dado o número do hedge.</param>
        /// <param name="onHedgingResult">Callback tipado síncrono opcional.</param>
        /// <param name="onHedgingResultAsync">Callback tipado assíncrono opcional.</param>
        /// <returns>Uma nova instância de <see cref="HedgingPolicy{TResult}"/>.</returns>
        public static HedgingPolicy<TResult> Hedging<TResult>(
            this IPolicyBuilder builder,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy<TResult>(
                b.Condition,
                maxHedges,
                hedgingDelayProvider,
                onHedgingResult,
                onHedgingResultAsync);
        }
    }
}
