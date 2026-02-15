namespace NServiceBus;

/// <summary>
/// Compression algorithm to use for message body compression.
/// The receiver must have a version that supports the chosen algorithm to decompress the message.
/// Decompression supports all algorithms regardless of the configured compression algorithm, enabling rolling upgrades.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>GZip compression (Content-Encoding: gzip). Receivers require any version.</summary>
    GZip,
    /// <summary>Brotli compression (Content-Encoding: br). Receivers require version 6.x or later.</summary>
    Brotli,
    /// <summary>Deflate compression (Content-Encoding: deflate). Receivers require version 6.x or later.</summary>
    Deflate,
    /// <summary>ZLib compression (Content-Encoding: zlib). Receivers require version 6.x or later.</summary>
    ZLib
}
