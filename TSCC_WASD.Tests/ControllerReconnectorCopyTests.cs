using System.IO;
using Xunit;
using TSCC_WASD.Core.Services.Setup;

namespace TSCC_WASD.Tests;

public class ControllerReconnectorCopyTests
{
    [Fact]
    public void CopyLeavesOnlyTheCurrentBuildAndRunTimeFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "src"), helper = Path.Combine(root, "Helper");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "docs"));
            File.WriteAllText(Path.Combine(source, "TSCC_WASD.exe"), "new");
            File.WriteAllText(Path.Combine(source, "docs", "skipped.txt"), "x");

            Directory.CreateDirectory(Path.Combine(helper, "win-x64", "native"));
            Directory.CreateDirectory(Path.Combine(helper, "docs"));
            foreach (var stale in new[] { "TSCC_WASD.dll", "TSCC_WASD.Core.dll", "TSCC_WASD.pdb", "TSCC_WASD.deps.json",
                         "TSCC_WASD.runtimeconfig.json", "Nefarius.ViGEm.Client.dll", Path.Combine("win-x64", "native", "a.dll"),
                         Path.Combine("docs", "old.txt") })
                File.WriteAllText(Path.Combine(helper, stale), "old");
            File.WriteAllText(Path.Combine(helper, "TSCC_WASD.exe"), "old");
            File.WriteAllText(Path.Combine(helper, "last-reconnect.json"), "{}");

            ControllerReconnector.CopyApp(source, helper);

            var left = Directory.EnumerateFileSystemEntries(helper, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(helper, p)).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(["TSCC_WASD.exe", "last-reconnect.json"], left);
            Assert.Equal("new", File.ReadAllText(Path.Combine(helper, "TSCC_WASD.exe")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(helper, "last-reconnect.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
