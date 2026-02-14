using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        //LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        var endpointConfiguration = new EndpointConfiguration("compression");
        endpointConfiguration.UseSerialization(new SystemJsonSerializer());
        endpointConfiguration.UsePersistence<LearningPersistence>();
        endpointConfiguration.UseTransport(new LearningTransport());
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.EnableInstallers();

        endpointConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 1000);

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
