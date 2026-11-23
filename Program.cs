using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Silence verbose ASP.NET Core framework diagnostic logs
builder.Logging.ClearProviders();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "tiny-cs-api";

// Configure OpenTelemetry tracing & W3C Trace Context propagation
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(options =>
        {
            // Filter out /metrics endpoint from tracing noise
            options.Filter = httpContext =>
            {
                var path = httpContext.Request.Path.Value;
                return path != "/metrics";
            };
        });

        var tracesExporter = Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER");
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if (tracesExporter == "none")
        {
            // Telemetry export explicitly disabled
        }
        else if (!string.IsNullOrEmpty(otlpEndpoint) || tracesExporter == "otlp")
        {
            tracing.AddOtlpExporter(opt =>
            {
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    opt.Endpoint = new Uri(otlpEndpoint);
                }
            });
        }
        else
        {
            // Default: stdout / console exporter for local development
            tracing.AddConsoleExporter();
        }
    });

var app = builder.Build();

var requestCounter = Metrics.CreateCounter(
    "http_requests_total",
    "Total number of HTTP requests processed");

var requestDuration = Metrics.CreateHistogram(
    "http_request_duration_seconds",
    "HTTP request duration in seconds");

// Structured request logging middleware with trace correlation
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    requestCounter.Inc();

    using (requestDuration.NewTimer())
    {
        await next();
    }

    sw.Stop();

    var path = context.Request.Path.Value ?? "";
    // Suppress logs for high-frequency Prometheus metrics scraping
    if (path == "/metrics")
    {
        return;
    }

    var traceId = Activity.Current?.TraceId.ToString();
    var spanId = Activity.Current?.SpanId.ToString();

    var logEntry = new Dictionary<string, object?>
    {
        ["timestamp"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        ["level"] = "INFO",
        ["service"] = serviceName,
        ["event"] = "request.completed",
        ["method"] = context.Request.Method,
        ["route"] = path,
        ["status"] = context.Response.StatusCode,
        ["duration_ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 3),
    };

    if (!string.IsNullOrEmpty(traceId) && traceId != "00000000000000000000000000000000")
    {
        logEntry["trace_id"] = traceId;
        logEntry["span_id"] = spanId;
    }

    Console.WriteLine(JsonSerializer.Serialize(logEntry));
});

app.MapGet("/", () => Results.Text(JsonSerializer.Serialize(new
{
    service = serviceName,
    message = "service is running"
}), "application/json"));

app.MapGet("/readyz", () => Results.Text(JsonSerializer.Serialize(new
{
    status = "ready",
    service = serviceName,
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
}), "application/json"));

app.MapGet("/livez", () => Results.Text(JsonSerializer.Serialize(new
{
    status = "live",
    service = serviceName,
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
}), "application/json"));

app.MapGet("/health/startup", () => Results.Text(JsonSerializer.Serialize(new
{
    status = "started",
    service = serviceName,
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
}), "application/json"));

app.MapGet("/health/ready", () => Results.Text(JsonSerializer.Serialize(new
{
    status = "ready",
    service = serviceName,
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
}), "application/json"));

app.MapGet("/health/live", () => Results.Text(JsonSerializer.Serialize(new
{
    status = "live",
    service = serviceName,
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
}), "application/json"));

app.MapMetrics("/metrics");

Console.WriteLine(JsonSerializer.Serialize(new
{
    timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
    level = "INFO",
    service = serviceName,
    message = "Starting " + serviceName + " on port 8080..."
}));

app.Run();

public partial class Program { }
