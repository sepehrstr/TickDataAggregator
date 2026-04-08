using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Application.Processing;

namespace TickDataAggregator.Application.Hosting;

/// <summary>
/// Фоновый сервис, периодически записывающий в лог основные метрики
/// конвейера обработки тиков: поток, дубликаты, ошибки и скорость персистенции.
/// </summary>
public sealed class MetricsReportingService(TickProcessingPipeline pipeline, ILogger<MetricsReportingService> logger)
    : BackgroundService
{
    private readonly TimeSpan _reportInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long lastPersisted = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reportInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var currentPersisted = pipeline.PersistedCount;
            var ticksPerSecond = (currentPersisted - lastPersisted) / _reportInterval.TotalSeconds;
            lastPersisted = currentPersisted;

            logger.LogInformation(
                "[Metrics] Persisted={Persisted}, Duplicates={Duplicates}, ProcessingErrors={ProcessingErrors}, " +
                "Flushes={Flushes}, PersistenceRetries={PersistenceRetries}, Rate={Rate:F1} ticks/sec",
                currentPersisted,
                pipeline.DuplicateCount,
                pipeline.ProcessingErrors,
                pipeline.FlushCount,
                pipeline.PersistenceRetries,
                ticksPerSecond);
        }
    }
}
