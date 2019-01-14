using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace Vostok.Clusterclient.Transport.Webrequest
{
    internal class WebRequestState : IDisposable
    {
        private readonly TimeSpan timeout;
        private readonly Stopwatch stopwatch;
        private int cancellationState;
        private int disposeBarrier;

        public WebRequestState(TimeSpan timeout)
        {
            this.timeout = timeout;
            stopwatch = Stopwatch.StartNew();
        }

        public HttpWebRequest Request { get; set; }
        public HttpWebResponse Response { get; set; }

        public Stream RequestStream { get; set; }
        public Stream ResponseStream { get; set; }

        public byte[] BodyBuffer { get; set; }
        public int BodyBufferLength { get; set; }

        public MemoryStream BodyStream { get; set; }
        public bool ReturnStreamDirectly { get; set; }

        public TimeSpan TimeRemaining
        {
            get
            {
                var remaining = timeout - stopwatch.Elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
        public bool RequestCancelled => cancellationState > 0;

        public void CancelRequest()
        {
            Interlocked.Exchange(ref cancellationState, 1);

            CancelRequestAttempt();
        }

        public void CancelRequestAttempt()
        {
            if (Request != null)
                try
                {
                    Request.Abort();
                }
                catch
                {
                    // ignored
                }
        }

        public void PreventNextDispose()
        {
            Interlocked.Exchange(ref disposeBarrier, 1);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposeBarrier, 0) > 0)
                return;

            CloseRequestStream();
            CloseResponseStream();
        }

        public void CloseRequestStream()
        {
            if (RequestStream != null)
                try
                {
                    RequestStream.Close();
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    RequestStream = null;
                }
        }

        private void CloseResponseStream()
        {
            if (ResponseStream != null)
                try
                {
                    ResponseStream.Close();
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    ResponseStream = null;
                }
        }
    }
}