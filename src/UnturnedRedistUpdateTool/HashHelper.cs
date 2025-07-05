using System.Security.Cryptography;

namespace UnturnedRedistUpdateTool;

internal static class HashHelper
{
    public static string GetFileHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}