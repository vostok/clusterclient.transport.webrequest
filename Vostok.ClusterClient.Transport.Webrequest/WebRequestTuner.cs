﻿using System;
using System.Net;
using System.Net.Security;
using Vostok.ClusterClient.Transport.Webrequest.ArpCache;
using Vostok.ClusterClient.Transport.Webrequest.Utilities;

namespace Vostok.ClusterClient.Transport.Webrequest
{
    internal static class WebRequestTuner
    {
        private static readonly bool IsMono = RuntimeDetector.IsMono;
        
        private static readonly BindIPEndPoint AddressSniffer = (servicePoint, endPoint, count) =>
        {
            ArpCacheMaintainer.ReportAddress(endPoint.Address);
            return null;
        };

        
        static WebRequestTuner()
        {
            if (!IsMono)
            {
                HttpWebRequest.DefaultMaximumErrorResponseLength = -1;  // (razboynikov): potential bug here in future. Just remember
                HttpWebRequest.DefaultMaximumResponseHeadersLength = int.MaxValue;  // (razboynikov): here was one with value -1

                ServicePointManager.CheckCertificateRevocationList = false;
                ServicePointManager.ServerCertificateValidationCallback = (_, __, ___, ____) => true;
            }
        }

        public static void Init()
        {
        }

        public static void Tune(HttpWebRequest request, TimeSpan timeout, WebRequestTransportSettings settings)
        {
            request.ConnectionGroupName = settings.ConnectionGroupName;
            request.Expect = null;
            request.KeepAlive = true;
            request.Pipelined = settings.Pipelined;
            request.Proxy = settings.Proxy;
            request.AllowAutoRedirect = settings.AllowAutoRedirect;
            request.AllowWriteStreamBuffering = false;
            request.AllowReadStreamBuffering = false;
            request.AuthenticationLevel = AuthenticationLevel.None;
            request.AutomaticDecompression = DecompressionMethods.None;
            
            var servicePoint = request.ServicePoint;

            servicePoint.Expect100Continue = false;
            servicePoint.UseNagleAlgorithm = false;
            servicePoint.ConnectionLimit = settings.MaxConnectionsPerEndpoint;
            servicePoint.MaxIdleTime = (int) settings.ConnectionIdleTimeout.TotalMilliseconds;

            if (settings.TcpKeepAliveEnabled)
            {
                servicePoint.SetTcpKeepAlive(true, (int) settings.TcpKeepAliveTime.TotalMilliseconds, (int) settings.TcpKeepAlivePeriod.TotalMilliseconds);
            }

            if (settings.ArpCacheWarmupEnabled)
            {
                if (servicePoint.BindIPEndPointDelegate == null)
                    servicePoint.BindIPEndPointDelegate = AddressSniffer;
            }
            else
            {
                servicePoint.BindIPEndPointDelegate = null;
            }

            if (!IsMono)
                servicePoint.ReceiveBufferSize = 16*1024;

            var timeoutInMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
            request.Timeout = timeoutInMilliseconds;
            request.ReadWriteTimeout = timeoutInMilliseconds;

            if (settings.ClientCertificates != null)
            {
                foreach (var certificate in settings.ClientCertificates)
                    request.ClientCertificates.Add(certificate);
            }
        }
    }
}