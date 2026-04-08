using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Application.Hosting;
using TickDataAggregator.Application.Processing;
using TickDataAggregator.Domain.Abstractions;
using TickDataAggregator.Infrastructure.Adapters;
using TickDataAggregator.Infrastructure.Adapters.Simulators;
using TickDataAggregator.Infrastructure.Configuration;
using TickDataAggregator.Infrastructure.Connectors;
using TickDataAggregator.Infrastructure.Database;

namespace TickDataAggregator.Infrastructure;

/// <summary>
/// Методы расширения для регистрации сервисов уровня Infrastructure в контейнере DI.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует параметры БД, репозиторий, адаптеры и инициализатор схемы.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database options with validation
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Database ConnectionString must not be empty")
            .ValidateOnStart();

        var connString = configuration[$"{DatabaseOptions.SectionName}:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connString))
        {
            throw new InvalidOperationException("Database ConnectionString is missing in configuration.");
        }

        services.AddNpgsqlDataSource(connString);

        // Репозиторий и инициализация схемы
        services.AddSingleton<ITickRepository, TickRepository>();
        services.AddSingleton<ISchemaInitializer, PostgreSqlSchemaInitializer>();

        // Адаптеры симуляторов (всегда регистрируются — используются с ExchangeSimulator)
        services.AddSingleton<ITickAdapter, ExchangeAAdapter>();
        services.AddSingleton<ITickAdapter, ExchangeBAdapter>();
        services.AddSingleton<ITickAdapter, ExchangeCAdapter>();

        // Адаптеры продукционных бирж (регистрируется заранее, чтобы быть доступны при обращении по имени эндпоинта)
        services.AddSingleton<ITickAdapter, BinanceAdapter>();
        services.AddSingleton<ITickAdapter, CoinbaseAdapter>();

        return services;
    }

    /// <summary>
    /// Создаёт по одному <see cref="ExchangeIngestionService"/> для каждой включённой конечной точки
    /// и выбирает подходящий коннектор на основе <see cref="ConnectorKind"/>.
    /// </summary>
    public static void AddExchangeIngestionServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ExchangeConnectionOptions>()
            .Bind(configuration.GetSection(ExchangeConnectionOptions.SectionName))
            .Validate(o => o.Endpoints.Count > 0, "At least one exchange endpoint must be configured")
            .Validate(
                o => o.Endpoints.TrueForAll(e => Uri.TryCreate(e.Url, UriKind.Absolute, out _)),
                "All endpoint URLs must be valid absolute URIs")
            .ValidateOnStart();

        var exchangeOptions = configuration
                                  .GetRequiredSection(ExchangeConnectionOptions.SectionName)
                                  .Get<ExchangeConnectionOptions>() ??
                              throw new InvalidOperationException(
                                  $"{ExchangeConnectionOptions.SectionName} section is missing or invalid.");

        foreach (var endpoint in exchangeOptions.Endpoints.Where(e => e.Enabled))
        {
            if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"Invalid URL '{endpoint.Url}' for exchange endpoint '{endpoint.Name}'");
            }

            services.AddSingleton<IHostedService>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var connectorLogger = loggerFactory.CreateLogger($"Connector.{endpoint.Name}");

                BaseWebSocketConnector connector = endpoint.Kind switch
                {
                    ConnectorKind.Coinbase => new CoinbaseConnector(
                        endpoint.Name, uri, connectorLogger, CoinbaseConnector.DefaultProductIds),
                    ConnectorKind.Binance => new BinanceConnector(endpoint.Name, uri, connectorLogger),
                    _ => new BaseWebSocketConnector(endpoint.Name, uri, connectorLogger)
                };

                return new ExchangeIngestionService(
                    connector,
                    sp.GetRequiredService<TickChannel>(),
                    loggerFactory.CreateLogger<ExchangeIngestionService>(),
                    endpoint.ReconnectBaseDelayMs,
                    endpoint.ReconnectMaxDelayMs);
            });
        }
    }
}