using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Xunit;

namespace TSCC_WASD.Tests;

public class AppIconTests
{
    private const string Png = "Assets/TSCC_WASD_256.png";

    private static string AppDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TSCC_WASD.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "TSCC_WASD.App");
    }

    [Theory]
    [InlineData("Pages/HomePage.xaml")]
    [InlineData("Pages/AboutPage.xaml")]
    public void HeaderImageUsesSharpPng(string xaml)
    {
        var text = File.ReadAllText(Path.Combine(AppDir(), xaml));
        var m = Regex.Match(text, "<Image Source=\"(/TSCC_WASD;component/Assets/TSCC_WASD[^\"]*)\"[^>]*>");
        Assert.True(m.Success);
        Assert.Equal("/TSCC_WASD;component/" + Png, m.Groups[1].Value);
        Assert.Contains("BitmapScalingMode=\"HighQuality\"", m.Value);
    }

    [Fact]
    public void PngIs256AndEmbeddedAsResource()
    {
        var path = Path.Combine(AppDir(), "Assets", "TSCC_WASD_256.png");
        using var fs = File.OpenRead(path);
        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(256, frame.PixelWidth);
        Assert.Equal(256, frame.PixelHeight);
        Assert.Contains("TSCC_WASD_256.png", File.ReadAllText(Path.Combine(AppDir(), "TSCC_WASD.App.csproj")));

        // The same file must be a loadable resource of the built app assembly.
        var asm = typeof(TSCC_WASD.App.AboutInfo).Assembly;
        var resName = asm.GetName().Name + ".g.resources";
        using var rs = asm.GetManifestResourceStream(resName)!;
        using var reader = new System.Resources.ResourceReader(rs);
        var names = reader.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToList();
        Assert.Contains("assets/tscc_wasd_256.png", names);
    }
}
