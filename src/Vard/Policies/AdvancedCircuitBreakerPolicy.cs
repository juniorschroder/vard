using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Common;

namespace Vard.Policies
{
    /// <summary>
    /// Política de Circuit Breaker baseada em taxa de falha (% de falhas em janela deslizante com throughput mínimo).
    /// </summary>
    public class AdvancedCircuitBreakerPolicy : ICircuitBreakerPolicy
    {
        private readonly HandleCondition _condition;
        private readonly double _failureThreshold;
        private readonly TimeSpan _samplingDuration;
        private readonly int _minimumThroughput;
        private readonly TimeSpan _durationOfBreak;
        private readonly Action<Exception?, TimeSpan, IDictionary<string, object>?>? _onBreakSync;
        private readonly Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? _onBreakAsync;
        private readonly Action<IDictionary<string, object>?>? _onResetSync;
        private readonly Func<IDictionary<string, object>?, Task>? _onResetAsync;
        private readonly Action<IDictionary<string, object>?>? _onHalfOpenSync;
        private readonly Func<IDictionary<string, object>?, Task>? _onHalfOpenAsync;
        private readonly Func<long> _ticksProvider;
        private readonly BucketedSlidingWindow _slidingWindow;

        private readonly object _stateLock = new object();
        private CircuitState _state = CircuitState.Closed;
        private long _lastOpenedAtTicks;
        private Exception? _lastException;
        private int _isTrialRunning;

        internal AdvancedCircuitBreakerPolicy(
            HandleCondition condition,
            double failureThreshold,
            TimeSpan samplingDuration,
            int minimumThroughput,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreakSync = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Action<IDictionary<string, object>?>? onResetSync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Action<IDictionary<string, object>?>? onHalfOpenSync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null,
            Func<long>? ticksProvider = null)
        {
            if (failureThreshold <= 0.0 || failureThreshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be between 0.0 (exclusive) and 1.0 (inclusive).");
            if (samplingDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(samplingDuration), "Sampling duration must be greater than zero.");
            if (minimumThroughput < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumThroughput), "Minimum throughput must be at least 1.");
            if (durationOfBreak <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(durationOfBreak), "Duration of break must be greater than zero.");

            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _failureThreshold = failureThreshold;
            _samplingDuration = samplingDuration;
            _minimumThroughput = minimumThroughput;
            _durationOfBreak = durationOfBreak;
            _onBreakSync = onBreakSync;
            _onBreakAsync = onBreakAsync;
            _onResetSync = onResetSync;
            _onResetAsync = onResetAsync;
            _onHalfOpenSync = onHalfOpenSync;
            _onHalfOpenAsync = onHalfOpenAsync;
            _ticksProvider = ticksProvider ?? GetMonotonicTicks;

            _slidingWindow = new BucketedSlidingWindow(samplingDuration, bucketCount: 10, ticksProvider: _ticksProvider);
        }

        /// <inheritdoc />
        public CircuitState CircuitState
        {
            get
            {
                long now = _ticksProvider();
                lock (_stateLock)
                {
                    UpdateStateIfExpired(now);
                    return _state;
                }
            }
        }

        /// <inheritdoc />
        public Exception? LastException
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastException;
                }
            }
        }

        /// <inheritdoc />
        public void Isolate()
        {
            lock (_stateLock)
            {
                _state = CircuitState.Isolated;
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            Action? onResetAction = null;
            lock (_stateLock)
            {
                _slidingWindow.Reset();
                _state = CircuitState.Closed;
                _lastException = null;
                _isTrialRunning = 0;
                onResetAction = () => InvokeOnReset(null);
            }
            onResetAction?.Invoke();
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var (isTrial, blockedException) = BeforeExecution(context);
            if (blockedException != null)
            {
                throw blockedException;
            }

            try
            {
                TResult result = action(context);
                OnSuccess(isTrial, result, context);
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(isTrial, ex, context);
                throw;
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
                if (_condition.ShouldHandle(null, result))
                {
                    return PolicyResult<TResult>.Failure(
                        new InvalidOperationException("Execution resulted in a handled result condition."),
                        ExceptionType.HandledByResult,
                        context,
                        sw.Elapsed,
                        1);
                }
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (CircuitBreakerOpenException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.HandledByCondition, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                ExceptionType exType = _condition.ShouldHandle(ex, null)
                    ? ExceptionType.HandledByCondition
                    : ExceptionType.Unhandled;
                return PolicyResult<TResult>.Failure(ex, exType, context, sw.Elapsed, 1);
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

            var (isTrial, blockedException, onHalfOpenAction) = BeforeExecutionAsync(context);
            if (blockedException != null)
            {
                throw blockedException;
            }

            if (onHalfOpenAction != null)
            {
                await onHalfOpenAction().ConfigureAwait(false);
            }

            try
            {
                TResult result = await action(context, cancellationToken).ConfigureAwait(false);
                await OnSuccessAsync(isTrial, result, context).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                await OnFailureAsync(isTrial, ex, context).ConfigureAwait(false);
                throw;
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
                if (_condition.ShouldHandle(null, result))
                {
                    return PolicyResult<TResult>.Failure(
                        new InvalidOperationException("Execution resulted in a handled result condition."),
                        ExceptionType.HandledByResult,
                        context,
                        sw.Elapsed,
                        1);
                }
                return PolicyResult<TResult>.Success(result, context, sw.Elapsed, 1);
            }
            catch (CircuitBreakerOpenException ex)
            {
                sw.Stop();
                return PolicyResult<TResult>.Failure(ex, ExceptionType.HandledByCondition, context, sw.Elapsed, 1);
            }
            catch (Exception ex)
            {
                sw.Stop();
                ExceptionType exType = _condition.ShouldHandle(ex, null)
                    ? ExceptionType.HandledByCondition
                    : ExceptionType.Unhandled;
                return PolicyResult<TResult>.Failure(ex, exType, context, sw.Elapsed, 1);
            }
        }

        private (bool isTrial, CircuitBreakerOpenException? blockedException) BeforeExecution(IDictionary<string, object>? context)
        {
            long now = _ticksProvider();
            Action? onHalfOpenAction = null;

            lock (_stateLock)
            {
                if (_state == CircuitState.Isolated)
                {
                    return (false, new CircuitBreakerOpenException(CircuitState.Isolated, null, _lastException));
                }

                if (_state == CircuitState.Open)
                {
                    long elapsed = now - _lastOpenedAtTicks;
                    if (elapsed < _durationOfBreak.Ticks)
                    {
                        TimeSpan retryAfter = TimeSpan.FromTicks(Math.Max(0, _durationOfBreak.Ticks - elapsed));
                        return (false, new CircuitBreakerOpenException(CircuitState.Open, retryAfter, _lastException));
                    }

                    _state = CircuitState.HalfOpen;
                    _isTrialRunning = 0;
                    onHalfOpenAction = () => InvokeOnHalfOpen(context);
                }

                if (_state == CircuitState.HalfOpen)
                {
                    if (Interlocked.CompareExchange(ref _isTrialRunning, 1, 0) == 0)
                    {
                        onHalfOpenAction?.Invoke();
                        return (true, null);
                    }
                    else
                    {
                        onHalfOpenAction?.Invoke();
                        return (false, new CircuitBreakerOpenException(CircuitState.HalfOpen, TimeSpan.Zero, _lastException));
                    }
                }

                return (false, null);
            }
        }

        private (bool isTrial, CircuitBreakerOpenException? blockedException, Func<Task>? onHalfOpenAction) BeforeExecutionAsync(IDictionary<string, object>? context)
        {
            long now = _ticksProvider();
            Func<Task>? onHalfOpenAction = null;

            lock (_stateLock)
            {
                if (_state == CircuitState.Isolated)
                {
                    return (false, new CircuitBreakerOpenException(CircuitState.Isolated, null, _lastException), null);
                }

                if (_state == CircuitState.Open)
                {
                    long elapsed = now - _lastOpenedAtTicks;
                    if (elapsed < _durationOfBreak.Ticks)
                    {
                        TimeSpan retryAfter = TimeSpan.FromTicks(Math.Max(0, _durationOfBreak.Ticks - elapsed));
                        return (false, new CircuitBreakerOpenException(CircuitState.Open, retryAfter, _lastException), null);
                    }

                    _state = CircuitState.HalfOpen;
                    _isTrialRunning = 0;
                    onHalfOpenAction = () => InvokeOnHalfOpenAsync(context);
                }

                if (_state == CircuitState.HalfOpen)
                {
                    if (Interlocked.CompareExchange(ref _isTrialRunning, 1, 0) == 0)
                    {
                        return (true, null, onHalfOpenAction);
                    }
                    else
                    {
                        return (false, new CircuitBreakerOpenException(CircuitState.HalfOpen, TimeSpan.Zero, _lastException), onHalfOpenAction);
                    }
                }

                return (false, null, null);
            }
        }

        private void OnSuccess(bool isTrial, object? result, IDictionary<string, object>? context)
        {
            if (_condition.ShouldHandle(null, result))
            {
                HandleFailure(isTrial, null, context);
                return;
            }

            Action? onResetAction = null;
            lock (_stateLock)
            {
                if (isTrial && _state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _slidingWindow.Reset();
                    _lastException = null;
                    _isTrialRunning = 0;
                    onResetAction = () => InvokeOnReset(context);
                }
                else if (_state == CircuitState.Closed)
                {
                    _slidingWindow.RecordSuccess();
                }
            }
            onResetAction?.Invoke();
        }

        private async Task OnSuccessAsync(bool isTrial, object? result, IDictionary<string, object>? context)
        {
            if (_condition.ShouldHandle(null, result))
            {
                await HandleFailureAsync(isTrial, null, context).ConfigureAwait(false);
                return;
            }

            Func<Task>? onResetAction = null;
            lock (_stateLock)
            {
                if (isTrial && _state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _slidingWindow.Reset();
                    _lastException = null;
                    _isTrialRunning = 0;
                    onResetAction = () => InvokeOnResetAsync(context);
                }
                else if (_state == CircuitState.Closed)
                {
                    _slidingWindow.RecordSuccess();
                }
            }
            if (onResetAction != null)
            {
                await onResetAction().ConfigureAwait(false);
            }
        }

        private void OnFailure(bool isTrial, Exception ex, IDictionary<string, object>? context)
        {
            if (!_condition.ShouldHandle(ex, null))
            {
                if (isTrial)
                {
                    Interlocked.Exchange(ref _isTrialRunning, 0);
                }
                return;
            }

            HandleFailure(isTrial, ex, context);
        }

        private Task OnFailureAsync(bool isTrial, Exception ex, IDictionary<string, object>? context)
        {
            if (!_condition.ShouldHandle(ex, null))
            {
                if (isTrial)
                {
                    Interlocked.Exchange(ref _isTrialRunning, 0);
                }
                return Task.CompletedTask;
            }

            return HandleFailureAsync(isTrial, ex, context);
        }

        private void HandleFailure(bool isTrial, Exception? ex, IDictionary<string, object>? context)
        {
            Action? onBreakAction = null;
            long now = _ticksProvider();

            lock (_stateLock)
            {
                _lastException = ex;
                if (isTrial && _state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                    _lastOpenedAtTicks = now;
                    _isTrialRunning = 0;
                    onBreakAction = () => InvokeOnBreak(ex, _durationOfBreak, context);
                }
                else if (_state == CircuitState.Closed)
                {
                    _slidingWindow.RecordFailure();
                    var (total, _, failureRate) = _slidingWindow.GetSnapshot();
                    if (total >= _minimumThroughput && failureRate >= _failureThreshold)
                    {
                        _state = CircuitState.Open;
                        _lastOpenedAtTicks = now;
                        onBreakAction = () => InvokeOnBreak(ex, _durationOfBreak, context);
                    }
                }
            }
            onBreakAction?.Invoke();
        }

        private async Task HandleFailureAsync(bool isTrial, Exception? ex, IDictionary<string, object>? context)
        {
            Func<Task>? onBreakAction = null;
            long now = _ticksProvider();

            lock (_stateLock)
            {
                _lastException = ex;
                if (isTrial && _state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                    _lastOpenedAtTicks = now;
                    _isTrialRunning = 0;
                    onBreakAction = () => InvokeOnBreakAsync(ex, _durationOfBreak, context);
                }
                else if (_state == CircuitState.Closed)
                {
                    _slidingWindow.RecordFailure();
                    var (total, _, failureRate) = _slidingWindow.GetSnapshot();
                    if (total >= _minimumThroughput && failureRate >= _failureThreshold)
                    {
                        _state = CircuitState.Open;
                        _lastOpenedAtTicks = now;
                        onBreakAction = () => InvokeOnBreakAsync(ex, _durationOfBreak, context);
                    }
                }
            }
            if (onBreakAction != null)
            {
                await onBreakAction().ConfigureAwait(false);
            }
        }

        private void UpdateStateIfExpired(long now)
        {
            if (_state == CircuitState.Open && now - _lastOpenedAtTicks >= _durationOfBreak.Ticks)
            {
                _state = CircuitState.HalfOpen;
                _isTrialRunning = 0;
            }
        }

        private void InvokeOnBreak(Exception? ex, TimeSpan duration, IDictionary<string, object>? context)
            => _onBreakSync?.Invoke(ex, duration, context);

        private async Task InvokeOnBreakAsync(Exception? ex, TimeSpan duration, IDictionary<string, object>? context)
        {
            if (_onBreakAsync != null)
                await _onBreakAsync(ex, duration, context).ConfigureAwait(false);
            else
                _onBreakSync?.Invoke(ex, duration, context);
        }

        private void InvokeOnReset(IDictionary<string, object>? context)
            => _onResetSync?.Invoke(context);

        private async Task InvokeOnResetAsync(IDictionary<string, object>? context)
        {
            if (_onResetAsync != null)
                await _onResetAsync(context).ConfigureAwait(false);
            else
                _onResetSync?.Invoke(context);
        }

        private void InvokeOnHalfOpen(IDictionary<string, object>? context)
            => _onHalfOpenSync?.Invoke(context);

        private async Task InvokeOnHalfOpenAsync(IDictionary<string, object>? context)
        {
            if (_onHalfOpenAsync != null)
                await _onHalfOpenAsync(context).ConfigureAwait(false);
            else
                _onHalfOpenSync?.Invoke(context);
        }

        private static long GetMonotonicTicks()
        {
            return (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
        }
    }
}
