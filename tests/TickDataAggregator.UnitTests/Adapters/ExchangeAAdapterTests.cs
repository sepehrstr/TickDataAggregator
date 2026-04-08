using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters;
using TickDataAggregator.Infrastructure.Adapters.Simulators;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class ExchangeAAdapterTests
{
    private readonly ExchangeAAdapter _sut = new(Mock.Of<ILogger<ExchangeAAdapter>>());

    [Fact]
    public void CanHandle_MatchingSource_ReturnsTrue()
    {
        Assert.True(_sut.CanHandle("ExchangeA"));
    }

    [Fact]
    public void CanHandle_DifferentSource_ReturnsFalse()
    {
        Assert.False(_sut.CanHandle("ExchangeB"));
    }

    [Fact]
    public void Normalize_ValidJson_ReturnsSingleTick()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"ticker":"BTCUSD","price":65000.50,"volume":1.25,"ts":"2024-06-15T12:00:00Z"}""",
            Source = "ExchangeA"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        var tick = result[0];
        Assert.Equal("BTCUSD", tick.Ticker);
        Assert.Equal(TradingSymbol.BTCUSD, tick.Symbol);
        Assert.Equal(65000.50m, tick.Price);
        Assert.Equal(1.25m, tick.Volume);
        Assert.Equal("ExchangeA", tick.Source);
    }

    [Fact]
    public void Normalize_InvalidJson_ReturnsEmpty()
    {
        var raw = new RawTickData { RawPayload = "not json", Source = "ExchangeA" };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_MissingField_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"ticker":"BTCUSD","price":65000.50}""",
            Source = "ExchangeA"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_UnrecognizedSymbol_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = """{"ticker":"FOOBAR","price":1.0,"volume":1.0,"ts":"2024-06-15T12:00:00Z"}""",
            Source = "ExchangeA"
        };

        Assert.Empty(_sut.Normalize(raw));
    }
}
