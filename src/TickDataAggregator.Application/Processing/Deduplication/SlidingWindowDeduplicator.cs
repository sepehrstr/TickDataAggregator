using Microsoft.Extensions.Options;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Application.Processing.Deduplication;

/// <summary>
/// Дедупликатор на основе скользящего временного окна.
/// Не потокобезопасен по замыслу — предназначен для вызова исключительно
/// из единственного потребителя конвейера.
/// Для вытеснения истёкших записей использует упорядоченную очередь,
/// что даёт амортизированную O(1) стоимость вместо периодического полного сканирования.
/// </summary>
public sealed class SlidingWindowDeduplicator(IOptions<PipelineOptions> options) : ITickDeduplicator
{
    // Обычного словаря достаточно: дедупликатор вызывается только из одного потока-потребителя.
    private readonly Dictionary<DeduplicationKey, long> _seen = new();
    private readonly Queue<(DeduplicationKey Key, long SeenAtMs)> _expiryQueue = new();
    private readonly long _windowMs = options.Value.DeduplicationWindowSeconds * 1_000L;

    /// <summary>
    /// Возвращает <c>true</c>, если идентичный тик уже был обработан в пределах окна дедупликации.
    /// Побочный эффект: регистрирует тик как «виденный», если он не является дубликатом.
    /// </summary>
    public bool IsDuplicate(NormalizedTick tick)
    {
        var key = DeduplicationKey.From(tick);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        EvictExpired(nowMs);

        if (_seen.TryGetValue(key, out var seenAtMs) && nowMs - seenAtMs < _windowMs)
            return true;

        _seen[key] = nowMs;
        _expiryQueue.Enqueue((key, nowMs));
        return false;
    }

    /// <summary>
    /// Удаляет из словаря записи, время жизни которых превысило окно дедупликации.
    /// Защищает от ошибочного удаления ключей, которые были повторно добавлены
    /// с более новой меткой времени.
    /// </summary>
    private void EvictExpired(long nowMs)
    {
        while (_expiryQueue.TryPeek(out var oldest) && nowMs - oldest.SeenAtMs >= _windowMs)
        {
            _expiryQueue.Dequeue();
            // Удаляем из словаря только если записанное время совпадает с временем из очереди.
            // Повторная вставка того же ключа обновляет _seen[key] до более нового значения,
            // поэтому более новую запись удалять нельзя.
            if (_seen.TryGetValue(oldest.Key, out var seenAtMs) && nowMs - seenAtMs >= _windowMs)
                _seen.Remove(oldest.Key);
        }
    }
}

