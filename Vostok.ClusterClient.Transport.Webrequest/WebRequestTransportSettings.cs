using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Vostok.Commons.Helpers.Conversions;
using Vostok.Commons.Primitives;

namespace Vostok.ClusterClient.Transport.Webrequest
{
    public class WebRequestTransportSettings
    {
        public bool Pipelined { get; set; } = true;

        public bool FixThreadPoolProblems { get; set; } = true;

        public int ConnectionAttempts { get; set; } = 2;

        public TimeSpan? ConnectionTimeout { get; set; } = 750.Milliseconds();

        public TimeSpan ConnectionIdleTimeout { get; set; } = 2.Minutes();

        public TimeSpan RequestAbortTimeout { get; set; } = 250.Milliseconds();

        public IWebProxy Proxy { get; set; } = null;

        public int MaxConnectionsPerEndpoint = 10 * 1000;

        public DataSize? MaxResponseBodySize { get; set; } = null;

        public Predicate<long?> UseResponseStreaming { get; set; } = _ => false;

        public string ConnectionGroupName { get; set; } = null;

        public bool AllowAutoRedirect { get; set; } = false;

        public bool TcpKeepAliveEnabled { get; set; } = false;

        public TimeSpan TcpKeepAliveTime { get; set; } = 3.Seconds();

        public TimeSpan TcpKeepAlivePeriod { get; set; } = 1.Seconds();

        public bool ArpCacheWarmupEnabled { get; set; } = false;

        public X509Certificate2[] ClientCertificates { get; set; } = null;

        internal Func<int, byte[]> BufferFactory { get; set; } = size => new byte[size];

        internal bool FixNonAsciiHeaders { get; set; } = false;
    }
}
