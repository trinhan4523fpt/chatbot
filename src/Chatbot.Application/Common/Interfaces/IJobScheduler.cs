namespace Chatbot.Application.Common.Interfaces;

/// <summary>Schedules background jobs (Hangfire) without leaking Hangfire into the Application layer.</summary>
public interface IJobScheduler
{
    /// <summary>Enqueues ingestion for a document; returns the background job id.</summary>
    string EnqueueIngest(long documentId);

    /// <summary>Enqueues an experiment run; returns the background job id.</summary>
    string EnqueueExperimentRun(long experimentRunId);
}
