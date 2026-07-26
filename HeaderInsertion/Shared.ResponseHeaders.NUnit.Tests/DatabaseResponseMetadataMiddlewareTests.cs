using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using Shared.ResponseHeaders;

namespace Shared.ResponseHeaders.NUnit.Tests;

[TestFixture]
public class DatabaseResponseMetadataMiddlewareTests
{
    private sealed class TestResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = new();

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state) { }

        public async Task FireOnStartingAsync()
        {
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }

            HasStarted = true;
        }
    }

    private static (DefaultHttpContext Context, TestResponseFeature ResponseFeature)
        CreateContext()
    {
        var context = new DefaultHttpContext();
        var responseFeature = new TestResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        return (context, responseFeature);
    }

    private static async Task RunMiddlewareAsync(
        HttpContext context,
        TestResponseFeature responseFeature,
        RequestDelegate? next = null)
    {
        next ??= _ => Task.CompletedTask;
        var middleware = new DatabaseResponseMetadataMiddleware(next);
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();
    }

    [Test]
    public async Task Invoke_WithoutMetadata_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(context.Response.Headers, Is.Empty);
    }

    [Test]
    public async Task Invoke_WithNullMetadata_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = null;

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(context.Response.Headers, Is.Empty);
    }

    [Test]
    public async Task Invoke_CallsNextDelegate()
    {
        var (context, responseFeature) = CreateContext();
        var nextCalled = false;

        await RunMiddlewareAsync(context, responseFeature, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task Invoke_WithHeaderMetadata_WritesHeaders()
    {
        var (context, responseFeature) = CreateContext();
        var correlationId = Guid.NewGuid();
        var timestamp = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        context.Items["ApiResponseHeaders"] = new HeaderMetadata
        {
            CorrelationId = correlationId,
            DateTime = timestamp
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.Response.Headers["bank-correlation-id"].ToString(),
                Is.EqualTo(correlationId.ToString("D")));
            Assert.That(
                context.Response.Headers["bank-date-time"].ToString(),
                Is.EqualTo(timestamp.ToString("O", CultureInfo.InvariantCulture)));
        });
    }

    private sealed class NullableMetadata
    {
        [AddApiResponseHeader("x-optional")]
        public string? Optional { get; set; }

        [AddApiResponseHeader("x-required")]
        public string? Required { get; set; }
    }

    [Test]
    public async Task Invoke_SkipsNullPropertyValues()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new NullableMetadata
        {
            Optional = null,
            Required = "present"
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers.ContainsKey("x-optional"), Is.False);
            Assert.That(
                context.Response.Headers["x-required"].ToString(),
                Is.EqualTo("present"));
        });
    }

    private sealed class MixedMetadata
    {
        [AddApiResponseHeader("x-guid")]
        public Guid Id { get; set; }

        [AddApiResponseHeader("x-date")]
        public DateTime Date { get; set; }

        [AddApiResponseHeader("x-offset")]
        public DateTimeOffset Offset { get; set; }

        [AddApiResponseHeader("x-int")]
        public int Count { get; set; }

        [AddApiResponseHeader("x-decimal")]
        public decimal Amount { get; set; }

        [AddApiResponseHeader("x-string")]
        public string? Name { get; set; }

        public string? NotAHeader { get; set; }
    }

    [Test]
    public async Task Invoke_FormatsValueTypesUsingInvariantCulture()
    {
        var (context, responseFeature) = CreateContext();
        var id = Guid.NewGuid();
        var date = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(5));
        context.Items["ApiResponseHeaders"] = new MixedMetadata
        {
            Id = id,
            Date = date,
            Offset = offset,
            Count = 42,
            Amount = 12.5m,
            Name = "abc",
            NotAHeader = "ignored"
        };

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            await RunMiddlewareAsync(context, responseFeature);

            Assert.Multiple(() =>
            {
                Assert.That(context.Response.Headers["x-guid"].ToString(),
                    Is.EqualTo(id.ToString("D")));
                Assert.That(context.Response.Headers["x-date"].ToString(),
                    Is.EqualTo(date.ToString("O", CultureInfo.InvariantCulture)));
                Assert.That(context.Response.Headers["x-offset"].ToString(),
                    Is.EqualTo(offset.ToString("O", CultureInfo.InvariantCulture)));
                Assert.That(context.Response.Headers["x-int"].ToString(), Is.EqualTo("42"));
                Assert.That(context.Response.Headers["x-decimal"].ToString(), Is.EqualTo("12.5"));
                Assert.That(context.Response.Headers["x-string"].ToString(), Is.EqualTo("abc"));
                Assert.That(context.Response.Headers.ContainsKey("NotAHeader"), Is.False);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private sealed class NoHeaderMetadata
    {
        public string Value { get; set; } = "nope";
    }

    [Test]
    public async Task Invoke_WithTypeHavingNoAttributedProperties_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new NoHeaderMetadata();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(context.Response.Headers, Is.Empty);
    }

    private sealed class OverwriteMetadata
    {
        [AddApiResponseHeader("x-overwrite")]
        public string? Value { get; set; }
    }

    [Test]
    public async Task Invoke_OverwritesExistingHeaderValue()
    {
        var (context, responseFeature) = CreateContext();
        context.Response.Headers["x-overwrite"] = "original";
        context.Items["ApiResponseHeaders"] = new OverwriteMetadata { Value = "replaced" };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(
            context.Response.Headers["x-overwrite"].ToString(),
            Is.EqualTo("replaced"));
    }

    private sealed class WriteOnlyMetadata
    {
        private string _hidden = "hidden";

        [AddApiResponseHeader("x-write-only")]
        public string WriteOnly
        {
            set => _hidden = value;
        }

        [AddApiResponseHeader("x-readable")]
        public string Readable => _hidden;
    }

    [Test]
    public async Task Invoke_IgnoresWriteOnlyProperties()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new WriteOnlyMetadata();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers.ContainsKey("x-write-only"), Is.False);
            Assert.That(context.Response.Headers["x-readable"].ToString(), Is.EqualTo("hidden"));
        });
    }

    private sealed class NameMetadata
    {
        [AddApiResponseHeader("x-name")]
        public string? Name { get; set; }

        [AddApiResponseHeader("x-count")]
        public int Count { get; set; }
    }

    [Test]
    public async Task Invoke_WithListMetadata_CombinesPropertyValuesWithPipeSeparator()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NameMetadata>
        {
            new() { Name = "alpha", Count = 1 },
            new() { Name = "beta", Count = 2 },
            new() { Name = "gamma", Count = 3 }
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.Response.Headers["x-name"].ToString(),
                Is.EqualTo("alpha|beta|gamma"));
            Assert.That(
                context.Response.Headers["x-count"].ToString(),
                Is.EqualTo("1|2|3"));
        });
    }

    [Test]
    public async Task Invoke_WithListMetadata_FormatsEachItemUsingInvariantCulture()
    {
        var (context, responseFeature) = CreateContext();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var date1 = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var date2 = new DateTime(2031, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        context.Items["ApiResponseHeaders"] = new List<HeaderMetadata>
        {
            new() { CorrelationId = id1, DateTime = date1 },
            new() { CorrelationId = id2, DateTime = date2 }
        };

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            await RunMiddlewareAsync(context, responseFeature);

            Assert.Multiple(() =>
            {
                Assert.That(
                    context.Response.Headers["bank-correlation-id"].ToString(),
                    Is.EqualTo($"{id1:D}|{id2:D}"));
                Assert.That(
                    context.Response.Headers["bank-date-time"].ToString(),
                    Is.EqualTo(
                        date1.ToString("O", CultureInfo.InvariantCulture)
                        + "|"
                        + date2.ToString("O", CultureInfo.InvariantCulture)));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task Invoke_WithListMetadata_SkipsNullPropertyValues()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NullableMetadata>
        {
            new() { Optional = null, Required = "first" },
            new() { Optional = "middle", Required = "second" },
            new() { Optional = null, Required = "third" }
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.Response.Headers["x-optional"].ToString(),
                Is.EqualTo("middle"));
            Assert.That(
                context.Response.Headers["x-required"].ToString(),
                Is.EqualTo("first|second|third"));
        });
    }

    [Test]
    public async Task Invoke_WithListMetadata_WithSingleItem_WritesUnjoinedValue()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NameMetadata>
        {
            new() { Name = "solo", Count = 7 }
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers["x-name"].ToString(), Is.EqualTo("solo"));
            Assert.That(context.Response.Headers["x-count"].ToString(), Is.EqualTo("7"));
        });
    }

    [Test]
    public async Task Invoke_WithEmptyListMetadata_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NameMetadata>();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(context.Response.Headers, Is.Empty);
    }

    [Test]
    public async Task Invoke_WithListMetadata_WhereAllPropertyValuesAreNull_DoesNotAddHeader()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NullableMetadata>
        {
            new() { Optional = null, Required = "a" },
            new() { Optional = null, Required = "b" }
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers.ContainsKey("x-optional"), Is.False);
            Assert.That(
                context.Response.Headers["x-required"].ToString(),
                Is.EqualTo("a|b"));
        });
    }

    [Test]
    public async Task Invoke_WithArrayMetadata_CombinesPropertyValuesWithPipeSeparator()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new[]
        {
            new NameMetadata { Name = "one", Count = 10 },
            new NameMetadata { Name = "two", Count = 20 }
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers["x-name"].ToString(), Is.EqualTo("one|two"));
            Assert.That(context.Response.Headers["x-count"].ToString(), Is.EqualTo("10|20"));
        });
    }

    [Test]
    public async Task Invoke_WithListMetadata_CapsCombinedValuesAtTenItems()
    {
        var (context, responseFeature) = CreateContext();
        var items = Enumerable.Range(1, 15)
            .Select(i => new NameMetadata { Name = $"n{i}", Count = i })
            .ToList();
        context.Items["ApiResponseHeaders"] = items;

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.Response.Headers["x-name"].ToString(),
                Is.EqualTo("n1|n2|n3|n4|n5|n6|n7|n8|n9|n10"));
            Assert.That(
                context.Response.Headers["x-count"].ToString(),
                Is.EqualTo("1|2|3|4|5|6|7|8|9|10"));
        });
    }

    [Test]
    public async Task Invoke_WithListOfTypeHavingNoAttributedProperties_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new List<NoHeaderMetadata>
        {
            new(),
            new()
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.That(context.Response.Headers, Is.Empty);
    }
}
