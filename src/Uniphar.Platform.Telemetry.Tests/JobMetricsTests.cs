using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class JobMetricsTests
{
    private MetricCollector<long> _execCountMetricsCollector;
    private JobMetrics _jobMetrics;
    private MetricCollector<long> _totalFilesMetricsCollector;

    [TestInitialize]
    public void TestInit()
    {
        var services = CreateServiceProvider();

        var meterFactory = services.GetRequiredService<IMeterFactory>();
        _execCountMetricsCollector = new MetricCollector<long>(meterFactory, JobMetrics.Name, JobMetrics.ExecutionCountMetricName);
        _totalFilesMetricsCollector = new MetricCollector<long>(meterFactory, JobMetrics.Name, JobMetrics.TotalFilesProcessedMetricName);

        _jobMetrics = services.GetRequiredService<JobMetrics>();
    }

    [TestMethod]
    public void TrackTotalFiles_ShouldRecord_WhenTotalFilesIsZeroOrGreater()
    {
        //Arrange
        var jobId = "files-moving-1";
        var jobType = "testJob";

        //Act
        _jobMetrics.TrackTotalFiles(0, 0, jobId, jobType);
        _jobMetrics.TrackTotalFiles(10, 0, jobId, jobType);

        //Assert
        var measurements = _totalFilesMetricsCollector.GetMeasurementSnapshot();
        Assert.HasCount(2, measurements);

        Assert.AreEqual(0, measurements[0].Value);
        Assert.AreEqual(jobId, measurements[0].Tags["JobId"]);
        Assert.AreEqual(jobType, measurements[0].Tags["JobType"]);

        Assert.AreEqual(10, measurements[1].Value);
        Assert.AreEqual(jobId, measurements[1].Tags["JobId"]);
        Assert.AreEqual(jobType, measurements[1].Tags["JobType"]);
    }

    [TestMethod]
    public void TrackTotalFiles_ShouldNotRecord_WhenTotalFilesIsLessThanZero()
    {
        //Arrange
        var totalFiles = -1;
        var jobId = "files-moving-1";
        var jobType = "testJob";

        //Act
        _jobMetrics.TrackTotalFiles(totalFiles, 0, jobId, jobType);

        //Assert
        var measurements = _totalFilesMetricsCollector.GetMeasurementSnapshot();
        Assert.IsEmpty(measurements);
    }

    [TestMethod]
    public void TrackExecutionCount_ShouldIncrementCounter()
    {
        //Arrange
        var jobId = "files-moving-1";
        var jobType = "testJob";

        //Act
        _jobMetrics.TrackExecutionCount(jobId, jobType);

        //Assert
        var measurements = _execCountMetricsCollector.GetMeasurementSnapshot();
        Assert.HasCount(1, measurements);

        Assert.AreEqual(1, measurements[0].Value);
        Assert.AreEqual(jobId, measurements[0].Tags["JobId"]);
        Assert.AreEqual(jobType, measurements[0].Tags["JobType"]);
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMetrics();

        serviceCollection.AddSingleton<JobMetrics>();
        return serviceCollection.BuildServiceProvider();
    }
}