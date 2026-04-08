using Serilog;
using TickDataAggregator.Application;
using TickDataAggregator.Infrastructure;
using TickDataAggregator.Infrastructure.Database;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var bootstrapLogger = Log.ForContext("SourceContext", "Startup");
try
{
    bootstrapLogger.Information("Starting TickDataAggregator Host");

    var builder = Host.CreateApplicationBuilder(args);
    
    builder.Services.AddSerilog((services, loggerConfiguration) => 
        loggerConfiguration.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    builder.Services
        .AddApplicationServices(builder.Configuration)
        .AddInfrastructureServices(builder.Configuration)
        .AddExchangeIngestionServices(builder.Configuration);

    var host = builder.Build();
    var startupLogger = host.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    
    using (var scope = host.Services.CreateScope())
    {
        var schemaInitializer = scope.ServiceProvider.GetRequiredService<ISchemaInitializer>();
        await schemaInitializer.EnsureSchemaAsync(CancellationToken.None);
    }
    startupLogger.LogInformation("Database schema initialized");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.ForContext("SourceContext", "Startup").Fatal(ex, "Host startup failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
