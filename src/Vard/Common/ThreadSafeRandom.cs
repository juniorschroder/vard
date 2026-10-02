using System;

namespace Vard.Common
{
    /// <summary>
    /// Gerador de números pseudo-aleatórios thread-safe compatível com .NET Standard 2.1.
    /// </summary>
    internal static class ThreadSafeRandom
    {
        [ThreadStatic]
        private static Random? _local;

        private static Random Instance => _local ??= new Random(Guid.NewGuid().GetHashCode());

        public static double NextDouble() => Instance.NextDouble();
    }
}
