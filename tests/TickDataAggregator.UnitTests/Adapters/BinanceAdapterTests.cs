using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class BinanceAdapterTests
{
    private readonly BinanceAdapter _sut = new(Mock.Of<ILogger<BinanceAdapter>>());

    [Fact]
    public void CanHandle_Binance_ReturnsTrue()
    {
        Assert.True(_sut.CanHandle("Binance"));
    }

    [Fact]
    public void CanHandle_OtherSource_ReturnsFalse()
    {
        Assert.False(_sut.CanHandle("Coinbase"));
    }

    [Fact]
    public void Normalize_ValidTradeEvent_ReturnsSingleTick()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"e":"trade","E":1672515782136,"s":"BNBBTC","t":12345,"p":"0.00100","q":"100.000","T":1672515782136,"m":true,"M":true}""",
            Source = "Binance"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        var tick = result[0];
        Assert.Equal("BNBBTC", tick.Ticker);
        Assert.Equal(TradingSymbol.BNBBTC, tick.Symbol);
        Assert.Equal(0.00100m, tick.Price);
        Assert.Equal(100.000m, tick.Volume);
        Assert.Equal("Binance", tick.Source);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1672515782136), tick.Timestamp);
        Assert.Equal("12345", tick.SourceTradeId);
    }

    [Fact]
    public void Normalize_NonTradeEvent_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"e":"kline","E":1672515782136,"s":"BNBBTC"}""",
            Source = "Binance"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_SubscriptionConfirmation_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"result":null,"id":1}""",
            Source = "Binance"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_InvalidJson_ReturnsEmpty()
    {
        var raw = new RawTickData { RawPayload = "not json", Source = "Binance" };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_UsdtTicker_MapsToUsd()
    {
        // Binance uses USDT as default stable-coin quote. The adapter strips the trailing T.
        var raw = new RawTickData
        {
            RawPayload = """{"e":"trade","E":1672515782136,"s":"BTCUSDT","t":99,"p":"50000","q":"1","T":1672515782136,"m":false,"M":true}""",
            Source = "Binance"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        Assert.Equal(TradingSymbol.BTCUSD, result[0].Symbol);
        Assert.Equal("99", result[0].SourceTradeId);
    }

    [Fact]
    public void Normalize_UnrecognizedSymbol_ReturnsEmpty()    {
        var raw = new RawTickData
        {
            RawPayload = """{"e":"trade","E":1672515782136,"s":"FOOBAR","t":12345,"p":"1.0","q":"1.0","T":1672515782136,"m":true,"M":true}""",
            Source = "Binance"
        };

        Assert.Empty(_sut.Normalize(raw));
    }
}
