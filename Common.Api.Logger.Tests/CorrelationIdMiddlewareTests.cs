using System.Globalization;
using Common.Api.LoggerV2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Common.Api.Logger.Tests
{

    public sealed class CorrelationIdMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_GeneratesIdAndAddsItEverywhere_WhenHeaderIsMissing()
        {
            var context = CreateContext(out var responseFeature);
            var logger = new ScopeCapturingLogger();
            var nextWasCalled = false;
            string? scopeIdDuringNext = null;

            var middleware = new CorrelationIdMiddleware(_ =>
            {
                nextWasCalled = true;
                scopeIdDuringNext = logger.CurrentCorrelationId;
                Assert.True(logger.IsScopeActive);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, logger);
            var beforeResponse = DateTimeOffset.UtcNow;
            await responseFeature.FireOnStartingAsync();
            var afterResponse = DateTimeOffset.UtcNow;

            var storedId = Assert.IsType<string>(
                context.Items[CorrelationId.HttpContextItemKey]);

            Assert.True(nextWasCalled);
            Assert.Matches("^[a-f0-9]{32}$", storedId);
            Assert.Equal(storedId, scopeIdDuringNext);
            Assert.Equal(
                storedId,
                context.Response.Headers[CorrelationId.HeaderName].ToString());
            Assert.False(logger.IsScopeActive);

            var timestampText = context.Response.Headers[
                CorrelationId.TimestampHeaderName].ToString();

            Assert.True(DateTimeOffset.TryParseExact(
                timestampText,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp));
            Assert.Equal(TimeSpan.Zero, timestamp.Offset);
            Assert.InRange(timestamp, beforeResponse, afterResponse);
        }

        [Fact]
        public async Task InvokeAsync_ReusesIncomingCorrelationId()
        {
            const string incomingId = "correlation-from-caller";
            var context = CreateContext(out var responseFeature);
            context.Request.Headers[CorrelationId.HeaderName] = incomingId;
            var logger = new ScopeCapturingLogger();

            var middleware = new CorrelationIdMiddleware(_ =>
            {
                Assert.Equal(incomingId, logger.CurrentCorrelationId);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, logger);
            await responseFeature.FireOnStartingAsync();

            Assert.Equal(incomingId, context.GetCorrelationId());
            Assert.Equal(
                incomingId,
                context.Response.Headers[CorrelationId.HeaderName].ToString());
        }

        [Fact]
        public async Task InvokeAsync_GeneratesId_WhenIncomingHeaderIsBlank()
        {
            var context = CreateContext(out var responseFeature);
            context.Request.Headers[CorrelationId.HeaderName] = "   ";
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context, new ScopeCapturingLogger());
            await responseFeature.FireOnStartingAsync();

            var generatedId = context.GetCorrelationId();
            Assert.NotNull(generatedId);
            Assert.Matches("^[a-f0-9]{32}$", generatedId);
            Assert.Equal(
                generatedId,
                context.Response.Headers[CorrelationId.HeaderName].ToString());
        }

        [Fact]
        public async Task InvokeAsync_GeneratesDifferentIds_ForDifferentRequests()
        {
            var firstContext = CreateContext(out _);
            var secondContext = CreateContext(out _);
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(firstContext, new ScopeCapturingLogger());
            await middleware.InvokeAsync(secondContext, new ScopeCapturingLogger());

            Assert.NotEqual(
                firstContext.GetCorrelationId(),
                secondContext.GetCorrelationId());
        }

        [Fact]
        public void GetCorrelationId_ReturnsNull_WhenMiddlewareHasNotRun()
        {
            var context = new DefaultHttpContext();

            Assert.Null(context.GetCorrelationId());
        }

        private static DefaultHttpContext CreateContext(out ControllableResponseFeature responseFeature)
        {
            var features = new FeatureCollection();
            var req = new HttpRequestFeature
            {
                Headers = new HeaderDictionary()
            };
            responseFeature = new ControllableResponseFeature();
            features.Set<IHttpRequestFeature>(req);
            features.Set<IHttpResponseFeature>(responseFeature);
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseFeature.Body));

            var defaultHttpContext = new DefaultHttpContext(features);
            
            return defaultHttpContext;
        }

        private sealed class ScopeCapturingLogger : ILogger<CorrelationIdMiddleware>
        {
            public bool IsScopeActive { get; private set; }

            public string? CurrentCorrelationId { get; private set; }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                var properties = Assert.IsAssignableFrom<
                    IEnumerable<KeyValuePair<string, object?>>>(state);

                CurrentCorrelationId = properties
                    .Single(pair => pair.Key == CorrelationId.LogPropertyName)
                    .Value as string;
                IsScopeActive = true;

                return new CallbackDisposable(() => IsScopeActive = false);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }

        private sealed class CallbackDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }

        private sealed class ControllableResponseFeature : IHttpResponseFeature
        {
            private readonly Stack<(Func<object, Task> Callback, object State)>
                _onStarting = new();

            public int StatusCode { get; set; } = StatusCodes.Status200OK;

            public string? ReasonPhrase { get; set; }

            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

            public Stream Body { get; set; } = new MemoryStream();

            public bool HasStarted { get; private set; }

            public void OnStarting(Func<object, Task> callback, object state)
            {
                _onStarting.Push((callback, state));
            }

            public void OnCompleted(Func<object, Task> callback, object state)
            {
            }

            public async Task FireOnStartingAsync()
            {
                while (_onStarting.TryPop(out var registration))
                {
                    await registration.Callback(registration.State);
                }

                HasStarted = true;
            }
        }
    }

}
