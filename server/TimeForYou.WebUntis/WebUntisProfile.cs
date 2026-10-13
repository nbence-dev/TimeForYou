namespace TimeForYou.WebUntis;

/// <summary>The signed-in student as WebUntis shows them (#113). <see cref="ClassName"/> is null
/// when WebUntis has no class for the student.</summary>
public sealed record WebUntisProfile(string DisplayName, string? ClassName);