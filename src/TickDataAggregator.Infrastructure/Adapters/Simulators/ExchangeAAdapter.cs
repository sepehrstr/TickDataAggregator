using System.Text.Json;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.Infrastructure.Adapters.Simulators;

/// <summary>
/// Адаптер симулятора Биржи A.
/// Формат сообщения: JSON — {"ticker":"BTCUSD","price":50000.5,"volume":1.2,"ts":"2024-01-01T00:00:00Z"}
/// </summary>
public sealed class ExchangeAAdapter(ILogger<ExchangeAAdapter> logger) : ITickAdapter
{
    private static readonly IReadOnlyList<NormalizedTick> Empty = [];

    public string ExchangeName => "ExchangeA";

    public bool CanHandle(string source) => source == ExchangeName;

    public IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.RawPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ticker", out var tickerProp)
                || !root.TryGetProperty("price", out var priceProp)
                || !root.TryGetProperty("volume", out var volumeProp)
                || !root.TryGetProperty("ts", out var tsProp))
            {
                logger.LogWarning("ExchangeA payload is missing required fields: {Payload}", raw.RawPayload);
                return Empty;
            }

            var rawTicker = tickerProp.GetString();
            if (rawTicker is null || !priceProp.TryGetDecimal(out var price) || !volumeProp.TryGetDecimal(out var volume))
            {
                logger.LogWarning("ExchangeA payload has unparseable values: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!tsProp.TryGetDateTimeOffset(out var timestamp))
            {
                logger.LogWarning("ExchangeA payload has unparseable timestamp: {Payload}", raw.RawPayload);
                return Empty;
            }

            if (!SymbolParsingHelpers.TryResolveSymbol(SymbolParsingHelpers.Canonicalize(rawTicker), out var symbol))
            {
                logger.LogWarning("ExchangeA unrecognized symbol {Symbol}: {Payload}", rawTicker, raw.RawPayload);
                return Empty;
            }

            return [new NormalizedTick(rawTicker, symbol, price, volume, timestamp, raw.Source, raw.ReceivedAt, raw.RawPayload)];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "ExchangeA invalid JSON: {Payload}", raw.RawPayload);
            return Empty;
        }
    }
}
