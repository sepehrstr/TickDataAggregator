using Microsoft.Extensions.Logging;
using Npgsql;

namespace TickDataAggregator.Infrastructure.Database;

/// <summary>
/// Реализация <see cref="ISchemaInitializer"/> для PostgreSQL.
/// Создаёт таблицу <c>ticks</c>, уникальные индексы дедупликации
/// и вспомогательные индексы если они не существуют.
/// </summary>
public sealed class PostgreSqlSchemaInitializer(NpgsqlDataSource dataSource, ILogger<PostgreSqlSchemaInitializer> logger)
    : ISchemaInitializer
{
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS ticks (
                               id BIGSERIAL PRIMARY KEY,
                               ticker VARCHAR(20) NOT NULL,
                               price NUMERIC(18,8) NOT NULL,
                               volume NUMERIC(18,8) NOT NULL,
                               occurred_at TIMESTAMPTZ NOT NULL,
                               source VARCHAR(50) NOT NULL,
                               received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                               raw_payload TEXT NOT NULL DEFAULT '',
                               source_trade_id VARCHAR(100) NULL
                           );

                           -- Основной ключ дедупликации: используется когда биржа передаёт стабильный идентификатор сделки.
                           CREATE UNIQUE INDEX IF NOT EXISTS uq_ticks_source_trade_id
                               ON ticks (source, source_trade_id)
                               WHERE source_trade_id IS NOT NULL;

                           -- Резервный ключ дедупликации: используется для источников без идентификаторов сделок.
                           CREATE UNIQUE INDEX IF NOT EXISTS uq_ticks_natural_key
                               ON ticks (source, ticker, price, volume, occurred_at);

                           CREATE INDEX IF NOT EXISTS ix_ticks_ticker_occurred_at
                               ON ticks (ticker, occurred_at);

                           CREATE INDEX IF NOT EXISTS ix_ticks_source
                               ON ticks (source);
                           """;

        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Database schema ensured");
    }
}