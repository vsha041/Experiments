namespace Common.Api.Logger;

public interface ICorrelationContextAccessor
{
    string? CorrelationId { get; }
}

internal interface IMutableCorrelationContextAccessor
    : ICorrelationContextAccessor
{
    new string? CorrelationId { get; set; }
}

internal sealed class CorrelationContextAccessor
    : IMutableCorrelationContextAccessor
{
    private static readonly AsyncLocal<CorrelationContextHolder?>
        CurrentContext = new();

    public string? CorrelationId
    {
        get => CurrentContext.Value?.CorrelationId;

        set
        {
            var existingHolder = CurrentContext.Value;

            if (existingHolder is not null)
            {
                existingHolder.CorrelationId = null;
            }

            if (value is not null)
            {
                CurrentContext.Value = new CorrelationContextHolder
                {
                    CorrelationId = value
                };
            }
        }
    }

    private sealed class CorrelationContextHolder
    {
        public string? CorrelationId;
    }
}