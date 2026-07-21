using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Common.Api.LoggerV2;

/// <summary>
/// Creates one correlation ID for a request and adds it to the request's logging scope.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Runs the request inside a correlation ID logging scope.</summary>
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var incomingCorrelationId =
            context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();

        var correlationId = string.IsNullOrWhiteSpace(incomingCorrelationId)
            ? Guid.NewGuid().ToString("N")
            : incomingCorrelationId;

        context.Items[CorrelationId.HttpContextItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            context.Response.Headers[CorrelationId.TimestampHeaderName] =
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   [CorrelationId.LogPropertyName] = correlationId
               }))
        {
            await _next(context);
        }
    }
}

