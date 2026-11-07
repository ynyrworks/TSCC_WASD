using System.IO;

namespace TSCC_WASD.Tests;

internal static class TempDir
{
    // A background writer (for example the log) can still hold a file for a moment; retry briefly, then give up quietly.
    public static void Delete(string path)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
