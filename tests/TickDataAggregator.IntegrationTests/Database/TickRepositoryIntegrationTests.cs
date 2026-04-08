using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TickDataAggregator.Domain.Models;
using TickDataAggregator.Infrastructure.Database;

namespace TickDataAggregator.IntegrationTests.Database;

/// <summary>
/// Интеграционные тесты <see cref="TickRepository"/> и <see cref="PostgreSqlSchemaInitializer"/>
/// с реальной СУБД PostgreSQL, запускаемой через Testcontainers.
/// </summary>
public sealed class TickRepositoryIntegrationTests(PostgreSqlFixture db)
    : IClassFixture<PostgreSqlFixture>
{
    private readonly TickRepository _sut = new(db.DataSource, NullLogger<TickRepository>.Instance);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task TruncateAsync()
    {
        await using var cmd = db.DataSource.CreateCommand("TRUNCATE ticks");
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> CountRowsAsync()
    {
        await using var cmd = db.DataSource.CreateCommand("SELECT COUNT(*) FROM ticks");
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private static NormalizedTick MakeTick(
        string source = "binance",
        string ticker = "BTCUSDT",
        decimal price = 50_000m,
        decimal volume = 0.5m,
        string rawPayload = "{}",
        string? sourceTradeId = null)
    {
        var ts = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return new NormalizedTick(
            ticker: ticker,
            symbol: TradingSymbol.BTCUSD,
            price: price,
            volume: volume,
            timestamp: ts,
            source: source,
            receivedAt: ts,
            rawPayload: rawPayload,
            sourceTradeId: sourceTradeId);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveTicksAsync_InsertsRow_RowCountIsOne()
    {
        await TruncateAsync();
        var tick = MakeTick();

        await _sut.SaveTicksAsync([tick], CancellationToken.None);

        Assert.Equal(1, await CountRowsAsync());
    }

    [Fact]
    public async Task SaveTicksAsync_DuplicateNaturalKey_RowCountStaysOne()
    {
        await TruncateAsync();
        var tick = MakeTick();

        await _sut.SaveTicksAsync([tick], CancellationToken.None);
        await _sut.SaveTicksAsync([tick], CancellationToken.None); // identical natural key

        Assert.Equal(1, await CountRowsAsync());
    }

    [Fact]
    public async Task SaveTicksAsync_DuplicateSourceTradeId_RowCountStaysOne()
    {
        await TruncateAsync();
        var first  = MakeTick(sourceTradeId: "trade-001");
        var second = MakeTick(sourceTradeId: "trade-001", price: 51_000m); // same trade id, different price

        await _sut.SaveTicksAsync([first],  CancellationToken.None);
        await _sut.SaveTicksAsync([second], CancellationToken.None);

        Assert.Equal(1, await CountRowsAsync());
    }

    [Fact]
    public async Task SaveTicksAsync_RawPayloadIsPersisted()
    {
        await TruncateAsync();
        const string payload = """{"e":"trade","s":"BTCUSDT","p":"50000"}""";
        var tick = MakeTick(rawPayload: payload);

        await _sut.SaveTicksAsync([tick], CancellationToken.None);

        await using var cmd = db.DataSource.CreateCommand("SELECT raw_payload FROM ticks LIMIT 1");
        var stored = (string?)await cmd.ExecuteScalarAsync();

        Assert.Equal(payload, stored);
    }

    [Fact]
    public async Task EnsureSchemaAsync_CalledTwice_DoesNotThrow()
    {
        var initializer = new PostgreSqlSchemaInitializer(
            db.DataSource,
            NullLogger<PostgreSqlSchemaInitializer>.Instance);

        // First call already ran in PostgreSqlFixture.InitializeAsync; second call must be idempotent.
        var ex = await Record.ExceptionAsync(
            () => initializer.EnsureSchemaAsync(CancellationToken.None));

        Assert.Null(ex);
    }
}
