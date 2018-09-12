using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.ClusterClient.Core.Model;
using Vostok.ClusterClient.Transport.Webrequest.Tests.Functional.Helpers;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Threading;

namespace Vostok.ClusterClient.Transport.Webrequest.Tests.Functional
{
    internal class ContentStreamingTests : TransportTestsBase
    {
        [SetUp]
        public void TestSetup()
        {
            transport.Settings.UseResponseStreaming = _ => true;
        }

        [Test]
        public void Should_return_a_response_with_stream_when_asked_for()
        {
            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.WriteByte(0xFF);
                }))
            {
                using (var response = Send(Request.Put(server.Url)))
                {
                    response.Code.Should().Be(ResponseCode.Ok);
                    response.HasContent.Should().BeFalse();
                    response.HasStream.Should().BeTrue();
                }
            }
        }

        [Test]
        public void Should_return_a_response_without_stream_when_asked_to()
        {
            transport.Settings.UseResponseStreaming = _ => false;

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.WriteByte(0xFF);
                }))
            {
                using (var response = Send(Request.Put(server.Url)))
                {
                    response.Code.Should().Be(ResponseCode.Ok);
                    response.HasContent.Should().BeTrue();
                    response.HasStream.Should().BeFalse();
                }
            }
        }

        [Test]
        [Timeout(3000)]
        public void Should_return_a_stream_immediately_even_when_server_sends_body_slowly()
        {
            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;

                    for (var i = 0; i < 10; i++)
                    {
                        var content = ThreadSafeRandom.NextBytes(4 * Constants.Kilobytes);

                        ctx.Response.OutputStream.Write(content, 0, content.Length);
                        ctx.Response.OutputStream.Flush();

                        Thread.Sleep(1.Seconds());
                    }
                }))
            {
                using (var response = Send(Request.Put(server.Url)))
                {
                    response.Code.Should().Be(ResponseCode.Ok);
                    response.HasStream.Should().BeTrue();
                }
            }
        }

        [Test]
        public void Should_release_server_connection_after_disposing_response()
        {
            var content = ThreadSafeRandom.NextBytes(4 * Constants.Kilobytes);

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Write(content, 0, content.Length);
                }))
            {
                for (var i = 0; i < 10; i++)
                {
                    Send(Request.Get(server.Url)).Dispose();
                }

                GetActiveConnections(server).Should().Be(1);
            }
        }

        [Test]
        public void Should_return_a_readable_stream_in_response()
        {
            var content = ThreadSafeRandom.NextBytes(512 * Constants.Kilobytes);

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Write(content, 0, content.Length);
                }))
            {
                using (var response = Send(Request.Get(server.Url)))
                {
                    var receivedBody = new MemoryStream();

                    response.Stream.CopyTo(receivedBody, 1024);

                    receivedBody.ToArray().Should().Equal(content);
                }
            }
        }

        [Test]
        public void Should_be_able_to_stream_a_really_large_response_body()
        {
            // (iloktionov): 2 GB
            var contentChunk = ThreadSafeRandom.NextBytes(64 * Constants.Kilobytes);
            var contentChunksCount = 32 * 1024L;

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;

                    for (var i = 0; i < contentChunksCount; i++)
                    {
                        ctx.Response.OutputStream.Write(contentChunk, 0, contentChunk.Length);
                    }
                }))
            {
                using (var response = Send(Request.Get(server.Url)))
                {
                    var watch = Stopwatch.StartNew();
                    var buffer = new byte[16 * 1024];
                    var bytesRead = 0;
                    var totalBytesRead = 0L;

                    while ((bytesRead = response.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytesRead += bytesRead;
                    }

                    totalBytesRead.Should().Be(contentChunk.Length * contentChunksCount);

                    Console.Out.WriteLine($"Read {totalBytesRead} response in {watch.Elapsed.ToPrettyString()}.");
                }
            }
        }

        [Test]
        public void Should_not_throw_any_errors_when_disposing_stream_after_server_closes_connection()
        {
            var content = ThreadSafeRandom.NextBytes(512 * Constants.Kilobytes);

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;

                    for (var i = 0; i < 3; i++)
                    {
                        ctx.Response.OutputStream.Write(content, 0, content.Length);
                    }

                    ctx.Response.Abort();
                }))
            {
                using (Send(Request.Get(server.Url)))
                {
                    Thread.Sleep(1.Seconds());
                }
            }
        }

        [Test]
        public void Should_throw_an_error_when_reading_stream_after_server_closes_connection()
        {
            var content = ThreadSafeRandom.NextBytes(512 * Constants.Kilobytes);

            using (var server = TestServer.StartNew(
                ctx =>
                {
                    ctx.Response.StatusCode = 200;

                    for (var i = 0; i < 3; i++)
                    {
                        ctx.Response.OutputStream.Write(content, 0, content.Length);
                    }

                    ctx.Response.Abort();
                }))
            {
                using (var response = Send(Request.Get(server.Url)))
                {
                    Thread.Sleep(1.Seconds());

                    Action action = () => response.Stream.CopyTo(Stream.Null);

                    action.Should().Throw<IOException>();
                }
            }
        }

        private static int GetActiveConnections(TestServer server)
        {
            return ServicePointManager.FindServicePoint(server.Url).CurrentConnections;
        }
    }
}