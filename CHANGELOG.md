# Changelog

## [6.0.0] - 2026-02-15

Target: NServiceBus 10.x | .NET 10.0

### Features

- Support NServiceBus 10.x
- Add multi-algorithm support: GZip, Brotli, Deflate, ZLib via `CompressionAlgorithm` enum
- Decompression supports all algorithms regardless of configured algorithm (enables rolling upgrades)
- ⚠️ Require explicit opt-in via `CompressMessageBody` configuration API (`EnableByDefault()` removed)

### Improvements

- Brotli uses span-based `BrotliEncoder.TryCompress` / `BrotliDecoder.TryDecompress` with `ArrayPool` — dramatically lower allocations
- Reduce allocations: span-based `Write`, `TryGetBuffer`, `MemoryMarshal.TryGetArray` for zero-copy streams

### Dependencies

- NServiceBus [10.0.0, 11.0.0)
- Remove `CommunityToolkit.HighPerformance` dependency
- Switch to MinVer for tag-based versioning

## [5.0.1] - 2024-06-14

- Fix NServiceBus 9.x package reference (was still targeting 8.x)

## [5.0.0] - 2024-06-14

Target: NServiceBus 9.x | .NET 8.0

- Support NServiceBus 9.x
- Add README to NuGet package
- Enable dependabot

### Dependencies

- CommunityToolkit.HighPerformance 8.2.1
- Microsoft.SourceLink.GitHub 1.1.1

## [4.0.0] - 2022-12-09

Target: NServiceBus 8.x

- Support NServiceBus 8.x

## [3.0.0] - 2020-05-27

Target: NServiceBus 7.x

- Support NServiceBus 7.x

## [2.0.0] - 2020-05-27

Target: NServiceBus 6.x

- Support NServiceBus 6.x

## [1.0.0] - 2020-05-26

Target: NServiceBus 5.x

- Initial release

[6.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/compare/5.0.1...6.0.0
[5.0.1]: https://github.com/ramonsmits/NServiceBus.Compression/compare/5.0.0...5.0.1
[5.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/compare/4.0.0...5.0.0
[4.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/compare/3.0.0...4.0.0
[3.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/compare/2.0.0...3.0.0
[2.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/compare/1.0.0...2.0.0
[1.0.0]: https://github.com/ramonsmits/NServiceBus.Compression/releases/tag/1.0.0
