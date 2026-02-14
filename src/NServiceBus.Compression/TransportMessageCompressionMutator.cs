using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;

class TransportMessageCompressionMutator(Options properties) : IMutateIncomingTransportMessages, IMutateOutgoingTransportMessages
{
    static readonly ILog Log = CompressionFeature.Log;
    static readonly bool IsDebugEnabled = Log.IsDebugEnabled;
    const string HeaderKey = "Content-Encoding";
    const string HeaderValue = "gzip";
    readonly int CompressThreshold = properties.ThresholdSize;
    readonly CompressionLevel CompressionLevel = properties.CompressionLevel;

    public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
    {
        if (context.OutgoingBody.Length <= CompressThreshold)
        {
            if (IsDebugEnabled) Log.Debug("Skip compression, not exceeding compression threshold.");
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
            Log.InfoFormat("Compression didn't save any bytes, ignoring. Consider raising the current compression threshold of {0:N0} bytes.", CompressThreshold);
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
