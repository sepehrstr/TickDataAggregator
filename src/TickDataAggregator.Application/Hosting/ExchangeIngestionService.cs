using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Application.Processing;
using TickDataAggregator.Domain.Abstractions;

namespace TickDataAggregator.Application.Hosting;

/// <summary>
/// Фоновый сервис, поддерживающий WebSocket-соединение с одной биржей
/// и передающий полученные сырые тики в <see cref="TickChannel"/>.
/// Реализует автоматическое переподключение с экспоненциальной выдержкой и джиттером.
/// </summary>
public sealed class ExchangeIngestionService(
    IExchangeConnector connector,
    TickChannel channel,
    ILogger<ExchangeIngestionService> logger,
    int reconnectBaseDelayMs = 1000,
    int reconnectMaxDelayMs = 30000)
    : BackgroundService
{
    private bool _firstTickReceived;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Connecting to {Exchange}...", connector.ExchangeName);
                await connector.ConnectAsync(cancellationToken);
                logger.LogInformation("WebSocket connected to {Exchange}", connector.ExchangeName);
                attempt = 0;

                await foreach (var tick in connector.StreamTicksAsync(cancellationToken))
                {
                    if (!_firstTickReceived)
                    {
                        _firstTickReceived = true;
                        logger.LogInformation("First tick received from {Exchange}", connector.ExchangeName);
                    }

                    await channel.Writer.WriteAsync(tick, cancellationToken);
                }

                // Stream ended cleanly (no exception), but unexpectedly — the server closed
                // the connection without an error frame.  Apply a jittered base delay so a
                // flapping source does not hammer a tight reconnect loop, and so multiple
                // connectors do not all reconnect in lockstep.
                var cleanCloseDelay = WithJitter(reconnectBaseDelayMs);
                logger.LogWarning(
                    "Stream from {Exchange} ended unexpectedly. Reconnecting in {Delay}ms",
                    connector.ExchangeName, cleanCloseDelay);

                try
                {
                    await Task.Delay(cleanCloseDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = WithJitter(CalculateBackoff(attempt));
                logger.LogError(ex, "Connection to {Exchange} lost. Reconnecting in {Delay}ms (attempt {Attempt})",
                    connector.ExchangeName, delay, attempt);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Ingestion service for {Exchange} stopped", connector.ExchangeName);
    }

    /// <summary>
    /// Вычисляет задержку перед повторной попыткой подключения с экспоненциальным увеличением.
    /// </summary>
    private int CalculateBackoff(int attempt)
    {
        var delay = reconnectBaseDelayMs * (1 << Math.Min(attempt - 1, 10));
        return Math.Min(delay, reconnectMaxDelayMs);
    }

    /// <summary>
    /// Добавляет случайный джиттер ±10 % от базовой задержки, чтобы несколько
    /// коннекторов не подключались одновременно после общего сбоя.
    /// </summary>
    private static int WithJitter(int baseMs)
    {
        var jitter = (int)(baseMs * 0.1 * (2 * Random.Shared.NextDouble() - 1));
        return Math.Max(0, baseMs + jitter);
    }

    /// <summary>
    /// Останавливает сервис, дожидается завершения базового процесса <see cref="BackgroundService"/>
    /// и затем освобождает ресурсы WebSocket-коннектора.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await connector.DisposeAsync();
    }
}
