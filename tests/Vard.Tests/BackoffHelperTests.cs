using System;
using FluentAssertions;
using Vard.Abstractions;
using Vard.Common;
using Xunit;

namespace Vard.Tests
{
    public class BackoffHelperTests
    {
        [Fact]
        public void FixedBackoff_ReturnsConstantDelay()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            for (int i = 1; i <= 3; i++)
            {
                var delay = BackoffHelper.CalculateDelay(i, initial, BackoffType.Fixed, useJitter: false);
                delay.Should().Be(initial);
            }
        }

        [Fact]
        public void LinearBackoff_ScalesLinearlyWithAttempt()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            BackoffHelper.CalculateDelay(1, initial, BackoffType.Linear, false).Should().Be(TimeSpan.FromMilliseconds(100));
            BackoffHelper.CalculateDelay(2, initial, BackoffType.Linear, false).Should().Be(TimeSpan.FromMilliseconds(200));
            BackoffHelper.CalculateDelay(3, initial, BackoffType.Linear, false).Should().Be(TimeSpan.FromMilliseconds(300));
        }

        [Fact]
        public void ExponentialBackoff_ScalesExponentiallyWithAttempt()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            BackoffHelper.CalculateDelay(1, initial, BackoffType.Exponential, false).Should().Be(TimeSpan.FromMilliseconds(100));
            BackoffHelper.CalculateDelay(2, initial, BackoffType.Exponential, false).Should().Be(TimeSpan.FromMilliseconds(200));
            BackoffHelper.CalculateDelay(3, initial, BackoffType.Exponential, false).Should().Be(TimeSpan.FromMilliseconds(400));
        }

        [Fact]
        public void FullJitter_AlwaysWithinBounds()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            for (int i = 0; i < 50; i++)
            {
                var delay = BackoffHelper.CalculateDelay(2, initial, BackoffType.Exponential, useJitter: true);
                delay.TotalMilliseconds.Should().BeInRange(0, 200);
            }
        }

        [Fact]
        public void MaxDelay_CapsDelayWhenExceeded()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            var max = TimeSpan.FromMilliseconds(250);
            var delay = BackoffHelper.CalculateDelay(4, initial, BackoffType.Exponential, useJitter: false, maxDelay: max);
            delay.Should().Be(max);
        }
    }
}
