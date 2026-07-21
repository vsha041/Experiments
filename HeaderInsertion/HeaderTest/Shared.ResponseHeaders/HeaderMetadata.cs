namespace Shared.ResponseHeaders;

public class HeaderMetadata
{
    [AddApiResponseHeader("bank-correlation-id")]
    public Guid CorrelationId { get; set; }

    [AddApiResponseHeader("bank-date-time")]
    public DateTime DateTime { get; set; }
}
