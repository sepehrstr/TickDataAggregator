namespace TickDataAggregator.Application.Configuration;

/// <summary>
/// Параметры конвейера обработки тиков, считываемые из секции конфигурации
/// <c>Pipeline</c>.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// Максимальное число сообщений в ограниченном канале. Обеспечивает обратное давление.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// Максимальный размер пакета перед принудительной записью в БД.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Интервал принудительной записи накопленного пакета (мс).
    /// </summary>
    public int BatchFlushIntervalMs { get; set; } = 500;

    /// <summary>
    /// Ширина скользящего окна дедупликации (секунд).
    /// </summary>
    public int DeduplicationWindowSeconds { get; set; } = 10;
}
