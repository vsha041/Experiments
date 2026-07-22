using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Shared.ResponseHeaders;

internal sealed class DatabaseResponseMetadataMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<Type, HeaderProperty[]> HeaderProperties = new();

    public Task InvokeAsync(
        HttpContext httpContext)
    {
        httpContext.Response.OnStarting(() =>
        {
            if (httpContext.Items.TryGetValue("ApiResponseHeaders", out var metadata) && metadata != null)
            {
                var properties = HeaderProperties.GetOrAdd(
                    metadata.GetType(),
                    FindHeaderProperties);

                foreach (var property in properties)
                {
                    var value = property.Property.GetValue(metadata);
                    if (value is not null)
                    {
                        httpContext.Response.Headers[property.HeaderName] =
                            FormatHeaderValue(value);
                    }
                }
            }

            return Task.CompletedTask;
        });

        return next(httpContext);
    }

    private static HeaderProperty[] FindHeaderProperties(Type metadataType) =>
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

    private static string FormatHeaderValue(object value) => value switch
    {
        Guid guid => guid.ToString("D"),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset =>
            dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable =>
            formattable.ToString(format: null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private sealed record HeaderProperty(string HeaderName, PropertyInfo Property);
}
