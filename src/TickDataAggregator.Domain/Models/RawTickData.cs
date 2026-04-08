namespace TickDataAggregator.Domain.Models;

/// <summary>
/// Сырые данные тика, полученные от биржи до нормализации.
/// </summary>
public sealed record RawTickData
{
    /// <summary>
    /// Исходное сообщение от биржи в виде строки.
    /// </summary>
    public required string RawPayload { get; init; }

    /// <summary>
    /// Название источника (биржи), от которой получены данные.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Момент получения сообщения на стороне приложения (UTC).
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
