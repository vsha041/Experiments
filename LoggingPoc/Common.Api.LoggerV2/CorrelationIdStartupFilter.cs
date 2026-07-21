using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Common.Api.LoggerV2;

internal sealed class CorrelationIdStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            next(app);
        };
    }
}
