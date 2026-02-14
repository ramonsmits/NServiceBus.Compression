using System;
using System.IO.Compression;
using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBus;

public static class ConfigureExtensions
{
    /// <summary>
    /// Enable compression of message bodies
    /// </summary>
    public static void CompressMessageBody(
        this EndpointConfiguration config,
        CompressionLevel compressionLevel,
        int thresholdSize)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdSize);

        var properties = new Options
        {
            CompressionLevel = compressionLevel,
            ThresholdSize = thresholdSize
        };
        config.GetSettings().Set(properties);
        config.EnableFeature<CompressionFeature>();
    }
}
