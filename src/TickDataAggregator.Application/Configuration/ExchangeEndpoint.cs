using JetBrains.Annotations;

namespace TickDataAggregator.Application.Configuration;

/// <summary>
/// Конфигурация одной конечной точки биржи: адрес WebSocket, тип коннектора
/// и параметры переподключения.
/// </summary>
[UsedImplicitly]
public sealed class ExchangeEndpoint
{
    /// <summary>
    /// Название биржи (используется для логирования и идентификации адаптера).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Адрес WebSocket-эндпоинта биржи.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Определяет тип коннектора, создаваемого для данной конечной точки.
    /// По умолчанию — <see cref="ConnectorKind.Generic"/>.
    /// </summary>
    public ConnectorKind Kind { get; set; } = ConnectorKind.Generic;

    /// <summary>
    /// Включена ли конечная точка. Отключённые точки игнорируются при запуске.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Начальная задержка перед первой попыткой переподключения (мс).
    /// </summary>
    public int ReconnectBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Максимальная задержка между попытками переподключения (мс).
    /// </summary>
    public int ReconnectMaxDelayMs { get; set; } = 30000;
}