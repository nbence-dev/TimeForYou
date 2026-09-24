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

    public DateTimeOffset CreatedAt { get; set; }

    public List<Device> Devices { get; set; } = [];

    public List<LessonSnapshot> LessonSnapshots { get; set; } = [];

    public List<Note> Notes { get; set; } = [];

    public List<ChangeLogEntry> ChangeLogEntries { get; set; } = [];

    public IcsToken? IcsToken { get; set; }
}
