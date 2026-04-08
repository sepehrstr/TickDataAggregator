namespace TickDataAggregator.Infrastructure.Adapters.Common;

/// <summary>
/// Утилиты разбора текстовых данных через <see cref="System.ReadOnlySpan{T}"/> без выделений памяти.
/// Используется всеми адаптерами, принимающими сообщения фиксированного формата (CSV, pipe-delimited и т.п.).
/// </summary>
public static class SpanParsingHelpers
{
    /// <summary>
    /// Сдвигает <paramref name="remaining"/> за ближайший разделитель <paramref name="delimiter"/>
    /// и записывает в <paramref name="field"/> текст до него.
    /// Возвращает <c>false</c>, если разделитель не найден — в этом случае последнее поле
    /// следует читать непосредственно из <paramref name="remaining"/>.
    /// </summary>
    public static bool TrySliceField(ref ReadOnlySpan<char> remaining, char delimiter, out ReadOnlySpan<char> field)
    {
        var idx = remaining.IndexOf(delimiter);
        if (idx < 0) { field = default; return false; }
        field = remaining[..idx];
        remaining = remaining[(idx + 1)..];
        return true;
    }
}
