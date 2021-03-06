﻿namespace NServiceBus
{
    using System;
    using System.IO.Compression;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public static partial class ConfigureExtensions
    {
        /// <summary>
        /// Enable compression of message bodies
        /// </summary>
        public static void CompressMessageBody(
            this EndpointConfiguration config,
            CompressionLevel compressionLevel,
            int thresholdSize
            )
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (thresholdSize <= 0) throw new ArgumentOutOfRangeException(nameof(thresholdSize), thresholdSize, "Threshold size must be greater than 0.");

            var properties = new Options
            {
                CompressionLevel = compressionLevel,
                ThresholdSize = thresholdSize
            };
            var settings = config.GetSettings();
            settings.Set<Options>(properties);
        }
    }
}
