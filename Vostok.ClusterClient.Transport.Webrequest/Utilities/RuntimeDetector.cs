using System;
using System.Runtime.InteropServices;

namespace Vostok.ClusterClient.Transport.Webrequest.Utilities
{
    internal static class RuntimeDetector
    {
        public static bool IsMono { get; } = Type.GetType("Mono.Runtime") != null;
        public static bool IsDotNetFramework { get; } = RuntimeEnvironment.GetRuntimeDirectory().Contains(@"Microsoft.NET\Framework");
    }
}