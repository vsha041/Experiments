using System.Globalization;
using Common.Api.LoggerV2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Common.Api.Logger.Tests;


public sealed class ResponseHeaderIntegrationTests
{
    [Fact]
    public async Task ResponseContainsGeneratedCorrelationIdAndUtcTimestamp()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var beforeRequest = DateTimeOffset.UtcNow;

        using var response = await client.GetAsync("/", new CancellationToken(false));
        var afterResponse = DateTimeOffset.UtcNow;

        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues(
            CorrelationId.HeaderName,
            out var correlationValues));
        var correlationId = Assert.Single(correlationValues);
        Assert.Matches("^[a-f0-9]{32}$", correlationId);

        Assert.True(response.Headers.TryGetValues(
            CorrelationId.TimestampHeaderName,
            out var timestampValues));
        var timestampText = Assert.Single(timestampValues);

        Assert.True(DateTimeOffset.TryParseExact(
            timestampText,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp));
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.InRange(timestamp, beforeRequest, afterResponse);
    }

    [Fact]
    public async Task ResponseReturnsIncomingCorrelationId()
    {
        const string incomingCorrelationId = "caller-correlation-id";
        using var server = CreateServer();
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationId.HeaderName, incomingCorrelationId);

        using var response = await client.SendAsync(request, new CancellationToken(false));

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues(
            CorrelationId.HeaderName,
            out var correlationValues));
        Assert.Equal(incomingCorrelationId, Assert.Single(correlationValues));
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
