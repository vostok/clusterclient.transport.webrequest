using System;
using Vostok.Commons.Primitives;

namespace Vostok.ClusterClient.Transport.Webrequest.Utilities
{
    public static class ThreadSafeRandom
    {
        [ThreadStatic]
        private static Random random;

        public static double NextDouble() =>
            ObtainRandom().NextDouble();

        public static int Next() =>
            ObtainRandom().Next();

        public static int Next(int maxValue) =>
            ObtainRandom().Next(maxValue);

        public static int Next(int minValue, int maxValue) =>
            ObtainRandom().Next(minValue, maxValue);

        public static long Next(long minValue, long maxValue) =>
            Math.Abs(BitConverter.ToInt64(NextBytes(8), 0)%(maxValue - minValue)) + minValue;

        public static void NextBytes(byte[] buffer) =>
            ObtainRandom().NextBytes(buffer);

        public static byte[] NextBytes(DataSize size) =>
            NextBytes(size.Bytes);

        public static byte[] NextBytes(long size)
        {
            var buffer = new byte[size];
            NextBytes(buffer);
            return buffer;
        }

        public static bool FlipCoin() => NextDouble() <= 0.5;

        private static Random ObtainRandom() =>
            random ?? (random = new Random(Guid.NewGuid().GetHashCode()));
    }
}