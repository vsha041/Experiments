using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Shared.ResponseHeaders;

internal sealed class DatabaseResponseMetadataMiddleware(RequestDelegate next)
{
    private const int MaxCollectionItems = 10;

    private static readonly ConcurrentDictionary<Type, HeaderProperty[]> HeaderProperties = new();

    public Task InvokeAsync(
        HttpContext httpContext)
    {
        httpContext.Response.OnStarting(() =>
        {
            if (httpContext.Items.TryGetValue("ApiResponseHeaders", out var metadata) && metadata != null)
            {
                if (TryGetItems(metadata, out var items, out var elementType))
                {
                    ApplyCollectionHeaders(httpContext, items, elementType);
                }
                else
                {
                    ApplySingleHeaders(httpContext, metadata);
                }
            }

            return Task.CompletedTask;
        });

        return next(httpContext);
    }

    private static void ApplySingleHeaders(HttpContext httpContext, object metadata)
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

    private static void ApplyCollectionHeaders(
        HttpContext httpContext,
        IEnumerable items,
        Type elementType)
    {
        var properties = HeaderProperties.GetOrAdd(elementType, FindHeaderProperties);
        if (properties.Length == 0)
        {
            return;
        }

        var valuesByHeader = properties.ToDictionary(p => p.HeaderName, _ => new List<string>());
        var processed = 0;

        foreach (var item in items)
        {
            if (processed >= MaxCollectionItems)
            {
                break;
            }

            if (item is null)
            {
                continue;
            }

            foreach (var property in properties)
            {
                var value = property.Property.GetValue(item);
                if (value is not null)
                {
                    valuesByHeader[property.HeaderName].Add(FormatHeaderValue(value));
                }
            }

            processed++;
        }

        foreach (var property in properties)
        {
            var values = valuesByHeader[property.HeaderName];
            if (values.Count > 0)
            {
                httpContext.Response.Headers[property.HeaderName] = string.Join("|", values);
            }
        }
    }

    private static bool TryGetItems(object metadata, out IEnumerable items, out Type elementType)
    {
        if (metadata is not IEnumerable enumerable || metadata is string)
        {
            items = default!;
            elementType = default!;
            return false;
        }

        var collectionType = metadata.GetType();
        var enumerableInterface = collectionType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface is null)
        {
            items = default!;
            elementType = default!;
            return false;
        }

        items = enumerable;
        elementType = enumerableInterface.GetGenericArguments()[0];
        return true;
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
