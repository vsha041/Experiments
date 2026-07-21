using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Shared.ResponseHeaders;

internal sealed class DatabaseResponseMetadataMiddleware(
    RequestDelegate next,
    IOptions<DatabaseResponseMetadataOptions> options)
{
    private readonly DatabaseResponseMetadataOptions _options = options.Value;

    public Task InvokeAsync(
        HttpContext httpContext,
        IDatabaseResponseMetadataContext metadataContext)
    {
        httpContext.Response.OnStarting(() =>
        {
            if (metadataContext.Metadata is { } metadata)
            {
                httpContext.Response.Headers[_options.CorrelationIdHeaderName] =
                    metadata.CorrelationId.ToString("D");
                httpContext.Response.Headers[_options.DateTimeHeaderName] =
                    metadata.DateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            return Task.CompletedTask;
        });

        return next(httpContext);
    }
}
