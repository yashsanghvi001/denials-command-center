using System.Security.Cryptography;
using System.Text;

namespace DenialsCommandCenter.Domain;

public static class Hashing
{
    public static string Sha256Hex(string text) => Sha256Hex(Encoding.UTF8.GetBytes(text));

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
