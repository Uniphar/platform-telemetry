namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class TelemetryExtensionsTests
{
    [TestMethod]
    public void ToDictionary_NullObject_ReturnsEmptyDictionary()
    {
        object? obj = null;

        var result = obj.ToDictionary();

        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ToDictionary_EmptyObject_ReturnsEmptyDictionary()
    {
        var obj = new object();

        var result = obj.ToDictionary();

        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ToDictionary_ObjectWithProperties_ReturnsCorrectDictionary()
    {
        var obj = new SampleObject();

        var result = obj.ToDictionary();

        Assert.HasCount(3, result);
        Assert.AreEqual(obj.Key, result["Key"]);
        Assert.AreEqual(obj.Value, result["Value"]);
        Assert.AreEqual(string.Empty, result["OptionalProperty"]);
    }

    [TestMethod]
    public void ToDictionary_AnonymousType_ReturnsCorrectDictionary()
    {
        var obj = new { PropertyA = "A", PropertyB = 123 };

        var result = obj.ToDictionary();

        Assert.HasCount(2, result);
        Assert.AreEqual(obj.PropertyA, result["PropertyA"]);
        Assert.AreEqual(obj.PropertyB, result["PropertyB"]);
    }

    private class SampleObject
    {
        public string Key { get; } = "Test";
        public int Value { get; } = 42;
        public string? OptionalProperty { get; set; } = null;
    }
}