using Xunit;
using System.Net.Http;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

public class UpdateCheckerTests
{
    [Fact]
    public async Task ChecksReleasesUnderTheCurrentOwner()
    {
        using var handler = new ReleaseHandler();
        using var client = new HttpClient(handler);
        Assert.Null(await UpdateChecker.GetLatestAsync(client));
        Assert.Equal("https://api.github.com/repos/ynyrworks/TSCC_WASD/releases?per_page=20", handler.Url);
        Assert.Equal("https://github.com/ynyrworks/TSCC_WASD/releases", UpdateChecker.ReleasesPage);
    }

    private sealed class ReleaseHandler : HttpMessageHandler
    {
        public string? Url { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Url = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }

    private const string Releases = """
        [
          { "tag_name": "v0.5.0", "draft": true,  "prerelease": true,  "html_url": "https://x/draft" },
          { "tag_name": "v0.4.1", "draft": false, "prerelease": true,  "html_url": "https://x/0.4.1" },
          { "tag_name": "nightly", "draft": false, "prerelease": true, "html_url": "https://x/nightly" },
          { "tag_name": "v0.4.0", "draft": false, "prerelease": true,  "html_url": "https://x/0.4.0" }
        ]
        """;

    [Fact]
    public void PicksHighestPublishedVersionIgnoringDraftsAndOddTags()
    {
        var latest = UpdateChecker.FindLatest(Releases)!;
        Assert.Equal(new Version(0, 4, 1), latest.Version);
        Assert.Equal("https://x/0.4.1", latest.Url);
        Assert.True(latest.Prerelease);
    }

    [Theory]
    [InlineData("0.4.0.0", true)]
    [InlineData("0.4.1.0", false)]
    [InlineData("0.5.0.0", false)]
    public void ComparesAgainstTheRunningVersion(string current, bool newer)
        => Assert.Equal(newer, UpdateChecker.IsNewer(UpdateChecker.FindLatest(Releases)!, Version.Parse(current)));

    [Fact]
    public void NoReleasesMeansNoUpdate() => Assert.Null(UpdateChecker.FindLatest("[]"));

    private const string Download = "https://github.com/ynyrworks/TSCC_WASD/releases/download";
    private static readonly string Digest = new('a', 64);

    private static readonly string WithAssets = $$"""
        [
          { "tag_name": "v0.9.2", "draft": false, "prerelease": true, "html_url": "https://x/0.9.2", "assets": [
            { "name": "TSCC_WASD-0.9.2-win-x64.zip.sha256", "size": 90,
              "browser_download_url": "{{Download}}/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip.sha256" },
            { "name": "TSCC_WASD-0.9.2-win-x64.zip", "size": 1234, "digest": "sha256:{{Digest}}",
              "browser_download_url": "{{Download}}/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip" },
            { "name": "TSCC_WASD-0.9.2-win-arm64.zip", "size": 1,
              "browser_download_url": "{{Download}}/v0.9.2/TSCC_WASD-0.9.2-win-arm64.zip" }
          ] }
        ]
        """;

    [Fact]
    public void SelectsTheWinX64ZipAndItsChecksum()
    {
        var release = UpdateChecker.FindLatest(WithAssets)!;
        var package = UpdateChecker.SelectPackage(release)!;
        Assert.Equal("TSCC_WASD-0.9.2-win-x64.zip", package.Zip.Name);
        Assert.Equal(1234, package.Zip.Size);
        Assert.Equal(Digest, package.Zip.Sha256);
        Assert.Equal("TSCC_WASD-0.9.2-win-x64.zip.sha256", package.Checksum!.Name);
        Assert.Null(package.Checksum.Sha256);
    }

    [Fact]
    public void ReleaseWithoutAssetsHasNoPackage()
        => Assert.Null(UpdateChecker.SelectPackage(UpdateChecker.FindLatest(Releases)!));

    [Fact]
    public void ZipServedFromElsewhereIsIgnored()
    {
        var release = UpdateChecker.FindLatest(WithAssets)!;
        var moved = release with
        {
            Assets = release.Assets.Select(a => a with { Url = a.Url.Replace("ynyrworks/TSCC_WASD", "someone/TSCC_WASD") }).ToList()
        };
        Assert.Null(UpdateChecker.SelectPackage(moved));
    }

    [Fact]
    public void ChecksumServedFromElsewhereIsDropped()
    {
        var release = UpdateChecker.FindLatest(WithAssets)!;
        var moved = release with
        {
            Assets = release.Assets.Select(a => a.Name.EndsWith(".sha256") ? a with { Url = "https://example.com/x.sha256" } : a).ToList()
        };
        var package = UpdateChecker.SelectPackage(moved)!;
        Assert.Null(package.Checksum);
        Assert.Equal(Digest, package.Zip.Sha256); // GitHub's digest still verifies the ZIP.
    }

    [Theory]
    [InlineData(Download + "/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", true)]
    [InlineData("https://GitHub.com/ynyrworks/TSCC_WASD/releases/download/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", true)]
    [InlineData("http://github.com/ynyrworks/TSCC_WASD/releases/download/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("https://github.com:8443/ynyrworks/TSCC_WASD/releases/download/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("https://user@github.com/ynyrworks/TSCC_WASD/releases/download/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("https://github.com.evil.test/ynyrworks/TSCC_WASD/releases/download/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("https://github.com/ynyrworks/TSCC_WASD/releases/download/v0.9.1/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("https://github.com/ynyrworks/TSCC_WASD/releases/download/v0.9.2/../v0.9.1/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData(Download + "/v0.9.2/TSCC_WASD-0.9.2-win-x64.zip?x=1", false)]
    [InlineData("https://objects.githubusercontent.com/TSCC_WASD-0.9.2-win-x64.zip", false)]
    [InlineData("not a url", false)]
    public void OnlyAcceptsThisRepositorysReleaseDownloads(string url, bool trusted)
        => Assert.Equal(trusted, UpdateChecker.IsReleaseDownload(url, "v0.9.2", "TSCC_WASD-0.9.2-win-x64.zip"));

    [Fact]
    public void TwoPartTagsCompareAsThreePartVersions()
    {
        var latest = UpdateChecker.FindLatest("""[{ "tag_name": "v1.0", "html_url": "https://x/1.0" }]""")!;
        Assert.True(UpdateChecker.IsNewer(latest, new Version(0, 9, 1, 0)));
        Assert.False(UpdateChecker.IsNewer(latest, new Version(1, 0, 0, 0)));
        Assert.Equal("TSCC_WASD-1.0.0-win-x64.zip", UpdateChecker.PackageFileName(latest.Version));
    }
}
