using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters;
using TickDataAggregator.Infrastructure.Adapters.Simulators;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class ExchangeBAdapterTests
{
    private readonly ExchangeBAdapter _sut = new(Mock.Of<ILogger<ExchangeBAdapter>>());

    [Fact]
    public void Normalize_ValidCsv_ReturnsSingleTick()
    {
        var raw = new RawTickData
        {
            RawPayload = "ETHUSD,3500.25,10.5,1718452800000",
            Source = "ExchangeB"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        var tick = result[0];
        Assert.Equal("ETHUSD", tick.Ticker);
        Assert.Equal(TradingSymbol.ETHUSD, tick.Symbol);
        Assert.Equal(3500.25m, tick.Price);
        Assert.Equal(10.5m, tick.Volume);
        Assert.Equal("ExchangeB", tick.Source);
    }

    [Fact]
    public void Normalize_InsufficientFields_ReturnsEmpty()
    {
        var raw = new RawTickData { RawPayload = "ETHUSD,3500", Source = "ExchangeB" };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_InvalidNumber_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = "ETHUSD,notanumber,10.5,1718452800000",
            Source = "ExchangeB"
        };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_UnrecognizedSymbol_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = "FOOBAR,100.0,1.0,1718452800000",
            Source = "ExchangeB"
        };

        Assert.Empty(_sut.Normalize(raw));
    }
}
