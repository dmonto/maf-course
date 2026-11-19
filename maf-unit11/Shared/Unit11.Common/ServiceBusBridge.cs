namespace Maf.Unit11;

using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

public static class ServiceBusBridge
{
    private static ServiceBusClient Client() => new(
        LabConfig.Required("SERVICEBUS_NAMESPACE"), new AzureCliCredential(),
        new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });

    public static async Task SendAsync(string file, CancellationToken ct)
    {
        byte[] bytes = await File.ReadAllBytesAsync(file, ct);
        if (bytes.Length > 8192) throw new InvalidDataException("Event exceeds 8 KiB.");
        SupportEvent e = JsonSerializer.Deserialize<SupportEvent>(bytes, LabJson.Options)
            ?? throw new InvalidDataException("Missing event.");
        e.Validate();
        await using var client = Client();
        await using var sender = client.CreateSender(LabConfig.Required("SERVICEBUS_QUEUE"));
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromBytes(bytes))
        {
            MessageId = e.Id, CorrelationId = e.CorrelationId,
            Subject = e.Type, ContentType = "application/json"
        }, ct);
        Console.WriteLine($"Broker accepted {e.Id}; agent processing is still separate.");
    }

    public static async Task ReceiveAsync(EventStore store, CancellationToken ct)
    {
        await using var client = Client();
        await using var processor = client.CreateProcessor(LabConfig.Required("SERVICEBUS_QUEUE"),
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false, ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxConcurrentCalls = 2, PrefetchCount = 0,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(2)
            });
        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                if (args.Message.Body.ToMemory().Length > 8192 ||
                    args.Message.ContentType != "application/json")
                    throw new InvalidDataException("Invalid event encoding or size.");
                SupportEvent e = JsonSerializer.Deserialize<SupportEvent>(
                    args.Message.Body.ToString(), LabJson.Options)
                    ?? throw new InvalidDataException("Missing event.");
                e.Validate();
                if (args.Message.MessageId != e.Id || args.Message.CorrelationId != e.CorrelationId ||
                    args.Message.Subject != e.Type)
                    throw new InvalidDataException("Broker metadata and event disagree.");
                bool created = store.Accept(e);
                // Complete only after the durable local hand-off, including on a matching duplicate.
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                Console.WriteLine($"Inbox accepted {e.Id}; new={created}.");
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                await args.DeadLetterMessageAsync(args.Message, "InvalidEvent",
                    "Contract, metadata or identity conflict.", args.CancellationToken);
            }
            // Other exceptions escape: the processor releases unsettled messages for redelivery.
        };
        processor.ProcessErrorAsync += args =>
        {
            Console.Error.WriteLine($"Bus error: {args.Exception.GetType().Name}; source={args.ErrorSource}");
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync(ct);
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { await processor.StopProcessingAsync(CancellationToken.None); }
    }
}
