using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace TimeForYou.WebUntis;

/// <summary>
/// Encrypts and decrypts the WebUntis SSO tuple stored in users.credentials_protected. Shared by
/// the api (#31, encrypts on QR scan) and the worker (#32, decrypts each poll) so both always use
/// the same purpose string and JSON shape.
/// </summary>
public sealed class WebUntisCredentialProtector(IDataProtectionProvider provider)
{
    /// <summary>Changing this would make every existing credentials_protected value undecryptable.</summary>
    public const string Purpose = "TimeForYou.WebUntisCredentials.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(WebUntisCredentials credentials) =>
        _protector.Protect(JsonSerializer.Serialize(credentials));

    public WebUntisCredentials Unprotect(string protectedCredentials) =>
        JsonSerializer.Deserialize<WebUntisCredentials>(_protector.Unprotect(protectedCredentials))
        ?? throw new InvalidOperationException("Decrypted WebUntis credentials were empty.");
}