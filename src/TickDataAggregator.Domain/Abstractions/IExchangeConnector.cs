using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Domain.Abstractions;

/// <summary>
/// Контракт WebSocket-подключения к бирже.
/// Реализации отвечают за установку соединения, стриминг сырых данных и корректное закрытие.
/// </summary>
public interface IExchangeConnector : IAsyncDisposable
{
    /// <summary>
    /// Название биржи, используемое для идентификации источника в системе.
    /// </summary>
    string ExchangeName { get; }

    /// <summary>
    /// Устанавливает WebSocket-соединение с биржей.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Асинхронно стримит сырые тик-данные из открытого соединения.
    /// </summary>
    IAsyncEnumerable<RawTickData> StreamTicksAsync(CancellationToken cancellationToken);
}
