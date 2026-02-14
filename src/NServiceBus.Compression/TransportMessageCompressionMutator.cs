using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;
using CommunityToolkit.HighPerformance;

class TransportMessageCompressionMutator : IMutateIncomingTransportMessages, IMutateOutgoingTransportMessages
{
    static readonly ILog Log = CompressionFeature.Log;
    static readonly bool IsDebugEnabled = Log.IsDebugEnabled;
    const string HeaderKey = "Content-Encoding";
    const string HeaderValue = "gzip";
    readonly int CompressThresshold;
    readonly CompressionLevel CompressionLevel;

    public TransportMessageCompressionMutator(Options properties)
    {
        CompressionLevel = properties.CompressionLevel;
        CompressThresshold = properties.ThresholdSize;
    }

    public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
    {
        var exceedsCompressionThresshold = context.OutgoingBody.Length > CompressThresshold;

        if (!exceedsCompressionThresshold)
        {
            if (IsDebugEnabled) Log.Debug("Skip compression, not exceeding compression thresshold.");
            return Task.CompletedTask;
        }

        using var uncompressedBodyStream = ReadOnlyMemoryExtensions.AsStream(context.OutgoingBody);
        var compressedBodyStream = new MemoryStream();

        using (var compressionStream = new GZipStream(compressedBodyStream, CompressionLevel))
        {
            uncompressedBodyStream.CopyTo(compressionStream);
        }

        var compressedBody = compressedBodyStream.ToArray();

        if (IsDebugEnabled) Log.DebugFormat("Uncompressed: {0:N0}, Compressed: {1:N0} ({2:N})", context.OutgoingBody.Length, compressedBody.Length, 100D * compressedBody.Length / context.OutgoingBody.Length);

        if (compressedBody.Length > context.OutgoingBody.Length)
        {
            Log.InfoFormat("Compression didn't save any bytes, ignoring. Consider raising the current compression thresshold of {0:N0} bytes.", CompressThresshold);
            return Task.CompletedTask;
        }

        context.OutgoingBody = compressedBody;
        context.OutgoingHeaders[HeaderKey] = HeaderValue;
        return Task.CompletedTask;
    }

    public Task MutateIncoming(MutateIncomingTransportMessageContext context)
    {
        if (context.Headers.TryGetValue(HeaderKey, out var value))
        {
            if (value != HeaderValue) return Task.CompletedTask;
            using var compressedBodyStream = ReadOnlyMemoryExtensions.AsStream(context.Body);
            using var bigStream = new GZipStream(compressedBodyStream, CompressionMode.Decompress);
            var uncompressedBodyStream = new MemoryStream();
            bigStream.CopyTo(uncompressedBodyStream);
            context.Body = uncompressedBodyStream.ToArray();
        }
        return Task.CompletedTask;
    }
}