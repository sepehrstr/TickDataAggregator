using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Domain.Abstractions;

/// <summary>
/// Контракт дедупликатора тиков на основе скользящего временного окна.
/// </summary>
public interface ITickDeduplicator
{
    /// <summary>
    /// Возвращает <c>true</c>, если идентичный тик уже был обработан внутри окна дедупликации.
    /// </summary>
    bool IsDuplicate(NormalizedTick tick);
}
