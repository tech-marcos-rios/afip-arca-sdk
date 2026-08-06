using System;
using System.Security.Cryptography;
using System.Text;

namespace Afip.Arca.Sdk.Demo.Services;

/// <summary>
/// AES-256-GCM symmetric encryption for certificate bytes and passwords stored in the
/// database. The key is 32 bytes and must come from a secure source (env variable,
/// Key Vault, etc.) — never hardcode it in source.
/// </summary>
internal sealed class AesCertificateEncryption
{
    private readonly byte[] _key;

    public AesCertificateEncryption(byte[] key)
    {
        if (key is null || key.Length != 32)
            throw new ArgumentException("Encryption key must be exactly 32 bytes (AES-256).", nameof(key));
        _key = key;
    }

    public (byte[] Ciphertext, byte[] Nonce, byte[] Tag) Encrypt(byte[] plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];    // 12 bytes
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];         // 16 bytes
        var ciphertext = new byte[plaintext.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return (ciphertext, nonce, tag);
    }

    public byte[] Decrypt(byte[] ciphertext, byte[] nonce, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public (byte[] Ciphertext, byte[] Nonce, byte[] Tag) EncryptString(string value) =>
        Encrypt(Encoding.UTF8.GetBytes(value));

    public string DecryptString(byte[] ciphertext, byte[] nonce, byte[] tag) =>
        Encoding.UTF8.GetString(Decrypt(ciphertext, nonce, tag));
}
