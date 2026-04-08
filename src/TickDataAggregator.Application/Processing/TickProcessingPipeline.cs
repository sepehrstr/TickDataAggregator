using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Application.Processing;

/// <summary>
/// Фоновый сервис, читающий сырые тики из <see cref="TickChannel"/>,
/// нормализующий, дедуплицирующий и пакетно сохраняющий их в репозиторий.
/// Единственный потребитель канала — не требует синхронизации внутри цикла обработки.
/// </summary>
public sealed class TickProcessingPipeline : BackgroundService
{
    private readonly TickChannel _channel;
    private readonly Dictionary<string, ITickAdapter> _adaptersBySource;
    private readonly ITickDeduplicator _deduplicator;
    private readonly ITickRepository _repository;
    private readonly PipelineOptions _options;
    private readonly ILogger<TickProcessingPipeline> _logger;

    private long _persistedCount;
    private long _duplicateCount;
    private long _processingErrorCount;
    private long _flushCount;
    private long _persistenceRetries;
    private readonly ConcurrentDictionary<string, long> _persistedBySource = new();

    /// <summary>Общее количество тиков, успешно сохранённых в БД.</summary>
    public long PersistedCount => Volatile.Read(ref _persistedCount);

    /// <summary>Количество тиков, отброшенных как дубликаты.</summary>
    public long DuplicateCount => Volatile.Read(ref _duplicateCount);

    /// <summary>Ошибки нормализации и разбора. Сбои записи в БД учитываются в <see cref="PersistenceRetries"/>.</summary>
    public long ProcessingErrors => Volatile.Read(ref _processingErrorCount);

    /// <summary>Количество успешно выполненных пакетных записей в БД.</summary>
    public long FlushCount => Volatile.Read(ref _flushCount);

    /// <summary>Количество повторных попыток записи в БД из-за временных сбоев.</summary>
    public long PersistenceRetries => Volatile.Read(ref _persistenceRetries);

    /// <summary>Количество сохранённых тиков в разбивке по источнику (бирже).</summary>
    public IReadOnlyDictionary<string, long> PersistedBySource => _persistedBySource;

    public TickProcessingPipeline(
        TickChannel channel,
        IEnumerable<ITickAdapter> adapters,
        ITickDeduplicator deduplicator,
        ITickRepository repository,
        IOptions<PipelineOptions> options,
        ILogger<TickProcessingPipeline> logger)
    {
        _channel = channel;
        _adaptersBySource = adapters.ToDictionary(a => a.ExchangeName);
        _deduplicator = deduplicator;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uptime = Stopwatch.StartNew();

        _logger.LogInformation("Processing pipeline started. BatchSize={BatchSize}, FlushInterval={FlushInterval}ms",
            _options.BatchSize, _options.BatchFlushIntervalMs);

        var batch = new List<NormalizedTick>(_options.BatchSize);
        var flushInterval = TimeSpan.FromMilliseconds(_options.BatchFlushIntervalMs);
        var sw = Stopwatch.StartNew();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dual-wait: either a message arrives or the flush timer expires
                var remaining = flushInterval - sw.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    remaining = TimeSpan.Zero;

                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timerCts.CancelAfter(remaining);

                bool hasData;
                try
                {
                    hasData = await _channel.Reader.WaitToReadAsync(timerCts.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Timer expired, not a shutdown — flush whatever we have
                    hasData = false;
                }

                // Drain all currently available messages
                while (_channel.Reader.TryRead(out var raw))
                {
                    try
                    {
                        var ticks = Normalize(raw);
                        if (ticks.Count == 0)
                            continue;

                        foreach (var tick in ticks)
                        {
                            if (_deduplicator.IsDuplicate(tick))
                            {
                                Interlocked.Increment(ref _duplicateCount);
                                continue;
                            }

                            batch.Add(tick);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref _processingErrorCount);
                        _logger.LogError(ex, "Error processing tick from {Source}", raw.Source);
                    }
                }

                // Flush when batch is full OR timer expired with pending data
                if (batch.Count >= _options.BatchSize || (batch.Count > 0 && sw.Elapsed >= flushInterval))
                {
                    await FlushWithRetryAsync(batch, stoppingToken);
                    sw.Restart();
                }

                // Channel completed (all writers done) — exit
                if (!hasData && _channel.Reader.Completion.IsCompleted)
                    break;
            }

            // Flush remaining on shutdown — retry with a timeout so we don't block forever
            if (batch.Count > 0)
            {
                await ShutdownFlushAsync(batch);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing pipeline stopping gracefully");

            if (batch.Count > 0)
            {
                _logger.LogInformation("Flushing {Count} remaining ticks before shutdown", batch.Count);
                await ShutdownFlushAsync(batch);
            }
        }

        LogShutdownSummary(uptime.Elapsed);
    }

    private IReadOnlyList<NormalizedTick> Normalize(RawTickData raw)
    {
        if (_adaptersBySource.TryGetValue(raw.Source, out var adapter))
        {
            return adapter.Normalize(raw);
        }

        _logger.LogWarning("No adapter found for source {Source}", raw.Source);
        return [];
    }

    /// <summary>
    /// Записывает пакет с экспоненциальной выдержкой при повторных попытках.
    /// Во время повторов канал не опустошается — это создаёт обратное давление
    /// через ограниченный канал и предотвращает неограниченный рост памяти.
    /// </summary>
    private async Task FlushWithRetryAsync(List<NormalizedTick> batch, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            if (await FlushBatchAsync(batch, cancellationToken))
            {
                RecordFlushSuccess(batch);
                batch.Clear();
                return;
            }

            attempt++;
            Interlocked.Increment(ref _persistenceRetries);
            var delayMs = Math.Min(1000 * (1 << Math.Min(attempt - 1, 5)), 30_000);
            _logger.LogWarning(
                "DB flush failed — retry {Attempt} in {Delay}ms. Batch of {Count} ticks retained, channel drain paused",
                attempt, delayMs, batch.Count);
            await Task.Delay(delayMs, cancellationToken);
        }
    }

    private void RecordFlushSuccess(List<NormalizedTick> batch)
    {
        Interlocked.Add(ref _persistedCount, batch.Count);
        Interlocked.Increment(ref _flushCount);

        foreach (var tick in batch)
        {
            _persistedBySource.AddOrUpdate(tick.Source, 1, static (_, v) => v + 1);
        }
    }

    private async Task<bool> FlushBatchAsync(List<NormalizedTick> batch, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveTicksAsync(batch, cancellationToken);
            _logger.LogDebug("Flushed {Count} ticks to storage", batch.Count);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to flush {Count} ticks to storage", batch.Count);
            return false;
        }
    }

    /// <summary>
    /// Выполняет попытку записи оставшихся тиков при плановом завершении с ограничением по времени.
    /// Предотвращает бесконечное ожидание при недоступности БД во время остановки.
    /// </summary>
    private async Task ShutdownFlushAsync(List<NormalizedTick> batch)
    {
        using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await FlushWithRetryAsync(batch, shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Shutdown flush timed out after 30s — {Count} ticks could not be persisted", batch.Count);
        }
    }

    private void LogShutdownSummary(TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Pipeline shutdown — Uptime={Uptime}, Persisted={Persisted}, Duplicates={Duplicates}, " +
            "ProcessingErrors={ProcessingErrors}, Flushes={Flushes}, PersistenceRetries={PersistenceRetries}",
            elapsed, PersistedCount, DuplicateCount, ProcessingErrors, FlushCount, PersistenceRetries);

        foreach (var (source, count) in _persistedBySource)
        {
            _logger.LogInformation("  Source {Source}: {Count} ticks persisted", source, count);
        }
    }
}
