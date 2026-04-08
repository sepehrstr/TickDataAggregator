using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Application.Processing.Deduplication;

/// <summary>
/// Типизированный ключ дедупликации.
/// Если источник передаёт стабильный идентификатор сделки, используется только пара
/// (Source, TradeId). Для источников без идентификаторов — полный натуральный ключ сделки.
/// </summary>
internal readonly record struct DeduplicationKey(
    string Source,
    string? TradeId,
    TradingSymbol Symbol,
    decimal Price,
    decimal Volume,
    long TimestampMs)
{
    /// <summary>
    /// Создаёт ключ из нормализованного тика.
    /// Если у тика есть <see cref="NormalizedTick.SourceTradeId"/>, все поля натурального ключа
    /// обнуляются — идентификатор сделки является достаточным для обнаружения дубликата.
    /// </summary>
    public static DeduplicationKey From(NormalizedTick tick) =>
        tick.SourceTradeId is not null
            ? new DeduplicationKey(tick.Source, tick.SourceTradeId, default, 0, 0, 0)
            : new DeduplicationKey(
                tick.Source,
                null,
                tick.Symbol,
                tick.Price,
                tick.Volume,
                tick.Timestamp.ToUnixTimeMilliseconds());
}