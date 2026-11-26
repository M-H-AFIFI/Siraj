using System.IO.Compression;

namespace Siraj.Core.Tests;

internal static class Reference
{
    public static IEnumerable<string[]> Rows(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "reference", name);
        Stream stream = File.OpenRead(path);
        if (name.EndsWith(".gz")) stream = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(stream);
        reader.ReadLine(); // header
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0) yield return line.Split(',');
        }
    }
}
