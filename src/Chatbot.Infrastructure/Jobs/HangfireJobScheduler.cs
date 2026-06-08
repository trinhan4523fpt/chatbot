using Chatbot.Application.Common.Interfaces;
using Hangfire;

namespace Chatbot.Infrastructure.Jobs;

public sealed class HangfireJobScheduler(IBackgroundJobClient jobs) : IJobScheduler
{
    public string EnqueueIngest(long documentId) =>
        jobs.Enqueue<IngestDocumentJob>("ingestion", job => job.RunAsync(documentId, CancellationToken.None));

    public string EnqueueExperimentRun(long experimentRunId) =>
        jobs.Enqueue<RunExperimentJob>("evaluation", job => job.RunAsync(experimentRunId, CancellationToken.None));
}
