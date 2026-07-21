using Microsoft.AspNetCore.Http;

namespace Common.Api.LoggerV2;

/// <summary>Provides access to the current request's correlation ID.</summary>
public static class HttpContextExtensions
{
    /// <summary>Gets the correlation ID created for the current request.</summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(CorrelationId.HttpContextItemKey, out var value)
            ? value as string
            : null;
    }
}
