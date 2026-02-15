using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;
using NServiceBus.Logging;
using NServiceBus.MessageMutator;

namespace NServiceBus;

class CompressionFeature : Feature
{
    internal static readonly ILog Log = LogManager.GetLogger("NServiceBus.Compression");

    protected override void Setup(FeatureConfigurationContext context)
    {
        if (!context.Settings.TryGet(out Options properties)) properties = new Options();
        Log.InfoFormat("Algorithm: {0}", properties.Algorithm);
        Log.InfoFormat("Compression level: {0}", properties.CompressionLevel);
        Log.InfoFormat("Threshold: {0:N0} bytes", properties.ThresholdSize);
        context.Services.AddSingleton(properties);

        context.Services.AddSingleton<TransportMessageCompressionMutator>();
        context.Services.AddSingleton<IMutateIncomingTransportMessages>(b => b.GetRequiredService<TransportMessageCompressionMutator>());
        context.Services.AddSingleton<IMutateOutgoingTransportMessages>(b => b.GetRequiredService<TransportMessageCompressionMutator>());
    }
}
