using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;

namespace Vard.Policies
{
    /// <summary>
    /// Política de isolamento de concorrência Bulkhead com controle de paralelismo e fila de espera.
    /// </summary>
    public class BulkheadPolicy : IBulkheadPolicy, IDisposable
    {
        private readonly int _maxParallelization;
        private readonly int _maxQueuedActions;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly SemaphoreSlim? _queueSemaphore;
        private readonly Action<IDictionary<string, object>?>? _onBulkheadRejectedSync;
        private readonly Func<IDictionary<string, object>?, Task>? _onBulkheadRejectedAsync;
        private bool _disposed;

        internal BulkheadPolicy(
            int maxParallelization,
            int maxQueuedActions = 0,
            Action<IDictionary<string, object>?>? onBulkheadRejectedSync = null,
            Func<IDictionary<string, object>?, Task>? onBulkheadRejectedAsync = null)
        {
            if (maxParallelization <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxParallelization), "Max parallelization must be greater than zero.");
            if (maxQueuedActions < 0)
                throw new ArgumentOutOfRangeException(nameof(maxQueuedActions), "Max queued actions must be greater than or equal to zero.");

            _maxParallelization = maxParallelization;
            _maxQueuedActions = maxQueuedActions;
            _executionSemaphore = new SemaphoreSlim(maxParallelization, maxParallelization);
            _queueSemaphore = maxQueuedActions > 0 ? new SemaphoreSlim(maxQueuedActions, maxQueuedActions) : null;
            _onBulkheadRejectedSync = onBulkheadRejectedSync;
            _onBulkheadRejectedAsync = onBulkheadRejectedAsync;
        }

        /// <inheritdoc />
        public int MaxParallelization => _maxParallelization;

        /// <inheritdoc />
        public int MaxQueuedActions => _maxQueuedActions;

        /// <inheritdoc />
        public int BulkheadAvailableCount
        {
            get
            {
                ThrowIfDisposed();
                return _executionSemaphore.CurrentCount;
            }
        }

        /// <inheritdoc />
        public int QueueAvailableCount
        {
            get
            {
                ThrowIfDisposed();
                return _queueSemaphore?.CurrentCount ?? 0;
            }
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> action)
            => Execute((_) => action(), context: null!);

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<IDictionary<string, object>, TResult> action, IDictionary<string, object> context)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();

            if (!_executionSemaphore.Wait(0))
            {
                if (_maxQueuedActions == 0)
                {
                    InvokeOnBulkheadRejectedSync(context);
                    throw new BulkheadRejectedException(_maxParallelization, 0, BulkheadRejectionReason.ExecutionSaturated);
                }

                if (!_queueSemaphore!.Wait(0))
                {
                    InvokeOnBulkheadRejectedSync(context);
                    throw new BulkheadRejectedException(_maxParallelization, _maxQueuedActions, BulkheadRejectionReason.QueueFull);
                }

                try
                {
                    _executionSemaphore.Wait();
                }
                finally
                {
                    _queueSemaphore.Release();
                }
            }

            try
            {
                return action(context);
            }
            finally
            {
                _executionSemaphore.Release();
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
            catch (BulkheadRejectedException ex)
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
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (!_executionSemaphore.Wait(0))
            {
                if (_maxQueuedActions == 0)
                {
                    await InvokeOnBulkheadRejectedAsync(context).ConfigureAwait(false);
                    throw new BulkheadRejectedException(_maxParallelization, 0, BulkheadRejectionReason.ExecutionSaturated);
                }

                if (!_queueSemaphore!.Wait(0))
                {
                    await InvokeOnBulkheadRejectedAsync(context).ConfigureAwait(false);
                    throw new BulkheadRejectedException(_maxParallelization, _maxQueuedActions, BulkheadRejectionReason.QueueFull);
                }

                try
                {
                    await _executionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _queueSemaphore.Release();
                }
            }

            try
            {
                return await action(context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _executionSemaphore.Release();
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
            catch (BulkheadRejectedException ex)
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

        /// <summary>
        /// Libera os recursos gerenciados utilizados pela política de bulkhead (semáforos de concorrência e fila).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Libera recursos não gerenciados e opcionalmente os gerenciados.
        /// </summary>
        /// <param name="disposing"><c>true</c> para liberar recursos gerenciados e não gerenciados; <c>false</c> para liberar apenas não gerenciados.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _executionSemaphore.Dispose();
                    _queueSemaphore?.Dispose();
                }
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BulkheadPolicy));
            }
        }

        private void InvokeOnBulkheadRejectedSync(IDictionary<string, object>? context)
        {
            if (_onBulkheadRejectedSync != null)
            {
                _onBulkheadRejectedSync(context);
            }
            else if (_onBulkheadRejectedAsync != null)
            {
                _onBulkheadRejectedAsync(context).GetAwaiter().GetResult();
            }
        }

        private async Task InvokeOnBulkheadRejectedAsync(IDictionary<string, object>? context)
        {
            if (_onBulkheadRejectedAsync != null)
            {
                await _onBulkheadRejectedAsync(context).ConfigureAwait(false);
            }
            else if (_onBulkheadRejectedSync != null)
            {
                _onBulkheadRejectedSync(context);
            }
        }
    }
}
