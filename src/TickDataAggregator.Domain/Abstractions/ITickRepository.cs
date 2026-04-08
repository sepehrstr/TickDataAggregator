using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Domain.Abstractions;

/// <summary>
/// Контракт репозитория для пакетного сохранения нормализованных тиков.
/// </summary>
public interface ITickRepository
{
    /// <summary>
    /// Асинхронно сохраняет пакет тиков в постоянное хранилище.
    /// </summary>
    Task SaveTicksAsync(IReadOnlyList<NormalizedTick> ticks, CancellationToken cancellationToken);
}