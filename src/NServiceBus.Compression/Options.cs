using System.IO.Compression;
using NServiceBus;

class Options
{
    public CompressionAlgorithm Algorithm = CompressionAlgorithm.GZip;
    public CompressionLevel CompressionLevel = CompressionLevel.Fastest;
    public int ThresholdSize = 1000;
}
