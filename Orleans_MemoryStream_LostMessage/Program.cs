// <configuration>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Runtime;
using Orleans.Streams;


var builder = new HostBuilder();

builder.UseOrleans(static siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorage("PubSubStore");
    siloBuilder.AddMemoryStreams(Constants.StreamProviderName, cfg =>
    {
        cfg.ConfigureCacheEviction(cacheEvictionOptions =>
        {
            cacheEvictionOptions.Configure(options =>
            {
                options.DataMaxAgeInCache = TimeSpan.FromSeconds(20);
                options.DataMinTimeInCache = TimeSpan.FromSeconds(20);
            });
        });

        cfg.ConfigurePullingAgent(pullingAgentOptions =>
        {
            pullingAgentOptions.Configure(opt =>
            {
                opt.StreamInactivityPeriod = TimeSpan.FromMinutes(1);
            });
        });
    });
});

using var host = builder.Build();
await host.StartAsync();

var consumerGrain = host.Services.GetRequiredService<IGrainFactory>().GetGrain<IConsumerGrain>(Guid.Empty);
await consumerGrain.Subscribe();

var producerGrain = host.Services.GetRequiredService<IGrainFactory>().GetGrain<IEventProducerTestGrain>(Guid.Empty);
await producerGrain.Produce(1);

// Wait >1 minute to make stream inactive
await Task.Delay(TimeSpan.FromSeconds(80));

await producerGrain.Produce(2);
await producerGrain.Produce(3);

// Observe the console. Message 2 is lost.

Console.ReadKey();

public interface IEventProducerTestGrain : IGrainWithGuidKey
{
    Task Produce(int item);
}

public sealed class EventProducerTestGrain : Grain, IEventProducerTestGrain
{
    private IAsyncStream<int>? _stream;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamId = StreamId.Create(Constants.NamespaceName, Guid.Empty);
        _stream = this.GetStreamProvider(Constants.StreamProviderName).GetStream<int>(streamId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task Produce(int item)
    {
        await _stream!.OnNextAsync(item);
    }
}

public interface IConsumerGrain : IGrainWithGuidKey
{
    Task Subscribe();
}

public class ConsumerGrain : Grain, IConsumerGrain
{
    private readonly LoggerObserver _observer;
    private readonly IClusterClient _clusterClient;

    public ConsumerGrain(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
        _observer = new LoggerObserver();
    }

    public async Task Subscribe()
    {
        var streamProvider = _clusterClient.GetStreamProvider(Constants.StreamProviderName);
        var streamId = StreamId.Create(Constants.NamespaceName, Guid.Empty);
        var stream = streamProvider.GetStream<int>(streamId);
        await stream.SubscribeAsync(_observer);
    }

    /// <summary>
    /// Class that will log streaming events
    /// </summary>
    private class LoggerObserver : IAsyncObserver<int>
    {
        public Task OnCompletedAsync() => Task.CompletedTask;

        public Task OnErrorAsync(Exception ex) => Task.CompletedTask;

        public Task OnNextAsync(int item, StreamSequenceToken? token = null)
        {
            Console.WriteLine($"OnNextAsync: Item: {item}, Token = {token}");
            return Task.CompletedTask;
        }
    }

}

public static class Constants
{
    public const string StreamProviderName = "StreamProvider-1";
    public const string NamespaceName = "ns-1";
}