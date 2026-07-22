using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shared.ResponseHeaders;

namespace Shared.ResponseHeaders.NUnit.Tests;

[TestFixture]
public class DatabaseResponseMetadataExtensionsTests
{
    [Test]
    public void AddDatabaseResponseMetadata_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddDatabaseResponseMetadata();

        Assert.That(result, Is.SameAs(services));
    }

    [Test]
    public void AddDatabaseResponseMetadata_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.That(
            () => services.AddDatabaseResponseMetadata(),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void UseDatabaseResponseMetadata_NullApp_Throws()
    {
        IApplicationBuilder app = null!;

        Assert.That(
            () => app.UseDatabaseResponseMetadata(),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void UseDatabaseResponseMetadata_ReturnsSameBuilder()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        var result = app.UseDatabaseResponseMetadata();

        Assert.That(result, Is.SameAs(app));
    }

    [Test]
    public async Task UseDatabaseResponseMetadata_RegistersMiddlewareInPipeline()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var terminalCalled = false;

        app.UseDatabaseResponseMetadata();
        app.Run(_ =>
        {
            terminalCalled = true;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        await pipeline(new DefaultHttpContext());

        Assert.That(terminalCalled, Is.True);
    }
}
