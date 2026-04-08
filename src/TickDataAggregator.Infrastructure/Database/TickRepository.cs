using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Domain.Models;

namespace TickDataAggregator.Infrastructure.Database;

/// <summary>
/// Реализация <see cref="ITickRepository"/> для PostgreSQL.
/// Использует <see cref="Npgsql.NpgsqlBatch"/> для пакетной вставки с
/// <c>ON CONFLICT DO NOTHING</c>, покрывающей оба уникальных индекса.
/// </summary>
public sealed class TickRepository(NpgsqlDataSource dataSource, ILogger<TickRepository> logger)
    : ITickRepository
{
    private const string InsertSql = """
                                     INSERT INTO ticks (ticker, price, volume, occurred_at, source, received_at, raw_payload, source_trade_id)
                                     VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                                     ON CONFLICT DO NOTHING
                                     """;
    
    public async Task SaveTicksAsync(IReadOnlyList<NormalizedTick> ticks, CancellationToken cancellationToken)
    {
        if (ticks.Count == 0)
            return;

        await using var batch = dataSource.CreateBatch();

        foreach (var tick in ticks)
        {
            var cmd = batch.CreateBatchCommand();
            cmd.CommandText = InsertSql;

            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tick.Symbol.ToString() });
            cmd.Parameters.Add(new NpgsqlParameter<decimal> { TypedValue = tick.Price });
            cmd.Parameters.Add(new NpgsqlParameter<decimal> { TypedValue = tick.Volume });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset>
                { TypedValue = tick.Timestamp.ToUniversalTime(), NpgsqlDbType = NpgsqlDbType.TimestampTz });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tick.Source });
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset>
                { TypedValue = tick.ReceivedAt.ToUniversalTime(), NpgsqlDbType = NpgsqlDbType.TimestampTz });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = tick.RawPayload });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)tick.SourceTradeId ?? DBNull.Value });

            batch.BatchCommands.Add(cmd);
        }

        await batch.ExecuteNonQueryAsync(cancellationToken);

        logger.LogDebug("Inserted batch of {Count} ticks", ticks.Count);
    }
}