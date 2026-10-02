using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Policies
{
    /// <summary>
    /// Política de fallback tipada para retorno de valor ou execução de delegate alternativo.
    /// </summary>
    public class FallbackPolicy<TResult> : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        private readonly HandleCondition _condition;
        private readonly Func<Exception?, object?, IDictionary<string, object>?, TResult>? _fallbackActionSync;
        private readonly Func<Exception?, object?, IDictionary<string, object>?, CancellationToken, Task<TResult>>? _fallbackActionAsync;
        private readonly Action<Exception?, IDictionary<string, object>?>? _onFallbackSync;
        private readonly Func<Exception?, IDictionary<string, object>?, Task>? _onFallbackAsync;

        internal FallbackPolicy(
            HandleCondition condition,
            Func<Exception?, object?, IDictionary<string, object>?, TResult>? fallbackActionSync,
            Func<Exception?, object?, IDictionary<string, object>?, CancellationToken, Task<TResult>>? fallbackActionAsync = null,
            Action<Exception?, IDictionary<string, object>?>? onFallbackSync = null,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _fallbackActionSync = fallbackActionSync;
            _fallbackActionAsync = fallbackActionAsync;
            _onFallbackSync = onFallbackSync;
            _onFallbackAsync = onFallbackAsync;
        }

        public TActionRes Execute<TActionRes>(Func<TActionRes> action)
            => Execute((_) => action(), context: null!);

        public TActionRes Execute<TActionRes>(Func<IDictionary<string, object>, TActionRes> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                TActionRes result = action(context);
                if (_condition.ShouldHandle(null, result))
                {
                    _onFallbackSync?.Invoke(null, context);
                    if (_fallbackActionSync != null)
                    {
                        return (TActionRes)(object)_fallbackActionSync(null, result, context)!;
                    }
                    return default!;
                }
                return result;
            }
            catch (Exception ex) when (_condition.ShouldHandle(ex, null))
            {
                _onFallbackSync?.Invoke(ex, context);
                if (_fallbackActionSync != null)
                {
                    return (TActionRes)(object)_fallbackActionSync(ex, null, context)!;
                }
                return default!;
            }
        }

        public PolicyResult<TActionRes> ExecuteAndCapture<TActionRes>(Func<TActionRes> action)
            => ExecuteAndCapture((_) => action(), context: null!);

        public PolicyResult<TActionRes> ExecuteAndCapture<TActionRes>(Func<IDictionary<string, object>, TActionRes> action, IDictionary<string, object> context)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TActionRes result = Execute(action, context);
                sw.Stop();
                return PolicyResult<TActionRes>.Success(result, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TActionRes>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }

        public Task<TActionRes> ExecuteAsync<TActionRes>(
            Func<CancellationToken, Task<TActionRes>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAsync((_, ct) => action(ct), context: null!, cancellationToken);

        public async Task<TActionRes> ExecuteAsync<TActionRes>(
            Func<IDictionary<string, object>, CancellationToken, Task<TActionRes>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                TActionRes result = await action(context, cancellationToken).ConfigureAwait(false);
                if (_condition.ShouldHandle(null, result))
                {
                    if (_onFallbackAsync != null)
                        await _onFallbackAsync(null, context).ConfigureAwait(false);
                    else
                        _onFallbackSync?.Invoke(null, context);

                    if (_fallbackActionAsync != null)
                    {
                        TResult fbRes = await _fallbackActionAsync(null, result, context, cancellationToken).ConfigureAwait(false);
                        return (TActionRes)(object)fbRes!;
                    }
                    if (_fallbackActionSync != null)
                    {
                        return (TActionRes)(object)_fallbackActionSync(null, result, context)!;
                    }
                    return default!;
                }
                return result;
            }
            catch (Exception ex) when (_condition.ShouldHandle(ex, null))
            {
                if (_onFallbackAsync != null)
                    await _onFallbackAsync(ex, context).ConfigureAwait(false);
                else
                    _onFallbackSync?.Invoke(ex, context);

                if (_fallbackActionAsync != null)
                {
                    TResult fbRes = await _fallbackActionAsync(ex, null, context, cancellationToken).ConfigureAwait(false);
                    return (TActionRes)(object)fbRes!;
                }
                if (_fallbackActionSync != null)
                {
                    return (TActionRes)(object)_fallbackActionSync(ex, null, context)!;
                }
                return default!;
            }
        }

        public Task<PolicyResult<TActionRes>> ExecuteAndCaptureAsync<TActionRes>(
            Func<CancellationToken, Task<TActionRes>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAndCaptureAsync((_, ct) => action(ct), context: null!, cancellationToken);

        public async Task<PolicyResult<TActionRes>> ExecuteAndCaptureAsync<TActionRes>(
            Func<IDictionary<string, object>, CancellationToken, Task<TActionRes>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TActionRes result = await ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                return PolicyResult<TActionRes>.Success(result, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TActionRes>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }
    }

    /// <summary>
    /// Política de fallback para ações void sem retorno genérico.
    /// </summary>
    public class FallbackPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        private readonly HandleCondition _condition;
        private readonly Action<Exception?, IDictionary<string, object>?>? _fallbackActionSync;
        private readonly Func<Exception?, IDictionary<string, object>?, CancellationToken, Task>? _fallbackActionAsync;
        private readonly Action<Exception?, IDictionary<string, object>?>? _onFallbackSync;
        private readonly Func<Exception?, IDictionary<string, object>?, Task>? _onFallbackAsync;

        internal FallbackPolicy(
            HandleCondition condition,
            Action<Exception?, IDictionary<string, object>?>? fallbackActionSync,
            Func<Exception?, IDictionary<string, object>?, CancellationToken, Task>? fallbackActionAsync = null,
            Action<Exception?, IDictionary<string, object>?>? onFallbackSync = null,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _fallbackActionSync = fallbackActionSync;
            _fallbackActionAsync = fallbackActionAsync;
            _onFallbackSync = onFallbackSync;
            _onFallbackAsync = onFallbackAsync;
        }

        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                return action(context);
            }
            catch (Exception ex) when (_condition.ShouldHandle(ex, null))
            {
                _onFallbackSync?.Invoke(ex, context);
                _fallbackActionSync?.Invoke(ex, context);
                if (typeof(TResult) == typeof(Vard.Abstractions.Void)) return (TResult)(object)Vard.Abstractions.Void.Instance;
                return default!;
            }
        }

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => ExecuteAndCapture((_) => action(), context: null!);

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = Execute(action, context);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }

        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAsync((_, ct) => action(ct), context: null!, cancellationToken);

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                return await action(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (_condition.ShouldHandle(ex, null))
            {
                if (_onFallbackAsync != null)
                    await _onFallbackAsync(ex, context).ConfigureAwait(false);
                else
                    _onFallbackSync?.Invoke(ex, context);

                if (_fallbackActionAsync != null)
                    await _fallbackActionAsync(ex, context, cancellationToken).ConfigureAwait(false);
                else
                    _fallbackActionSync?.Invoke(ex, context);

                if (typeof(TResult) == typeof(Vard.Abstractions.Void)) return (TResult)(object)Vard.Abstractions.Void.Instance;
                return default!;
            }
        }

        public Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAndCaptureAsync((_, ct) => action(ct), context: null!, cancellationToken);

        public async Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }
    }
}
