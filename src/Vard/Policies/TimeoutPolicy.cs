using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;

namespace Vard.Policies
{
    /// <summary>
    /// Política para imposição de tempo limite em execuções de ações.
    /// </summary>
    public class TimeoutPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        private readonly TimeSpan _timeout;
        private readonly Action<IDictionary<string, object>?, TimeSpan>? _onTimeoutSync;
        private readonly Func<IDictionary<string, object>?, TimeSpan, Task>? _onTimeoutAsync;

        internal TimeoutPolicy(
            TimeSpan timeout,
            Action<IDictionary<string, object>?, TimeSpan>? onTimeoutSync = null,
            Func<IDictionary<string, object>?, TimeSpan, Task>? onTimeoutAsync = null)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
            _timeout = timeout;
            _onTimeoutSync = onTimeoutSync;
            _onTimeoutAsync = onTimeoutAsync;
        }

        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var task = Task.Run(() => action(context));
            bool completed;
            try
            {
                completed = task.Wait(_timeout);
            }
            catch (AggregateException ae)
            {
                var baseEx = ae.Flatten().InnerExceptions.Count == 1 ? ae.Flatten().InnerExceptions[0] : ae;
                ExceptionDispatchInfo.Capture(baseEx).Throw();
                throw;
            }

            if (!completed)
            {
                _onTimeoutSync?.Invoke(context, _timeout);
                throw new TimeoutRejectedException(_timeout);
            }

            return task.GetAwaiter().GetResult();
        }

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => ExecuteAndCapture((_) => action(), context: null!);

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TResult result = Execute(action, context);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (TimeoutRejectedException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.HandledByCondition, context, sw.Elapsed, 1);
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

            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                return await action(context, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (_onTimeoutAsync != null)
                    await _onTimeoutAsync(context, _timeout).ConfigureAwait(false);
                else
                    _onTimeoutSync?.Invoke(context, _timeout);

                throw new TimeoutRejectedException(_timeout);
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
                TResult result = await ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (TimeoutRejectedException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.HandledByCondition, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }
    }
}
