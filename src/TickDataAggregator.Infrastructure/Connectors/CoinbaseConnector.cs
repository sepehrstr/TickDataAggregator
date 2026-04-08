using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TickDataAggregator.Infrastructure.Connectors;

/// <summary>
/// Коннектор Coinbase Advanced Trade WebSocket.
/// После установки соединения отправляет фрейм подписки на канал <c>market_trades</c>
/// для всех product_ids, настроенных для данного экземпляра.
/// </summary>
public sealed class CoinbaseConnector(
    string exchangeName,
    Uri uri,
    ILogger logger,
    IReadOnlyList<string> productIds)
    : BaseWebSocketConnector(exchangeName, uri, logger)
{
    /// <summary>
    /// Перечень product_ids по умолчанию, соответствующий значениям перечисления
    /// <see cref="TickDataAggregator.Domain.Models.TradingSymbol"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultProductIds =
    [
        "BTC-USD", "ETH-USD", "XRP-USD", "SOL-USD", "ADA-USD", "DOGE-USD", "LINK-USD", "BTC-EUR"
    ];

    protected override async Task OnConnectedAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var subscribePayload = JsonSerializer.Serialize(new
        {
            type = "subscribe",
            channel = "market_trades",
            product_ids = productIds
        });

        var bytes = Encoding.UTF8.GetBytes(subscribePayload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

        Logger.LogInformation(
            "Coinbase: sent subscribe frame for {Count} product(s): {Products}",
            productIds.Count,
            string.Join(", ", productIds));
    }
}
