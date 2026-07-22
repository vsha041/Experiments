using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ResponseHeaders;

public static class DatabaseResponseMetadataExtensions
{
    public static IServiceCollection AddDatabaseResponseMetadata(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDatabaseResponseMetadataContext, DatabaseResponseMetadataContext>();
        return services;
    }

    public static IApplicationBuilder UseDatabaseResponseMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<DatabaseResponseMetadataMiddleware>();
    }
}
