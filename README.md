# NServiceBus.Compression

Adds message body compression to the NServiceBus pipeline.

## When to use

Compressing message bodies can be useful when the message body exceeds the transports its maximum message size or just in general to reduce network IO.

| Transport                  | Maximum size |
| -------------------------- | ------------:|
| MSMQ                       | 4MB          |
| Azure Service Bus Standard | 256KB        |
| Azure Service Bus Premium  | 100MB        |
| RabbitMQ                   | No limit     |

Note: In the past the maximum message size for Azure Service Bus was much smaller.

## Alternatives

Compression only works well on text-based payloads like XML and Json any payload (text or binary) that contains repetitive data.

1. Use a binary serializer
   - In the past some ready to use packages were available via https://github.com/NServiceBusExtensions but none of the binary serializers are available for NServiceBus 8+.
   - [ramonsmits/NServiceBus.ProtoBufNet](https://github.com/ramonsmits/NServiceBus.ProtoBufNet) (original archived at [NServiceBusExtensions/NServiceBus.ProtoBufNet](https://github.com/NServiceBusExtensions/NServiceBus.ProtoBufNet)
   - Implementation a binary serializer is simple and just requires a few lines of code
3. Use the claim check pattern for binary message attachments via https://github.com/NServiceBusExtensions/NServiceBus.Attachments
4. Use any of the above in combination with compression

## Version compatibility

| NServiceBus | NServiceBus.Compression |
| ----------- | ----------------------- |
| v5.x        | v1.x                    |
| v6.x        | v2.x                    |
| v7.x        | v3.x                    |
| v8.x        | v4.x                    |
| v9.x        | v5.x                    |
| v10.x       | v6.x                    |

Please note that there might be versions targeting other NServiceBus versions. [Please check the Releases for all versions.](https://github.com/ramonsmits/nservicebus.compression/releases) or [check the root of the  `master` branch of the repository](https://github.com/ramonsmits/nservicebus.compression).


## Introduction

This package is based on the mutator example from the NServiceBus documentation website but is has these additional features:

- Drop in auto enable compresssion, no need to recompile! Can be saviour if affected by production incidents due to unable to send too large messages.
- Requires messages to be of a minimal size but this thresshold is configurable.
- Compression level can be configured to have more flexibility between CPU cycles and message size.
- Multiple compression algorithms: GZip, Brotli, Deflate, and ZLib.
- Uses a header similar as the http specification `Content-Encoding`.

## Supported algorithms

| Algorithm | Content-Encoding | Receiver version | Notes |
| --------- | ---------------- | ---------------- | ----- |
| GZip      | `gzip`           | Any              | Default. Widely supported, good balance of speed and ratio. |
| Brotli    | `br`             | 6.x+             | Typically better compression ratio than GZip. Uses span-based zero-stream API for reduced allocations. |
| Deflate   | `deflate`        | 6.x+             | Raw deflate compression. |
| ZLib      | `zlib`           | 6.x+             | Deflate with zlib header/checksum. |

Decompression supports all algorithms regardless of the configured compression algorithm, enabling rolling upgrades where endpoints can switch algorithms independently. When switching to a non-GZip algorithm, ensure all receivers are upgraded to v6.x first. GZip is the only algorithm supported by all versions.

## Configuration

The defaults are:

- Algorithm: GZip
- Size treshold: 1,000 bytes
- Compression level: Fastest

This is to not spend CPU cycles on messages that are small. Compression not only costs CPU cycles but also introduces latency both during outgoing (compression) and incoming (decompression) messages.

The following only compresses messages over 16KB in size and uses the highest compression level.

```c#
endpointConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 16 * 1024);
```

To use a specific algorithm:

```c#
endpointConfiguration.CompressMessageBody(CompressionAlgorithm.Brotli, System.IO.Compression.CompressionLevel.Optimal, 16 * 1024);
```

## Benchmarks

Compression and decompression performance using `CompressionLevel.Fastest` with a repeating text payload.

| Method             | Size    | Mean         | Ratio | Allocated | Alloc Ratio |
|------------------- |-------- |-------------:|------:|----------:|------------:|
| Compress_GZip      | 1000    |   3,797.9 ns |  1.00 |    1640 B |        1.00 |
| Compress_Brotli    | 1000    |   2,121.1 ns |  0.56 |     424 B |        0.26 |
| Compress_Deflate   | 1000    |   3,601.2 ns |  0.95 |    1608 B |        0.98 |
| Compress_ZLib      | 1000    |   3,696.0 ns |  0.97 |    1640 B |        1.00 |
| Decompress_GZip    | 1000    |     705.0 ns |  0.19 |    1728 B |        1.05 |
| Decompress_Brotli  | 1000    |   1,691.2 ns |  0.45 |    1296 B |        0.79 |
| Decompress_Deflate | 1000    |     642.2 ns |  0.17 |    1696 B |        1.03 |
| Decompress_ZLib    | 1000    |     653.5 ns |  0.17 |    1728 B |        1.05 |
|                    |         |              |       |           |             |
| Compress_GZip      | 10000   |   5,240.9 ns |  1.00 |   10640 B |        1.00 |
| Compress_Brotli    | 10000   |   3,016.5 ns |  0.58 |     432 B |        0.04 |
| Compress_Deflate   | 10000   |   5,206.5 ns |  0.99 |   10608 B |        1.00 |
| Compress_ZLib      | 10000   |   5,163.4 ns |  0.99 |   10640 B |        1.00 |
| Decompress_GZip    | 10000   |   2,457.4 ns |  0.47 |   10728 B |        1.01 |
| Decompress_Brotli  | 10000   |   6,698.1 ns |  1.28 |   10296 B |        0.97 |
| Decompress_Deflate | 10000   |   2,400.2 ns |  0.46 |   10696 B |        1.01 |
| Decompress_ZLib    | 10000   |   2,394.3 ns |  0.46 |   10728 B |        1.01 |
|                    |         |              |       |           |             |
| Compress_GZip      | 100000  |  20,893.4 ns |  1.00 |  100661 B |       1.000 |
| Compress_Brotli    | 100000  |  10,321.3 ns |  0.49 |     432 B |       0.004 |
| Compress_Deflate   | 100000  |  20,889.4 ns |  1.00 |  100629 B |       1.000 |
| Compress_ZLib      | 100000  |  20,892.1 ns |  1.00 |  100661 B |       1.000 |
| Decompress_GZip    | 100000  |  11,362.3 ns |  0.54 |  100749 B |       1.001 |
| Decompress_Brotli  | 100000  |  57,636.7 ns |  2.76 |  100317 B |       0.997 |
| Decompress_Deflate | 100000  |  10,689.5 ns |  0.51 |  100717 B |       1.001 |
| Decompress_ZLib    | 100000  |  11,220.8 ns |  0.54 |  100749 B |       1.001 |
|                    |         |              |       |           |             |
| Compress_GZip      | 1000000 | 185,188.5 ns |  1.00 | 1002132 B |       1.000 |
| Compress_Brotli    | 1000000 |  55,341.8 ns |  0.30 |     728 B |       0.001 |
| Compress_Deflate   | 1000000 | 186,259.2 ns |  1.01 | 1002104 B |       1.000 |
| Compress_ZLib      | 1000000 | 182,055.8 ns |  0.98 | 1002115 B |       1.000 |
| Decompress_GZip    | 1000000 | 183,378.6 ns |  0.99 | 1967192 B |       1.963 |
| Decompress_Brotli  | 1000000 | 320,663.2 ns |  1.73 | 1000425 B |       0.998 |
| Decompress_Deflate | 1000000 | 194,815.5 ns |  1.05 | 1967158 B |       1.963 |
| Decompress_ZLib    | 1000000 | 202,411.7 ns |  1.09 | 1967192 B |       0.998 |

**Key observations:**

- **Brotli compression** is consistently faster than GZip (0.30x-0.56x) with dramatically lower allocations (0.1%-26% of GZip).
- **Brotli decompression** is slower than GZip/Deflate/ZLib. This is inherent to the Brotli algorithm which trades decompression speed for better compression ratios. Brotli uses a span-based decompression path that halves memory allocations at larger sizes compared to the stream-based approach used by other algorithms.
- **GZip, Deflate, and ZLib** have nearly identical performance as they all use the DEFLATE codec internally.
- Brotli is best suited when compression ratio and bandwidth savings outweigh decompression latency, such as over slow or metered network links.
