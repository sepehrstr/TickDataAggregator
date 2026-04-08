using Microsoft.Extensions.Logging;
using Moq;
using TickDataAggregator.Application.Hosting;
using TickDataAggregator.Application.Processing;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.UnitTests.Services;

public sealed class ExchangeIngestionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StreamsTicks_WritesToChannel()
    {
        var channel = new TickChannel(100);

        var ticks = new[]
        {
            new RawTickData { RawPayload = "tick1", Source = "TestExchange" },
            new RawTickData { RawPayload = "tick2", Source = "TestExchange" },
            new RawTickData { RawPayload = "tick3", Source = "TestExchange" }
        };

        var connector = new Mock<IExchangeConnector>();
        connector.Setup(c => c.ExchangeName).Returns("TestExchange");
        connector.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        connector.Setup(c => c.StreamTicksAsync(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(ticks));

        var service = new ExchangeIngestionService(
            connector.Object,
            channel,
            Mock.Of<ILogger<ExchangeIngestionService>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        _ = service.StartAsync(cts.Token);

        var received = new List<RawTickData>();
        var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        for (var i = 0; i < 3; i++)
        {
            try
            {
                var item = await channel.Reader.ReadAsync(readTimeout.Token);
                received.Add(item);
            }
            catch (OperationCanceledException) { break; }
        }

        await cts.CancelAsync();
        try { await service.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.Equal(3, received.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ConnectionError_Reconnects()
    {
        var channel = new TickChannel(100);
        var connectCount = 0;
        // Signalled as soon as the second ConnectAsync call begins — no fixed sleep required.
        var secondConnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connector = new Mock<IExchangeConnector>();
        connector.Setup(c => c.ExchangeName).Returns("TestExchange");
        connector.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref connectCount) >= 2)
                    secondConnect.TrySetResult();
            })
            .Returns(Task.CompletedTask);
        connector.Setup(c => c.StreamTicksAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Connection lost"));

        var service = new ExchangeIngestionService(
            connector.Object,
            channel,
            Mock.Of<ILogger<ExchangeIngestionService>>(),
            reconnectBaseDelayMs: 50,
            reconnectMaxDelayMs: 200);

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await secondConnect.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        try { await service.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.True(connectCount >= 2, $"Expected at least 2 connect attempts, got {connectCount}");
    }

    /// <summary>
    /// Поток завершается без исключения (сервер закрыл соединение чисто).
    /// Сервис должен подождать базовую задержку, затем переподключиться.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CleanStreamEnd_ReconnectsAfterDelay()
    {
        var channel = new TickChannel(100);
        var connectCount = 0;
        // Signalled the moment the second connect attempt starts.
        var secondConnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connector = new Mock<IExchangeConnector>();
        connector.Setup(c => c.ExchangeName).Returns("TestExchange");
        connector.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref connectCount) >= 2)
                    secondConnect.TrySetResult();
            })
            .Returns(Task.CompletedTask);
        connector.Setup(c => c.StreamTicksAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable<RawTickData>());

        var service = new ExchangeIngestionService(
            connector.Object,
            channel,
            Mock.Of<ILogger<ExchangeIngestionService>>(),
            reconnectBaseDelayMs: 50,
            reconnectMaxDelayMs: 200);

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await secondConnect.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        try { await service.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        // Reconnect did happen — the service did not give up after a clean close.
        Assert.True(connectCount >= 2, $"Expected at least 2 connect attempts after clean close, got {connectCount}");
        // We cancel immediately after the signal; a very large count would indicate the
        // backoff delay is being skipped and the loop is spinning freely.
        Assert.True(connectCount <= 10, $"connectCount {connectCount} suggests reconnects are spinning without the expected delay");
    }

    /// <summary>
    /// При отмене токена сервис должен остановиться без дополнительных попыток переподключения.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsWithoutExtraReconnects()
    {
        var channel = new TickChannel(100);
        var connectCount = 0;
        // Signalled as soon as ConnectAsync is called — the service is guaranteed to be
        // entering the streaming loop, so it is safe to cancel without any fixed sleep.
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connector = new Mock<IExchangeConnector>();
        connector.Setup(c => c.ExchangeName).Returns("TestExchange");
        connector.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                Interlocked.Increment(ref connectCount);
                connected.TrySetResult();
            })
            .Returns(Task.CompletedTask);
        connector.Setup(c => c.StreamTicksAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => InfiniteAsyncEnumerable<RawTickData>(ct));

        var service = new ExchangeIngestionService(
            connector.Object,
            channel,
            Mock.Of<ILogger<ExchangeIngestionService>>(),
            reconnectBaseDelayMs: 50,
            reconnectMaxDelayMs: 200);

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        try { await service.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        // Должно быть ровно одно подключение — отмена не вызывает повторного цикла.
        Assert.Equal(1, connectCount);
    }

    /// <summary>
    /// DisposeAsync должен быть вызван при остановке через StopAsync.
    /// </summary>
    [Fact]
    public async Task StopAsync_CallsDisposeAsyncOnConnector()
    {
        var channel = new TickChannel(100);
        var disposed = false;
        // Signalled once the service is live and inside StreamTicksAsync.
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connector = new Mock<IExchangeConnector>();
        connector.Setup(c => c.ExchangeName).Returns("TestExchange");
        connector.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .Callback(() => connected.TrySetResult())
            .Returns(Task.CompletedTask);
        connector.Setup(c => c.StreamTicksAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(InfiniteAsyncEnumerable<RawTickData>);
        connector.Setup(c => c.DisposeAsync())
            .Callback(() => disposed = true)
            .Returns(ValueTask.CompletedTask);

        var service = new ExchangeIngestionService(
            connector.Object,
            channel,
            Mock.Of<ILogger<ExchangeIngestionService>>());

        using var cts = new CancellationTokenSource();
        _ = service.StartAsync(cts.Token);

        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        try { await service.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        Assert.True(disposed, "DisposeAsync was not called on the connector");
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<T> InfiniteAsyncEnumerable<T>(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Never ends cleanly — Task.Delay throws OperationCanceledException when ct fires,
        // which propagates to the consumer and triggers the cancellation exit path in the service.
        while (true)
        {
            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        // ReSharper disable once IteratorNeverReturns
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
