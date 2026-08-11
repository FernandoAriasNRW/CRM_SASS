using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
  public static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
  {
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:lj}{NewLine}{Exception}")
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
        {
          AutoRegisterTemplate = true,
          AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7
        })
        .CreateLogger();

    builder.Host.UseSerilog();

    return builder;
  }

  public static IServiceCollection AddObservability(this IServiceCollection services, string serviceName)
  {
    services.AddHealthChecks();

    services.AddTracing(serviceName);
    services.AddMetrics(serviceName);

    return services;
  }

  private static IServiceCollection AddTracing(this IServiceCollection services, string serviceName)
  {
    return services;
  }

  private static IServiceCollection AddMetrics(this IServiceCollection services, string serviceName)
  {
    return services;
  }
}