using System;
using JetBrains.Annotations;
using Vostok.Clusterclient.Core;

namespace Vostok.Clusterclient.Transport.Webrequest
{
    [PublicAPI]
    public static class IClusterClientConfigurationExtensions
    {
        /// <summary>
        /// Initialiazes configuration transport with a <see cref="WebRequestTransport"/> with given settings.
        /// </summary>
        [Obsolete("This module is now obsolete. Please use an equivalent extension from Vostok.ClusterClient.Transport library.")]
        public static void SetupWebRequestTransport(this IClusterClientConfiguration self, WebRequestTransportSettings settings)
        {
            self.Transport = new WebRequestTransport(settings, self.Log);
        }

        /// <summary>
        /// Initialiazes configuration transport with a <see cref="WebRequestTransport"/> with default settings.
        /// </summary>
        [Obsolete("This module is now obsolete. Please use an equivalent extension from Vostok.ClusterClient.Transport library.")]
        public static void SetupWebRequestTransport(this IClusterClientConfiguration self)
        {
            self.Transport = new WebRequestTransport(self.Log);
        }
    }
}