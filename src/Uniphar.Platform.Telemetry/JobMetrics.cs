using System.Diagnostics.Metrics;

namespace Uniphar.Platform.Telemetry;

public class JobMetrics : IDisposable
{
    public const string Name = "frontgate.jobs";

    public const string TotalFilesProcessedMetricName = "frontgate.jobs.total-files";
    public const string ExecutionCountMetricName = "frontgate.jobs.exec-count";

    private readonly Counter<long> _executionCounter;
    private readonly Meter _meter;
    private readonly Histogram<long> _totalFilesHistogram;

    public JobMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);

        _totalFilesHistogram = _meter.CreateHistogram<long>(TotalFilesProcessedMetricName,
            "files",
            "Total files processed by job");

        _executionCounter = _meter.CreateCounter<long>(ExecutionCountMetricName,
            "executions",
            "Number of job executions");
    }

    public void Dispose() => _meter.Dispose();

    public void TrackTotalFiles(int countSuccessFiles, int countFailedFiles, string jobId, string jobType)
    {
        var totalFiles = countSuccessFiles + countFailedFiles;

        //use histogram to understand the distribution of values and compute trends over time.(e.g., how many and which jobs processed 0 files vs. 1000 files)
        if (totalFiles >= 0)
        {
            _totalFilesHistogram.Record(totalFiles,
                new KeyValuePair<string, object?>(nameof(countSuccessFiles), countSuccessFiles),
                new KeyValuePair<string, object?>(nameof(countFailedFiles), countFailedFiles),
                new KeyValuePair<string, object?>("JobId", jobId),
                new KeyValuePair<string, object?>("JobType", jobType));
        }
    }

    public void TrackExecutionCount(string jobId, string jobType)
    {
        _executionCounter.Add(1, new KeyValuePair<string, object?>("JobId", jobId), new KeyValuePair<string, object?>("JobType", jobType));
    }
}