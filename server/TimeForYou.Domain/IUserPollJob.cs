namespace TimeForYou.Domain;

/// <summary>
/// The worker's single-user poll as a Hangfire job the api can enqueue through the shared Hangfire
/// storage (#58) without referencing the worker project. Implemented by the worker's PollJob.
/// </summary>
public interface IUserPollJob
{
    Task RunForUserAsync(Guid userId, CancellationToken cancellationToken);
}
