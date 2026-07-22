namespace Shared.ResponseHeaders;

internal sealed class DatabaseResponseMetadataContext : IDatabaseResponseMetadataContext
{
    public HeaderMetadata? Metadata { get; private set; }

    public void Capture(HeaderMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }
}
