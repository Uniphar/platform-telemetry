using System.Collections.Generic;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Uniphar.Platform.Telemetry;

[TestClass]
[TestCategory("Unit")]
public class HealthProbeFilteringTests
{
    private static DefaultHttpContext CreateContext(string path, int statusCode)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString(path);
        ctx.Response.StatusCode = statusCode;
        return ctx;
    }

    [TestMethod]
    [DataRow("/health", 200)]
    [DataRow("/healthz", 204)]
    [DataRow("/health/live", 301)]
    public void SuccessfulHealthProbeRequests_AreFiltered(string path, int statusCode)
    {
        var ctx = CreateContext(path, statusCode);
        var paths = new[] { "/health" };

        var sampled = TelemetryBuilder.ShouldSampleRequest(ctx, paths);
        sampled.Should().BeFalse();

    }

    [TestMethod]
    [DataRow("/health", 400)]
    [DataRow("/healthz", 500)]
    [DataRow("/app-prefix/health", 404)]
    [DataRow("/health/live", 503)]
    public void UnsuccessfulHealthProbeRequests_AreNotFiltered(string path, int statusCode)
    {
        var ctx = CreateContext(path, statusCode);
        var paths = new[] { "/health" };

        var sampled = TelemetryBuilder.ShouldSampleRequest(ctx, paths);

        sampled.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("/api/health", 200)]
    [DataRow("/status", 204)]
    [DataRow("/metrics", 302)]
    public void NonHealthPaths_AreNotFiltered(string path, int statusCode)
    {
        var ctx = CreateContext(path, statusCode);
        var paths = new[] { "/health" };

        var sampled = TelemetryBuilder.ShouldSampleRequest(ctx, paths);

        sampled.Should().BeTrue();
    }
}