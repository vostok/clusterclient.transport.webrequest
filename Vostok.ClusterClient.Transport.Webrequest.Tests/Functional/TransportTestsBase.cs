using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Clusterclient.Core.Model;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;

namespace Vostok.Clusterclient.Transport.Webrequest.Tests.Functional
{
    [TestFixture]
    internal class TransportTestsBase
    {
        protected ILog log;
        protected WebRequestTransport transport;

        static TransportTestsBase()
        {
            ThreadPoolUtility.Setup();
        }

        [SetUp]
        public virtual void SetUp()
        {
            log = new ConsoleLog();
            transport = new WebRequestTransport(log);
        }

        protected Task<Response> SendAsync(Request request, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendAsync(request, null, timeout ?? 1.Minutes(), cancellationToken);
        }

        protected Response Send(Request request, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return transport.SendAsync(request, null, timeout ?? 1.Minutes(), cancellationToken).GetAwaiter().GetResult();
        }
        
        protected Task<Response> SendAsync(Request request, TimeSpan? connectionTimeout, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return transport.SendAsync(request, connectionTimeout, timeout ?? 1.Minutes(), cancellationToken);
        }

        protected Response Send(Request request, TimeSpan? connectionTimeout, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return transport.SendAsync(request, connectionTimeout, timeout ?? 1.Minutes(), cancellationToken).GetAwaiter().GetResult();
        }
    }
}
