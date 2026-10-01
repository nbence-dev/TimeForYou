using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace TimeForYou.WebUntis;

/// <summary>
/// Hands DataProtection a single key descriptor built directly from <c>AES_KEY</c> instead of a
/// randomly generated one persisted to disk. DataProtection does the actual encryption (nonces,
/// auth tags, key derivation) — this class only supplies the master key material and the storage
/// format DataProtection itself defines; it does not implement any cryptography.
///
/// Moved here from TimeForYou.Api (#31) so the worker (#32) can rebuild the exact same key: every
/// process derives it from the same env var, so there is nothing to persist and no key ring to
/// lose. The key id must never change, or existing credentials_protected values stop decrypting.
/// </summary>
public sealed class FixedKeyXmlRepository : IXmlRepository
{
    private static readonly Guid KeyId = new("6a1a6b0e-31f0-4c1e-8d1a-f0f0f0f0f0f1");

    private readonly XElement _keyElement;

    public FixedKeyXmlRepository(byte[] masterKey)
    {
        var configuration = new AuthenticatedEncryptorConfiguration
        {
            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
            ValidationAlgorithm = ValidationAlgorithm.HMACSHA256,
        };
        var descriptor = new AuthenticatedEncryptorDescriptor(configuration, new Secret(masterKey));
        var exported = descriptor.ExportToXml();

        _keyElement = new XElement(
            "key",
            new XAttribute("id", KeyId),
            new XAttribute("version", 1),
            new XElement("creationDate", DateTimeOffset.UnixEpoch.ToString("o")),
            new XElement("activationDate", DateTimeOffset.UnixEpoch.ToString("o")),
            new XElement("expirationDate", DateTimeOffset.MaxValue.ToString("o")),
            new XElement(
                "descriptor",
                new XAttribute("deserializerType", exported.DeserializerType.AssemblyQualifiedName!),
                exported.SerializedDescriptorElement));
    }

    public IReadOnlyCollection<XElement> GetAllElements() => [_keyElement];

    public void StoreElement(XElement element, string friendlyName)
    {
        // Automatic key generation is disabled, so DataProtection never tries to mint a second
        // key; a no-op keeps the fixed key authoritative if it ever does.
    }
}