using System.Security.Cryptography;
using System.Text;

namespace OhMyGPA.Bot.Models;

public class AesCrypto
{
    private readonly ICryptoTransform _decryptor;
    private readonly ICryptoTransform _encryptor;

    public AesCrypto(string key, string iv)
    {
        var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        _encryptor = aes.CreateEncryptor();
        _decryptor = aes.CreateDecryptor();
    }

    public byte[] Encrypt(string plainText)
    {
        return _encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(plainText), 0, plainText.Length);
    }

    public string Decrypt(byte[]? cipherText)
    {
        if (cipherText == null)
            return "";
        var resultArray = _decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        return Encoding.UTF8.GetString(resultArray);
    }
}