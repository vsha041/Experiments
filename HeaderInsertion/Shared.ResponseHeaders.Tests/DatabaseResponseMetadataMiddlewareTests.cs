using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Shared.ResponseHeaders;
using Xunit;

namespace Shared.ResponseHeaders.Tests;

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

    [Fact]
    public async Task Invoke_WithoutMetadata_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Empty(context.Response.Headers);
    }

    [Fact]
    public async Task Invoke_WithNullMetadata_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = null;

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Empty(context.Response.Headers);
    }

    [Fact]
    public async Task Invoke_CallsNextDelegate()
    {
        var (context, responseFeature) = CreateContext();
        var nextCalled = false;

        await RunMiddlewareAsync(context, responseFeature, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
    }

    [Fact]
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

        Assert.Equal(correlationId.ToString("D"),
            context.Response.Headers["bank-correlation-id"]);
        Assert.Equal(timestamp.ToString("O", CultureInfo.InvariantCulture),
            context.Response.Headers["bank-date-time"]);
    }

    private sealed class NullableMetadata
    {
        [AddApiResponseHeader("x-optional")]
        public string? Optional { get; set; }

        [AddApiResponseHeader("x-required")]
        public string? Required { get; set; }
    }

    [Fact]
    public async Task Invoke_SkipsNullPropertyValues()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new NullableMetadata
        {
            Optional = null,
            Required = "present"
        };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.False(context.Response.Headers.ContainsKey("x-optional"));
        Assert.Equal("present", context.Response.Headers["x-required"]);
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

    [Fact]
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

            Assert.Equal(id.ToString("D"), context.Response.Headers["x-guid"]);
            Assert.Equal(date.ToString("O", CultureInfo.InvariantCulture),
                context.Response.Headers["x-date"]);
            Assert.Equal(offset.ToString("O", CultureInfo.InvariantCulture),
                context.Response.Headers["x-offset"]);
            Assert.Equal("42", context.Response.Headers["x-int"]);
            Assert.Equal("12.5", context.Response.Headers["x-decimal"]);
            Assert.Equal("abc", context.Response.Headers["x-string"]);
            Assert.False(context.Response.Headers.ContainsKey("NotAHeader"));
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

    [Fact]
    public async Task Invoke_WithTypeHavingNoAttributedProperties_DoesNotAddHeaders()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new NoHeaderMetadata();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Empty(context.Response.Headers);
    }

    private sealed class OverwriteMetadata
    {
        [AddApiResponseHeader("x-overwrite")]
        public string? Value { get; set; }
    }

    [Fact]
    public async Task Invoke_OverwritesExistingHeaderValue()
    {
        var (context, responseFeature) = CreateContext();
        context.Response.Headers["x-overwrite"] = "original";
        context.Items["ApiResponseHeaders"] = new OverwriteMetadata { Value = "replaced" };

        await RunMiddlewareAsync(context, responseFeature);

        Assert.Equal("replaced", context.Response.Headers["x-overwrite"]);
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

    [Fact]
    public async Task Invoke_IgnoresWriteOnlyProperties()
    {
        var (context, responseFeature) = CreateContext();
        context.Items["ApiResponseHeaders"] = new WriteOnlyMetadata();

        await RunMiddlewareAsync(context, responseFeature);

        Assert.False(context.Response.Headers.ContainsKey("x-write-only"));
        Assert.Equal("hidden", context.Response.Headers["x-readable"]);
    }
}
