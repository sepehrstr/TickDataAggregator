using TickDataAggregator.Application.Processing;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.UnitTests.Services;

public sealed class TickChannelTests
{
    [Fact]
    public async Task WriteAndRead_SingleTick_Succeeds()
    {
        var channel = new TickChannel(100);
        var tick = new RawTickData { RawPayload = "test", Source = "Test" };

        await channel.Writer.WriteAsync(tick);

        var result = await channel.Reader.ReadAsync();

        Assert.Equal("test", result.RawPayload);
        Assert.Equal("Test", result.Source);
    }

    [Fact]
    public async Task Write_BeyondCapacity_Blocks()
    {
        var channel = new TickChannel(1);
        var tick = new RawTickData { RawPayload = "test", Source = "Test" };

        await channel.Writer.WriteAsync(tick);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await channel.Writer.WriteAsync(tick, cts.Token);
        });
    }

    [Fact]
    public async Task ReadAllAsync_CompletedChannel_EndsEnumeration()
    {
        var channel = new TickChannel(100);

        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "1", Source = "Test" });
        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "2", Source = "Test" });
        channel.Writer.Complete();

        var items = new List<RawTickData>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
    }
}
