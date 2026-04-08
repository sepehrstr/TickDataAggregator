using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Domain.Abstractions;

/// <summary>
/// Контракт адаптера, преобразующего сырые данные биржи в нормализованные тики.
/// Каждая биржа имеет собственную реализацию.
/// </summary>
public interface ITickAdapter
{
    /// <summary>
    /// Название биржи, которую обслуживает данный адаптер.
    /// </summary>
    string ExchangeName { get; }

    /// <summary>
    /// Возвращает <c>true</c>, если адаптер умеет обрабатывать указанный источник.
    /// </summary>
    bool CanHandle(string source);

    /// <summary>
    /// Разбирает сырые данные и возвращает список нормализованных тиков.
    /// При невалидных или неизвестных данных возвращает пустой список — исключения не бросаются.
    /// </summary>
    IReadOnlyList<NormalizedTick> Normalize(RawTickData raw);
}
