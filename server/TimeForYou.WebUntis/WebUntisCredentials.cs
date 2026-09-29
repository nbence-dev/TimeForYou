namespace TimeForYou.WebUntis;

/// <summary>The WebUntis SSO QR payload's four fields (issue #3 §*Onboarding*), used to log in.</summary>
public sealed record WebUntisCredentials(string Server, string School, string User, string Key);
