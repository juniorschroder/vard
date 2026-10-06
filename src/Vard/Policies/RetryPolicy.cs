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
    /// Política de repetição para tratamento de falhas transitórias.
    /// </summary>
    public class RetryPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        private readonly HandleCondition _condition;
        private readonly int _maxRetryAttempts;
        private readonly Func<int, IDictionary<string, object>?, TimeSpan> _sleepDurationProvider;
        private readonly Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?>? _onRetrySync;
        private readonly Func<Exception?, object?, int, TimeSpan, IDictionary<string, object>?, Task>? _onRetryAsync;

        internal RetryPolicy(
            HandleCondition condition,
            int retryCount,
            Func<int, IDictionary<string, object>?, TimeSpan>? sleepDurationProvider = null,
            Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?>? onRetrySync = null,
            Func<Exception?, object?, int, TimeSpan, IDictionary<string, object>?, Task>? onRetryAsync = null)
        {
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count cannot be negative.");
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _maxRetryAttempts = retryCount;
            _sleepDurationProvider = sleepDurationProvider ?? ((_, _) => TimeSpan.Zero);
            _onRetrySync = onRetrySync;
            _onRetryAsync = onRetryAsync;
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            int attempt = 0;

            while (true)
            {
                attempt++;
                try
                {
                    TResult result = action(context);
                    if (_condition.ShouldHandle(null, result) && attempt <= _maxRetryAttempts)
                    {
                        TimeSpan delay = _sleepDurationProvider(attempt, context);
                        _onRetrySync?.Invoke(null, result, attempt, delay, context);
                        if (delay > TimeSpan.Zero) Thread.Sleep(delay);
                        continue;
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    if (_condition.ShouldHandle(ex, null) && attempt <= _maxRetryAttempts)
                    {
                        TimeSpan delay = _sleepDurationProvider(attempt, context);
                        _onRetrySync?.Invoke(ex, null, attempt, delay, context);
                        if (delay > TimeSpan.Zero) Thread.Sleep(delay);
                        continue;
                    }
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => ExecuteAndCapture((_) => action(), context: null!);

        /// <inheritdoc />
        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;
            try
            {
                while (true)
                {
                    attempt++;
                    try
                    {
                        TResult result = action(context);
                        if (_condition.ShouldHandle(null, result))
                        {
                            if (attempt <= _maxRetryAttempts)
                            {
                                TimeSpan delay = _sleepDurationProvider(attempt, context);
                                _onRetrySync?.Invoke(null, result, attempt, delay, context);
                                if (delay > TimeSpan.Zero) Thread.Sleep(delay);
                                continue;
                            }

                            sw.Stop();
                            return PolicyResult<TResult>.Failure(
                                new InvalidOperationException("Execution resulted in a handled result condition."),
                                ExceptionType.HandledByResult,
                                context,
                                sw.Elapsed,
                                attempt);
                        }

                        sw.Stop();
                        return PolicyResult<TResult>.Success(result, context, sw.Elapsed, attempt);
                    }
                    catch (Exception ex)
                    {
                        if (_condition.ShouldHandle(ex, null) && attempt <= _maxRetryAttempts)
                        {
                            TimeSpan delay = _sleepDurationProvider(attempt, context);
                            _onRetrySync?.Invoke(ex, null, attempt, delay, context);
                            if (delay > TimeSpan.Zero) Thread.Sleep(delay);
                            continue;
                        }

                        sw.Stop();
                        ExceptionType exType = _condition.ShouldHandle(ex, null)
                            ? ExceptionType.HandledByCondition
                            : ExceptionType.Unhandled;

                        return PolicyResult<TResult>.Failure(ex, exType, context, sw.Elapsed, attempt);
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, attempt);
            }
        }

        // IAsyncPolicy ExecuteAsync
        /// <inheritdoc />
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAsync((_, ct) => action(ct), context: null!, cancellationToken);

        /// <inheritdoc />
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            int attempt = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;
                try
                {
                    TResult result = await action(context, cancellationToken).ConfigureAwait(false);
                    if (_condition.ShouldHandle(null, result) && attempt <= _maxRetryAttempts)
                    {
                        TimeSpan delay = _sleepDurationProvider(attempt, context);
                        if (_onRetryAsync != null)
                            await _onRetryAsync(null, result, attempt, delay, context).ConfigureAwait(false);
                        else
                            _onRetrySync?.Invoke(null, result, attempt, delay, context);

                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    return result;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken))
                {
                    if (_condition.ShouldHandle(ex, null) && attempt <= _maxRetryAttempts)
                    {
                        TimeSpan delay = _sleepDurationProvider(attempt, context);
                        if (_onRetryAsync != null)
                            await _onRetryAsync(ex, null, attempt, delay, context).ConfigureAwait(false);
                        else
                            _onRetrySync?.Invoke(ex, null, attempt, delay, context);

                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAndCaptureAsync((_, ct) => action(ct), context: null!, cancellationToken);

        /// <inheritdoc />
        public async Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    attempt++;
                    try
                    {
                        TResult result = await action(context, cancellationToken).ConfigureAwait(false);
                        if (_condition.ShouldHandle(null, result))
                        {
                            if (attempt <= _maxRetryAttempts)
                            {
                                TimeSpan delay = _sleepDurationProvider(attempt, context);
                                if (_onRetryAsync != null)
                                    await _onRetryAsync(null, result, attempt, delay, context).ConfigureAwait(false);
                                else
                                    _onRetrySync?.Invoke(null, result, attempt, delay, context);

                                if (delay > TimeSpan.Zero)
                                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            sw.Stop();
                            return PolicyResult<TResult>.Failure(
                                new InvalidOperationException("Execution resulted in a handled result condition."),
                                ExceptionType.HandledByResult,
                                context,
                                sw.Elapsed,
                                attempt);
                        }

                        sw.Stop();
                        return PolicyResult<TResult>.Success(result, context, sw.Elapsed, attempt);
                    }
                    catch (Exception ex)
                    {
                        if (_condition.ShouldHandle(ex, null) && attempt <= _maxRetryAttempts)
                        {
                            TimeSpan delay = _sleepDurationProvider(attempt, context);
                            if (_onRetryAsync != null)
                                await _onRetryAsync(ex, null, attempt, delay, context).ConfigureAwait(false);
                            else
                                _onRetrySync?.Invoke(ex, null, attempt, delay, context);

                            if (delay > TimeSpan.Zero)
                                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        sw.Stop();
                        ExceptionType exType = _condition.ShouldHandle(ex, null)
                            ? ExceptionType.HandledByCondition
                            : ExceptionType.Unhandled;

                        return PolicyResult<TResult>.Failure(ex, exType, context, sw.Elapsed, attempt);
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, attempt);
            }
        }
    }
}
