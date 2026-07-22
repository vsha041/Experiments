using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.ResponseHeaders;
using Xunit;

namespace Shared.ResponseHeaders.Tests;

public class DatabaseResponseMetadataExtensionsTests
{
    [Fact]
    public void AddDatabaseResponseMetadata_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddDatabaseResponseMetadata();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddDatabaseResponseMetadata_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddDatabaseResponseMetadata());
    }

    [Fact]
    public void UseDatabaseResponseMetadata_NullApp_Throws()
    {
        IApplicationBuilder app = null!;

        Assert.Throws<ArgumentNullException>(() => app.UseDatabaseResponseMetadata());
    }

    [Fact]
    public void UseDatabaseResponseMetadata_ReturnsSameBuilder()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        var result = app.UseDatabaseResponseMetadata();

        Assert.Same(app, result);
    }

    [Fact]
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

        Assert.True(terminalCalled);
    }
}
