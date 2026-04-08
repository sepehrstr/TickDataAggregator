using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.Infrastructure.Adapters;

/// <summary>
/// Адаптер тиков Coinbase Advanced Trade.
/// WebSocket: wss://advanced-trade-ws.coinbase.com
/// Канал «market_trades», формат сообщения:
/// {"type":"update","channel":"market_trades","events":[{"type":"update","trades":[{"trade_id":"...","product_id":"BTC-USD","price":"65000.50","size":"0.001","side":"BUY","time":"2024-06-15T12:00:00Z"}]}]}
/// </summary>
public sealed class CoinbaseAdapter(ILogger<CoinbaseAdapter> logger) : ITickAdapter
{
    private static readonly IReadOnlyList<NormalizedTick> Empty = [];

    public string ExchangeName => "Coinbase";

    public bool CanHandle(string source) => source == ExchangeName;

    public IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.RawPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("channel", out var channel) || channel.GetString() != "market_trades")
                return Empty;

            if (!root.TryGetProperty("events", out var events) || events.GetArrayLength() == 0)
                return Empty;

            var results = new List<NormalizedTick>();

            foreach (var evt in events.EnumerateArray())
            {
                if (!evt.TryGetProperty("trades", out var trades))
                    continue;

                foreach (var trade in trades.EnumerateArray())
                {
                    var tick = ParseTrade(trade, raw);
                    if (tick is not null)
                        results.Add(tick);
                }
            }

            return results;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Coinbase invalid JSON: {Payload}", raw.RawPayload);
            return Empty;
        }
    }

    private NormalizedTick? ParseTrade(JsonElement trade, RawTickData raw)
    {
        if (!trade.TryGetProperty("trade_id", out var tradeIdProp)
            || !trade.TryGetProperty("product_id", out var productIdProp)
            || !trade.TryGetProperty("price", out var priceProp)
            || !trade.TryGetProperty("size", out var sizeProp)
            || !trade.TryGetProperty("time", out var timeProp))
        {
            logger.LogWarning("Coinbase trade is missing required fields: {Trade}", trade.GetRawText());
            return null;
        }

        var tradeId = tradeIdProp.GetString();
        var productId = productIdProp.GetString();
        var priceStr = priceProp.GetString();
        var sizeStr = sizeProp.GetString();
        var timeStr = timeProp.GetString();

        if (tradeId is null || productId is null || priceStr is null || sizeStr is null || timeStr is null)
        {
            logger.LogWarning("Coinbase trade has null string fields: {Trade}", trade.GetRawText());
            return null;
        }

        if (!decimal.TryParse(priceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || !decimal.TryParse(sizeStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var size))
        {
            logger.LogWarning("Coinbase trade has unparseable numeric values: {Trade}", trade.GetRawText());
            return null;
        }

        if (!DateTimeOffset.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            logger.LogWarning("Coinbase trade has unparseable timestamp: {Trade}", trade.GetRawText());
            return null;
        }

        // Coinbase использует формат «BTC-USD»; Canonicalize удаляет дефис и переводит в верхний регистр.
        var rawTicker = SymbolParsingHelpers.Canonicalize(productId);

        if (!SymbolParsingHelpers.TryResolveSymbol(rawTicker, out var symbol))
        {
            logger.LogWarning("Coinbase unrecognized symbol {Symbol}: {Trade}", rawTicker, trade.GetRawText());
            return null;
        }

        return new NormalizedTick(rawTicker, symbol, price, size, time, raw.Source, raw.ReceivedAt, raw.RawPayload, tradeId);
    }
}
