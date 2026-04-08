using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Infrastructure.Connectors;

/// <summary>
/// Базовый WebSocket-коннектор. Подклассы могут переопределить <see cref="OnConnectedAsync"/>
/// для отправки специфичного для биржи фрейма (например, подписки)
/// сразу после установки соединения.
/// </summary>
public class BaseWebSocketConnector(string exchangeName, Uri uri, ILogger logger)
    : IExchangeConnector
{
    protected readonly ILogger Logger = logger;
    private ClientWebSocket? _ws;

    public string ExchangeName { get; } = exchangeName;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(uri, cancellationToken);
        await OnConnectedAsync(_ws, cancellationToken);
    }

    /// <summary>
    /// Вызывается один раз после успешного WebSocket-хэндшейка.
    /// Переопределите, чтобы отправить фрейм подписки.
    /// Реализация по умолчанию — пустая операция.
    /// </summary>
    protected virtual Task OnConnectedAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async IAsyncEnumerable<RawTickData> StreamTicksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");

        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                result = await _ws.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Logger.LogWarning("WebSocket close received from {Exchange}", ExchangeName);
                    yield break;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var payload = Encoding.UTF8.GetString(ms.ToArray());

            yield return new RawTickData
            {
                RawPayload = payload,
                Source = ExchangeName,
                ReceivedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws is not null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None);
                }
                catch
                {
                    // Закрытие по возможности — ошибку игнорируем
                }
            }
            _ws.Dispose();
            _ws = null;
        }
    }
}
