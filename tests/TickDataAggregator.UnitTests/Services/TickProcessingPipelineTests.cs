using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Application.Processing;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.UnitTests.Services;

public sealed class TickProcessingPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesTicks_CallsRepository()
    {
        var channel = new TickChannel(100);
        var repositoryMock = new Mock<ITickRepository>();
        var savedTicks = new List<NormalizedTick>();

        repositoryMock
            .Setup(r => r.SaveTicksAsync(It.IsAny<IReadOnlyList<NormalizedTick>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<NormalizedTick>, CancellationToken>((ticks, _) => savedTicks.AddRange(ticks))
            .Returns(Task.CompletedTask);

        var adapter = new Mock<ITickAdapter>();
        adapter.Setup(n => n.ExchangeName).Returns("TestExchange");
        adapter.Setup(n => n.CanHandle("TestExchange")).Returns(true);
        adapter.Setup(n => n.Normalize(It.IsAny<RawTickData>()))
            .Returns<RawTickData>(raw => new List<NormalizedTick>
            {
                new("BTCUSD", TradingSymbol.BTCUSD, 65000m, 1m, DateTimeOffset.UtcNow, raw.Source, raw.ReceivedAt)
            });

        var deduplicator = new Mock<ITickDeduplicator>();
        deduplicator.Setup(d => d.IsDuplicate(It.IsAny<NormalizedTick>())).Returns(false);

        var options = Options.Create(new PipelineOptions
        {
            BatchSize = 2,
            BatchFlushIntervalMs = 50000 // high so we test batch size trigger
        });

        var pipeline = new TickProcessingPipeline(
            channel,
            new[] { adapter.Object },
            deduplicator.Object,
            repositoryMock.Object,
            options,
            Mock.Of<ILogger<TickProcessingPipeline>>());

        using var cts = new CancellationTokenSource();
        var pipelineTask = pipeline.StartAsync(cts.Token);

        // Write ticks to trigger a batch flush
        for (var i = 0; i < 4; i++)
        {
            await channel.Writer.WriteAsync(new RawTickData { RawPayload = $"tick{i}", Source = "TestExchange" });
        }

        // Give pipeline time to process
        await Task.Delay(500);
        await cts.CancelAsync();

        try { await pipeline.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.True(savedTicks.Count >= 2, $"Expected at least 2 saved ticks, got {savedTicks.Count}");
        Assert.Equal(4, pipeline.PersistedCount);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateTicks_AreFiltered()
    {
        var channel = new TickChannel(100);
        var repositoryMock = new Mock<ITickRepository>();

        var adapter = new Mock<ITickAdapter>();
        adapter.Setup(n => n.ExchangeName).Returns("TestExchange");
        adapter.Setup(n => n.CanHandle("TestExchange")).Returns(true);
        adapter.Setup(n => n.Normalize(It.IsAny<RawTickData>()))
            .Returns(new List<NormalizedTick>
            {
                new("BTCUSD", TradingSymbol.BTCUSD, 65000m, 1m, DateTimeOffset.UtcNow, "TestExchange", DateTimeOffset.UtcNow)
            });

        var callCount = 0;
        var deduplicator = new Mock<ITickDeduplicator>();
        deduplicator.Setup(d => d.IsDuplicate(It.IsAny<NormalizedTick>()))
            .Returns(() => Interlocked.Increment(ref callCount) > 1); // first call false, rest true

        var options = Options.Create(new PipelineOptions { BatchSize = 100, BatchFlushIntervalMs = 100 });

        var pipeline = new TickProcessingPipeline(
            channel,
            new[] { adapter.Object },
            deduplicator.Object,
            repositoryMock.Object,
            options,
            Mock.Of<ILogger<TickProcessingPipeline>>());

        using var cts = new CancellationTokenSource();
        _ = pipeline.StartAsync(cts.Token);

        for (var i = 0; i < 5; i++)
        {
            await channel.Writer.WriteAsync(new RawTickData { RawPayload = $"tick{i}", Source = "TestExchange" });
        }

        await Task.Delay(300);
        await cts.CancelAsync();
        try { await pipeline.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.Equal(1, pipeline.PersistedCount);
        Assert.Equal(4, pipeline.DuplicateCount);
    }

    [Fact]
    public async Task ExecuteAsync_DbFailure_RetriesAndPersistsOnRecovery()
    {
        var channel = new TickChannel(100);
        var callCount = 0;
        var repositoryMock = new Mock<ITickRepository>();

        repositoryMock
            .Setup(r => r.SaveTicksAsync(It.IsAny<IReadOnlyList<NormalizedTick>>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<NormalizedTick>, CancellationToken>((_, _) =>
            {
                if (Interlocked.Increment(ref callCount) <= 2)
                    throw new InvalidOperationException("DB unavailable");
                return Task.CompletedTask;
            });

        var adapter = new Mock<ITickAdapter>();
        adapter.Setup(n => n.ExchangeName).Returns("TestExchange");
        adapter.Setup(n => n.Normalize(It.IsAny<RawTickData>()))
            .Returns<RawTickData>(raw => new List<NormalizedTick>
            {
                new("BTCUSD", TradingSymbol.BTCUSD, 65000m, 1m, DateTimeOffset.UtcNow, raw.Source, raw.ReceivedAt)
            });

        var deduplicator = new Mock<ITickDeduplicator>();
        deduplicator.Setup(d => d.IsDuplicate(It.IsAny<NormalizedTick>())).Returns(false);

        var options = Options.Create(new PipelineOptions { BatchSize = 2, BatchFlushIntervalMs = 50 });

        var pipeline = new TickProcessingPipeline(
            channel,
            new[] { adapter.Object },
            deduplicator.Object,
            repositoryMock.Object,
            options,
            Mock.Of<ILogger<TickProcessingPipeline>>());

        using var cts = new CancellationTokenSource();
        _ = pipeline.StartAsync(cts.Token);

        // Write 2 ticks to trigger a batch flush (which will fail twice, then succeed)
        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "tick0", Source = "TestExchange" });
        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "tick1", Source = "TestExchange" });

        // Wait for retries (1s + 2s backoff at most, but test uses small intervals)
        await Task.Delay(5000);
        await cts.CancelAsync();
        try { await pipeline.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.Equal(2, pipeline.PersistedCount);
        Assert.Equal(2, pipeline.PersistenceRetries);
        Assert.True(pipeline.FlushCount >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_TracksPerSourceMetrics()
    {
        var channel = new TickChannel(100);
        var repositoryMock = new Mock<ITickRepository>();

        var adapterA = new Mock<ITickAdapter>();
        adapterA.Setup(n => n.ExchangeName).Returns("ExchangeA");
        adapterA.Setup(n => n.Normalize(It.IsAny<RawTickData>()))
            .Returns<RawTickData>(raw => new List<NormalizedTick>
            {
                new("BTCUSD", TradingSymbol.BTCUSD, 65000m, 1m, DateTimeOffset.UtcNow, raw.Source, raw.ReceivedAt)
            });

        var adapterB = new Mock<ITickAdapter>();
        adapterB.Setup(n => n.ExchangeName).Returns("ExchangeB");
        adapterB.Setup(n => n.Normalize(It.IsAny<RawTickData>()))
            .Returns<RawTickData>(raw => new List<NormalizedTick>
            {
                new("ETHUSD", TradingSymbol.ETHUSD, 3000m, 2m, DateTimeOffset.UtcNow, raw.Source, raw.ReceivedAt)
            });

        var deduplicator = new Mock<ITickDeduplicator>();
        deduplicator.Setup(d => d.IsDuplicate(It.IsAny<NormalizedTick>())).Returns(false);

        var options = Options.Create(new PipelineOptions { BatchSize = 100, BatchFlushIntervalMs = 100 });

        var pipeline = new TickProcessingPipeline(
            channel,
            new[] { adapterA.Object, adapterB.Object },
            deduplicator.Object,
            repositoryMock.Object,
            options,
            Mock.Of<ILogger<TickProcessingPipeline>>());

        using var cts = new CancellationTokenSource();
        _ = pipeline.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "a1", Source = "ExchangeA" });
        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "b1", Source = "ExchangeB" });
        await channel.Writer.WriteAsync(new RawTickData { RawPayload = "a2", Source = "ExchangeA" });

        await Task.Delay(300);
        await cts.CancelAsync();
        try { await pipeline.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.Equal(3, pipeline.PersistedCount);
        var bySource = pipeline.PersistedBySource;
        Assert.Equal(2, bySource["ExchangeA"]);
        Assert.Equal(1, bySource["ExchangeB"]);
    }
}
