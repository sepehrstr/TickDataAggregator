using Microsoft.Extensions.Options;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Application.Processing.Deduplication;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.UnitTests.Services;

public sealed class SlidingWindowDeduplicatorTests
{
    private readonly SlidingWindowDeduplicator _sut;

    public SlidingWindowDeduplicatorTests()
    {
        var options = Options.Create(new PipelineOptions { DeduplicationWindowSeconds = 10 });
        _sut = new SlidingWindowDeduplicator(options);
    }

    [Fact]
    public void IsDuplicate_FirstTick_ReturnsFalse()
    {
        var tick = CreateTick("BTCUSD", 65000m, 1m);

        Assert.False(_sut.IsDuplicate(tick));
    }

    [Fact]
    public void IsDuplicate_SameTickTwice_ReturnsTrueOnSecond()
    {
        var tick = CreateTick("BTCUSD", 65000m, 1m);

        Assert.False(_sut.IsDuplicate(tick));
        Assert.True(_sut.IsDuplicate(tick));
    }

    [Fact]
    public void IsDuplicate_DifferentTickers_BothReturnFalse()
    {
        var tick1 = CreateTick("BTCUSD", 65000m, 1m);
        var tick2 = CreateTick("ETHUSD", 3500m, 2m);

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.False(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_SameTickerDifferentPrice_BothReturnFalse()
    {
        var tick1 = CreateTick("BTCUSD", 65000m, 1m);
        var tick2 = CreateTick("BTCUSD", 65001m, 1m);

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.False(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_DifferentSources_BothReturnFalse()
    {
        var ts = DateTimeOffset.UtcNow;
        var tick1 = CreateTick("BTCUSD", 65000m, 1m, "ExchangeA", ts);
        var tick2 = CreateTick("BTCUSD", 65000m, 1m, "ExchangeB", ts);

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.False(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_SameSourceTradeId_ReturnsTrueOnSecond()
    {
        // Two ticks from the same exchange with the same trade ID should be deduped
        // even if price/volume/timestamp differ (e.g. a replayed message)
        var ts = DateTimeOffset.UtcNow;
        var tick1 = CreateTickWithTradeId("BTCUSD", 65000m, 1m, "Binance", "trade-99", ts);
        var tick2 = CreateTickWithTradeId("BTCUSD", 65001m, 2m, "Binance", "trade-99", ts.AddMilliseconds(1));

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.True(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_SameNaturalKeyDifferentTradeId_BothReturnFalse()
    {
        // Two real trades with identical symbol/price/volume/timestamp but different IDs
        // must NOT be collapsed — the trade ID is the source of truth
        var ts = DateTimeOffset.UtcNow;
        var tick1 = CreateTickWithTradeId("BTCUSD", 65000m, 1m, "Binance", "trade-100", ts);
        var tick2 = CreateTickWithTradeId("BTCUSD", 65000m, 1m, "Binance", "trade-101", ts);

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.False(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_SameTradeIdDifferentSources_BothReturnFalse()
    {
        var ts = DateTimeOffset.UtcNow;
        var tick1 = CreateTickWithTradeId("BTCUSD", 65000m, 1m, "Binance", "trade-1", ts);
        var tick2 = CreateTickWithTradeId("BTCUSD", 65000m, 1m, "Coinbase", "trade-1", ts);

        Assert.False(_sut.IsDuplicate(tick1));
        Assert.False(_sut.IsDuplicate(tick2));
    }

    [Fact]
    public void IsDuplicate_KeyExpiredAfterWindow_AcceptsAgain()
    {
        // Use a 0-second window so entries expire immediately
        var sut = new SlidingWindowDeduplicator(
            Options.Create(new PipelineOptions { DeduplicationWindowSeconds = 0 }));

        var tick = CreateTick("BTCUSD", 65000m, 1m);

        // First call: not a duplicate
        Assert.False(sut.IsDuplicate(tick));

        // Because the window is 0 ms, the next call should evict the entry and accept it again
        Assert.False(sut.IsDuplicate(tick));
    }

    private static NormalizedTick CreateTick(
        string ticker, decimal price, decimal volume,
        string source = "TestExchange", DateTimeOffset? timestamp = null)
    {
        SymbolParsingHelpers.TryResolveSymbol(SymbolParsingHelpers.Canonicalize(ticker), out var symbol);
        return new NormalizedTick(ticker, symbol, price, volume, timestamp ?? DateTimeOffset.UtcNow, source, DateTimeOffset.UtcNow);
    }

    private static NormalizedTick CreateTickWithTradeId(
        string ticker, decimal price, decimal volume,
        string source, string tradeId, DateTimeOffset? timestamp = null)
    {
        SymbolParsingHelpers.TryResolveSymbol(SymbolParsingHelpers.Canonicalize(ticker), out var symbol);
        return new NormalizedTick(ticker, symbol, price, volume, timestamp ?? DateTimeOffset.UtcNow, source, DateTimeOffset.UtcNow, sourceTradeId: tradeId);
    }
}
