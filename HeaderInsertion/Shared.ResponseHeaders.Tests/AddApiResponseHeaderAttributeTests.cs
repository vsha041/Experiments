using Shared.ResponseHeaders;
using Xunit;

namespace Shared.ResponseHeaders.Tests;

public class AddApiResponseHeaderAttributeTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var attribute = new AddApiResponseHeaderAttribute("x-my-header");

        Assert.Equal("x-my-header", attribute.Name);
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AddApiResponseHeaderAttribute(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespaceName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new AddApiResponseHeaderAttribute(name));
    }

    [Fact]
    public void AttributeUsage_TargetsPropertiesOnly()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AddApiResponseHeaderAttribute),
            typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Property, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }
}
