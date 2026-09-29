using System.Security.Cryptography;
using System.Text;

namespace TimeForYou.Api.Features.Auth;

internal static class AuthCrypto
{
    /// <summary>A random opaque bearer token (issue #31) — carries no claims of its own; the API
    /// looks it up against the issuing device's row (docs/openapi.yaml, bearerAuth).</summary>
    public static string GenerateBearerToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    /// <summary>The raw bearer token is never stored — only this hash (devices.bearer_token_hash).</summary>
    public static string HashBearerToken(string token) => Sha256Hex(token);

    /// <summary>SHA-256 of the normalised `(school, user)` pair (users.untis_user_hash) — lets a
    /// second device's QR scan find the existing user despite the non-deterministic ciphertext in
    /// credentials_protected.</summary>
    public static string HashUntisUser(string school, string user) =>
        Sha256Hex($"{Normalize(school)}\u001f{Normalize(user)}");

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string Sha256Hex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
