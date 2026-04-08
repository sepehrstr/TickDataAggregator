using System.Globalization;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Adapters.Common;

namespace TickDataAggregator.Infrastructure.Adapters.Simulators;

/// <summary>
/// Адаптер симулятора Биржи C.
/// Формат сообщения: pipe-delimited — XAUUSD|1950.30|5.0|2024-01-01T00:00:00.000Z
/// (тикер|цена|объём|iso8601-utc-datetime)
/// </summary>
public sealed class ExchangeCAdapter(ILogger<ExchangeCAdapter> logger) : ITickAdapter
{
    private static readonly IReadOnlyList<NormalizedTick> Empty = [];

    public string ExchangeName => "ExchangeC";

    public bool CanHandle(string source) => source == ExchangeName;

    public IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        // Разбор через span избегает выделения string[] и попергментных строк при Split.
        var rem = raw.RawPayload.AsSpan();

        if (!SpanParsingHelpers.TrySliceField(ref rem, '|', out var tickerSpan)
            || !SpanParsingHelpers.TrySliceField(ref rem, '|', out var priceSpan)
            || !SpanParsingHelpers.TrySliceField(ref rem, '|', out var volumeSpan))
        {
            logger.LogWarning("ExchangeC payload has insufficient pipe-delimited fields: {Payload}", raw.RawPayload);
            return Empty;
        }

        // rem — это поле datetime (остаток после третьего разделителя)
        var rawTicker = tickerSpan.Trim().ToString();

        if (!decimal.TryParse(priceSpan.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || !decimal.TryParse(volumeSpan.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var volume)
            || !DateTimeOffset.TryParse(rem.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            logger.LogWarning("ExchangeC payload has unparseable values: {Payload}", raw.RawPayload);
            return Empty;
        }

        if (!SymbolParsingHelpers.TryResolveSymbol(SymbolParsingHelpers.Canonicalize(rawTicker), out var symbol))
        {
            logger.LogWarning("ExchangeC unrecognized symbol {Symbol}: {Payload}", rawTicker, raw.RawPayload);
            return Empty;
        }

        return [new NormalizedTick(rawTicker, symbol, price, volume, timestamp, raw.Source, raw.ReceivedAt, raw.RawPayload)];
    }
}
