using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ResponseHeaders;

public static class DatabaseResponseMetadataExtensions
{
    public static IServiceCollection AddDatabaseResponseMetadata(
        this IServiceCollection services,
        Action<DatabaseResponseMetadataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is null)
        {
            services.AddOptions<DatabaseResponseMetadataOptions>();
        }
        else
        {
            services.Configure(configure);
        }

        services.AddScoped<IDatabaseResponseMetadataContext, DatabaseResponseMetadataContext>();
        return services;
    }

    public static IApplicationBuilder UseDatabaseResponseMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<DatabaseResponseMetadataMiddleware>();
    }
}
