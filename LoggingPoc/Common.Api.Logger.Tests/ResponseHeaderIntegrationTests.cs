using System.Globalization;
using Common.Api.LoggerV2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using NUnit.Framework;

namespace Common.Api.Logger.Tests;


public sealed class ResponseHeaderIntegrationTests
{
    [Test]
    public async Task ResponseContainsGeneratedCorrelationIdAndUtcTimestamp()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var beforeRequest = DateTimeOffset.UtcNow;

        using var response = await client.GetAsync("/");
        var afterResponse = DateTimeOffset.UtcNow;

        response.EnsureSuccessStatusCode();

        Assert.That(response.Headers.TryGetValues(
            CorrelationId.HeaderName,
            out var correlationValues), Is.True);
        var correlationId = correlationValues!.Single();
        Assert.That(correlationId, Does.Match("^[a-f0-9]{32}$"));

        Assert.That(response.Headers.TryGetValues(
            CorrelationId.TimestampHeaderName,
            out var timestampValues), Is.True);
        var timestampText = timestampValues!.Single();

        Assert.That(DateTimeOffset.TryParseExact(
            timestampText,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp), Is.True);
        Assert.That(timestamp.Offset, Is.EqualTo(TimeSpan.Zero));
        Assert.That(timestamp, Is.InRange(beforeRequest, afterResponse));
    }

    [Test]
    public async Task ResponseReturnsIncomingCorrelationId()
    {
        const string incomingCorrelationId = "caller-correlation-id";
        using var server = CreateServer();
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationId.HeaderName, incomingCorrelationId);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.That(response.Headers.TryGetValues(
            CorrelationId.HeaderName,
            out var correlationValues), Is.True);
        Assert.That(correlationValues!.Single(), Is.EqualTo(incomingCorrelationId));
    }

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRequestCorrelationLogging();
            })
            .Configure(app =>
            {
                app.Run(context => context.Response.WriteAsync("OK"));
            });

        return new TestServer(builder);
    }
}
