using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.Infrastructure.Adapters;

/// <summary>
/// Адаптер тиков Binance.
/// Поток: wss://stream.binance.com:9443/ws/btcusdt@trade
/// Формат сообщения: {"e":"trade","E":1672515782136,"s":"BNBBTC","t":12345,"p":"0.001","q":"100","T":1672515782136,"m":true,"M":true}
/// </summary>
public sealed class BinanceAdapter(ILogger<BinanceAdapter> logger) : ITickAdapter
{
    private static readonly IReadOnlyList<NormalizedTick> Empty = [];

    public string ExchangeName => "Binance";

    public bool CanHandle(string source) => source == ExchangeName;

    public IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.RawPayload);
            var root = doc.RootElement;

            // Пропускаем неторговые сообщения (подтверждения подписки и т.п.)
            if (!root.TryGetProperty("e", out var eventType) || eventType.GetString() != "trade")
                return Empty;

            if (!root.TryGetProperty("s", out var symbolProp)
                || !root.TryGetProperty("p", out var priceProp)
                || !root.TryGetProperty("q", out var quantityProp)
                || !root.TryGetProperty("T", out var tradeTimeProp)
                || !root.TryGetProperty("t", out var tradeIdProp))
            {
                logger.LogWarning("Binance payload is missing required fields: {Payload}", raw.RawPayload);
                return Empty;
            }

            var rawSymbol = symbolProp.GetString();
            var priceStr = priceProp.GetString();
            var quantityStr = quantityProp.GetString();

            if (rawSymbol is null || priceStr is null || quantityStr is null)
            {
                logger.LogWarning("Binance payload has null string fields: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!decimal.TryParse(priceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || !decimal.TryParse(quantityStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
            {
                logger.LogWarning("Binance payload has unparseable numeric values: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!tradeTimeProp.TryGetInt64(out var tradeTimeMs))
            {
                logger.LogWarning("Binance payload has unparseable trade time: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!tradeIdProp.TryGetInt64(out var tradeId))
            {
                logger.LogWarning("Binance payload has unparseable trade id: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!SymbolParsingHelpers.TryResolveSymbol(StripTether(SymbolParsingHelpers.Canonicalize(rawSymbol)), out var symbol))
            {
                logger.LogWarning("Binance unrecognized symbol {Symbol}: {Payload}", rawSymbol, raw.RawPayload);
                return Empty;
            }

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tradeTimeMs);
            return [new NormalizedTick(rawSymbol, symbol, price, quantity, timestamp, raw.Source, raw.ReceivedAt, raw.RawPayload, tradeId.ToString())];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Binance invalid JSON: {Payload}", raw.RawPayload);
            return Empty;
        }
    }

    /// <summary>
    /// Удаляет хвостовой символ «T» из USDT-пар Binance (например, BTCUSDT→BTCUSD).
    /// Binance использует USDT как стабильную котировочную валюту по умолчанию;
    /// в домене они отображаются как USD.
    /// </summary>
    private static string StripTether(string canonical) =>
        canonical.EndsWith("USDT", StringComparison.Ordinal) ? canonical[..^1] : canonical;
}
