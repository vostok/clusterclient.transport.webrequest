using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Transport;
using Vostok.Commons.Collections;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

// ReSharper disable MethodSupportsCancellation

namespace Vostok.Clusterclient.Transport.Webrequest
{
    /// <summary>
    /// <para>Represents an <see cref="ITransport"/> implementation which uses <see cref="HttpWebRequest"/> to send requests to replicas.</para>
    /// <para>You can also use <see cref="IClusterClientConfigurationExtensions.SetupWebRequestTransport(Core.IClusterClientConfiguration)"/> extension to set up this transport in your configuration.</para>
    /// </summary>
    public class WebRequestTransport : ITransport
    {
        private const int BufferSize = 16 * 1024;
        private const int LOHObjectSizeThreshold = 84 * 1000;

        private static readonly UnboundedObjectPool<byte[]> BuffersPool = new UnboundedObjectPool<byte[]>(() => new byte[BufferSize]);

        private readonly ILog log;
        private readonly ConnectTimeLimiter connectTimeLimiter;
        private readonly ThreadPoolMonitor threadPoolMonitor;

        /// <inheritdoc cref="WebRequestTransport" />
        public WebRequestTransport(WebRequestTransportSettings settings, ILog log)
        {
            Settings = settings;

            this.log = log ?? throw new ArgumentNullException(nameof(log));

            connectTimeLimiter = new ConnectTimeLimiter(log);
            threadPoolMonitor = ThreadPoolMonitor.Instance;

            WebRequestTuner.Touch();
        }

        /// <inheritdoc cref="WebRequestTransport" />
        public WebRequestTransport(ILog log)
            : this(new WebRequestTransportSettings(), log)
        {
        }

        /// <inheritdoc />
        public TransportCapabilities Capabilities =>
            TransportCapabilities.RequestStreaming |
            TransportCapabilities.ResponseStreaming;

        /// <inheritdoc />
        public async Task<Response> SendAsync(Request request, TimeSpan? connectionTimeout, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout.TotalMilliseconds < 1)
            {
                LogRequestTimeout(request, timeout);
                return new Response(ResponseCode.RequestTimeout);
            }

            var state = new WebRequestState(timeout);

            using (var timeoutCancellation = new CancellationTokenSource())
            {
                var timeoutTask = Task.Delay(state.TimeRemaining, timeoutCancellation.Token);
                var senderTask = SendInternalAsync(request, state, connectionTimeout, cancellationToken);
                var completedTask = await Task.WhenAny(timeoutTask, senderTask).ConfigureAwait(false);
                if (completedTask is Task<Response> taskWithResponse)
                {
                    timeoutCancellation.Cancel();
                    return taskWithResponse.GetAwaiter().GetResult();
                }

                // If the completed task does not cast to Task<Response>, it means that timeout was triggered.
                state.CancelRequest();
                LogRequestTimeout(request, timeout);

                if (Settings.FixThreadPoolProblems)
                {
                    threadPoolMonitor.ReportAndFixIfNeeded(log);
                }

                // Lets try to wait for request sending task completion before returning result:
                var senderTaskContinuation = senderTask.ContinueWith(
                    t =>
                    {
                        if (t.IsCompleted)
                            t.GetAwaiter().GetResult().Dispose();
                    });

                var abortWaitingDelay = Task.Delay(Settings.RequestAbortTimeout);

                await Task.WhenAny(senderTaskContinuation, abortWaitingDelay).ConfigureAwait(false);

                if (!senderTask.IsCompleted)
                    LogFailedToWaitForRequestAbort();

                return ResponseFactory.BuildResponse(ResponseCode.RequestTimeout, state);
            }
        }

        internal WebRequestTransportSettings Settings { get; }

        private static bool NeedToReadResponseBody(Request request, WebRequestState state)
        {
            if (request.Method == RequestMethods.Head)
                return false;

            // ContentLength can be -1 in case server returns content without setting the header. It is a default on HttpWebRequest level.
            return state.Response.ContentLength != 0;
        }

        private static bool IsCancellationException(Exception error)
        {
            return error is OperationCanceledException || (error as WebException)?.Status == WebExceptionStatus.RequestCanceled;
        }

        private async Task<Response> SendInternalAsync(Request request, WebRequestState state, TimeSpan? connectionTimeout, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(state.CancelRequest))
            using (state)
            {
                if (state.RequestCancelled)
                    return new Response(ResponseCode.Canceled);

                state.Request = WebRequestFactory.Create(request, state.TimeRemaining, Settings, log);

                HttpActionStatus status;

                // Step 1 - send request body if it exists.
                if (state.RequestCancelled)
                    return new Response(ResponseCode.Canceled);

                if (request.HasBody)
                {
                    status = await connectTimeLimiter.LimitConnectTime(SendRequestBodyAsync(request, state), request, state, connectionTimeout).ConfigureAwait(false);

                    if (status == HttpActionStatus.ConnectionFailure)
                        return Responses.ConnectFailure;

                    if (status != HttpActionStatus.Success)
                        return ResponseFactory.BuildFailureResponse(status, state);
                }

                // Step 2 - receive response from server.
                if (state.RequestCancelled)
                    return new Response(ResponseCode.Canceled);

                status = request.HasBody
                    ? await GetResponseAsync(request, state).ConfigureAwait(false)
                    : await connectTimeLimiter.LimitConnectTime(GetResponseAsync(request, state), request, state, connectionTimeout).ConfigureAwait(false);

                if (status == HttpActionStatus.ConnectionFailure)
                    return Responses.ConnectFailure;

                if (status != HttpActionStatus.Success)
                    return ResponseFactory.BuildFailureResponse(status, state);

                // Step 3 - download request body if it exists.
                if (!NeedToReadResponseBody(request, state))
                    return ResponseFactory.BuildSuccessResponse(state);

                if (ResponseBodyIsTooLarge(state))
                {
                    state.CancelRequestAttempt();
                    return ResponseFactory.BuildResponse(ResponseCode.InsufficientStorage, state);
                }

                if (state.RequestCancelled)
                    return new Response(ResponseCode.Canceled);

                if (NeedToStreamResponseBody(state))
                {
                    state.ReturnStreamDirectly = true;
                    state.PreventNextDispose();
                    return ResponseFactory.BuildSuccessResponse(state);
                }

                status = await ReadResponseBodyAsync(request, state).ConfigureAwait(false);
                return status == HttpActionStatus.Success
                    ? ResponseFactory.BuildSuccessResponse(state)
                    : ResponseFactory.BuildFailureResponse(status, state);
            }
        }

        private async Task<HttpActionStatus> SendRequestBodyAsync(Request request, WebRequestState state)
        {
            try
            {
                state.RequestStream = await state.Request.GetRequestStreamAsync().ConfigureAwait(false);
            }
            catch (WebException error)
            {
                return HandleWebException(request, state, error);
            }
            catch (Exception error)
            {
                LogUnknownException(error);
                return HttpActionStatus.UnknownFailure;
            }

            try
            {
                var content = request.Content;

                if (content != null)
                {
                    if (content.Length < LOHObjectSizeThreshold)
                    {
                        await state.RequestStream
                            .WriteAsync(content.Buffer, request.Content.Offset, request.Content.Length)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        using (BuffersPool.Acquire(out var buffer))
                        {
                            var index = content.Offset;
                            var end = content.Offset + content.Length;
                            while (index < end)
                            {
                                var size = Math.Min(buffer.Length, end - index);
                                Buffer.BlockCopy(content.Buffer, index, buffer, 0, size);
                                await state.RequestStream.WriteAsync(buffer, 0, size).ConfigureAwait(false);
                                index += size;
                            }
                        }
                    }
                }
                else if (request.StreamContent != null)
                {
                    var bodyStream = request.StreamContent.Stream;
                    var bytesToSend = request.StreamContent.Length ?? long.MaxValue;
                    var bytesSent = 0L;

                    using (BuffersPool.Acquire(out var buffer))
                    {
                        while (bytesSent < bytesToSend)
                        {
                            var bytesToRead = (int) Math.Min(buffer.Length, bytesToSend - bytesSent);

                            int bytesRead;

                            try
                            {
                                bytesRead = await bodyStream.ReadAsync(buffer, 0, bytesToRead).ConfigureAwait(false);
                            }
                            catch (StreamAlreadyUsedException)
                            {
                                throw;
                            }
                            catch (Exception error)
                            {
                                if (IsCancellationException(error))
                                    return HttpActionStatus.RequestCanceled;

                                LogUserStreamFailure(error);

                                return HttpActionStatus.UserStreamFailure;
                            }

                            if (bytesRead == 0)
                                break;

                            await state.RequestStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                            bytesSent += bytesRead;
                        }
                    }
                }

                state.CloseRequestStream();
            }
            catch (StreamAlreadyUsedException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (IsCancellationException(error))
                    return HttpActionStatus.RequestCanceled;

                LogSendBodyFailure(request, error);
                return HttpActionStatus.SendFailure;
            }

            return HttpActionStatus.Success;
        }

        private async Task<HttpActionStatus> GetResponseAsync(Request request, WebRequestState state)
        {
            try
            {
                state.Response = (HttpWebResponse)await state.Request.GetResponseAsync().ConfigureAwait(false);
                state.ResponseStream = state.Response.GetResponseStream();
                return HttpActionStatus.Success;
            }
            catch (WebException error)
            {
                var status = HandleWebException(request, state, error);
                // HttpWebRequest reacts to response codes like 404 or 500 with an exception with ProtocolError status. 
                if (status == HttpActionStatus.ProtocolError)
                {
                    state.Response = (HttpWebResponse)error.Response;
                    state.ResponseStream = state.Response.GetResponseStream();
                    return HttpActionStatus.Success;
                }

                return status;
            }
            catch (Exception error)
            {
                LogUnknownException(error);
                return HttpActionStatus.UnknownFailure;
            }
        }

        private bool NeedToStreamResponseBody(WebRequestState state)
        {
            try
            {
                var contentLength = null as long?;

                if (state.Response.ContentLength >= 0)
                    contentLength = state.Response.ContentLength;

                return Settings.UseResponseStreaming(contentLength);
            }
            catch (Exception error)
            {
                log.Error(error);
                return false;
            }
        }

        private bool ResponseBodyIsTooLarge(WebRequestState state)
        {
            var size = Math.Max(state.Response.ContentLength, state.BodyStream?.Length ?? 0L);
            var limit = Settings.MaxResponseBodySize ?? long.MaxValue;

            if (size > limit)
            {
                LogResponseBodyTooLarge(size, limit);
            }

            return size > limit;
        }

        private async Task<HttpActionStatus> ReadResponseBodyAsync(Request request, WebRequestState state)
        {
            try
            {
                var contentLength = (int)state.Response.ContentLength;
                if (contentLength > 0)
                {
                    state.BodyBuffer = Settings.BufferFactory(contentLength);

                    var totalBytesRead = 0;

                    // If a contentLength-sized buffer won't end up in LOH, it can be used directly to work with socket.
                    // Otherwise, it's better to use a small temporary buffer from a pool because the passed reference will be stored for a long time due to Keep-Alive.
                    if (contentLength < LOHObjectSizeThreshold)
                    {
                        while (totalBytesRead < contentLength)
                        {
                            var bytesToRead = Math.Min(contentLength - totalBytesRead, BufferSize);
                            var bytesRead = await state.ResponseStream.ReadAsync(state.BodyBuffer, totalBytesRead, bytesToRead).ConfigureAwait(false);
                            if (bytesRead == 0)
                                break;

                            totalBytesRead += bytesRead;
                        }
                    }
                    else
                    {
                        using (BuffersPool.Acquire(out var buffer))
                        {
                            while (totalBytesRead < contentLength)
                            {
                                var bytesToRead = Math.Min(contentLength - totalBytesRead, buffer.Length);
                                var bytesRead = await state.ResponseStream.ReadAsync(buffer, 0, bytesToRead).ConfigureAwait(false);
                                if (bytesRead == 0)
                                    break;

                                Buffer.BlockCopy(buffer, 0, state.BodyBuffer, totalBytesRead, bytesRead);

                                totalBytesRead += bytesRead;
                            }
                        }
                    }

                    if (totalBytesRead < contentLength)
                        throw new EndOfStreamException($"Response stream ended prematurely. Read only {totalBytesRead} byte(s), but Content-Length specified {contentLength}.");

                    state.BodyBufferLength = totalBytesRead;
                }
                else
                {
                    state.BodyStream = new MemoryStream();

                    using (BuffersPool.Acquire(out var buffer))
                    {
                        while (true)
                        {
                            var bytesRead = await state.ResponseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if (bytesRead == 0)
                                break;

                            state.BodyStream.Write(buffer, 0, bytesRead);

                            if (ResponseBodyIsTooLarge(state))
                            {
                                state.CancelRequestAttempt();
                                state.BodyStream = null;
                                return HttpActionStatus.InsufficientStorage;
                            }
                        }
                    }
                }

                return HttpActionStatus.Success;
            }
            catch (Exception error)
            {
                if (IsCancellationException(error))
                    return HttpActionStatus.RequestCanceled;

                LogReceiveBodyFailure(request, error);
                return HttpActionStatus.ReceiveFailure;
            }
        }

        private HttpActionStatus HandleWebException(Request request, WebRequestState state, WebException error)
        {
            switch (error.Status)
            {
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.KeepAliveFailure:
                case WebExceptionStatus.ConnectionClosed:
                case WebExceptionStatus.PipelineFailure:
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.ProxyNameResolutionFailure:
                case WebExceptionStatus.SecureChannelFailure:
                    LogConnectionFailure(request, error, state.ConnectionAttempt);
                    return HttpActionStatus.ConnectionFailure;
                case WebExceptionStatus.SendFailure:
                    LogWebException(error);
                    return HttpActionStatus.SendFailure;
                case WebExceptionStatus.ReceiveFailure:
                    LogWebException(error);
                    return HttpActionStatus.ReceiveFailure;
                case WebExceptionStatus.RequestCanceled: return HttpActionStatus.RequestCanceled;
                case WebExceptionStatus.Timeout: return HttpActionStatus.Timeout;
                case WebExceptionStatus.ProtocolError: return HttpActionStatus.ProtocolError;
                default:
                    LogWebException(error);
                    return HttpActionStatus.UnknownFailure;
            }
        }

        #region Logging

        private void LogRequestTimeout(Request request, TimeSpan timeout)
        {
            log.Error("Request timed out. Target = {Target}. Timeout = {Timeout}.", request.Url.Authority, timeout.ToPrettyString());
        }

        private void LogConnectionFailure(Request request, WebException error, int attempt)
        {
            log.Warn(
                error.InnerException ?? error,
                "Connection failure. Target = {Target}. Status = {Status}.",
                request.Url.Authority,
                error.Status);
        }

        private void LogWebException(WebException error)
        {
            log.Error(error.InnerException ?? error, "Error in sending request. Status = {Status}.", error.Status);
        }

        private void LogUnknownException(Exception error)
        {
            log.Error(error, "Unknown error in sending request.");
        }

        private void LogSendBodyFailure(Request request, Exception error)
        {
            log.Error(error, "Error in sending request body to {Target}", request.Url.Authority);
        }

        private void LogUserStreamFailure(Exception error)
        {
            log.Error(error, "Failure in reading input stream while sending request body.");
        }

        private void LogReceiveBodyFailure(Request request, Exception error)
        {
            log.Error(error, "Error in receiving request body from {Target}", request.Url.Authority);
        }

        private void LogFailedToWaitForRequestAbort()
        {
            log.Warn("Timed out request was aborted but did not complete in {RequestAbortTimeout}.", Settings.RequestAbortTimeout.ToPrettyString());
        }

        private void LogResponseBodyTooLarge(long size, long limit)
        {
            log.Error("Response body size {Size} is larger than configured limit of {Limit} bytes.", size, limit);
        }

        #endregion
    }
}