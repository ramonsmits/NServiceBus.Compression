using System;
using System.IO.Compression;
using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBus;

public static class ConfigureExtensions
{
    /// <summary>
    /// Enable compression of message bodies using the specified algorithm.
    /// Receivers must have a version that supports the chosen algorithm. See <see cref="CompressionAlgorithm"/> for version requirements.
    /// </summary>
    public static void CompressMessageBody(
        this EndpointConfiguration config,
        CompressionAlgorithm algorithm,
        CompressionLevel compressionLevel,
        int thresholdSize)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdSize);

        var properties = new Options
        {
            Algorithm = algorithm,
            CompressionLevel = compressionLevel,
            ThresholdSize = thresholdSize
        };
        config.GetSettings().Set(properties);
        config.EnableFeature<CompressionFeature>();
    }

    /// <summary>
    /// Enable GZip compression of message bodies
    /// </summary>
    public static void CompressMessageBody(
        this EndpointConfiguration config,
        CompressionLevel compressionLevel,
        int thresholdSize)
    {
        CompressMessageBody(config, CompressionAlgorithm.GZip, compressionLevel, thresholdSize);
    }
}
