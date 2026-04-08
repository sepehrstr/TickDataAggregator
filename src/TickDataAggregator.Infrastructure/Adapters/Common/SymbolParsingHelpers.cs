using System.Collections.Frozen;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Infrastructure.Adapters.Common;

/// <summary>
/// Общие утилиты разбора торговых символов, используемые всеми адаптерами.
/// Отвечают только за удаление разделителей и сопоставление с перечислением.
/// Специфичные для биржи преобразования (например, обрезка USDT→USD у Binance
/// или формат «BTC-USD» у Coinbase) лежат на ответственности каждого адаптера
/// до вызова <see cref="TryResolveSymbol"/>.
/// </summary>
public static class SymbolParsingHelpers
{
    private static readonly FrozenDictionary<string, TradingSymbol> CanonicalMappings = BuildMappings();

    /// <summary>
    /// Приводит строку символа к верхнему регистру и удаляет типичные разделители (-, /, _),
    /// формируя вид, сопоставимый с именами значений <see cref="TradingSymbol"/>.
    /// </summary>
    public static string Canonicalize(string raw) =>
        raw.ToUpperInvariant()
           .Replace("-", "")
           .Replace("/", "")
           .Replace("_", "");

    /// <summary>
    /// Возвращает <c>true</c> и устанавливает <paramref name="symbol"/>, если <paramref name="canonical"/>
    /// соответствует известному значению <see cref="TradingSymbol"/>. Входная строка должна быть
    /// предварительно канонизирована (верхний регистр, разделители удалены).
    /// Потокобезопасно — словарь доступен только для чтения после инициализации.
    /// </summary>
    public static bool TryResolveSymbol(string canonical, out TradingSymbol symbol)
    {
        symbol = CanonicalMappings.GetValueOrDefault(canonical, TradingSymbol.Unknown);
        return symbol != TradingSymbol.Unknown;
    }

    private static FrozenDictionary<string, TradingSymbol> BuildMappings()
    {
        var dict = new Dictionary<string, TradingSymbol>(StringComparer.Ordinal);
        foreach (var s in Enum.GetValues<TradingSymbol>())
        {
            if (s != TradingSymbol.Unknown)
                dict[s.ToString().ToUpperInvariant()] = s;
        }
        return dict.ToFrozenDictionary();
    }
}
