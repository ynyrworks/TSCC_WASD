using Xunit;
using TSCC_WASD.App;

namespace TSCC_WASD.Tests;

public class AboutInfoTests
{
    [Theory]
    [InlineData("0.99.1", true)]
    [InlineData("0.99.0", true)]
    [InlineData("1.0.0-rc.1", true)]
    [InlineData("1.0.0-rc", true)]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.1", false)]
    [InlineData("1.2.0", false)]
    [InlineData("2.0.0", false)]
    [InlineData("1.0.0+abc", false)]
    public void PreviewRule(string version, bool preview) => Assert.Equal(preview, AboutInfo.IsPreview(version));

    [Fact]
    public void ChannelTextFollowsLanguage()
    {
        Assert.Equal("預覽版", AboutInfo.ChannelText("0.99.1", true));
        Assert.Equal("Preview", AboutInfo.ChannelText("1.0.0-rc.1", false));
        Assert.Equal("正式版", AboutInfo.ChannelText("1.0.0", true));
        Assert.Equal("Stable", AboutInfo.ChannelText("1.0.0", false));
    }

    [Fact]
    public void ManualUrlFollowsLanguage()
    {
        Assert.Equal("https://github.com/ynyrworks/TSCC_WASD/blob/main/docs/MANUAL.zh-TW.md", AboutInfo.ManualUrl(true));
        Assert.Equal("https://github.com/ynyrworks/TSCC_WASD/blob/main/docs/MANUAL.md", AboutInfo.ManualUrl(false));
    }

    [Theory]
    [InlineData(true, "MANUAL.zh-TW.md")]
    [InlineData(false, "MANUAL.md")]
    public void HelpUrlEqualsAboutManualUrl(bool chinese, string file)
    {
        Assert.Equal(AboutInfo.ManualUrl(chinese), AboutInfo.HelpUrl(chinese));
        Assert.EndsWith("/docs/" + file, AboutInfo.HelpUrl(chinese));
    }

    [Fact]
    public void SigningPolicyLinkPointsAtTheDoc() =>
        Assert.Equal("https://github.com/ynyrworks/TSCC_WASD/blob/main/docs/CODE-SIGNING-POLICY.md", AboutInfo.SigningPolicyUrl);
}
