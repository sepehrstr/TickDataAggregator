namespace TickDataAggregator.Domain.Models;

/// <summary>
/// Канонические торговые символы, отслеживаемые системой.
/// Каждая биржа может использовать собственный формат (например, «BTC-USD», «BTCUSD», «btcusdt»),
/// но все они приводятся к единственному значению перечисления для унификации хранения и запросов.
/// </summary>
public enum TradingSymbol
{
    Unknown = 0,
    BTCUSD,
    ETHUSD,
    BNBBTC,
    XRPUSD,
    SOLUSD,
    XAUUSD,
    ADAUSD,
    DOGEUSD,
    LINKUSD,
    BTCEUR,
    EURUSD,
    GBPUSD,
    USDJPY
}
