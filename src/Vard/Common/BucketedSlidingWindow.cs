using System;
using System.Diagnostics;

namespace Vard.Common
{
    /// <summary>
    /// Janela deslizante de amostragem baseada em anel circular de buckets de tempo discretos.
    /// Thread-safe, complexidade O(1) de CPU e sem alocação de memória no heap durante a operação.
    /// </summary>
    internal sealed class BucketedSlidingWindow
    {
        private readonly int _bucketCount;
        private readonly long _bucketDurationTicks;
        private readonly long _samplingDurationTicks;
        private readonly Func<long> _ticksProvider;
        private readonly Bucket[] _buckets;
        private readonly object _syncLock = new object();

        internal sealed class Bucket
        {
            public long BucketStartTicks;
            public int SuccessCount;
            public int FailureCount;

            public void Reset(long startTicks)
            {
                BucketStartTicks = startTicks;
                SuccessCount = 0;
                FailureCount = 0;
            }
        }

        public BucketedSlidingWindow(
            TimeSpan samplingDuration,
            int bucketCount = 10,
            Func<long>? ticksProvider = null)
        {
            if (samplingDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(samplingDuration), "Sampling duration must be greater than zero.");
            if (bucketCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(bucketCount), "Bucket count must be greater than zero.");

            _samplingDurationTicks = samplingDuration.Ticks;
            _bucketCount = bucketCount;
            _bucketDurationTicks = Math.Max(1, _samplingDurationTicks / _bucketCount);
            _ticksProvider = ticksProvider ?? GetMonotonicTicks;

            _buckets = new Bucket[_bucketCount];
            for (int i = 0; i < _bucketCount; i++)
            {
                _buckets[i] = new Bucket();
            }
        }

        public void RecordSuccess() => Record(isSuccess: true);

        public void RecordFailure() => Record(isSuccess: false);

        private void Record(bool isSuccess)
        {
            long now = _ticksProvider();
            lock (_syncLock)
            {
                Bucket currentBucket = GetOrCreateCurrentBucket(now);
                if (isSuccess)
                    currentBucket.SuccessCount++;
                else
                    currentBucket.FailureCount++;
            }
        }

        public (int total, int failures, double failureRate) GetSnapshot()
        {
            long now = _ticksProvider();
            lock (_syncLock)
            {
                int totalSuccess = 0;
                int totalFailures = 0;
                long windowStart = now - _samplingDurationTicks;

                for (int i = 0; i < _bucketCount; i++)
                {
                    var b = _buckets[i];
                    if (b.BucketStartTicks > 0 && b.BucketStartTicks >= windowStart && b.BucketStartTicks <= now)
                    {
                        totalSuccess += b.SuccessCount;
                        totalFailures += b.FailureCount;
                    }
                }

                int total = totalSuccess + totalFailures;
                double rate = total == 0 ? 0.0 : (double)totalFailures / total;
                return (total, totalFailures, rate);
            }
        }

        public void Reset()
        {
            lock (_syncLock)
            {
                for (int i = 0; i < _bucketCount; i++)
                {
                    _buckets[i].Reset(0);
                }
            }
        }

        private Bucket GetOrCreateCurrentBucket(long now)
        {
            long bucketStart = (now / _bucketDurationTicks) * _bucketDurationTicks;
            int index = (int)((now / _bucketDurationTicks) % _bucketCount);
            if (index < 0) index = -index;

            var bucket = _buckets[index];
            if (bucket.BucketStartTicks != bucketStart)
            {
                bucket.Reset(bucketStart);
            }
            return bucket;
        }

        private static long GetMonotonicTicks()
        {
            return (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
        }
    }
}
