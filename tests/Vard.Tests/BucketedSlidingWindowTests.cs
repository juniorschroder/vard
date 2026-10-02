using System;
using Vard.Common;
using Xunit;

namespace Vard.Tests
{
    public class BucketedSlidingWindowTests
    {
        [Fact]
        public void Constructor_InvalidArguments_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BucketedSlidingWindow(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BucketedSlidingWindow(TimeSpan.FromSeconds(-5)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BucketedSlidingWindow(TimeSpan.FromSeconds(10), bucketCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BucketedSlidingWindow(TimeSpan.FromSeconds(10), bucketCount: -1));
        }

        [Fact]
        public void EmptyWindow_ReturnsZeroCountsAndZeroRate()
        {
            var window = new BucketedSlidingWindow(TimeSpan.FromSeconds(10));
            var (total, failures, rate) = window.GetSnapshot();

            Assert.Equal(0, total);
            Assert.Equal(0, failures);
            Assert.Equal(0.0, rate);
        }

        [Fact]
        public void RecordSuccessAndFailure_CalculatesRateCorrectly()
        {
            long currentTicks = 10_000_000; // 1s
            var window = new BucketedSlidingWindow(TimeSpan.FromSeconds(10), bucketCount: 10, ticksProvider: () => currentTicks);

            window.RecordSuccess();
            window.RecordSuccess();
            window.RecordFailure();

            var (total, failures, rate) = window.GetSnapshot();

            Assert.Equal(3, total);
            Assert.Equal(1, failures);
            Assert.Equal(1.0 / 3.0, rate, precision: 4);
        }

        [Fact]
        public void Expiration_BucketsOutsideSamplingDuration_AreExcluded()
        {
            long currentTicks = 10_000_000; // t = 1s
            var window = new BucketedSlidingWindow(TimeSpan.FromSeconds(10), bucketCount: 10, ticksProvider: () => currentTicks);

            // Grava falha em t = 1s
            window.RecordFailure();

            // Avança o tempo para t = 5s (ainda na janela de 10s)
            currentTicks += 4 * TimeSpan.TicksPerSecond; // t = 5s
            window.RecordSuccess();

            var snapshotMid = window.GetSnapshot();
            Assert.Equal(2, snapshotMid.total);
            Assert.Equal(1, snapshotMid.failures);

            // Avança o tempo para t = 12s (a falha de t = 1s expirou pois 12s - 10s = 2s > 1s)
            currentTicks += 7 * TimeSpan.TicksPerSecond; // t = 12s

            var snapshotLater = window.GetSnapshot();
            Assert.Equal(1, snapshotLater.total); // Apenas o sucesso de t = 5s permanece
            Assert.Equal(0, snapshotLater.failures);
            Assert.Equal(0.0, snapshotLater.failureRate);
        }

        [Fact]
        public void Reset_ClearsAllCounts()
        {
            var window = new BucketedSlidingWindow(TimeSpan.FromSeconds(10));

            window.RecordSuccess();
            window.RecordFailure();

            var before = window.GetSnapshot();
            Assert.Equal(2, before.total);

            window.Reset();

            var after = window.GetSnapshot();
            Assert.Equal(0, after.total);
            Assert.Equal(0, after.failures);
            Assert.Equal(0.0, after.failureRate);
        }
    }
}
