namespace Common.Api.Logger
{
    public sealed class CorrelationLoggingOptions
    {
        public const string DefaultHeaderName = "X-Correlation-ID";
        public const string DefaultLogPropertyName = "CorrelationId";

        /// <summary>
        /// Request and response header containing the correlation ID.
        /// </summary>
        public string HeaderName { get; set; } = DefaultHeaderName;

        /// <summary>
        /// Structured property added to the logging scope.
        /// </summary>
        public string LogPropertyName { get; set; } =
            DefaultLogPropertyName;

        /// <summary>
        /// Maximum accepted length of a caller-provided correlation ID.
        /// </summary>
        public int MaximumLength { get; set; } = 128;

        /// <summary>
        /// Return the correlation ID in the response headers.
        /// </summary>
        public bool AddToResponse { get; set; } = true;
    }
}
