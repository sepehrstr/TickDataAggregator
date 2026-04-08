namespace TickDataAggregator.Infrastructure.Database;

/// <summary>
/// Контракт инициализации схемы БД.
/// Вызывается при запуске приложения, если таблицы и индексы ещё не существуют.
/// </summary>
public interface ISchemaInitializer
{
    /// <summary>
    /// Убеждается, что схема БД создана (выполняет CREATE TABLE IF NOT EXISTS
    /// и сопутствующие операции).
    /// </summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken);
}