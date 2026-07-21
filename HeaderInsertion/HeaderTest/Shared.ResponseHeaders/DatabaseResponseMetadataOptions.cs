namespace Shared.ResponseHeaders;

public sealed class DatabaseResponseMetadataOptions
{
    public string CorrelationIdHeaderName { get; set; } = "CorrelationId";
    public string DateTimeHeaderName { get; set; } = "DateTime";
}
