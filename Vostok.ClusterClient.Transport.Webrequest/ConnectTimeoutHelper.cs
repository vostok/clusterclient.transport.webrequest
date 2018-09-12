using System;
using System.Linq.Expressions;
using System.Net;
using Vostok.ClusterClient.Transport.Webrequest.Utilities;
using Vostok.Logging.Abstractions;

namespace Vostok.ClusterClient.Transport.Webrequest
{
    internal static class ConnectTimeoutHelper
    {
        private static readonly object sync = new object();

        private static volatile bool canCheckSocket = true;

        private static Func<HttpWebRequest, bool> isSocketConnected;

        public static bool IsSocketConnected(HttpWebRequest request, ILog log)
        {
            Initialize(log);

            if (!canCheckSocket)
                return true;

            try
            {
                return isSocketConnected(request);
            }
            catch (Exception error)
            {
                canCheckSocket = false;

                WrapLog(log).Error("Failed to check socket connection", error);
            }

            return true;
        }

        public static bool CanCheckSocket => canCheckSocket;

        private static void Initialize(ILog log)
        {
            if (isSocketConnected != null || !canCheckSocket)
                return;

            Exception savedError = null;

            lock (sync)
            {
                if (isSocketConnected != null || !canCheckSocket)
                    return;

                try
                {
                    if (RuntimeDetector.IsDotNetFramework)
                    {
                        isSocketConnected = BuildSocketConnectedChecker();
                    }
                    else
                    {
                        isSocketConnected = _ => true;
                        canCheckSocket = false;
                    }
                }
                catch (Exception error)
                {
                    canCheckSocket = false;
                    savedError = error;
                }
            }

            if (savedError != null)
                WrapLog(log).Error("Failed to build connection checker lambda", savedError);
        }

        /// <summary>
        /// Builds the following lambda:
        /// (HttpWebRequest request) => request._SubmitWriteStream != null && request._SubmitWriteStream.InternalSocket != null && request._SubmitWriteStream.InternalSocket.Connected
        /// </summary>
        private static Func<HttpWebRequest, bool> BuildSocketConnectedChecker()
        {
            var request = Expression.Parameter(typeof (HttpWebRequest));

            var stream = Expression.Field(request, "_SubmitWriteStream");
            var socket = Expression.Property(stream, "InternalSocket");
            var isConnected = Expression.Property(socket, "Connected");

            var body = Expression.AndAlso(
                Expression.ReferenceNotEqual(stream, Expression.Constant(null)),
                Expression.AndAlso(
                    Expression.ReferenceNotEqual(socket, Expression.Constant(null)),
                    isConnected));

            return Expression.Lambda<Func<HttpWebRequest, bool>>(body, request).Compile();
        }

        private static ILog WrapLog(ILog log)
        {
            return log.ForContext(typeof (ConnectTimeoutHelper).Name);
        }
    }
}
