namespace Student
{
    public class HeaderMetadata
    {
        public Guid CorrelationId { get; set; }
        public DateTime DateTime { get; set; }
    }

    public sealed class Student : HeaderMetadata
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }
}