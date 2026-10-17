using Brain.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Brain.Infrastructure;

public static class ActivityRegistration
{
    public static IServiceCollection AddBrainActivity(this IServiceCollection services, IConfiguration configuration)
    {
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        var options = configuration.GetSection("ActivityTracing").Get<ActivityTracingOptions>() ?? new();
        if (options.RetentionDays is < 0 or > 365 || options.CleanupIntervalSeconds < 1 ||
            options.QueueCapacity is < 16 or > 65536 || options.SubscriberCapacity is < 1 or > 4096)
            throw new InvalidOperationException("Invalid ActivityTracing retention, interval or buffer limits.");
        Uri? endpoint = null;
        if (options.OtlpEndpoint is not null &&
            (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out endpoint) || endpoint.Scheme is not ("http" or "https") ||
             endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0))
            throw new InvalidOperationException("Configure an HTTP(S) OTLP endpoint without credentials, query or fragment.");
        services.AddSingleton(options);
        services.AddSingleton<ActivitySanitizer>();
        services.AddSingleton<ActivityEventQueue>();
        services.AddSingleton<IActivityPublisher, BrainActivityPublisher>();
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.GetConnectionString("Brain")
            ?? throw new InvalidOperationException("ConnectionStrings:Brain must be configured.")));
        services.AddSingleton<ActivityLiveBus>();
        services.AddSingleton<IActivityStream>(sp => sp.GetRequiredService<ActivityLiveBus>());
        services.AddScoped<IActivityStore, ActivityStore>();
        services.AddScoped<ActivityQueryService>();
        services.AddHostedService<OutboundActivityObserver>();
        services.AddHostedService<ActivityDispatchService>();
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService("homelab-brain"))
            .WithTracing(tracing =>
            {
                tracing.SetSampler(new AlwaysOnSampler())
                    .AddSource(BrainActivityPublisher.SourceName)
                    .AddAspNetCoreInstrumentation(o => o.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health") &&
                        !context.Request.Path.StartsWithSegments("/api/traces") &&
                        !context.Request.Path.StartsWithSegments("/api/activity"))
                    .AddHttpClientInstrumentation()
                    .AddProcessor<SanitizingActivityProcessor>();
                if (endpoint is not null) tracing.AddOtlpExporter(o => o.Endpoint = endpoint);
            });
        return services;
    }
}
