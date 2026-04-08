using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5100");
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
app.UseWebSockets();

var tickers = new[] { "BTCUSD", "ETHUSD", "XAUUSD", "EURUSD", "GBPUSD", "USDJPY" };
var random = new Random();

// Exchange A — JSON format
app.Map("/exchange-a", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("ExchangeA client connected");

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var ticker = tickers[random.Next(tickers.Length)];
            var price = Math.Round(GetBasePrice(ticker) + (random.NextDouble() - 0.5) * 10, 2);
            var volume = Math.Round(random.NextDouble() * 100, 4);
            var now = DateTimeOffset.UtcNow;

            var json = JsonSerializer.Serialize(new
            {
                ticker,
                price,
                volume,
                ts = now
            });

            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            await Task.Delay(random.Next(20, 80)); // ~15-50 ticks/sec
        }
    }
    catch (WebSocketException)
    {
        logger.LogInformation("ExchangeA client disconnected");
    }
});

// Exchange B — CSV format: ticker,price,volume,timestamp_ms
app.Map("/exchange-b", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("ExchangeB client connected");

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var ticker = tickers[random.Next(tickers.Length)];
            var price = Math.Round(GetBasePrice(ticker) + (random.NextDouble() - 0.5) * 10, 2);
            var volume = Math.Round(random.NextDouble() * 100, 4);
            var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var csv = string.Create(CultureInfo.InvariantCulture, $"{ticker},{price},{volume},{tsMs}");
            var bytes = Encoding.UTF8.GetBytes(csv);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            await Task.Delay(random.Next(25, 100)); // ~10-40 ticks/sec
        }
    }
    catch (WebSocketException)
    {
        logger.LogInformation("ExchangeB client disconnected");
    }
});

// Exchange C — Pipe-delimited: ticker|price|volume|datetime
app.Map("/exchange-c", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("ExchangeC client connected");

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var ticker = tickers[random.Next(tickers.Length)];
            var price = Math.Round(GetBasePrice(ticker) + (random.NextDouble() - 0.5) * 10, 2);
            var volume = Math.Round(random.NextDouble() * 100, 4);
            // ISO 8601 with explicit Z suffix — unambiguously UTC, no machine-timezone dependency
            var dt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            var pipe = string.Create(CultureInfo.InvariantCulture, $"{ticker}|{price}|{volume}|{dt}");
            var bytes = Encoding.UTF8.GetBytes(pipe);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            await Task.Delay(random.Next(30, 120)); // ~8-33 ticks/sec
        }
    }
    catch (WebSocketException)
    {
        logger.LogInformation("ExchangeC client disconnected");
    }
});

app.MapGet("/health", () => "OK");

app.Run();

static double GetBasePrice(string ticker) => ticker switch
{
    "BTCUSD" => 65000,
    "ETHUSD" => 3500,
    "XAUUSD" => 2350,
    "EURUSD" => 1.08,
    "GBPUSD" => 1.27,
    "USDJPY" => 154,
    _ => 100
};

// Make Program accessible for integration tests
public partial class Program;
