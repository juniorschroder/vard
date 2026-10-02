using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class RetryPolicyTests
    {
        // RET-01: Sucesso imediato
        [Fact]
        public void Execute_WhenActionSucceedsImmediately_ReturnsResultAndNoRetry()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(3);

            var result = policy.Execute(() =>
            {
                invocations++;
                return "ok";
            });

            result.Should().Be("ok");
            invocations.Should().Be(1);
        }

        // RET-01: Sucesso após falhas transitórias
        [Fact]
        public void Execute_WhenActionFailsThenSucceeds_RetriesAndReturnsResult()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(3);

            var result = policy.Execute(() =>
            {
                invocations++;
                if (invocations < 3) throw new InvalidOperationException("transient");
                return 42;
            });

            result.Should().Be(42);
            invocations.Should().Be(3);
        }

        // RET-01: Exaustão de tentativas
        [Fact]
        public void Execute_WhenActionExhaustsRetries_ThrowsLastException()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(2);

            Action act = () => policy.Execute(() =>
            {
                invocations++;
                throw new InvalidOperationException("persistent error");
            });

            act.Should().Throw<InvalidOperationException>().WithMessage("persistent error");
            invocations.Should().Be(3); // 1 initial + 2 retries
        }

        // RET-05: Exceção não tratada não causa retry
        [Fact]
        public void Execute_WhenUnhandledExceptionOccurs_ThrowsImmediatelyWithoutRetry()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(3);

            Action act = () => policy.Execute(() =>
            {
                invocations++;
                throw new ArgumentException("unhandled");
            });

            act.Should().Throw<ArgumentException>();
            invocations.Should().Be(1);
        }

        // RET-05: Retry por resultado
        [Fact]
        public void Execute_WhenResultMatchesCondition_RetriesUntilSuccess()
        {
            int invocations = 0;
            var policy = VardPolicy.HandleResult<int>(r => r == 500).Retry(2);

            var result = policy.Execute(() =>
            {
                invocations++;
                return invocations < 2 ? 500 : 200;
            });

            result.Should().Be(200);
            invocations.Should().Be(2);
        }

        // OBS-01: Callback OnRetry síncrono recebe dados corretos
        [Fact]
        public void Execute_WhenRetryOccurs_CallsOnRetryCallback()
        {
            var attempts = new List<int>();
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(
                retryCount: 2,
                onRetry: (ex, res, attempt, delay, ctx) =>
                {
                    attempts.Add(attempt);
                });

            policy.Execute(() =>
            {
                if (attempts.Count < 2) throw new InvalidOperationException();
                return true;
            });

            attempts.Should().Equal(1, 2);
        }

        // D-11: Callback OnRetry lançando exceção propaga fail-fast
        [Fact]
        public void Execute_WhenOnRetryThrows_PropagatesExceptionImmediately()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(
                retryCount: 2,
                onRetry: (ex, res, attempt, delay, ctx) =>
                {
                    throw new ApplicationException("callback crash");
                });

            Action act = () => policy.Execute(() => throw new InvalidOperationException());

            act.Should().Throw<ApplicationException>().WithMessage("callback crash");
        }

        // Async Execution
        [Fact]
        public async Task ExecuteAsync_WhenActionFailsThenSucceeds_RetriesSuccessfully()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(3);

            var result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                invocations++;
                if (invocations < 2) throw new InvalidOperationException();
                return "async ok";
            });

            result.Should().Be("async ok");
            invocations.Should().Be(2);
        }

        // Async OnRetry Task Callback
        [Fact]
        public async Task ExecuteAsync_WithAsyncOnRetryCallback_AwaitsCallback()
        {
            bool callbackRan = false;
            var policy = VardPolicy.Handle<InvalidOperationException>().RetryWithBackoff(
                retryCount: 1,
                initialDelay: TimeSpan.FromMilliseconds(5),
                onRetryAsync: async (ex, res, attempt, delay, ctx) =>
                {
                    await Task.Yield();
                    callbackRan = true;
                });

            int invocations = 0;
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                invocations++;
                if (invocations == 1) throw new InvalidOperationException();
                return 100;
            });

            callbackRan.Should().BeTrue();
            invocations.Should().Be(2);
        }

        // ExecuteAndCapture Success
        [Fact]
        public void ExecuteAndCapture_OnSuccess_ReturnsSuccessResult()
        {
            var policy = VardPolicy.Handle<Exception>().Retry(2);
            var result = policy.ExecuteAndCapture(() => 42);

            result.IsSuccess.Should().BeTrue();
            result.Result.Should().Be(42);
            result.AttemptNumber.Should().Be(1);
        }

        // ExecuteAndCapture Failure
        [Fact]
        public void ExecuteAndCapture_OnExhaustion_ReturnsHandledByConditionFailure()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(2);
            var result = policy.ExecuteAndCapture<int>(() => throw new InvalidOperationException("boom"));

            result.IsSuccess.Should().BeFalse();
            result.ExceptionType.Should().Be(ExceptionType.HandledByCondition);
            result.Exception.Should().BeOfType<InvalidOperationException>();
            result.AttemptNumber.Should().Be(3);
        }

        // Context retention
        [Fact]
        public void Execute_WithContext_PreservesContextInstance()
        {
            IDictionary<string, object>? receivedCtx = null;
            var policy = VardPolicy.Handle<InvalidOperationException>().Retry(
                retryCount: 1,
                onRetry: (ex, res, attempt, delay, ctx) => { receivedCtx = ctx; });

            var myCtx = new Dictionary<string, object> { ["traceId"] = "123" };
            policy.Execute(ctx =>
            {
                if (receivedCtx == null) throw new InvalidOperationException();
                return true;
            }, myCtx);

            receivedCtx.Should().BeSameAs(myCtx);
        }
    }
}
