namespace TickDataAggregator.Infrastructure.Configuration;

/// <summary>
/// Параметры подключения к базе данных, считываемые из секции конфигурации
/// <c>Database</c>.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Строка подключения Npgsql.
    /// </summary>
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=tickdata;Username=postgres;Password=postgres";
}
