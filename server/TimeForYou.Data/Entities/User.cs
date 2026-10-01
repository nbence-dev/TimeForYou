namespace TimeForYou.Data.Entities;

public class User
{
    public Guid Id { get; set; }

    public required string School { get; set; }

    /// <summary>
    /// The `{server, school, user, key}` WebUntis SSO tuple, DataProtection-encrypted as a
    /// single opaque payload. Never persisted in plaintext.
    /// </summary>
    public required string CredentialsProtected { get; set; }

    /// <summary>
    /// SHA-256 of the normalised `(school, user)` pair. <see cref="CredentialsProtected"/> is
    /// non-deterministic ciphertext and can't be searched, so a second device scanning the same
    /// QR code needs this to find the existing user instead of creating a duplicate (issue #31).
    /// </summary>
    public required string UntisUserHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The last day (SAST) of the newest poll window this user has been polled for; null until the
    /// first poll. Lessons the worker has never seen that fall after this date are weeks that just
    /// entered the rolling window, so they are seeded silently instead of reported as "added".
    /// </summary>
    public DateOnly? SeededThrough { get; set; }

    public List<Device> Devices { get; set; } = [];

    public List<LessonSnapshot> LessonSnapshots { get; set; } = [];

    public List<Note> Notes { get; set; } = [];

    public List<ChangeLogEntry> ChangeLogEntries { get; set; } = [];

    public IcsToken? IcsToken { get; set; }
}
