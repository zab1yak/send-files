using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace SendFiles.Core.Hosting;

/// <summary>Connection-level limits against slow or abusive clients (slowloris, header floods).</summary>
public static class KestrelLimits
{
    public const long DefaultMaxRequestBodySize = 64 * 1024;

    public static void Apply(KestrelServerLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.MaxConcurrentConnections = 64;
        limits.MaxConcurrentUpgradedConnections = 16;
        limits.MaxRequestHeadersTotalSize = 16 * 1024;
        limits.MaxRequestHeaderCount = 50;
        limits.MaxRequestLineSize = 4 * 1024;
        limits.MaxRequestBodySize = DefaultMaxRequestBodySize; // raised per endpoint for chunks
        limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
        limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 1024, gracePeriod: TimeSpan.FromSeconds(10));
        limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 1024, gracePeriod: TimeSpan.FromSeconds(10));
        limits.Http2.MaxStreamsPerConnection = 32;
    }
}
