using System.IO;
using System.Text;

namespace TSCC_WASD.Tests;

internal static class LogFile
{
    // Other test classes run in parallel and may append to the log; read it without blocking them.
    public static string ReadAllText(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        return sr.ReadToEnd();
    }
}
