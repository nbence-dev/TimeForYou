namespace TimeForYou.WebUntis;

/// <summary>An opaque session handle returned by <see cref="IWebUntisClient.LoginAsync"/>, passed
/// back into <see cref="IWebUntisClient.GetTimetableAsync"/> as the mock's bearer session id.</summary>
public sealed record WebUntisSession(string SessionId);
