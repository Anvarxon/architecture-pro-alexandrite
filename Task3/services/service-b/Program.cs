using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string ActivitySourceName = "Alexandrite.ServiceB";

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "service-b";

builder.Services.AddSingleton(new ActivitySource(ActivitySourceName));

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

app.MapGet("/", (string? orderId, ActivitySource activitySource) =>
{
    var resolvedOrderId = string.IsNullOrWhiteSpace(orderId)
        ? $"ord-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}"
        : orderId;

    using var activity = activitySource.StartActivity("calculate-manufacturing-price", ActivityKind.Internal);
    activity?.SetTag("order.id", resolvedOrderId);
    activity?.SetTag("alexandrite.operation", "calculate-manufacturing-price");
    activity?.SetTag("model.complexity_bucket", "medium");

    return Results.Ok(new ManufacturingQuote(
        serviceName,
        resolvedOrderId,
        "PRICE_CALCULATED",
        EstimatedPrice: 12500m,
        TraceId: Activity.Current?.TraceId.ToString()));
});

app.Run();

internal sealed record ManufacturingQuote(
    string Service,
    string OrderId,
    string Status,
    decimal EstimatedPrice,
    string? TraceId);
