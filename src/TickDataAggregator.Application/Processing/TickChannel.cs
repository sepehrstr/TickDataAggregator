using System.Threading.Channels;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Application.Processing;

/// <summary>
/// Ограниченный канал для передачи сырых тиков от нескольких производителей
/// единственному потребителю — конвейеру обработки.
/// При заполнении канала производители блокируются, обеспечивая обратное давление.
/// </summary>
public sealed class TickChannel(int capacity)
{
    private readonly Channel<RawTickData> _channel = Channel.CreateBounded<RawTickData>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    /// <summary>Сторона записи канала, используемая коннекторами бирж.</summary>
    public ChannelWriter<RawTickData> Writer => _channel.Writer;

    /// <summary>Сторона чтения канала, используемая конвейером обработки.</summary>
    public ChannelReader<RawTickData> Reader => _channel.Reader;
}
