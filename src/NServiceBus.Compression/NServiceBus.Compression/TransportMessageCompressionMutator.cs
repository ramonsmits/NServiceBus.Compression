﻿using System;
using System.IO;
using System.IO.Compression;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;
using NServiceBus.Unicast.Messages;

class TransportMessageCompressionMutator : IMutateTransportMessages
{
    static readonly ILog Log = LogManager.GetLogger("TransportMessageCompressionMutator");
    static readonly bool IsDebugEnabled = Log.IsDebugEnabled;
    const string HeaderKey = "Content-Encoding";
    const string HeaderValue = "gzip";
    int compressThresshold = 1000;
    private readonly CompressionLevel CompressionLevel = CompressionLevel.Fastest;

    public void MutateOutgoing(LogicalMessage message, TransportMessage transportMessage)
    {
        var exceedsCompressionThresshold = transportMessage.Body.Length > compressThresshold;

        if (!exceedsCompressionThresshold) return;

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
            Log.InfoFormat("Compression didn't save any bytes, ignoring. Consider raising the current compression thresshold of {0:N0} bytes.", compressThresshold);
            return;
        }

        transportMessage.Body = compressedBody;
        transportMessage.Headers[HeaderKey] = HeaderValue;
    }

    public void MutateIncoming(TransportMessage transportMessage)
    {
        if (!transportMessage.Headers.ContainsKey(HeaderKey)) return;
        var value = transportMessage.Headers[HeaderKey];
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