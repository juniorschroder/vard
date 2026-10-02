using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;

namespace Vard.Policies
{
    /// <summary>
    /// Política de controle de taxa de requisições baseada no algoritmo Token Bucket.
    /// Suporta burst inicial, recarga contínua lazy matemática (sem timers de background)
    /// e cálculo dinâmico de RetryAfter.
    /// </summary>
    public class TokenBucketRateLimiterPolicy : IRateLimiterPolicy
    {
        private readonly int _maxTokens;
        private readonly double _tokensPerSecond;
        private readonly TimeSpan _maxWaitTime;
        private readonly Action<TimeSpan, IDictionary<string, object>?>? _onRateLimitExceededSync;
        private readonly Func<TimeSpan, IDictionary<string, object>?, Task>? _onRateLimitExceededAsync;
        private readonly Func<long> _ticksProvider;
        private readonly object _syncLock = new object();
        private double _availableTokens;
        private long _lastReplenishTicks;

        internal TokenBucketRateLimiterPolicy(
            int maxTokens,
            double tokensPerSecond,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceededSync = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null,
            Func<long>? ticksProvider = null)
        {
            if (maxTokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTokens), "Max tokens must be greater than zero.");
            if (tokensPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokensPerSecond), "Tokens per second must be greater than zero.");
            if (maxWaitTime.HasValue && maxWaitTime.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxWaitTime), "Max wait time must be greater than or equal to zero.");

            _maxTokens = maxTokens;
            _tokensPerSecond = tokensPerSecond;
            _maxWaitTime = maxWaitTime ?? TimeSpan.Zero;
            _onRateLimitExceededSync = onRateLimitExceededSync;
            _onRateLimitExceededAsync = onRateLimitExceededAsync;
            _ticksProvider = ticksProvider ?? GetMonotonicTicks;

            _availableTokens = maxTokens;
            _lastReplenishTicks = _ticksProvider();
        }

        public int PermitLimit => _maxTokens;

        public int AvailablePermits
        {
            get
            {
                lock (_syncLock)
                {
                    Replenish(_ticksProvider());
                    return (int)Math.Max(0, Math.Floor(_availableTokens));
                }
            }
        }

        public string AlgorithmName => "TokenBucket";

        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            int permits = GetPermits(context);

            long deadlineTicks = _maxWaitTime > TimeSpan.Zero ? _ticksProvider() + _maxWaitTime.Ticks : 0;

            while (true)
            {
                var (granted, retryAfter) = TryAcquire(permits);
                if (granted)
                {
                    return action(context);
                }

                long now = _ticksProvider();
                long remainingTicks = deadlineTicks - now;
                if (_maxWaitTime > TimeSpan.Zero && permits <= _maxTokens && retryAfter.Ticks <= remainingTicks)
                {
                    Thread.Sleep(retryAfter);
                    continue;
                }

                InvokeOnRateLimitExceededSync(retryAfter, context);
                throw new RateLimiterRejectedException(retryAfter, _maxTokens, AlgorithmName);
            }
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
            cancellationToken.ThrowIfCancellationRequested();
            int permits = GetPermits(context);

            long deadlineTicks = _maxWaitTime > TimeSpan.Zero ? _ticksProvider() + _maxWaitTime.Ticks : 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (granted, retryAfter) = TryAcquire(permits);
                if (granted)
                {
                    return await action(context, cancellationToken).ConfigureAwait(false);
                }

                long now = _ticksProvider();
                long remainingTicks = deadlineTicks - now;
                if (_maxWaitTime > TimeSpan.Zero && permits <= _maxTokens && retryAfter.Ticks <= remainingTicks)
                {
                    await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await InvokeOnRateLimitExceededAsync(retryAfter, context).ConfigureAwait(false);
                throw new RateLimiterRejectedException(retryAfter, _maxTokens, AlgorithmName);
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

        private (bool granted, TimeSpan retryAfter) TryAcquire(int permits)
        {
            long now = _ticksProvider();
            lock (_syncLock)
            {
                Replenish(now);
                if (_availableTokens >= permits)
                {
                    _availableTokens -= permits;
                    return (true, TimeSpan.Zero);
                }

                double missing = permits - _availableTokens;
                double seconds = missing / _tokensPerSecond;
                var retryAfter = TimeSpan.FromSeconds(Math.Max(0.001, seconds));
                return (false, retryAfter);
            }
        }

        private void Replenish(long nowTicks)
        {
            long elapsedTicks = nowTicks - _lastReplenishTicks;
            if (elapsedTicks > 0)
            {
                double elapsedSeconds = (double)elapsedTicks / TimeSpan.TicksPerSecond;
                double tokensToAdd = elapsedSeconds * _tokensPerSecond;
                _availableTokens = Math.Min(_maxTokens, _availableTokens + tokensToAdd);
                _lastReplenishTicks = nowTicks;
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
