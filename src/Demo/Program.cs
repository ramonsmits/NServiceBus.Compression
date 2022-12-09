using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        //LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        var endpointConfiguration = new EndpointConfiguration("compression");
        endpointConfiguration.UsePersistence<LearningPersistence>();
        endpointConfiguration.UseTransport<LearningTransport>();
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.EnableInstallers();

        // Commenting the following will NOT disable, feature is enabled by
        // default. Can only be disabled by excluding the assembly from scanning.
        // This is because the feature relies on `INeedInitialization` to
        // workaround a limitation in version 5 which is not able to register
        // mutators from features.

        endpointConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 1000);

        // Uncomment to disable compression

        // var excludesBuilder = AllAssemblies.Except("NServiceBus.Compression.dll");
        // busConfiguration.AssembliesToScan(excludesBuilder);

        var endpointInstance = await Endpoint.Start(endpointConfiguration);

        var myMessage = new Message
        {
            Data = new byte[1024 * 1024 * 10] //10MB
        };

        await endpointInstance.SendLocal(myMessage);

        Console.WriteLine("Press ANY key to exit...");
        Console.ReadKey();
    }
}

class Message : IMessage
{
    public byte[] Data { get; set; }
}

class Handler : IHandleMessages<Message>
{
    public Task Handle(Message message, IMessageHandlerContext context)
    {
        return Console.Out.WriteLineAsync($"Data size: {message.Data.Length:N0} bytes");
    }
}
