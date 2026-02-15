using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using NServiceBus;
using NServiceBus.MessageMutator;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class CompressionBenchmarks
{
    byte[] compressibleData = null!;
    byte[] gzipCompressed = null!;
    byte[] brotliCompressed = null!;
    byte[] deflateCompressed = null!;
    byte[] zlibCompressed = null!;

    TransportMessageCompressionMutator gzipMutator = null!;
    TransportMessageCompressionMutator brotliMutator = null!;
    TransportMessageCompressionMutator deflateMutator = null!;
    TransportMessageCompressionMutator zlibMutator = null!;

    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        compressibleData = MakeCompressible(Size);

        gzipMutator = CreateMutator(CompressionAlgorithm.GZip);
        brotliMutator = CreateMutator(CompressionAlgorithm.Brotli);
        deflateMutator = CreateMutator(CompressionAlgorithm.Deflate);
        zlibMutator = CreateMutator(CompressionAlgorithm.ZLib);

        gzipCompressed = Compress(gzipMutator, compressibleData, "gzip");
        brotliCompressed = Compress(brotliMutator, compressibleData, "br");
        deflateCompressed = Compress(deflateMutator, compressibleData, "deflate");
        zlibCompressed = Compress(zlibMutator, compressibleData, "zlib");
    }

    static TransportMessageCompressionMutator CreateMutator(CompressionAlgorithm algorithm) =>
        new(new Options
        {
            Algorithm = algorithm,
            CompressionLevel = CompressionLevel.Fastest,
            ThresholdSize = 1
        });

    static byte[] MakeCompressible(int size)
    {
        var pattern = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. ");
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = pattern[i % pattern.Length];
        }
        return data;
    }

    static byte[] Compress(TransportMessageCompressionMutator mutator, byte[] data, string expectedEncoding)
    {
        var ctx = new MutateOutgoingTransportMessageContext(data, new object(), new Dictionary<string, string>(), null, null, CancellationToken.None);
        mutator.MutateOutgoing(ctx).GetAwaiter().GetResult();
        return ctx.OutgoingBody.ToArray();
    }

    // --- Compression benchmarks ---

    [Benchmark(Baseline = true)]
    public ReadOnlyMemory<byte> Compress_GZip()
    {
        var ctx = new MutateOutgoingTransportMessageContext(compressibleData, new object(), new Dictionary<string, string>(), null, null, CancellationToken.None);
        gzipMutator.MutateOutgoing(ctx).GetAwaiter().GetResult();
        return ctx.OutgoingBody;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Compress_Brotli()
    {
        var ctx = new MutateOutgoingTransportMessageContext(compressibleData, new object(), new Dictionary<string, string>(), null, null, CancellationToken.None);
        brotliMutator.MutateOutgoing(ctx).GetAwaiter().GetResult();
        return ctx.OutgoingBody;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Compress_Deflate()
    {
        var ctx = new MutateOutgoingTransportMessageContext(compressibleData, new object(), new Dictionary<string, string>(), null, null, CancellationToken.None);
        deflateMutator.MutateOutgoing(ctx).GetAwaiter().GetResult();
        return ctx.OutgoingBody;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Compress_ZLib()
    {
        var ctx = new MutateOutgoingTransportMessageContext(compressibleData, new object(), new Dictionary<string, string>(), null, null, CancellationToken.None);
        zlibMutator.MutateOutgoing(ctx).GetAwaiter().GetResult();
        return ctx.OutgoingBody;
    }

    // --- Decompression benchmarks ---

    [Benchmark]
    public ReadOnlyMemory<byte> Decompress_GZip()
    {
        var ctx = new MutateIncomingTransportMessageContext(gzipCompressed, new Dictionary<string, string> { { "Content-Encoding", "gzip" } }, CancellationToken.None);
        gzipMutator.MutateIncoming(ctx).GetAwaiter().GetResult();
        return ctx.Body;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Decompress_Brotli()
    {
        var ctx = new MutateIncomingTransportMessageContext(brotliCompressed, new Dictionary<string, string> { { "Content-Encoding", "br" } }, CancellationToken.None);
        brotliMutator.MutateIncoming(ctx).GetAwaiter().GetResult();
        return ctx.Body;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Decompress_Deflate()
    {
        var ctx = new MutateIncomingTransportMessageContext(deflateCompressed, new Dictionary<string, string> { { "Content-Encoding", "deflate" } }, CancellationToken.None);
        deflateMutator.MutateIncoming(ctx).GetAwaiter().GetResult();
        return ctx.Body;
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Decompress_ZLib()
    {
        var ctx = new MutateIncomingTransportMessageContext(zlibCompressed, new Dictionary<string, string> { { "Content-Encoding", "zlib" } }, CancellationToken.None);
        zlibMutator.MutateIncoming(ctx).GetAwaiter().GetResult();
        return ctx.Body;
    }
}
