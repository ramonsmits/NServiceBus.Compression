# NServiceBus.Compression

Adds message body compression to the NSericeBus pipeline.

## Version compatibility

| NServiceBus | NServiceBus.Compression |
| ----------- | ----------------------- |
| v5.x        | v1.x                    |

Please note that there might be versions targeting other NServiceBus versions. [Please check the Releases for all versions.](https://github.com/ramonsmits/nservicebus.compression/releases) or [check the root of the  `master` branch of the repository](https://github.com/ramonsmits/nservicebus.compression).


## Introduction

This package is based on the mutator example from the NServiceBus documentation website but is has these additional features:

- Drop in auto enable compresssion, no need to recompile! Can be saviour is affected by production incidents due to too large messages.
- Requires messages to be of a minimal size but this thresshold is configurable.
- Compression level can be configured to have more flexibility between CPU cycles and message size.

## Configuration

The default are:

- Size treshold: 1000 bytes
- Compression level: Fastest

This is to not spend CPU cycles on messages that are small. Compression not only costs CPU cycles but also introduces latency both during outgoing and incoming messages.

The following only compresses message over 16KB in size and uses the highest compression level.

```c#
busConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 16 * 1024);
```
