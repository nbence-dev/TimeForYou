namespace TimeForYou.WebUntis;

/// <summary>Base for the two failures the poll job (#32) must tell apart.</summary>
public abstract class WebUntisException : Exception
{
    protected WebUntisException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>The mock rejected the supplied WebUntis credentials (401) — re-onboarding is needed,
/// not a retry.</summary>
public sealed class WebUntisCredentialsRejectedException(string message) : WebUntisException(message);

/// <summary>The mock could not be reached, or returned a server error (5xx) — transient, and
/// covered by Hangfire's job retry (#32).</summary>
public sealed class WebUntisUnavailableException(string message, Exception? innerException = null)
    : WebUntisException(message, innerException);
