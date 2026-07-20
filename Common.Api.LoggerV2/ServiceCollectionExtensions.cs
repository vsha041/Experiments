using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Api.LoggerV2;

/// <summary>Registers automatic request correlation logging.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Automatically adds a unique correlation ID logging scope to every HTTP request.
    /// </summary>
    public static IServiceCollection AddRequestCorrelationLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IStartupFilter, CorrelationIdStartupFilter>());

        return services;
    }
}
