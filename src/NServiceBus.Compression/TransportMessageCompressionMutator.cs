using System;
using System.Buffers;
using System.Buffers.Binary;
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
        var totalLength = maxLength + sizeof(long);
        var rented = ArrayPool<byte>.Shared.Rent(totalLength);
        try
        {
            var window = Math.Clamp((int)Math.Ceiling(Math.Log2(input.Length)) + 1, 10, 22);

            if (!BrotliEncoder.TryCompress(input.Span, rented, out var bytesWritten, (int)CompressionLevel, window))
            {
                throw new InvalidOperationException("Brotli compression failed.");
            }

            // Append original size as a trailer for span-based decompression
            BinaryPrimitives.WriteInt64LittleEndian(rented.AsSpan(bytesWritten), input.Length);

            return rented.AsMemory(0, bytesWritten + sizeof(long)).ToArray();
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

        if (algorithm == CompressionAlgorithm.Brotli)
        {
            context.Body = DecompressBrotli(context.Body);
        }
        else
        {
            using var compressedBodyStream = CreateReadStream(context.Body);
            using var decompressionStream = CreateDecompressionStream(compressedBodyStream, algorithm.Value);
            var output = new MemoryStream();
            decompressionStream.CopyTo(output);
            context.Body = GetBufferAsMemory(output);
        }

        return Task.CompletedTask;
    }

    static ReadOnlyMemory<byte> DecompressBrotli(ReadOnlyMemory<byte> input)
    {
        var originalSize = (int)BinaryPrimitives.ReadInt64LittleEndian(input.Span[^sizeof(long)..]);
        var compressedData = input[..^sizeof(long)];
        var result = new byte[originalSize];

        if (!BrotliDecoder.TryDecompress(compressedData.Span, result, out var bytesWritten) || bytesWritten != originalSize)
        {
            throw new InvalidOperationException("Brotli decompression failed.");
        }

        return result;
    }

    static Stream CreateCompressionStream(Stream output, CompressionAlgorithm algorithm, CompressionLevel level) => algorithm switch
    {
        CompressionAlgorithm.GZip => new GZipStream(output, level, leaveOpen: true),
        CompressionAlgorithm.Deflate => new DeflateStream(output, level, leaveOpen: true),
        CompressionAlgorithm.ZLib => new ZLibStream(output, level, leaveOpen: true),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    static Stream CreateDecompressionStream(Stream input, CompressionAlgorithm algorithm) => algorithm switch
    {
        CompressionAlgorithm.GZip => new GZipStream(input, CompressionMode.Decompress),
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
