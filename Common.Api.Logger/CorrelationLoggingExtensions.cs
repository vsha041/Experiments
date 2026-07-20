using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Api.Logger;

public static class CorrelationLoggingExtensions
{
    public static IServiceCollection AddAsbCorrelationLogging(
        this IServiceCollection services,
        Action<CorrelationLoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder =
            services.AddOptions<CorrelationLoggingOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(
                options => !string.IsNullOrWhiteSpace(
                    options.HeaderName),
                "The correlation ID header name is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(
                    options.LogPropertyName),
                "The correlation log property name is required.")
            .Validate(
                options => options.MaximumLength is >= 16 and <= 512,
                "MaximumLength must be between 16 and 512.")
            .ValidateOnStart();

        services.TryAddSingleton<CorrelationContextAccessor>();

        services.TryAddSingleton<ICorrelationContextAccessor>(
            provider =>
                provider.GetRequiredService<
                    CorrelationContextAccessor>());

        services.TryAddSingleton<IMutableCorrelationContextAccessor>(
            provider =>
                provider.GetRequiredService<
                    CorrelationContextAccessor>());

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IStartupFilter,
                CorrelationLoggingStartupFilter>());

        return services;
    }
}