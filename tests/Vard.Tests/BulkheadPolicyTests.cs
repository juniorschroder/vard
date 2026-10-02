using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class BulkheadPolicyTests
    {
        [Fact]
        public void Constructor_InvalidParameters_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BulkheadPolicy(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BulkheadPolicy(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BulkheadPolicy(1, -1));
        }

        [Fact]
        public void Execute_WithinParallelLimit_ExecutesSuccessfully()
        {
            using var policy = VardPolicy.Bulkhead(2, 0);

            int resultSync = policy.Execute(() => 42);
            Assert.Equal(42, resultSync);
            Assert.Equal(2, policy.BulkheadAvailableCount);
            Assert.Equal(0, policy.QueueAvailableCount);

            int resultWithContext = policy.Execute(ctx => (int)ctx["val"] * 2, new Dictionary<string, object> { ["val"] = 21 });
            Assert.Equal(42, resultWithContext);
            Assert.Equal(2, policy.BulkheadAvailableCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithinParallelLimit_ExecutesSuccessfully()
        {
            using var policy = VardPolicy.Bulkhead(2, 1);

            int resultAsync = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 84;
            });
            Assert.Equal(84, resultAsync);
            Assert.Equal(2, policy.BulkheadAvailableCount);
            Assert.Equal(1, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task Execute_ZeroQueue_ExceedingParallelLimit_ThrowsBulkheadRejectedException_ExecutionSaturated()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);
            var runningTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var backgroundTask = Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    runningTcs.SetResult(true);
                    releaseTcs.Task.GetAwaiter().GetResult();
                    return 1;
                });
            });

            await runningTcs.Task;

            var ex = Assert.Throws<BulkheadRejectedException>(() =>
            {
                policy.Execute(() => 2);
            });

            Assert.Equal(BulkheadRejectionReason.ExecutionSaturated, ex.Reason);
            Assert.Equal(1, ex.MaxParallelization);
            Assert.Equal(0, ex.MaxQueuedActions);

            releaseTcs.SetResult(true);
            await backgroundTask;
            Assert.Equal(1, policy.BulkheadAvailableCount);
        }

        [Fact]
        public async Task ExecuteAsync_ZeroQueue_ExceedingParallelLimit_ThrowsBulkheadRejectedException_ExecutionSaturated()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);
            var tcsRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var backgroundTask = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcsRunning.SetResult(true);
                    await tcsRelease.Task;
                    return 1;
                });
            });

            await tcsRunning.Task;

            var ex = await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return 2;
                });
            });

            Assert.Equal(BulkheadRejectionReason.ExecutionSaturated, ex.Reason);
            Assert.Equal(1, ex.MaxParallelization);
            Assert.Equal(0, ex.MaxQueuedActions);

            tcsRelease.SetResult(true);
            await backgroundTask;
            Assert.Equal(1, policy.BulkheadAvailableCount);
        }

        [Fact]
        public async Task Execute_WithQueue_ExceedingQueueLimit_ThrowsBulkheadRejectedException_QueueFull()
        {
            using var policy = VardPolicy.Bulkhead(1, 1);
            var runningTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Task 1: Occupies execution slot
            var task1 = Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    runningTcs.SetResult(true);
                    releaseTcs.Task.GetAwaiter().GetResult();
                    return 1;
                });
            });

            await runningTcs.Task;

            // Task 2: Occupies queue slot
            var task2 = Task.Run(() =>
            {
                queueTcs.SetResult(true);
                policy.Execute(() => 2);
            });

            await queueTcs.Task;
            // Wait until task2 acquired queue permit
            for (int i = 0; i < 50 && policy.QueueAvailableCount > 0; i++)
            {
                await Task.Delay(10);
            }
            Assert.Equal(0, policy.QueueAvailableCount);

            // Task 3: Queue is full, should throw QueueFull immediately
            var ex = Assert.Throws<BulkheadRejectedException>(() =>
            {
                policy.Execute(() => 3);
            });

            Assert.Equal(BulkheadRejectionReason.QueueFull, ex.Reason);
            Assert.Equal(1, ex.MaxParallelization);
            Assert.Equal(1, ex.MaxQueuedActions);

            releaseTcs.SetResult(true);
            await Task.WhenAll(task1, task2);
            Assert.Equal(1, policy.BulkheadAvailableCount);
            Assert.Equal(1, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithQueue_ExceedingQueueLimit_ThrowsBulkheadRejectedException_QueueFull()
        {
            using var policy = VardPolicy.Bulkhead(1, 1);
            var tcsRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Task 1 occupies execution slot
            var task1 = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcsRunning.SetResult(true);
                    await tcsRelease.Task;
                    return 1;
                });
            });

            await tcsRunning.Task;

            // Task 2 occupies queue slot
            var task2 = Task.Run(async () =>
            {
                return await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return 2;
                });
            });

            // Wait until task2 acquired queue permit
            for (int i = 0; i < 50 && policy.QueueAvailableCount > 0; i++)
            {
                await Task.Delay(10);
            }
            Assert.Equal(0, policy.QueueAvailableCount);

            // Task 3 rejects immediately with QueueFull
            var ex = await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return 3;
                });
            });

            Assert.Equal(BulkheadRejectionReason.QueueFull, ex.Reason);

            tcsRelease.SetResult(true);
            await Task.WhenAll(task1, task2);
            Assert.Equal(1, policy.BulkheadAvailableCount);
            Assert.Equal(1, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task Execute_QueuedActions_ExecuteSequentiallyWhenSlotsFreed()
        {
            using var policy = VardPolicy.Bulkhead(1, 2);
            var results = new List<int>();
            var lockObj = new object();

            var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task1 = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcs1.SetResult(true);
                    await tcsRelease.Task;
                    lock (lockObj) results.Add(1);
                    return 1;
                });
            });

            await tcs1.Task;

            var task2 = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    lock (lockObj) results.Add(2);
                    return 2;
                });
            });

            var task3 = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    lock (lockObj) results.Add(3);
                    return 3;
                });
            });

            // Release task 1
            tcsRelease.SetResult(true);
            await Task.WhenAll(task1, task2, task3);

            Assert.Equal(new[] { 1, 2, 3 }, results);
            Assert.Equal(1, policy.BulkheadAvailableCount);
            Assert.Equal(2, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task Execute_CancellationToken_WhileQueued_ReleasesQueueSlot_WithoutFiringCallback()
        {
            bool callbackFired = false;
            using var policy = VardPolicy.Bulkhead(
                maxParallelization: 1,
                maxQueuedActions: 2,
                onBulkheadRejected: _ => callbackFired = true,
                onBulkheadRejectedAsync: async _ =>
                {
                    await Task.Yield();
                    callbackFired = true;
                });

            var tcsRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Task 1 occupies execution slot
            var task1 = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcsRunning.SetResult(true);
                    await tcsRelease.Task;
                    return 1;
                });
            });

            await tcsRunning.Task;

            using var cts = new CancellationTokenSource();

            // Task 2 waits in queue with cancellation token
            var task2 = Task.Run(async () =>
            {
                return await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return 2;
                }, cts.Token);
            });

            // Wait until task2 is queued
            for (int i = 0; i < 50 && policy.QueueAvailableCount == 2; i++)
            {
                await Task.Delay(10);
            }
            Assert.Equal(1, policy.QueueAvailableCount);

            // Cancel task 2 while queued
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);

            // Queue slot must be released
            Assert.Equal(2, policy.QueueAvailableCount);
            // Callback must NOT have fired on cancellation
            Assert.False(callbackFired);

            tcsRelease.SetResult(true);
            await task1;
            Assert.Equal(1, policy.BulkheadAvailableCount);
        }

        [Fact]
        public void Execute_ActionThrowsException_ReleasesExecutionSlot()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);

            Assert.Throws<InvalidOperationException>(() =>
            {
                policy.Execute<int>(() => throw new InvalidOperationException("Boom"));
            });

            Assert.Equal(1, policy.BulkheadAvailableCount);

            // Next execution succeeds
            int res = policy.Execute(() => 100);
            Assert.Equal(100, res);
            Assert.Equal(1, policy.BulkheadAvailableCount);
        }

        [Fact]
        public async Task ExecuteAsync_ActionThrowsException_ReleasesExecutionSlot()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await policy.ExecuteAsync<int>(async ct =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Boom");
                });
            });

            Assert.Equal(1, policy.BulkheadAvailableCount);

            int res = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 200;
            });
            Assert.Equal(200, res);
            Assert.Equal(1, policy.BulkheadAvailableCount);
        }

        [Fact]
        public async Task Execute_ObservabilityCallbacks_InvokedOnRejection_WithContext()
        {
            IDictionary<string, object>? capturedContextSync = null;
            using var policy = VardPolicy.Bulkhead(
                1,
                0,
                onBulkheadRejected: ctx => capturedContextSync = ctx);

            var runningTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    runningTcs.SetResult(true);
                    releaseTcs.Task.GetAwaiter().GetResult();
                });
            });

            await runningTcs.Task;

            var ctx = new Dictionary<string, object> { ["reqId"] = "12345" };
            Assert.Throws<BulkheadRejectedException>(() =>
            {
                policy.Execute(_ => 42, ctx);
            });

            Assert.NotNull(capturedContextSync);
            Assert.Equal("12345", capturedContextSync!["reqId"]);

            releaseTcs.SetResult(true);
            await task;
        }

        [Fact]
        public async Task ExecuteAsync_ObservabilityCallbacks_InvokedOnRejection_WithContext()
        {
            IDictionary<string, object>? capturedContextAsync = null;
            using var policy = VardPolicy.Bulkhead(
                1,
                0,
                onBulkheadRejectedAsync: async ctx =>
                {
                    await Task.Yield();
                    capturedContextAsync = ctx;
                });

            var tcsRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcsRunning.SetResult(true);
                    await tcsRelease.Task;
                });
            });

            await tcsRunning.Task;

            var ctx = new Dictionary<string, object> { ["traceId"] = "trace-abc" };
            await Assert.ThrowsAsync<BulkheadRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async (c, ct) =>
                {
                    await Task.Yield();
                    return 1;
                }, ctx);
            });

            Assert.NotNull(capturedContextAsync);
            Assert.Equal("trace-abc", capturedContextAsync!["traceId"]);

            tcsRelease.SetResult(true);
            await task;
        }

        [Fact]
        public async Task Execute_CallbackThrowsException_FailsFast()
        {
            using var policy = VardPolicy.Bulkhead(
                1,
                0,
                onBulkheadRejected: _ => throw new ApplicationException("Callback error"));

            var runningTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    runningTcs.SetResult(true);
                    releaseTcs.Task.GetAwaiter().GetResult();
                });
            });

            await runningTcs.Task;

            Assert.Throws<ApplicationException>(() =>
            {
                policy.Execute(() => 42);
            });

            releaseTcs.SetResult(true);
            await task;
        }

        [Fact]
        public async Task ExecuteAndCapture_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);
            var runningTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    runningTcs.SetResult(true);
                    releaseTcs.Task.GetAwaiter().GetResult();
                });
            });

            await runningTcs.Task;

            var result = policy.ExecuteAndCapture(() => 42);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result.ExceptionType);
            Assert.IsType<BulkheadRejectedException>(result.FinalException);
            Assert.Equal(default, result.Result);

            releaseTcs.SetResult(true);
            await task;
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);
            var tcsRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var task = Task.Run(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    tcsRunning.SetResult(true);
                    await tcsRelease.Task;
                });
            });

            await tcsRunning.Task;

            var result = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Yield();
                return 42;
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result.ExceptionType);
            Assert.IsType<BulkheadRejectedException>(result.FinalException);

            tcsRelease.SetResult(true);
            await task;
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_WhenCancelled_ReturnsPolicyResult_Unhandled()
        {
            using var policy = VardPolicy.Bulkhead(1, 1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Yield();
                return 42;
            }, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExceptionType.Unhandled, result.ExceptionType);
            Assert.IsAssignableFrom<OperationCanceledException>(result.FinalException);
        }

        [Fact]
        public async Task Dispose_SubsequentExecutionsThrowObjectDisposedException()
        {
            var policy = VardPolicy.Bulkhead(2, 2);
            policy.Dispose();

            Assert.Throws<ObjectDisposedException>(() => policy.Execute(() => 1));
            Assert.Throws<ObjectDisposedException>(() => _ = policy.BulkheadAvailableCount);
            Assert.Throws<ObjectDisposedException>(() => _ = policy.QueueAvailableCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => policy.ExecuteAsync(ct => Task.FromResult(1)));
        }

        [Fact]
        public void FluentBuilder_And_VardPolicy_ConstructCorrectly()
        {
            var p1 = VardPolicy.Bulkhead(3, 5);
            Assert.Equal(3, p1.MaxParallelization);
            Assert.Equal(5, p1.MaxQueuedActions);
            Assert.Equal(3, p1.BulkheadAvailableCount);
            Assert.Equal(5, p1.QueueAvailableCount);

            var p2 = VardPolicy.Handle<Exception>().Bulkhead(4, 2);
            Assert.Equal(4, p2.MaxParallelization);
            Assert.Equal(2, p2.MaxQueuedActions);
            Assert.Equal(4, p2.BulkheadAvailableCount);
            Assert.Equal(2, p2.QueueAvailableCount);
        }

        [Fact]
        public async Task ActionExtensions_ExecuteVoidActionAndFuncTask_WorkAsExpected()
        {
            using var policy = VardPolicy.Bulkhead(1, 0);
            int count = 0;

            policy.Execute(() => { count++; });
            Assert.Equal(1, count);

            await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                count++;
            });
            Assert.Equal(2, count);
        }
    }
}
