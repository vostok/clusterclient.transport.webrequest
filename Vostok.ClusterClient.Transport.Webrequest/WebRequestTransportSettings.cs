using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace Vostok.Clusterclient.Transport.Webrequest
{
    [PublicAPI]
    public class WebRequestTransportSettings
    {
        public bool Pipelined { get; set; } = true;

        public bool FixThreadPoolProblems { get; set; } = true;

        public TimeSpan ConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        public TimeSpan RequestAbortTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

        public IWebProxy Proxy { get; set; }

        public int MaxConnectionsPerEndpoint { get; set; } = 10 * 1000;

        public long? MaxResponseBodySize { get; set; }

        public Predicate<long?> UseResponseStreaming { get; set; } = _ => false;

        public string ConnectionGroupName { get; set; }

        public bool AllowAutoRedirect { get; set; }

        public bool TcpKeepAliveEnabled { get; set; } = true;

        public TimeSpan TcpKeepAliveTime { get; set; } = TimeSpan.FromSeconds(3);

        public TimeSpan TcpKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);

        public bool ArpCacheWarmupEnabled { get; set; } = true;

        public X509Certificate2[] ClientCertificates { get; set; }

        internal Func<int, byte[]> BufferFactory { get; set; } = size => new byte[size];

        internal bool FixNonAsciiHeaders { get; set; }
    }
}