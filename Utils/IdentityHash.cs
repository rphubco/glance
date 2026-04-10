namespace Glance.Utils;

using System.Security.Cryptography;
using System.Text;

public static class IdentityHash
{
    const string Salt = "glance-v1:";

    public static string Hash(ulong contentId)
    {
        if (contentId == 0) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(Salt + contentId);
        var digest = SHA256.HashData(bytes);
        var sb = new StringBuilder(64);
        foreach (var b in digest) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
