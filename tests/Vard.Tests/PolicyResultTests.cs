using System;
using System.Collections.Generic;
using FluentAssertions;
using Vard.Abstractions;
using Xunit;
using Void = Vard.Abstractions.Void;

namespace Vard.Tests
{
    public class PolicyResultTests
    {
        // API-07: PolicyResult<T>.Success factory
        [Fact]
        public void Success_SetsIsSuccessTrue()
        {
            var result = PolicyResult<int>.Success(42);
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Success_SetsResult()
        {
            var result = PolicyResult<int>.Success(42);
            result.Result.Should().Be(42);
        }

        [Fact]
        public void Success_SetsExceptionTypeNone()
        {
            var result = PolicyResult<int>.Success(42);
            result.ExceptionType.Should().Be(ExceptionType.None);
        }

        [Fact]
        public void Success_SetsExceptionNull()
        {
            var result = PolicyResult<int>.Success(42);
            result.Exception.Should().BeNull();
        }

        [Fact]
        public void Success_WithContext_SetsContextReference()
        {
            var ctx = new Dictionary<string, object> { ["key"] = "value" };
            var result = PolicyResult<int>.Success(42, context: ctx);
            // D-04: mesmo objeto, não cópia
            result.Context.Should().BeSameAs(ctx);
        }

        [Fact]
        public void Success_WithExecutionTimeAndAttempt_SetsMetadata()
        {
            var elapsed = TimeSpan.FromMilliseconds(123);
            var result = PolicyResult<int>.Success(42, executionTime: elapsed, attemptNumber: 3);
            result.ExecutionTime.Should().Be(elapsed);
            result.AttemptNumber.Should().Be(3);
        }

        // API-07: PolicyResult<T>.Failure factory
        [Fact]
        public void Failure_SetsIsSuccessFalse()
        {
            var ex = new InvalidOperationException("fail");
            var result = PolicyResult<int>.Failure(ex, ExceptionType.HandledByCondition);
            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public void Failure_SetsException()
        {
            var ex = new InvalidOperationException("fail");
            var result = PolicyResult<int>.Failure(ex, ExceptionType.HandledByCondition);
            result.Exception.Should().BeSameAs(ex);
        }

        [Fact]
        public void Failure_SetsExceptionType()
        {
            var ex = new InvalidOperationException("fail");
            var result = PolicyResult<int>.Failure(ex, ExceptionType.Unhandled);
            result.ExceptionType.Should().Be(ExceptionType.Unhandled);
        }

        [Fact]
        public void Failure_SetsFinalExceptionToExceptionWhenNotProvided()
        {
            var ex = new InvalidOperationException("fail");
            var result = PolicyResult<int>.Failure(ex, ExceptionType.HandledByCondition);
            result.FinalException.Should().BeSameAs(ex);
        }

        [Fact]
        public void Failure_SetsResultToDefault()
        {
            var ex = new InvalidOperationException("fail");
            var result = PolicyResult<int>.Failure(ex, ExceptionType.HandledByCondition);
            result.Result.Should().Be(default(int));
        }

        [Fact]
        public void Failure_WithNullException_ThrowsArgumentNullException()
        {
            Action act = () => PolicyResult<int>.Failure(null!, ExceptionType.Unhandled);
            act.Should().Throw<ArgumentNullException>();
        }

        // Void sentinel
        [Fact]
        public void Void_CanBeUsedAsGenericArg()
        {
            var result = PolicyResult<Void>.Success(Void.Instance);
            result.IsSuccess.Should().BeTrue();
            result.Result.Should().Be(Void.Instance);
        }

        // ExceptionType enum completeness
        [Fact]
        public void ExceptionType_HasExactlyFiveValues()
        {
            var values = Enum.GetValues(typeof(ExceptionType));
            values.Length.Should().Be(5);
        }
    }
}
