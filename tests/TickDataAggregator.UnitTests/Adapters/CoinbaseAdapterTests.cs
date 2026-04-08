using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class CoinbaseAdapterTests
{
    private readonly CoinbaseAdapter _sut = new(Mock.Of<ILogger<CoinbaseAdapter>>());

    [Fact]
    public void CanHandle_Coinbase_ReturnsTrue()
    {
        Assert.True(_sut.CanHandle("Coinbase"));
    }

    [Fact]
    public void CanHandle_OtherSource_ReturnsFalse()
    {
        Assert.False(_sut.CanHandle("Binance"));
    }

    [Fact]
    public void Normalize_ValidMarketTrade_ReturnsSingleTick()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"123","product_id":"BTC-USD","price":"65000.50","size":"0.001","side":"BUY","time":"2024-06-15T12:00:00Z"}]}]}""",
            Source = "Coinbase"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        var tick = result[0];
        Assert.Equal("BTCUSD", tick.Ticker);
        Assert.Equal(TradingSymbol.BTCUSD, tick.Symbol);
        Assert.Equal(65000.50m, tick.Price);
        Assert.Equal(0.001m, tick.Volume);
        Assert.Equal("Coinbase", tick.Source);
        Assert.Equal("123", tick.SourceTradeId);
    }

    [Fact]
    public void Normalize_ProductIdDashRemoved()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"456","product_id":"ETH-USD","price":"3500.25","size":"2.5","side":"SELL","time":"2024-06-15T12:00:00Z"}]}]}""",
            Source = "Coinbase"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        Assert.Equal("ETHUSD", result[0].Ticker);
        Assert.Equal(TradingSymbol.ETHUSD, result[0].Symbol);
        Assert.Equal("456", result[0].SourceTradeId);
    }

    [Fact]
    public void Normalize_BatchedTrades_ReturnsAllTicks()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"1","product_id":"BTC-USD","price":"65000.50","size":"0.001","side":"BUY","time":"2024-06-15T12:00:00Z"},{"trade_id":"2","product_id":"ETH-USD","price":"3500.25","size":"2.5","side":"SELL","time":"2024-06-15T12:00:01Z"}]}]}""",
            Source = "Coinbase"
        };

        var result = _sut.Normalize(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal(TradingSymbol.BTCUSD, result[0].Symbol);
        Assert.Equal(65000.50m, result[0].Price);
        Assert.Equal("1", result[0].SourceTradeId);
        Assert.Equal(TradingSymbol.ETHUSD, result[1].Symbol);
        Assert.Equal(3500.25m, result[1].Price);
        Assert.Equal("2", result[1].SourceTradeId);
    }

    [Fact]
    public void Normalize_MultipleEvents_ReturnsAllTicks()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"1","product_id":"BTC-USD","price":"65000","size":"1","side":"BUY","time":"2024-06-15T12:00:00Z"}]},{"type":"update","trades":[{"trade_id":"2","product_id":"ETH-USD","price":"3500","size":"2","side":"SELL","time":"2024-06-15T12:00:01Z"}]}]}""",
            Source = "Coinbase"
        };

        var result = _sut.Normalize(raw);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Normalize_NonMarketTradesChannel_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"heartbeats","events":[]}""",
            Source = "Coinbase"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_EmptyEvents_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[]}""",
            Source = "Coinbase"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_InvalidJson_ReturnsEmpty()
    {
        var raw = new RawTickData { RawPayload = "not json", Source = "Coinbase" };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_UnrecognizedSymbol_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"1","product_id":"FOO-BAR","price":"1.0","size":"1.0","side":"BUY","time":"2024-06-15T12:00:00Z"}]}]}""",
            Source = "Coinbase"
        };

        Assert.Empty(_sut.Normalize(raw));
    }
}
