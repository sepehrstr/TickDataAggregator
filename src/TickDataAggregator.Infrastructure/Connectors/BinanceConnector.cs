using Microsoft.Extensions.Logging;

namespace TickDataAggregator.Infrastructure.Connectors;

/// <summary>
/// Коннектор Binance. Подписка закодирована в URL потока
/// (например, /ws/btcusdt@trade), поэтому дополнительный фрейм подписки не нужен.
/// </summary>
public sealed class BinanceConnector(string exchangeName, Uri uri, ILogger logger)
    : BaseWebSocketConnector(exchangeName, uri, logger);
