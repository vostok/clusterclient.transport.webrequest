using System.Net;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;

namespace Vostok.Clusterclient.Transport.Webrequest.Tests
{
    [TestFixture]
    internal class ConnectTimeoutHelper_Tests
    {
        private ILog log;

        [SetUp]
        public void TestSetup()
        {
            log = new SynchronousConsoleLog();
        }

        [Test]
        public void Should_be_able_to_build_checker_lambda()
        {
            ConnectTimeoutHelper.CanCheckSocket.Should().BeTrue();
        }

        [Test]
        public void Should_be_able_to_check_webrequest_connection()
        {
            var request = WebRequest.CreateHttp("http://kontur.ru/");

            ConnectTimeoutHelper.IsSocketConnected(request, log).Should().BeFalse();
        }
    }
}
