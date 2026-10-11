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
}
