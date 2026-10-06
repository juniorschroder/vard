using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão fluentes para configuração de políticas de contingência (<see cref="FallbackPolicy"/>).
    /// </summary>
    public static class FallbackPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de fallback que retorna um valor estático constante quando a execução original falhar.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado esperado.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackValue">O valor de contingência a ser retornado em caso de falha.</param>
        /// <param name="onFallback">Callback síncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy{TResult}"/>.</returns>
        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            TResult fallbackValue,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (_, _, _) => fallbackValue, onFallbackSync: onFallback);
        }

        /// <summary>
        /// Configura uma política de fallback que executa uma função substituta sem argumentos quando a execução original falhar.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado esperado.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackAction">Função geradora do valor de contingência.</param>
        /// <param name="onFallback">Callback síncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy{TResult}"/>.</returns>
        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            Func<TResult> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (_, _, _) => fallbackAction(), onFallbackSync: onFallback);
        }

        /// <summary>
        /// Configura uma política de fallback que recebe a exceção e contexto para computar o valor de contingência.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado esperado.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackAction">Função geradora que recebe a exceção e contexto.</param>
        /// <param name="onFallback">Callback síncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy{TResult}"/>.</returns>
        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, TResult> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (ex, _, ctx) => fallbackAction(ex, ctx), onFallbackSync: onFallback);
        }

        /// <summary>
        /// Configura uma política de fallback assíncrono para operações tipadas.
        /// </summary>
        /// <typeparam name="TResult">O tipo de resultado esperado.</typeparam>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackActionAsync">Função assíncrona geradora de contingência.</param>
        /// <param name="onFallbackAsync">Callback assíncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy{TResult}"/>.</returns>
        public static FallbackPolicy<TResult> FallbackAsync<TResult>(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, CancellationToken, Task<TResult>> fallbackActionAsync,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(
                b.Condition,
                fallbackActionSync: null,
                fallbackActionAsync: (ex, _, ctx, ct) => fallbackActionAsync(ex, ctx, ct),
                onFallbackAsync: onFallbackAsync);
        }

        /// <summary>
        /// Configura uma política de fallback síncrono para ações sem retorno (void).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackAction">Ação síncrona alternativa executada em caso de falha.</param>
        /// <param name="onFallback">Callback síncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy"/> não-genérica.</returns>
        public static FallbackPolicy Fallback(
            this IPolicyBuilder builder,
            Action fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(b.Condition, (_, _) => fallbackAction(), onFallbackSync: onFallback);
        }

        /// <summary>
        /// Configura uma política de fallback síncrono recebendo a exceção e contexto para ações sem retorno (void).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackAction">Ação síncrona alternativa que recebe exceção e contexto.</param>
        /// <param name="onFallback">Callback síncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy"/> não-genérica.</returns>
        public static FallbackPolicy Fallback(
            this IPolicyBuilder builder,
            Action<Exception?, IDictionary<string, object>?> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(b.Condition, fallbackAction, onFallbackSync: onFallback);
        }

        /// <summary>
        /// Configura uma política de fallback assíncrono para ações sem retorno (void).
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="fallbackActionAsync">Ação assíncrona alternativa executada em caso de falha.</param>
        /// <param name="onFallbackAsync">Callback assíncrono opcional executado ao acionar o fallback.</param>
        /// <returns>Uma nova instância de <see cref="FallbackPolicy"/> não-genérica.</returns>
        public static FallbackPolicy FallbackAsync(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, CancellationToken, Task> fallbackActionAsync,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(
                b.Condition,
                fallbackActionSync: null,
                fallbackActionAsync: fallbackActionAsync,
                onFallbackAsync: onFallbackAsync);
        }
    }
}
