using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal static class Check
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("FAIL " + message);
        }

        Console.WriteLine("PASS " + message);
    }

    public static string CanaryFingerprint(string canary)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canary)))[..12];

    public static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    public static void AssertBytesExclude(byte[] data, string label, params string[] canaries)
    {
        foreach (var canary in canaries)
        {
            True(
                !ContainsBytes(data, Encoding.UTF8.GetBytes(canary)),
                label + " excludes canary " + CanaryFingerprint(canary));
        }
    }

    public static void AssertFileExcludes(string path, params string[] canaries)
    {
        byte[] data;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            data = new byte[stream.Length];
            var offset = 0;
            while (offset < data.Length)
            {
                var read = stream.Read(data, offset, data.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }
        }

        AssertBytesExclude(data, Path.GetFileName(path), canaries);
    }

    public static async Task AssertZipExcludesAsync(string archivePath, string[] canaries, string[]? expectedEntryNames)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (expectedEntryNames is not null)
        {
            var actual = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var expected = expectedEntryNames.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            True(actual.SequenceEqual(expected), Path.GetFileName(archivePath) + " entries are exactly: " + string.Join(", ", actual));
        }

        foreach (var entry in archive.Entries)
        {
            await using var stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            AssertBytesExclude(memory.ToArray(), Path.GetFileName(archivePath) + ":" + entry.FullName, canaries);
        }
    }
}
