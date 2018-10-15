using System;
using System.Threading;
using Vostok.Clusterclient.Transport.Webrequest.Utilities;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Transport.Webrequest
{
    internal class ThreadPoolMonitor
    {
        private const int TargetMultiplier = 128;

        public static readonly ThreadPoolMonitor Instance = new ThreadPoolMonitor();

        private static readonly TimeSpan minReportInterval = TimeSpan.FromSeconds(1);

        private readonly object syncObject;
        private DateTime lastReportTimestamp;

        public ThreadPoolMonitor()
        {
            syncObject = new object();
            lastReportTimestamp = DateTime.MinValue;
        }

        public void ReportAndFixIfNeeded(ILog log)
        {
            int minWorkerThreads;
            int minIocpThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minIocpThreads);

            int maxWorkerThreads;
            int maxIocpThreads;
            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxIocpThreads);

            int availableWorkerThreads;
            int availableIocpThreads;
            ThreadPool.GetAvailableThreads(out availableWorkerThreads, out availableIocpThreads);

            var busyWorkerThreads = maxWorkerThreads - availableWorkerThreads;
            var busyIocpThreads = maxIocpThreads - availableIocpThreads;

            if (busyWorkerThreads < minWorkerThreads && busyIocpThreads < minIocpThreads)
                return;

            var currentTimestamp = DateTime.UtcNow;

            lock (syncObject)
            {
                if (currentTimestamp - lastReportTimestamp < minReportInterval)
                    return;

                lastReportTimestamp = currentTimestamp;
            }

            log = log.ForContext<ThreadPoolMonitor>();

            log.Warn(
                "Looks like you're kinda low on ThreadPool, buddy. Workers: {0}/{1}/{2}, IOCP: {3}/{4}/{5} (busy/min/max).",
                busyWorkerThreads,
                minWorkerThreads,
                maxWorkerThreads,
                busyIocpThreads,
                minIocpThreads,
                maxIocpThreads);

            var currentMultiplier = Math.Min(minWorkerThreads/Environment.ProcessorCount, minIocpThreads/Environment.ProcessorCount);
            if (currentMultiplier < TargetMultiplier)
            {
                log.Info("I will configure ThreadPool for you, buddy!");
                ThreadPoolUtility.SetUp(TargetMultiplier);
            }
        }
    }
}
