using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Policies
{
    /// <summary>
    /// Política de resiliência Hedging para execuções especulativas concorrentes.
    /// </summary>
    public class HedgingPolicy : IHedgingPolicy, IPolicy, ISyncPolicy, IAsyncPolicy
    {
        internal readonly HandleCondition _condition;
        protected readonly int _maxHedges;
        protected readonly Func<int, TimeSpan> _hedgingDelayProvider;
        protected readonly Action<object?, int, TimeSpan, IDictionary<string, object>?>? _onHedgingResultSync;
        protected readonly Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? _onHedgingResultAsync;

        public int MaxHedges => _maxHedges;
        public TimeSpan HedgingDelay => _hedgingDelayProvider(1);

        internal HedgingPolicy(
            HandleCondition condition,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResultSync = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
            : this(
                condition,
                maxHedges,
                _ => ValidateDelay(hedgingDelay),
                onHedgingResultSync,
                onHedgingResultAsync)
        {
        }

        internal HedgingPolicy(
            HandleCondition condition,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResultSync = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (maxHedges < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHedges), "MaxHedges must be greater than or equal to 1.");

            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _maxHedges = maxHedges;
            _hedgingDelayProvider = hedgingDelayProvider ?? throw new ArgumentNullException(nameof(hedgingDelayProvider));
            _onHedgingResultSync = onHedgingResultSync;
            _onHedgingResultAsync = onHedgingResultAsync;
        }

        private static TimeSpan ValidateDelay(TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delay), "Hedging delay cannot be negative.");
            return delay;
        }

        // IPolicy — Synchronous execution
        public TResult Execute<TResult>(Func<TResult> action)
            => Execute(_ => action(), context: null!);

        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                return ExecuteAsync((ctx, ct) => Task.Run(() => action(ctx), ct), context, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (AggregateException ae)
            {
                var baseEx = ae.Flatten().InnerExceptions.Count == 1 ? ae.Flatten().InnerExceptions[0] : ae;
                ExceptionDispatchInfo.Capture(baseEx).Throw();
                throw;
            }
        }

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => ExecuteAndCapture(_ => action(), context: null!);

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                return ExecuteAndCaptureAsync((ctx, ct) => Task.Run(() => action(ctx), ct), context, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (AggregateException ae)
            {
                var baseEx = ae.Flatten().InnerExceptions.Count == 1 ? ae.Flatten().InnerExceptions[0] : ae;
                ExceptionDispatchInfo.Capture(baseEx).Throw();
                throw;
            }
        }

        // IAsyncPolicy — Asynchronous execution
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => ExecuteAsync((_, ct) => action(ct), context: null!, cancellationToken);

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var outcome = await ExecuteCoreAsync(action, context, cancellationToken).ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                return outcome.Result!;
            }

            if (outcome.Exception != null)
            {
                ExceptionDispatchInfo.Capture(outcome.Exception).Throw();
            }

            if (outcome.ExceptionType == ExceptionType.HandledByResult)
            {
                return outcome.Result!;
            }

            return default!;
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
            var outcome = await ExecuteCoreAsync(action, context, cancellationToken).ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                return PolicyResult<TResult>.Success(outcome.Result!, context, outcome.Elapsed, outcome.AttemptNumber);
            }

            if (outcome.ExceptionType == ExceptionType.HandledByResult)
            {
                var ex = new InvalidOperationException($"Hedging policy exhausted all attempts with handled result: {outcome.Result}");
                return PolicyResult<TResult>.Failure(ex, ExceptionType.HandledByResult, context, outcome.Elapsed, outcome.AttemptNumber);
            }

            if (outcome.Exception != null)
            {
                return PolicyResult<TResult>.Failure(outcome.Exception, outcome.ExceptionType, context, outcome.Elapsed, outcome.AttemptNumber);
            }

            var genericEx = new InvalidOperationException("Hedging policy exhausted all attempts.");
            return PolicyResult<TResult>.Failure(genericEx, ExceptionType.Unhandled, context, outcome.Elapsed, outcome.AttemptNumber);
        }

        private sealed class AttemptResult<T>
        {
            public int AttemptNumber { get; set; }
            public T? Result { get; set; }
            public Exception? Exception { get; set; }
            public bool HasException => Exception != null;
            public TimeSpan Duration { get; set; }
            public bool Cancelled { get; set; }
        }

        private sealed class ExecutionOutcome<T>
        {
            public bool IsSuccess { get; set; }
            public T? Result { get; set; }
            public Exception? Exception { get; set; }
            public ExceptionType ExceptionType { get; set; }
            public int AttemptNumber { get; set; }
            public TimeSpan Elapsed { get; set; }
        }

        private static async Task<AttemptResult<T>> RunAttemptAsync<T>(
            int attemptNumber,
            Func<IDictionary<string, object>, CancellationToken, Task<T>> action,
            IDictionary<string, object> context,
            CancellationToken attemptToken,
            CancellationToken callerToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var task = action(context, attemptToken);
                T res = task != null ? await task.ConfigureAwait(false) : default!;
                sw.Stop();
                return new AttemptResult<T>
                {
                    AttemptNumber = attemptNumber,
                    Result = res,
                    Duration = sw.Elapsed
                };
            }
            catch (OperationCanceledException oce) when (callerToken.IsCancellationRequested || attemptToken.IsCancellationRequested)
            {
                sw.Stop();
                return new AttemptResult<T>
                {
                    AttemptNumber = attemptNumber,
                    Cancelled = true,
                    Duration = sw.Elapsed,
                    Exception = oce
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new AttemptResult<T>
                {
                    AttemptNumber = attemptNumber,
                    Exception = ex,
                    Duration = sw.Elapsed
                };
            }
        }

        private async Task<ExecutionOutcome<TResult>> ExecuteCoreAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken callerToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            callerToken.ThrowIfCancellationRequested();

            var totalSw = Stopwatch.StartNew();
            var allCts = new List<CancellationTokenSource>();
            var activeTasks = new List<Task<AttemptResult<TResult>>>();

            CancellationTokenSource? currentDelayCts = null;
            Task? delayTask = null;
            int launchedAttempts = 0;

            Exception? lastHandledException = null;
            TResult? lastHandledResult = default;
            bool hasHandledResult = false;
            int lastAttemptNumber = 1;

            try
            {
                // Launch attempt 1
                launchedAttempts = 1;
                var firstCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                allCts.Add(firstCts);
                activeTasks.Add(RunAttemptAsync(1, action, context, firstCts.Token, callerToken));

                // If maxHedges >= 1, schedule initial delay
                if (launchedAttempts < 1 + _maxHedges)
                {
                    currentDelayCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                    var delay = _hedgingDelayProvider(launchedAttempts);
                    if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException("hedgingDelayProvider", "Hedging delay cannot be negative.");
                    delayTask = Task.Delay(delay, currentDelayCts.Token);
                }

                while (activeTasks.Count > 0 || delayTask != null)
                {
                    callerToken.ThrowIfCancellationRequested();

                    var waitList = new List<Task>(activeTasks);
                    if (delayTask != null)
                    {
                        waitList.Add(delayTask);
                    }

                    var completed = await Task.WhenAny(waitList).ConfigureAwait(false);

                    if (callerToken.IsCancellationRequested)
                    {
                        callerToken.ThrowIfCancellationRequested();
                    }

                    if (completed == delayTask)
                    {
                        currentDelayCts?.Dispose();
                        currentDelayCts = null;
                        delayTask = null;

                        if (launchedAttempts < 1 + _maxHedges)
                        {
                            launchedAttempts++;
                            var nextCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                            allCts.Add(nextCts);
                            activeTasks.Add(RunAttemptAsync(launchedAttempts, action, context, nextCts.Token, callerToken));

                            if (launchedAttempts < 1 + _maxHedges)
                            {
                                currentDelayCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                                var delay = _hedgingDelayProvider(launchedAttempts);
                                if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException("hedgingDelayProvider", "Hedging delay cannot be negative.");
                                delayTask = Task.Delay(delay, currentDelayCts.Token);
                            }
                        }
                    }
                    else
                    {
                        var attemptTask = (Task<AttemptResult<TResult>>)completed;
                        activeTasks.Remove(attemptTask);
                        var attemptResult = attemptTask.Result;
                        lastAttemptNumber = attemptResult.AttemptNumber;

                        if (attemptResult.Cancelled && callerToken.IsCancellationRequested)
                        {
                            callerToken.ThrowIfCancellationRequested();
                        }

                        if (attemptResult.HasException)
                        {
                            if (_condition.ShouldHandle(attemptResult.Exception, null))
                            {
                                lastHandledException = attemptResult.Exception;

                                // D-01: Early failure before delay
                                if (attemptResult.AttemptNumber == launchedAttempts && delayTask != null)
                                {
                                    try { currentDelayCts?.Cancel(); } catch { }
                                    currentDelayCts?.Dispose();
                                    currentDelayCts = null;
                                    delayTask = null;

                                    if (launchedAttempts < 1 + _maxHedges)
                                    {
                                        launchedAttempts++;
                                        var nextCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                                        allCts.Add(nextCts);
                                        activeTasks.Add(RunAttemptAsync(launchedAttempts, action, context, nextCts.Token, callerToken));

                                        if (launchedAttempts < 1 + _maxHedges)
                                        {
                                            currentDelayCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                                            var delay = _hedgingDelayProvider(launchedAttempts);
                                            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException("hedgingDelayProvider", "Hedging delay cannot be negative.");
                                            delayTask = Task.Delay(delay, currentDelayCts.Token);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Unhandled exception
                                CancelAll(allCts);
                                try { currentDelayCts?.Cancel(); } catch { }
                                totalSw.Stop();

                                return new ExecutionOutcome<TResult>
                                {
                                    IsSuccess = false,
                                    Exception = attemptResult.Exception,
                                    ExceptionType = ExceptionType.Unhandled,
                                    AttemptNumber = attemptResult.AttemptNumber,
                                    Elapsed = totalSw.Elapsed
                                };
                            }
                        }
                        else
                        {
                            if (_condition.ShouldHandle(null, attemptResult.Result))
                            {
                                lastHandledResult = attemptResult.Result;
                                hasHandledResult = true;

                                // D-01: Early failure before delay
                                if (attemptResult.AttemptNumber == launchedAttempts && delayTask != null)
                                {
                                    try { currentDelayCts?.Cancel(); } catch { }
                                    currentDelayCts?.Dispose();
                                    currentDelayCts = null;
                                    delayTask = null;

                                    if (launchedAttempts < 1 + _maxHedges)
                                    {
                                        launchedAttempts++;
                                        var nextCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                                        allCts.Add(nextCts);
                                        activeTasks.Add(RunAttemptAsync(launchedAttempts, action, context, nextCts.Token, callerToken));

                                        if (launchedAttempts < 1 + _maxHedges)
                                        {
                                            currentDelayCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
                                            var delay = _hedgingDelayProvider(launchedAttempts);
                                            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException("hedgingDelayProvider", "Hedging delay cannot be negative.");
                                            delayTask = Task.Delay(delay, currentDelayCts.Token);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // WINNER!
                                CancelAll(allCts);
                                try { currentDelayCts?.Cancel(); } catch { }
                                totalSw.Stop();

                                if (_onHedgingResultAsync != null)
                                {
                                    await _onHedgingResultAsync(attemptResult.Result, attemptResult.AttemptNumber, totalSw.Elapsed, context).ConfigureAwait(false);
                                }
                                if (_onHedgingResultSync != null)
                                {
                                    _onHedgingResultSync(attemptResult.Result, attemptResult.AttemptNumber, totalSw.Elapsed, context);
                                }

                                return new ExecutionOutcome<TResult>
                                {
                                    IsSuccess = true,
                                    Result = attemptResult.Result,
                                    AttemptNumber = attemptResult.AttemptNumber,
                                    Elapsed = totalSw.Elapsed
                                };
                            }
                        }
                    }
                }

                // Exhaustion!
                totalSw.Stop();
                if (lastHandledException != null)
                {
                    return new ExecutionOutcome<TResult>
                    {
                        IsSuccess = false,
                        Exception = lastHandledException,
                        ExceptionType = ExceptionType.HandledByCondition,
                        AttemptNumber = lastAttemptNumber,
                        Elapsed = totalSw.Elapsed
                    };
                }

                if (hasHandledResult)
                {
                    return new ExecutionOutcome<TResult>
                    {
                        IsSuccess = false,
                        Result = lastHandledResult,
                        ExceptionType = ExceptionType.HandledByResult,
                        AttemptNumber = lastAttemptNumber,
                        Elapsed = totalSw.Elapsed
                    };
                }

                return new ExecutionOutcome<TResult>
                {
                    IsSuccess = false,
                    Exception = new InvalidOperationException("Hedging policy exhausted all attempts."),
                    ExceptionType = ExceptionType.Unhandled,
                    AttemptNumber = lastAttemptNumber,
                    Elapsed = totalSw.Elapsed
                };
            }
            finally
            {
                try { currentDelayCts?.Cancel(); } catch { }
                currentDelayCts?.Dispose();

                foreach (var cts in allCts)
                {
                    try { cts.Dispose(); } catch { }
                }
            }
        }

        private static void CancelAll(List<CancellationTokenSource> allCts)
        {
            foreach (var cts in allCts)
            {
                try
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception) { }
            }
        }
    }

    /// <summary>
    /// Subclasse genérica fortemente tipada de HedgingPolicy.
    /// </summary>
    public class HedgingPolicy<TResult> : HedgingPolicy
    {
        internal HedgingPolicy(
            HandleCondition condition,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResultSync = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
            : base(
                condition,
                maxHedges,
                hedgingDelay,
                onHedgingResultSync != null ? (res, att, dur, ctx) => onHedgingResultSync((TResult?)res, att, dur, ctx) : null,
                onHedgingResultAsync != null ? (res, att, dur, ctx) => onHedgingResultAsync((TResult?)res, att, dur, ctx) : null)
        {
        }

        internal HedgingPolicy(
            HandleCondition condition,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResultSync = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
            : base(
                condition,
                maxHedges,
                hedgingDelayProvider,
                onHedgingResultSync != null ? (res, att, dur, ctx) => onHedgingResultSync((TResult?)res, att, dur, ctx) : null,
                onHedgingResultAsync != null ? (res, att, dur, ctx) => onHedgingResultAsync((TResult?)res, att, dur, ctx) : null)
        {
        }
    }
}
