using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Common;

namespace Vard.Policies
{
    /// <summary>
    /// Política de controle de taxa de requisições baseada em janela deslizante particionada (Sliding Window).
    /// Utiliza anel circular de buckets de tempo discretos para contabilidade O(1) e zero alocações heap.
    /// </summary>
    public class SlidingWindowRateLimiterPolicy : IRateLimiterPolicy
    {
        private readonly int _permitLimit;
        private readonly TimeSpan _windowDuration;
        private readonly TimeSpan _maxWaitTime;
        private readonly BucketedSlidingWindow _window;
        private readonly Action<TimeSpan, IDictionary<string, object>?>? _onRateLimitExceededSync;
        private readonly Func<TimeSpan, IDictionary<string, object>?, Task>? _onRateLimitExceededAsync;
        private readonly Func<long> _ticksProvider;

        internal SlidingWindowRateLimiterPolicy(
            int permitLimit,
            TimeSpan windowDuration,
            int segmentsPerWindow = 10,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceededSync = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null,
            Func<long>? ticksProvider = null)
        {
            if (permitLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(permitLimit), "Permit limit must be greater than zero.");
            if (windowDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(windowDuration), "Window duration must be greater than zero.");
            if (segmentsPerWindow <= 0)
                throw new ArgumentOutOfRangeException(nameof(segmentsPerWindow), "Segments per window must be greater than zero.");
            if (maxWaitTime.HasValue && maxWaitTime.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxWaitTime), "Max wait time must be greater than or equal to zero.");

            _permitLimit = permitLimit;
            _windowDuration = windowDuration;
            _maxWaitTime = maxWaitTime ?? TimeSpan.Zero;
            _onRateLimitExceededSync = onRateLimitExceededSync;
            _onRateLimitExceededAsync = onRateLimitExceededAsync;
            _ticksProvider = ticksProvider ?? GetMonotonicTicks;
            _window = new BucketedSlidingWindow(windowDuration, segmentsPerWindow, _ticksProvider);
        }

        /// <inheritdoc />
        public int PermitLimit => _permitLimit;

        /// <inheritdoc />
        public int AvailablePermits => Math.Max(0, _permitLimit - _window.GetCurrentCount());

        /// <inheritdoc />
        public string AlgorithmName => "SlidingWindow";

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            int permits = GetPermits(context);

            long deadlineTicks = _maxWaitTime > TimeSpan.Zero ? _ticksProvider() + _maxWaitTime.Ticks : 0;

            while (true)
            {
                if (_window.TryConsume(permits, _permitLimit, out var retryAfter, out _))
                {
                    return action(context);
                }

                long now = _ticksProvider();
                long remainingTicks = deadlineTicks - now;
                if (_maxWaitTime > TimeSpan.Zero && permits <= _permitLimit && retryAfter.Ticks <= remainingTicks)
                {
                    Thread.Sleep(retryAfter);
                    continue;
                }

                InvokeOnRateLimitExceededSync(retryAfter, context);
                throw new RateLimiterRejectedException(retryAfter, _permitLimit, AlgorithmName);
            }
        }

        /// <inheritdoc />
        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => ExecuteAndCapture((_) => action(), context: null!);

        /// <inheritdoc />
        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TResult result = Execute(action, context);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (RateLimiterRejectedException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.PolicyBypassed, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }

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
            cancellationToken.ThrowIfCancellationRequested();
            int permits = GetPermits(context);

            long deadlineTicks = _maxWaitTime > TimeSpan.Zero ? _ticksProvider() + _maxWaitTime.Ticks : 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_window.TryConsume(permits, _permitLimit, out var retryAfter, out _))
                {
                    return await action(context, cancellationToken).ConfigureAwait(false);
                }

                long now = _ticksProvider();
                long remainingTicks = deadlineTicks - now;
                if (_maxWaitTime > TimeSpan.Zero && permits <= _permitLimit && retryAfter.Ticks <= remainingTicks)
                {
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await InvokeOnRateLimitExceededAsync(retryAfter, context).ConfigureAwait(false);
                throw new RateLimiterRejectedException(retryAfter, _permitLimit, AlgorithmName);
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
            try
            {
                TResult result = await ExecuteAsync(action, context, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (RateLimiterRejectedException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.PolicyBypassed, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.Unhandled, context, sw.Elapsed, 1);
            }
        }

        private static int GetPermits(IDictionary<string, object>? context)
        {
            if (context != null && context.TryGetValue(RateLimiterContextKeys.Cost, out var costObj))
            {
                if (costObj is int intCost && intCost > 0)
                    return intCost;
            }
            return 1;
        }

        private void InvokeOnRateLimitExceededSync(TimeSpan retryAfter, IDictionary<string, object>? context)
        {
            if (_onRateLimitExceededSync != null)
            {
                _onRateLimitExceededSync(retryAfter, context);
            }
            else if (_onRateLimitExceededAsync != null)
            {
                _onRateLimitExceededAsync(retryAfter, context).GetAwaiter().GetResult();
            }
        }

        private async Task InvokeOnRateLimitExceededAsync(TimeSpan retryAfter, IDictionary<string, object>? context)
        {
            if (_onRateLimitExceededAsync != null)
            {
                await _onRateLimitExceededAsync(retryAfter, context).ConfigureAwait(false);
            }
            else if (_onRateLimitExceededSync != null)
            {
                _onRateLimitExceededSync(retryAfter, context);
            }
        }

        private static long GetMonotonicTicks()
        {
            return (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
        }
    }
}
