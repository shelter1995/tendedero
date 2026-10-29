namespace Liangtu;

// Dispatcher-owned: a completed selection stays owned until both its durable
// file and collection entry have been published. Failed jobs retain their image
// closure for one retry on the next explicit exit/update attempt.
internal sealed class CapturePublications
{
    sealed class Job
    {
        public required string Path;
        public required Func<Task> Publish;
        public TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    readonly List<Job> jobs = new();
    readonly Action<Exception> report;
    internal CapturePublications(Action<Exception> report) => this.report = report;
    internal int Count => jobs.Count;
    internal bool ContainsPath(string path) => jobs.Any(job => string.Equals(job.Path, path, StringComparison.OrdinalIgnoreCase));
    internal Task<bool> Enqueue(string path, Func<Task> publish)
    {
        var job = new Job { Path = path, Publish = publish };
        jobs.Add(job); // Reserve before invoking a delegate that may yield/reenter.
        _ = Run(job);
        return job.Completion.Task;
    }
    async Task Run(Job job)
    {
        var success = false;
        try { await job.Publish(); success = true; jobs.Remove(job); }
        catch (Exception error) {
            try { report(error); }
            catch (Exception reportingError) { Settings.Log("Capture error reporting: " + reportingError.Message); }
        }
        finally { job.Completion.TrySetResult(success); }
    }
    internal async Task<bool> Settle()
    {
        var tasks = new List<Task<bool>>();
        foreach (var job in jobs.ToArray()) {
            // A previously reported failure is retried exactly once per request.
            // An in-flight failure is returned to the caller without a retry loop.
            if (job.Completion.Task.IsCompleted) {
                job.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = Run(job);
            }
            tasks.Add(job.Completion.Task);
        }
        return (await Task.WhenAll(tasks)).All(success => success);
    }
}
