using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TickDataAggregator.Application.Configuration;
using TickDataAggregator.Application.Hosting;
using TickDataAggregator.Application.Processing;
using TickDataAggregator.Application.Processing.Deduplication;
using TickDataAggregator.Domain.Abstractions;

namespace TickDataAggregator.Application;

/// <summary>
/// Методы расширения для регистрации сервисов уровня Application в контейнере DI.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует канал, дедупликатор, конвейер обработки тиков и сервис метрик.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Options with validation
        services
            .AddOptions<PipelineOptions>()
            .Bind(configuration.GetSection(PipelineOptions.SectionName))
            .Validate(o => o.ChannelCapacity > 0, "ChannelCapacity must be > 0")
            .Validate(o => o.BatchSize > 0, "BatchSize must be > 0")
            .Validate(o => o.BatchFlushIntervalMs > 0, "BatchFlushIntervalMs must be > 0")
            .Validate(o => o.DeduplicationWindowSeconds > 0, "DeduplicationWindowSeconds must be > 0")
            .ValidateOnStart();

        // Channel
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PipelineOptions>>().Value;
            return new TickChannel(options.ChannelCapacity);
        });

        // Deduplicator
        services.AddSingleton<ITickDeduplicator, SlidingWindowDeduplicator>();

        // Pipeline
        services.AddSingleton<TickProcessingPipeline>();
        services.AddHostedService(sp => sp.GetRequiredService<TickProcessingPipeline>());

        // Metrics
        services.AddHostedService<MetricsReportingService>();

        return services;
    }
}
