namespace TickDataAggregator.Application.Configuration;

/// <summary>
/// Определяет тип WebSocket-коннектора, создаваемого для конечной точки биржи.
/// Отделяет выбор коннектора от человекочитаемого <see cref="ExchangeEndpoint.Name"/>,
/// чтобы переименование эндпоинта в конфигурации не приводило к молчаливому
/// откату на универсальный коннектор.
/// </summary>
public enum ConnectorKind
{
    /// <summary>
    /// Универсальный WebSocket-коннектор — фрейм подписки не требуется.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Коннектор Binance — подписка зашита в URL потока.
    /// </summary>
    Binance = 1,

    /// <summary>
    /// Коннектор Coinbase Advanced Trade — отправляет фрейм подписки market_trades сразу после подключения.
    /// </summary>
    Coinbase = 2
}