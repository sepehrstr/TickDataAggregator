using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.UnitTests.Adapters;

public sealed class SymbolParsingHelpersTests
{
    // --- Canonicalize ---

    [Theory]
    [InlineData("BTCUSD", "BTCUSD")]
    [InlineData("btcusd", "BTCUSD")]
    [InlineData("BTC-USD", "BTCUSD")]
    [InlineData("BTC/USD", "BTCUSD")]
    [InlineData("BTC_USD", "BTCUSD")]
    [InlineData("eth-usd", "ETHUSD")]
    public void Canonicalize_StripsDelimitersAndUppercases(string raw, string expected)
    {
        Assert.Equal(expected, SymbolParsingHelpers.Canonicalize(raw));
    }

    // --- TryResolveSymbol ---

    [Theory]
    [InlineData("BTCUSD", TradingSymbol.BTCUSD)]
    [InlineData("ETHUSD", TradingSymbol.ETHUSD)]
    [InlineData("BNBBTC", TradingSymbol.BNBBTC)]
    [InlineData("XRPUSD", TradingSymbol.XRPUSD)]
    [InlineData("XAUUSD", TradingSymbol.XAUUSD)]
    [InlineData("EURUSD", TradingSymbol.EURUSD)]
    public void TryResolveSymbol_KnownCanonicalSymbol_ReturnsTrueAndValue(string canonical, TradingSymbol expected)
    {
        Assert.True(SymbolParsingHelpers.TryResolveSymbol(canonical, out var symbol));
        Assert.Equal(expected, symbol);
    }

    [Theory]
    [InlineData("FOOBAR")]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    public void TryResolveSymbol_UnknownSymbol_ReturnsFalseAndUnknown(string canonical)
    {
        Assert.False(SymbolParsingHelpers.TryResolveSymbol(canonical, out var symbol));
        Assert.Equal(TradingSymbol.Unknown, symbol);
    }

    [Fact]
    public void TryResolveSymbol_LowercaseInput_ReturnsFalse()
    {
        // TryResolveSymbol expects pre-canonicalized input; lowercase should not resolve
        Assert.False(SymbolParsingHelpers.TryResolveSymbol("btcusd", out _));
    }

    [Fact]
    public void Canonicalize_ThenTryResolveSymbol_RoundTrips()
    {
        var canonical = SymbolParsingHelpers.Canonicalize("BTC-USD");
        Assert.True(SymbolParsingHelpers.TryResolveSymbol(canonical, out var symbol));
        Assert.Equal(TradingSymbol.BTCUSD, symbol);
    }
}
