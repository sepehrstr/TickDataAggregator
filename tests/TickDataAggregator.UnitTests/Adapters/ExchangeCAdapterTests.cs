using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters;
using TickDataAggregator.Infrastructure.Adapters.Simulators;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class ExchangeCAdapterTests
{
    private readonly ExchangeCAdapter _sut = new(Mock.Of<ILogger<ExchangeCAdapter>>());

    [Fact]
    public void Normalize_ValidPipeDelimited_ReturnsSingleTick()
    {
        var raw = new RawTickData
        {
            RawPayload = "XAUUSD|2350.30|5.0|2024-06-15T12:00:00.000Z",
            Source = "ExchangeC"
        };

        var result = _sut.Normalize(raw);

        Assert.Single(result);
        var tick = result[0];
        Assert.Equal("XAUUSD", tick.Ticker);
        Assert.Equal(TradingSymbol.XAUUSD, tick.Symbol);
        Assert.Equal(2350.30m, tick.Price);
        Assert.Equal(5.0m, tick.Volume);
        Assert.Equal("ExchangeC", tick.Source);
        Assert.Equal(TimeSpan.Zero, tick.Timestamp.Offset); // must be UTC
        Assert.Equal(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero), tick.Timestamp);
    }

    [Fact]
    public void Normalize_InsufficientFields_ReturnsEmpty()
    {
        var raw = new RawTickData { RawPayload = "XAUUSD|2350.30", Source = "ExchangeC" };

        Assert.Empty(_sut.Normalize(raw));
    }

    [Fact]
    public void Normalize_UnrecognizedSymbol_ReturnsEmpty()
    {
        var raw = new RawTickData
        {
            RawPayload = "FOOBAR|100.0|1.0|2024-06-15T12:00:00.000Z",
            Source = "ExchangeC"
        };

        Assert.Empty(_sut.Normalize(raw));
    }
}
