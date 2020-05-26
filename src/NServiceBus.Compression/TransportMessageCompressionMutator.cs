﻿using System;
using System.IO;
using System.IO.Compression;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;
using NServiceBus.Unicast.Messages;

class CompressionFeature : Feature
{
    static readonly ILog Log = LogManager.GetLogger("NServiceBus.Compression");

    public CompressionFeature()
    {
        EnableByDefault();
    }

    // This is requires to workaround a limitation in version 5 which is not able to register mutators from features.
    class RegisterMutator : INeedInitialization
    {
        public void Customize(BusConfiguration busConfiguration)
        {
            busConfiguration.RegisterComponents(
                registration: components =>
                {
                    components.ConfigureComponent<TransportMessageCompressionMutator>(DependencyLifecycle.SingleInstance);
                });
        }
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        if (!context.Settings.TryGet(out Properties properties)) properties = new Properties();
        Log.InfoFormat("Compression level: {0}", properties.CompressionLevel);
        Log.InfoFormat("Threshold: {0:N0} bytes", properties.ThresholdSize);
        context.Container.RegisterSingleton(properties);
    }

    class TransportMessageCompressionMutator : IMutateTransportMessages
    {
        static readonly bool IsDebugEnabled = Log.IsDebugEnabled;
        const string HeaderKey = "Content-Encoding";
        const string HeaderValue = "gzip";
        readonly int CompressThresshold;
        readonly CompressionLevel CompressionLevel;

        public TransportMessageCompressionMutator(Properties properties)
        {
            CompressionLevel = properties.CompressionLevel;
            CompressThresshold = properties.ThresholdSize;
        }

        public void MutateOutgoing(LogicalMessage message, TransportMessage transportMessage)
        {
            var exceedsCompressionThresshold = transportMessage.Body.Length > CompressThresshold;

            if (!exceedsCompressionThresshold)
            {
                if (IsDebugEnabled) Log.Debug("Skip compression, not exceeding compression thresshold.");
                return;
            }

            var uncompressedBodyStream = new MemoryStream(transportMessage.Body, false);
            var compressedBodyStream = new MemoryStream();

            using (var compressionStream = new GZipStream(compressedBodyStream, CompressionLevel))
            {
                uncompressedBodyStream.CopyTo(compressionStream);
            }

            var compressedBody = compressedBodyStream.ToArray();

            if (IsDebugEnabled) Log.DebugFormat("Uncompressed: {0:N0}, Compressed: {1:N0} ({2:N})", transportMessage.Body.Length, compressedBody.Length, 100D * compressedBody.Length / transportMessage.Body.Length);

            if (compressedBody.Length > transportMessage.Body.Length)
            {
                Log.InfoFormat("Compression didn't save any bytes, ignoring. Consider raising the current compression thresshold of {0:N0} bytes.", CompressThresshold);
                return;
            }

            transportMessage.Body = compressedBody;
            transportMessage.Headers[HeaderKey] = HeaderValue;
        }

        public void MutateIncoming(TransportMessage transportMessage)
        {
            if (transportMessage.Headers.TryGetValue(HeaderKey, out var value))
            {
                if (value != HeaderValue) throw new NotSupportedException($"Unsupported compression method: {value}");
                var compressedBodyStream = new MemoryStream(transportMessage.Body, false);
                using (var bigStream = new GZipStream(compressedBodyStream, CompressionMode.Decompress))
                {
                    var uncompressedBodyStream = new MemoryStream();
                    bigStream.CopyTo(uncompressedBodyStream);
                    transportMessage.Body = uncompressedBodyStream.ToArray();
                }
            }
        }
    }
}
