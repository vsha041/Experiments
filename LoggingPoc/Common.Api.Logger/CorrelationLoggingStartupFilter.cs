using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Common.Api.Logger;

internal sealed class CorrelationLoggingStartupFilter
    : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(
        Action<IApplicationBuilder> next)
    {
        return application =>
        {
            application.UseMiddleware<CorrelationLoggingMiddleware>();

            next(application);
        };
    }
}