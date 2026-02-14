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
- Uses a header similar as the http specification `Content-Encoding`.

## Configuration

The defaults are:

- Size treshold: 1,000 bytes
- Compression level: Fastest

This is to not spend CPU cycles on messages that are small. Compression not only costs CPU cycles but also introduces latency both during outgoing (compression) and incoming (decompression) messages.

The following only compresses messages over 16KB in size and uses the highest compression level.

```c#
endpointConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 16 * 1024);
```
