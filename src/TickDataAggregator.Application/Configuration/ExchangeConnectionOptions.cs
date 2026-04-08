namespace TickDataAggregator.Application.Configuration;

/// <summary>
/// Параметры подключения к биржам, считываемые из секции конфигурации
/// <c>ExchangeConnections</c>.
/// </summary>
public sealed class ExchangeConnectionOptions
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public const string SectionName = "ExchangeConnections";

    /// <summary>
    /// Список настроенных конечных точек бирж.
    /// </summary>
    public List<ExchangeEndpoint> Endpoints { get; set; } = [];
}