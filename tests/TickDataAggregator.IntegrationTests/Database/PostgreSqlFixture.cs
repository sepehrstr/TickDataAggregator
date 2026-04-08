using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using TickDataAggregator.Infrastructure.Database;

namespace TickDataAggregator.IntegrationTests.Database;

/// <summary>
/// Разделяемая фикстура xUnit: поднимает контейнер PostgreSQL один раз для всего класса тестов,
/// инициализирует схему и предоставляет <see cref="NpgsqlDataSource"/> для тестов.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());

        var initializer = new PostgreSqlSchemaInitializer(
            DataSource,
            NullLogger<PostgreSqlSchemaInitializer>.Instance);

        await initializer.EnsureSchemaAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}
