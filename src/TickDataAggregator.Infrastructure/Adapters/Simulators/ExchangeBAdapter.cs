using System.Globalization;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.Infrastructure.Adapters.Simulators;

/// <summary>
/// Адаптер симулятора Биржи B.
/// Формат сообщения: CSV — ETHUSD,3000.25,10.5,1704067200000
/// (тикер, цена, объём, timestamp_ms)
/// </summary>
public sealed class ExchangeBAdapter(ILogger<ExchangeBAdapter> logger) : ITickAdapter
{
    private static readonly IReadOnlyList<NormalizedTick> Empty = [];

    public string ExchangeName => "ExchangeB";

    public bool CanHandle(string source) => source == ExchangeName;

    public IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        // Разбор через span избегает выделения string[] и попергментных строк при Split.
        var rem = raw.RawPayload.AsSpan();

        if (!SpanParsingHelpers.TrySliceField(ref rem, ',', out var tickerSpan)
            || !SpanParsingHelpers.TrySliceField(ref rem, ',', out var priceSpan)
            || !SpanParsingHelpers.TrySliceField(ref rem, ',', out var volumeSpan))
        {
            logger.LogWarning("ExchangeB payload has insufficient CSV fields: {Payload}", raw.RawPayload);
            return Empty;
        }

        // rem — это поле timestamp_ms (остаток после третьего разделителя)
        var rawTicker = tickerSpan.Trim().ToString();

        if (!decimal.TryParse(priceSpan.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || !decimal.TryParse(volumeSpan.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var volume)
            || !long.TryParse(rem.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampMs))
        {
            logger.LogWarning("ExchangeB payload has unparseable numeric values: {Payload}", raw.RawPayload);
            return Empty;
        }

        if (!SymbolParsingHelpers.TryResolveSymbol(SymbolParsingHelpers.Canonicalize(rawTicker), out var symbol))
        {
            logger.LogWarning("ExchangeB unrecognized symbol {Symbol}: {Payload}", rawTicker, raw.RawPayload);
            return Empty;
        }

        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        return [new NormalizedTick(rawTicker, symbol, price, volume, timestamp, raw.Source, raw.ReceivedAt, raw.RawPayload)];
    }
}
