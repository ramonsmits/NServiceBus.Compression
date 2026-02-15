namespace NServiceBus;

/// <summary>
/// Compression algorithm to use for message body compression
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>GZip compression (Content-Encoding: gzip)</summary>
    GZip,
    /// <summary>Brotli compression (Content-Encoding: br)</summary>
    Brotli,
    /// <summary>Deflate compression (Content-Encoding: deflate)</summary>
    Deflate,
    /// <summary>ZLib compression (Content-Encoding: zlib)</summary>
    ZLib
}
