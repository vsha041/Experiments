namespace Customer
{
    public class HeaderMetadata
    {
        public Guid CorrelationId { get; set; }
        public DateTime DateTime { get; set; }
    }

    public sealed class Customer : HeaderMetadata
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}
