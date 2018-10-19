using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Transport.Tests.Shared.Performance;
using Vostok.Clusterclient.Transport.Webrequest.Utilities;
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
            ThreadPoolUtility.SetUp();
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
    
    
    public class Class1 : EchoServerTestsBase
    {
        [Test]
        public void Test()
        {
            ThreadPool.SetMaxThreads(32767, 32767);
            ThreadPool.SetMinThreads(2048, 2048);
            var transport = new WebRequestTransport(new WebRequestTransportSettings(), new SilentLog());
            var bytes = new byte[10000000];
            new Random(42).NextBytes(bytes);
            var request = Request.Post("http://localhost:8080").WithContent(bytes);

            var count = SendRequests(transport, request, 10.Seconds(), 10);
            Console.WriteLine(count);
        }
    }
}
