using System.Globalization;
using Common.Api.LoggerV2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Common.Api.Logger.Tests
{

    public sealed class CorrelationIdMiddlewareTests
    {
        [Test]
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
                Assert.That(logger.IsScopeActive, Is.True);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, logger);
            var beforeResponse = DateTimeOffset.UtcNow;
            await responseFeature.FireOnStartingAsync();
            var afterResponse = DateTimeOffset.UtcNow;

            var storedId = context.Items[CorrelationId.HttpContextItemKey] as string;

            Assert.That(nextWasCalled, Is.True);
            Assert.That(storedId, Is.Not.Null);
            Assert.That(storedId, Does.Match("^[a-f0-9]{32}$"));
            Assert.That(scopeIdDuringNext, Is.EqualTo(storedId));
            Assert.That(
                context.Response.Headers[CorrelationId.HeaderName].ToString(),
                Is.EqualTo(storedId));
            Assert.That(logger.IsScopeActive, Is.False);

            var timestampText = context.Response.Headers[
                CorrelationId.TimestampHeaderName].ToString();

            Assert.That(DateTimeOffset.TryParseExact(
                timestampText,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp), Is.True);
            Assert.That(timestamp.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(timestamp, Is.InRange(beforeResponse, afterResponse));
        }

        [Test]
        public async Task InvokeAsync_ReusesIncomingCorrelationId()
        {
            const string incomingId = "correlation-from-caller";
            var context = CreateContext(out var responseFeature);
            context.Request.Headers[CorrelationId.HeaderName] = incomingId;
            var logger = new ScopeCapturingLogger();

            var middleware = new CorrelationIdMiddleware(_ =>
            {
                Assert.That(logger.CurrentCorrelationId, Is.EqualTo(incomingId));
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, logger);
            await responseFeature.FireOnStartingAsync();

            Assert.That(context.GetCorrelationId(), Is.EqualTo(incomingId));
            Assert.That(
                context.Response.Headers[CorrelationId.HeaderName].ToString(),
                Is.EqualTo(incomingId));
        }

        [Test]
        public async Task InvokeAsync_GeneratesId_WhenIncomingHeaderIsBlank()
        {
            var context = CreateContext(out var responseFeature);
            context.Request.Headers[CorrelationId.HeaderName] = "   ";
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context, new ScopeCapturingLogger());
            await responseFeature.FireOnStartingAsync();

            var generatedId = context.GetCorrelationId();
            Assert.That(generatedId, Is.Not.Null);
            Assert.That(generatedId, Does.Match("^[a-f0-9]{32}$"));
            Assert.That(
                context.Response.Headers[CorrelationId.HeaderName].ToString(),
                Is.EqualTo(generatedId));
        }

        [Test]
        public async Task InvokeAsync_GeneratesDifferentIds_ForDifferentRequests()
        {
            var firstContext = CreateContext(out _);
            var secondContext = CreateContext(out _);
            var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(firstContext, new ScopeCapturingLogger());
            await middleware.InvokeAsync(secondContext, new ScopeCapturingLogger());

            Assert.That(
                secondContext.GetCorrelationId(),
                Is.Not.EqualTo(firstContext.GetCorrelationId()));
        }

        [Test]
        public void GetCorrelationId_ReturnsNull_WhenMiddlewareHasNotRun()
        {
            var context = new DefaultHttpContext();

            Assert.That(context.GetCorrelationId(), Is.Null);
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
                Assert.That(
                    state,
                    Is.AssignableTo<IEnumerable<KeyValuePair<string, object?>>>());
                var properties =
                    (IEnumerable<KeyValuePair<string, object?>>)(object)state;

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
