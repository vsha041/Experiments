using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace ReflectionTest;

public class ReflectionHelper
{
    public static readonly ConcurrentDictionary<Type, HeaderProperty[]> HeaderProperties = new();

    public static HeaderProperty[] FindHeaderProperties(Type metadataType) =>
        metadataType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<AddApiResponseHeaderAttribute>(
                    inherit: true)
            })
            .Where(item => item.Attribute is not null && item.Property.CanRead)
            .Select(item => new HeaderProperty(item.Attribute!.Name, item.Property))
            .ToArray();

    public static string FormatHeaderValue(object value) => value switch
    {
        Guid guid => guid.ToString("D"),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset =>
            dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable =>
            formattable.ToString(format: null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    public sealed record HeaderProperty(string HeaderName, PropertyInfo Property);
}

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