using System.Net;
using System.Net.Sockets;

namespace Vostok.ClusterClient.Transport.Webrequest.Utilities
{
    /// <summary>
    /// Helper class to discover free TCP ports.
    /// </summary>
    public static class FreeTcpPortFinder
    {
        /// <summary>
        /// Returns a currently available TCP port to bind on.
        /// </summary>
        public static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}