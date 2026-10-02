using System;
using FluentAssertions;
using Vard.Builders;
using Xunit;

namespace Vard.Tests
{
    public class HandleConditionTests
    {
        // API-08: HandleException<T> por tipo
        [Fact]
        public void ShouldHandle_ReturnsTrueForMatchingExceptionType()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>();

            condition.ShouldHandle(new InvalidOperationException("x"), null).Should().BeTrue();
        }

        [Fact]
        public void ShouldHandle_ReturnsFalseForNonMatchingExceptionType()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>();

            condition.ShouldHandle(new ArgumentException("y"), null).Should().BeFalse();
        }

        [Fact]
        public void ShouldHandle_ReturnsTrueForSubclassException()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<Exception>(); // base type

            condition.ShouldHandle(new InvalidOperationException("sub"), null).Should().BeTrue();
        }

        // API-09: HandleException<T> com predicate
        [Fact]
        public void ShouldHandle_WithPredicate_ReturnsTrueWhenPredicatePasses()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>(ex => ex.Message == "targeted");

            condition.ShouldHandle(new InvalidOperationException("targeted"), null).Should().BeTrue();
        }

        [Fact]
        public void ShouldHandle_WithPredicate_ReturnsFalseWhenPredicateFails()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>(ex => ex.Message == "targeted");

            condition.ShouldHandle(new InvalidOperationException("other"), null).Should().BeFalse();
        }

        // API-10: HandleResult<T> com predicate
        [Fact]
        public void ShouldHandle_WithResultPredicate_ReturnsTrueWhenPredicatePasses()
        {
            var condition = new HandleCondition();
            condition.AddResultPredicate<int>(r => r == 500);

            condition.ShouldHandle(null, 500).Should().BeTrue();
        }

        [Fact]
        public void ShouldHandle_WithResultPredicate_ReturnsFalseWhenPredicateFails()
        {
            var condition = new HandleCondition();
            condition.AddResultPredicate<int>(r => r == 500);

            condition.ShouldHandle(null, 200).Should().BeFalse();
        }

        [Fact]
        public void ShouldHandle_WithResultPredicate_IgnoresNullWhenPredicateDoesNotMatch()
        {
            var condition = new HandleCondition();
            condition.AddResultPredicate<string>(r => r == "fail");

            condition.ShouldHandle(null, null).Should().BeFalse();
        }

        // API-11: Or() combinator
        [Fact]
        public void ShouldHandle_WithOrCombinator_ReturnsTrueForFirstCondition()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>();
            condition.AddExceptionPredicate<TimeoutException>();

            condition.ShouldHandle(new InvalidOperationException(), null).Should().BeTrue();
        }

        [Fact]
        public void ShouldHandle_WithOrCombinator_ReturnsTrueForSecondCondition()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>();
            condition.AddExceptionPredicate<TimeoutException>();

            condition.ShouldHandle(new TimeoutException(), null).Should().BeTrue();
        }

        [Fact]
        public void ShouldHandle_WithOrCombinator_ReturnsFalseForUnmatchedCondition()
        {
            var condition = new HandleCondition();
            condition.AddExceptionPredicate<InvalidOperationException>();
            condition.AddExceptionPredicate<TimeoutException>();

            condition.ShouldHandle(new ArgumentException(), null).Should().BeFalse();
        }

        // Edge case: no predicates
        [Fact]
        public void ShouldHandle_WithNoPredicates_ReturnsFalse()
        {
            var condition = new HandleCondition();
            condition.ShouldHandle(new Exception(), null).Should().BeFalse();
        }
    }
}
