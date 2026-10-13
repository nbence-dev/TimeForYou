namespace TimeForYou.WebUntis;

/// <summary>
/// Thin, transport-agnostic client over the mock's REST surface (issue #3 §*Backend (.NET 10)*,
/// NFR-11). The mock is the system's only backend — there is no real WebUntis tenant and no Node
/// sidecar — but nothing in this interface names the mock, so a real WebUntis integration could
/// implement it later without touching callers.
/// </summary>
public interface IWebUntisClient
{
    /// <exception cref="WebUntisCredentialsRejectedException">The credentials were rejected.</exception>
    /// <exception cref="WebUntisUnavailableException">WebUntis could not be reached.</exception>
    Task<WebUntisSession> LoginAsync(WebUntisCredentials credentials, CancellationToken cancellationToken = default);

    /// <exception cref="WebUntisCredentialsRejectedException">The session is unknown or expired.</exception>
    /// <exception cref="WebUntisUnavailableException">WebUntis could not be reached.</exception>
    Task<IReadOnlyList<WebUntisLesson>> GetTimetableAsync(
        WebUntisSession session,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// The signed-in student's display name and class (#113), or null when WebUntis doesn't offer
    /// them. The default returns null, so a client that can't fetch a profile needn't implement it.
    /// </summary>
    /// <exception cref="WebUntisCredentialsRejectedException">The session is unknown or expired.</exception>
    /// <exception cref="WebUntisUnavailableException">WebUntis could not be reached.</exception>
    Task<WebUntisProfile?> GetProfileAsync(WebUntisSession session, CancellationToken cancellationToken = default) =>
        Task.FromResult<WebUntisProfile?>(null);
}
