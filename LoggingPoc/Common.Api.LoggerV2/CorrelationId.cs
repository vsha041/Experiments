namespace Common.Api.LoggerV2;

/// <summary>
/// Names used by the correlation ID middleware.
/// </summary>
public static class CorrelationId
{
    /// <summary>The structured logging property name.</summary>
    public const string LogPropertyName = "CorrelationId";

    /// <summary>The response header containing the request's correlation ID.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>The response header containing the response's UTC timestamp.</summary>
    public const string TimestampHeaderName = "X-Response-Timestamp";

    /// <summary>The key used to store the ID in <c>HttpContext.Items</c>.</summary>
    public const string HttpContextItemKey = "RequestCorrelation.Logging.CorrelationId";
}
