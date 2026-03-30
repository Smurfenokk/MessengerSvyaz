using System;
using System.Security.Cryptography;
using System.Text;

namespace MessengerSvyaz.Services;

public class EncryptionService
{
    private byte[] _sharedKey = Array.Empty<byte>();
    private readonly ECDiffieHellman _ecdh;
    
    public byte[] PublicKey { get; }

    public EncryptionService()
    {
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        PublicKey = _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
    }

    public void DeriveSharedKey(byte[] otherPartyPublicKey)
    {
        using var otherPartyEcdh = ECDiffieHellman.Create();
        otherPartyEcdh.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);
        _sharedKey = _ecdh.DeriveKeyMaterial(otherPartyEcdh.PublicKey);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText) || _sharedKey.Length == 0) return string.Empty;

        using var aes = new AesGcm(_sharedKey, AesGcm.TagByteSizes.MaxSize);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText) || _sharedKey.Length == 0) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using var aes = new AesGcm(_sharedKey, AesGcm.TagByteSizes.MaxSize);

            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            var cipherSize = fullCipher.Length - nonceSize - tagSize;

            var nonce = new byte[nonceSize];
            var tag = new byte[tagSize];
            var cipherBytes = new byte[cipherSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(fullCipher, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(fullCipher, nonceSize + tagSize, cipherBytes, 0, cipherSize);

            var plainBytes = new byte[cipherSize];
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
    
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hashedBytes);
    }
}
