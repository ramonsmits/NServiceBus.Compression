using System;
using NServiceBus;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;
using NServiceBus.Logging;

class Program
{
    static void Main()
    {
        //LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        var busConfiguration = new BusConfiguration();
        busConfiguration.EndpointName("compression");
        busConfiguration.UsePersistence<InMemoryPersistence>();
        busConfiguration.EnableInstallers();

        // Commenting the following will NOT disable, feature is enabled by
        // default. Can only be disabled by excluding the assembly from scanning.
        // This is because the feature relies on `INeedInitialization` to
        // workaround a limitation in version 5 which is not able to register
        // mutators from features.

        busConfiguration.CompressMessageBody(System.IO.Compression.CompressionLevel.Optimal, 1000);
        
        // Uncomment to disable compression

        // var excludesBuilder = AllAssemblies.Except("NServiceBus.Compression.dll");
        // busConfiguration.AssembliesToScan(excludesBuilder);

        var bus = Bus.Create(busConfiguration).Start();

        var myMessage = new Message
        {
            Data = new byte[1024 * 1024 * 10] //10MB
        };

        bus.SendLocal(myMessage);
        Console.ReadKey();
    }
}

class Message : IMessage
{
    public byte[] Data { get; set; }
}

class Handler : IHandleMessages<Message>
{
    public void Handle(Message message)
    {
        Console.WriteLine("Data size: {0:N0} bytes", message.Data.Length);
    }
}

class ProvideConfiguration :
    IProvideConfiguration<MessageForwardingInCaseOfFaultConfig>
{
    public MessageForwardingInCaseOfFaultConfig GetConfiguration()
    {
        return new MessageForwardingInCaseOfFaultConfig
        {
            ErrorQueue = "error"
        };
    }
}