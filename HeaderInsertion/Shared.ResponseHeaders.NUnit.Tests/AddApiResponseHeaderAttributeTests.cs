using NUnit.Framework;
using Shared.ResponseHeaders;

namespace Shared.ResponseHeaders.NUnit.Tests;

[TestFixture]
public class AddApiResponseHeaderAttributeTests
{
    [Test]
    public void Constructor_SetsName()
    {
        var attribute = new AddApiResponseHeaderAttribute("x-my-header");

        Assert.That(attribute.Name, Is.EqualTo("x-my-header"));
    }

    [Test]
    public void Constructor_NullName_Throws()
    {
        Assert.That(
            () => new AddApiResponseHeaderAttribute(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t")]
    public void Constructor_EmptyOrWhitespaceName_Throws(string name)
    {
        Assert.That(
            () => new AddApiResponseHeaderAttribute(name),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void AttributeUsage_TargetsPropertiesOnly()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AddApiResponseHeaderAttribute),
            typeof(AttributeUsageAttribute));

        Assert.That(usage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Property));
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
        });
    }
}
