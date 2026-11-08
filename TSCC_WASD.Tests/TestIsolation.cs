using System.IO;
using System.Runtime.CompilerServices;
using TSCC_WASD.Core;
using Xunit;

namespace TSCC_WASD.Tests;

/// <summary>
/// Points the app's data root at a temporary folder for the whole test assembly, so no test can read or
/// write the real %APPDATA%\TSCC_WASD (profiles, settings, hidhide-restore.json, logs).
/// </summary>
internal static class TestIsolation
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", "appdata-" + Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Initialize()
    {
        Directory.CreateDirectory(Root);
        AppPaths.RootOverride = Root;
    }
}

public class TestIsolationTests
{
    [Fact]
    public void DataDirectoryAndLogNeverPointAtTheRealAppData()
    {
        string real = Path.GetFullPath(AppPaths.RealDataDirectory);
        static bool IsReal(string path, string real)
        {
            string full = Path.GetFullPath(path);
            return full.Equals(real, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(real + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        Assert.False(IsReal(AppPaths.DataDirectory, real));
        Assert.False(IsReal(AppPaths.File("hidhide-restore.json"), real));
        Assert.False(IsReal(AppLog.Directory, real));
        Assert.StartsWith(Path.GetFullPath(TestIsolation.Root), Path.GetFullPath(AppPaths.DataDirectory), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultHidHideRecordPathIsUnderTheIsolatedRoot()
    {
        var guard = new TSCC_WASD.Core.Services.Setup.HidHideGuard(_ => throw new InvalidOperationException("no CLI in tests"));
        Assert.False(guard.HasPendingRestore);
        Assert.False(File.Exists(AppPaths.File("hidhide-restore.json")));
    }
}
