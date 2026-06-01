using System.Diagnostics;
using System.Net.Http.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string ActivitySourceName = "Alexandrite.ServiceA";

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "service-a";
var serviceBUrl = builder.Configuration["SERVICE_B_URL"] ?? "http://service-b:8080";

builder.Services.AddSingleton(new ActivitySource(ActivitySourceName));
builder.Services.AddHttpClient("service-b", client =>
{
    client.BaseAddress = new Uri(serviceBUrl);
});

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(ActivitySourceName)
            .AddOtlpExporter();
    });

var app = builder.Build();

app.MapGet("/", async (IHttpClientFactory httpClientFactory, ActivitySource activitySource) =>
{
    var orderId = $"ord-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

    using var activity = activitySource.StartActivity("order-price-request", ActivityKind.Internal);
    activity?.SetTag("order.id", orderId);
    activity?.SetTag("order.source", "trace-mvp");
    activity?.SetTag("alexandrite.operation", "request-price-calculation");

    var client = httpClientFactory.CreateClient("service-b");
    var quote = await client.GetFromJsonAsync<ManufacturingQuote>($"/?orderId={orderId}");

    return Results.Ok(new
    {
        service = serviceName,
        orderId,
        traceId = Activity.Current?.TraceId.ToString(),
        downstream = quote
    });
});

app.Run();

internal sealed record ManufacturingQuote(
    string Service,
    string OrderId,
    string Status,
    decimal EstimatedPrice,
    string? TraceId);
