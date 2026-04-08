using System.Diagnostics.CodeAnalysis;

namespace TickDataAggregator.Domain.Models;

/// <summary>
/// Нормализованный тик — валидированная сделка, полученная от одной из бирж
/// и приведённая к единому формату домена.
/// </summary>
public sealed record NormalizedTick
{
    /// <summary>
    /// Биржевой тикер в исходном формате источника (например, «BTCUSDT»).
    /// </summary>
    public required string Ticker { get; init; }

    /// <summary>
    /// Канонический торговый символ, соответствующий <see cref="TradingSymbol"/>.
    /// </summary>
    public required TradingSymbol Symbol { get; init; }

    /// <summary>
    /// Цена сделки.
    /// </summary>
    public required decimal Price { get; init; }

    /// <summary>
    /// Объём сделки.
    /// </summary>
    public required decimal Volume { get; init; }

    /// <summary>
    /// Время сделки по данным биржи.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Название источника (биржи), от которой получены данные.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Момент получения сообщения на стороне приложения (UTC).
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Исходная строка сообщения от биржи до разбора.
    /// </summary>
    public string RawPayload { get; init; } = "";

    /// <summary>
    /// Идентификатор сделки, присвоенный биржей. Используется как основной ключ дедупликации.
    /// Равен <c>null</c> для источников, не передающих идентификаторы отдельных сделок.
    /// </summary>
    public string? SourceTradeId { get; init; }

    /// <summary>
    /// Инициализирует новый нормализованный тик со всеми полями.
    /// </summary>
    [SetsRequiredMembers]
    public NormalizedTick(
        string ticker,
        TradingSymbol symbol,
        decimal price,
        decimal volume,
        DateTimeOffset timestamp,
        string source,
        DateTimeOffset receivedAt,
        string rawPayload = "",
        string? sourceTradeId = null)
    {
        Ticker = ticker;
        Symbol = symbol;
        Price = price;
        Volume = volume;
        Timestamp = timestamp;
        Source = source;
        ReceivedAt = receivedAt;
        RawPayload = rawPayload;
        SourceTradeId = sourceTradeId;
    }
}
