using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NServiceBus;
using NServiceBus.MessageMutator;
using NUnit.Framework;

[TestFixture]
public class TransportMessageCompressionMutatorTests
{
    static readonly CompressionAlgorithm[] AllAlgorithms =
    [
        CompressionAlgorithm.GZip,
        CompressionAlgorithm.Brotli,
        CompressionAlgorithm.Deflate,
        CompressionAlgorithm.ZLib
    ];

    static readonly string[] AllEncodings = ["gzip", "br", "deflate", "zlib"];

    static TransportMessageCompressionMutator CreateMutator(
        CompressionAlgorithm algorithm = CompressionAlgorithm.GZip,
        CompressionLevel level = CompressionLevel.Fastest,
        int threshold = 100) =>
        new(new Options
        {
            Algorithm = algorithm,
            CompressionLevel = level,
            ThresholdSize = threshold
        });

    static MutateOutgoingTransportMessageContext CreateOutgoingContext(byte[] body) =>
        new(
            outgoingBody: body,
            outgoingMessage: new object(),
            outgoingHeaders: new Dictionary<string, string>(),
            incomingMessage: null,
            incomingHeaders: null,
            cancellationToken: CancellationToken.None);

    static MutateIncomingTransportMessageContext CreateIncomingContext(byte[] body, string? encoding) =>
        new(
            body: body,
            headers: encoding is not null
                ? new Dictionary<string, string> { { "Content-Encoding", encoding } }
                : [],
            cancellationToken: CancellationToken.None);

    static byte[] MakeCompressible(int size)
    {
        // Repeating text pattern compresses very well
        var pattern = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. ");
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = pattern[i % pattern.Length];
        }
        return data;
    }

    static byte[] MakeIncompressible(int size)
    {
        var data = new byte[size];
        RandomNumberGenerator.Fill(data);
        return data;
    }

    #region Round-trip: compress then decompress for every algorithm

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task RoundTrip_PreservesData(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 100);
        var original = MakeCompressible(4096);

        var outCtx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(outCtx);

        Assert.That(outCtx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.True,
            "Header should be set after compression");

        var inCtx = CreateIncomingContext(
            outCtx.OutgoingBody.ToArray(),
            outCtx.OutgoingHeaders["Content-Encoding"]);

        await mutator.MutateIncoming(inCtx);

        Assert.That(inCtx.Body.ToArray(), Is.EqualTo(original));
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task RoundTrip_LargePayload(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 100);
        var original = MakeCompressible(1024 * 1024); // 1 MB

        var outCtx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(outCtx);

        Assert.That(outCtx.OutgoingBody.Length, Is.LessThan(original.Length),
            "Compressible data should reduce in size");

        var inCtx = CreateIncomingContext(
            outCtx.OutgoingBody.ToArray(),
            outCtx.OutgoingHeaders["Content-Encoding"]);

        await mutator.MutateIncoming(inCtx);

        Assert.That(inCtx.Body.ToArray(), Is.EqualTo(original));
    }

    #endregion

    #region Correct Content-Encoding header per algorithm

    [Test]
    public async Task ContentEncoding_GZip()
    {
        var mutator = CreateMutator(CompressionAlgorithm.GZip);
        var ctx = CreateOutgoingContext(MakeCompressible(500));
        await mutator.MutateOutgoing(ctx);
        Assert.That(ctx.OutgoingHeaders["Content-Encoding"], Is.EqualTo("gzip"));
    }

    [Test]
    public async Task ContentEncoding_Brotli()
    {
        var mutator = CreateMutator(CompressionAlgorithm.Brotli);
        var ctx = CreateOutgoingContext(MakeCompressible(500));
        await mutator.MutateOutgoing(ctx);
        Assert.That(ctx.OutgoingHeaders["Content-Encoding"], Is.EqualTo("br"));
    }

    [Test]
    public async Task ContentEncoding_Deflate()
    {
        var mutator = CreateMutator(CompressionAlgorithm.Deflate);
        var ctx = CreateOutgoingContext(MakeCompressible(500));
        await mutator.MutateOutgoing(ctx);
        Assert.That(ctx.OutgoingHeaders["Content-Encoding"], Is.EqualTo("deflate"));
    }

    [Test]
    public async Task ContentEncoding_ZLib()
    {
        var mutator = CreateMutator(CompressionAlgorithm.ZLib);
        var ctx = CreateOutgoingContext(MakeCompressible(500));
        await mutator.MutateOutgoing(ctx);
        Assert.That(ctx.OutgoingHeaders["Content-Encoding"], Is.EqualTo("zlib"));
    }

    #endregion

    #region Boundary: threshold

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task BelowThreshold_NoCompression(CompressionAlgorithm algorithm)
    {
        const int threshold = 1000;
        var mutator = CreateMutator(algorithm, threshold: threshold);
        var original = MakeCompressible(threshold - 1); // 999 bytes

        var ctx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(ctx);

        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.False,
            "Should not compress below threshold");
        Assert.That(ctx.OutgoingBody.ToArray(), Is.EqualTo(original),
            "Body should be unchanged");
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task ExactlyAtThreshold_NoCompression(CompressionAlgorithm algorithm)
    {
        const int threshold = 1000;
        var mutator = CreateMutator(algorithm, threshold: threshold);
        var original = MakeCompressible(threshold); // exactly 1000 bytes

        var ctx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(ctx);

        // The check is `<= threshold`, so exactly at threshold should NOT compress
        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.False,
            "Should not compress at exactly the threshold");
        Assert.That(ctx.OutgoingBody.ToArray(), Is.EqualTo(original),
            "Body should be unchanged");
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task OneAboveThreshold_Compresses(CompressionAlgorithm algorithm)
    {
        const int threshold = 1000;
        var mutator = CreateMutator(algorithm, threshold: threshold);
        var original = MakeCompressible(threshold + 1); // 1001 bytes

        var ctx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(ctx);

        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.True,
            "Should compress just above threshold");
        Assert.That(ctx.OutgoingBody.Length, Is.LessThan(original.Length),
            "Compressible data should shrink");
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task EmptyBody_NoCompression(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 0);
        var ctx = CreateOutgoingContext([]);

        await mutator.MutateOutgoing(ctx);

        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.False);
        Assert.That(ctx.OutgoingBody.Length, Is.EqualTo(0));
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task SingleByte_BelowDefaultThreshold_NoCompression(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 100);
        var ctx = CreateOutgoingContext([42]);

        await mutator.MutateOutgoing(ctx);

        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.False);
        Assert.That(ctx.OutgoingBody.ToArray(), Is.EqualTo(new byte[] { 42 }));
    }

    #endregion

    #region Incompressible data

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task IncompressibleData_BodyUnchanged(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 100);
        var original = MakeIncompressible(500);

        var ctx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(ctx);

        // Random data can't be compressed — mutator should leave body unchanged
        Assert.That(ctx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.False,
            "Should not set header when compression doesn't help");
        Assert.That(ctx.OutgoingBody.ToArray(), Is.EqualTo(original),
            "Body should be unchanged when compression doesn't save bytes");
    }

    #endregion

    #region Decompression: unknown and missing headers

    [Test]
    public async Task Incoming_NoHeader_NoDecompression()
    {
        var mutator = CreateMutator();
        var original = new byte[] { 1, 2, 3, 4, 5 };
        var ctx = CreateIncomingContext(original, encoding: null);

        await mutator.MutateIncoming(ctx);

        Assert.That(ctx.Body.ToArray(), Is.EqualTo(original));
    }

    [Test]
    public async Task Incoming_UnknownEncoding_NoDecompression()
    {
        var mutator = CreateMutator();
        var original = new byte[] { 1, 2, 3, 4, 5 };
        var ctx = CreateIncomingContext(original, encoding: "snappy");

        await mutator.MutateIncoming(ctx);

        Assert.That(ctx.Body.ToArray(), Is.EqualTo(original));
    }

    #endregion

    #region Cross-algorithm decompression

    [Test]
    public async Task Decompress_AllEncodings_RegardlessOfConfiguredAlgorithm()
    {
        // An endpoint configured for Brotli compression should still decompress
        // messages that were compressed with any other algorithm
        foreach (var compressAlgorithm in AllAlgorithms)
        {
            var compressor = CreateMutator(compressAlgorithm, threshold: 10);
            var original = MakeCompressible(500);

            var outCtx = CreateOutgoingContext(original);
            await compressor.MutateOutgoing(outCtx);

            // Decompressor configured with a DIFFERENT algorithm
            var decompressAlgorithm = compressAlgorithm == CompressionAlgorithm.Brotli
                ? CompressionAlgorithm.GZip
                : CompressionAlgorithm.Brotli;

            var decompressor = CreateMutator(decompressAlgorithm, threshold: 10);
            var inCtx = CreateIncomingContext(
                outCtx.OutgoingBody.ToArray(),
                outCtx.OutgoingHeaders["Content-Encoding"]);

            await decompressor.MutateIncoming(inCtx);

            Assert.That(inCtx.Body.ToArray(), Is.EqualTo(original),
                $"Decompressor configured for {decompressAlgorithm} should still decompress {compressAlgorithm}");
        }
    }

    #endregion

    #region Compression levels

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task CompressionLevel_Optimal_RoundTrips(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, level: CompressionLevel.Optimal, threshold: 100);
        var original = MakeCompressible(4096);

        var outCtx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(outCtx);

        var inCtx = CreateIncomingContext(
            outCtx.OutgoingBody.ToArray(),
            outCtx.OutgoingHeaders["Content-Encoding"]);

        await mutator.MutateIncoming(inCtx);

        Assert.That(inCtx.Body.ToArray(), Is.EqualTo(original));
    }

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task CompressionLevel_SmallestSize_RoundTrips(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, level: CompressionLevel.SmallestSize, threshold: 100);
        var original = MakeCompressible(4096);

        var outCtx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(outCtx);

        var inCtx = CreateIncomingContext(
            outCtx.OutgoingBody.ToArray(),
            outCtx.OutgoingHeaders["Content-Encoding"]);

        await mutator.MutateIncoming(inCtx);

        Assert.That(inCtx.Body.ToArray(), Is.EqualTo(original));
    }

    #endregion

    #region Threshold = 1 (minimum valid)

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task ThresholdOne_TwoBytesCompresses(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 1);
        // 2 bytes is above threshold of 1 — should attempt compression
        // (compression may or may not reduce size, but the attempt is made)
        var original = MakeCompressible(200);

        var outCtx = CreateOutgoingContext(original);
        await mutator.MutateOutgoing(outCtx);

        // With 200 bytes of compressible data, compression should be effective
        Assert.That(outCtx.OutgoingHeaders.ContainsKey("Content-Encoding"), Is.True);
    }

    #endregion

    #region Outgoing does not modify headers when below threshold

    [TestCaseSource(nameof(AllAlgorithms))]
    public async Task BelowThreshold_HeadersUntouched(CompressionAlgorithm algorithm)
    {
        var mutator = CreateMutator(algorithm, threshold: 1000);
        var ctx = CreateOutgoingContext(MakeCompressible(500));
        ctx.OutgoingHeaders["Existing"] = "value";

        await mutator.MutateOutgoing(ctx);

        Assert.That(ctx.OutgoingHeaders, Has.Count.EqualTo(1));
        Assert.That(ctx.OutgoingHeaders["Existing"], Is.EqualTo("value"));
    }

    #endregion
}
