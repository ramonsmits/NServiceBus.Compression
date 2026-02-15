using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;

class TransportMessageCompressionMutator(Options properties) : IMutateIncomingTransportMessages, IMutateOutgoingTransportMessages
{
    static readonly ILog Log = CompressionFeature.Log;
    static readonly bool IsDebugEnabled = Log.IsDebugEnabled;
    const string HeaderKey = "Content-Encoding";
    readonly CompressionAlgorithm Algorithm = properties.Algorithm;
    readonly int CompressThreshold = properties.ThresholdSize;
    readonly CompressionLevel CompressionLevel = properties.CompressionLevel;

    public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
    {
        if (context.OutgoingBody.Length <= CompressThreshold)
        {
            if (IsDebugEnabled) Log.Debug("Skip compression, not exceeding compression threshold.");
            return Task.CompletedTask;
        }

        ReadOnlyMemory<byte> compressedBody;

        if (Algorithm == CompressionAlgorithm.Brotli)
        {
            compressedBody = CompressBrotli(context.OutgoingBody);
        }
        else
        {
            compressedBody = CompressStream(context.OutgoingBody);
        }

        if (IsDebugEnabled) Log.DebugFormat("Uncompressed: {0:N0}, Compressed: {1:N0} ({2:N})", context.OutgoingBody.Length, compressedBody.Length, 100D * compressedBody.Length / context.OutgoingBody.Length);

        if (compressedBody.Length > context.OutgoingBody.Length)
        {
            Log.InfoFormat("Compression didn't save any bytes, ignoring. Consider raising the current compression threshold of {0:N0} bytes.", CompressThreshold);
            return Task.CompletedTask;
        }

        context.OutgoingBody = compressedBody;
        context.OutgoingHeaders[HeaderKey] = GetContentEncoding(Algorithm);
        return Task.CompletedTask;
    }

    ReadOnlyMemory<byte> CompressBrotli(ReadOnlyMemory<byte> input)
    {
        var maxLength = BrotliEncoder.GetMaxCompressedLength(input.Length);
        var rented = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            if (!BrotliEncoder.TryCompress(input.Span, rented, out var bytesWritten, (int)CompressionLevel, 22))
            {
                throw new InvalidOperationException("Brotli compression failed.");
            }

            return rented.AsMemory(0, bytesWritten).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    ReadOnlyMemory<byte> CompressStream(ReadOnlyMemory<byte> input)
    {
        var output = new MemoryStream(input.Length);

        using (var compressionStream = CreateCompressionStream(output, Algorithm, CompressionLevel))
        {
            compressionStream.Write(input.Span);
        }

        return GetBufferAsMemory(output);
    }

    public Task MutateIncoming(MutateIncomingTransportMessageContext context)
    {
        if (!context.Headers.TryGetValue(HeaderKey, out var encoding)) return Task.CompletedTask;

        var algorithm = ParseContentEncoding(encoding);
        if (algorithm is null) return Task.CompletedTask;

        using var compressedBodyStream = CreateReadStream(context.Body);
        using var decompressionStream = CreateDecompressionStream(compressedBodyStream, algorithm.Value);
        var output = new MemoryStream();
        decompressionStream.CopyTo(output);
        context.Body = GetBufferAsMemory(output);

        return Task.CompletedTask;
    }

    static Stream CreateCompressionStream(Stream output, CompressionAlgorithm algorithm, CompressionLevel level) => algorithm switch
    {
        CompressionAlgorithm.GZip => new GZipStream(output, level, leaveOpen: true),
        CompressionAlgorithm.Brotli => new BrotliStream(output, level, leaveOpen: true),
        CompressionAlgorithm.Deflate => new DeflateStream(output, level, leaveOpen: true),
        CompressionAlgorithm.ZLib => new ZLibStream(output, level, leaveOpen: true),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    static Stream CreateDecompressionStream(Stream input, CompressionAlgorithm algorithm) => algorithm switch
    {
        CompressionAlgorithm.GZip => new GZipStream(input, CompressionMode.Decompress),
        CompressionAlgorithm.Brotli => new BrotliStream(input, CompressionMode.Decompress),
        CompressionAlgorithm.Deflate => new DeflateStream(input, CompressionMode.Decompress),
        CompressionAlgorithm.ZLib => new ZLibStream(input, CompressionMode.Decompress),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    static string GetContentEncoding(CompressionAlgorithm algorithm) => algorithm switch
    {
        CompressionAlgorithm.GZip => "gzip",
        CompressionAlgorithm.Brotli => "br",
        CompressionAlgorithm.Deflate => "deflate",
        CompressionAlgorithm.ZLib => "zlib",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    static CompressionAlgorithm? ParseContentEncoding(string encoding) => encoding switch
    {
        "gzip" => CompressionAlgorithm.GZip,
        "br" => CompressionAlgorithm.Brotli,
        "deflate" => CompressionAlgorithm.Deflate,
        "zlib" => CompressionAlgorithm.ZLib,
        _ => null
    };

    static MemoryStream CreateReadStream(ReadOnlyMemory<byte> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment))
        {
            return new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false);
        }

        return new MemoryStream(memory.ToArray(), writable: false);
    }

    static ReadOnlyMemory<byte> GetBufferAsMemory(MemoryStream stream)
    {
        if (stream.TryGetBuffer(out var buffer))
        {
            return buffer.AsMemory();
        }

        return stream.ToArray();
    }
}
