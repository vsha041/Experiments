using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Api.Logger;

internal sealed class CorrelationLoggingMiddleware
{
    private const string HttpContextItemName =
        "Asb.Common.CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationLoggingMiddleware> _logger;
    private readonly IMutableCorrelationContextAccessor _accessor;
    private readonly CorrelationLoggingOptions _options;

    public CorrelationLoggingMiddleware(
        RequestDelegate next,
        ILogger<CorrelationLoggingMiddleware> logger,
        IMutableCorrelationContextAccessor accessor,
        IOptions<CorrelationLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _accessor = accessor;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var correlationId = GetOrCreateCorrelationId(httpContext);

        // Make ASP.NET Core's request identifier consistent with ours.
        httpContext.TraceIdentifier = correlationId;

        // Make the value available to other request components.
        httpContext.Items[HttpContextItemName] = correlationId;
        _accessor.CorrelationId = correlationId;

        if (_options.AddToResponse)
        {
            httpContext.Response.OnStarting(
                static state =>
                {
                    var responseState = (ResponseHeaderState)state;

                    responseState.HttpContext.Response.Headers[
                        responseState.HeaderName
                    ] = responseState.CorrelationId;

                    return Task.CompletedTask;
                },
                new ResponseHeaderState(
                    httpContext,
                    _options.HeaderName,
                    correlationId));
        }

        // Every ILogger call made inside this scope receives this property.
        using var loggingScope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                [_options.LogPropertyName] = correlationId
            });

        try
        {
            await _next(httpContext);
        }
        finally
        {
            // Prevent the request value leaking into another operation.
            _accessor.CorrelationId = null;
        }
    }

    private string GetOrCreateCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(
                _options.HeaderName,
                out var headerValues) &&
            headerValues.Count == 1)
        {
            var suppliedValue = headerValues[0]?.Trim();

            if (IsValid(suppliedValue))
            {
                return suppliedValue!;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > _options.MaximumLength)
        {
            return false;
        }

        // Restrict untrusted header content to safe characters.
        // This reduces the risk of log-forging or control characters.
        return value.All(
            character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_' or '.');
    }

    private sealed record ResponseHeaderState(
        HttpContext HttpContext,
        string HeaderName,
        string CorrelationId);
}