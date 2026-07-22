namespace Shared.ResponseHeaders;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AddApiResponseHeaderAttribute : Attribute
{
    public AddApiResponseHeaderAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
}
