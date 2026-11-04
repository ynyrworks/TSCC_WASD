using Xunit;
using TSCC_WASD.Core.Services.Diagnostics;

namespace TSCC_WASD.Tests;

public class StatusLogFilterTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SameTextIsLoggedOncePerWindow()
    {
        var filter = new StatusLogFilter();
        Assert.True(filter.ShouldLog("a", T0));
        Assert.False(filter.ShouldLog("a", T0.AddSeconds(59)));
        Assert.True(filter.ShouldLog("a", T0.AddSeconds(60)));
    }

    [Fact]
    public void AlternatingTextsDoNotFloodTheLog()
    {
        var filter = new StatusLogFilter();
        int logged = 0;
        for (int second = 0; second < 120; second++)
            if (filter.ShouldLog(second % 2 == 0 ? "a" : "b", T0.AddSeconds(second))) logged++;
        Assert.Equal(4, logged); // a and b, each at 0 s and again after 60 s.
    }

    [Fact]
    public void DifferentTextsAreAllLogged()
    {
        var filter = new StatusLogFilter();
        Assert.True(filter.ShouldLog("a", T0));
        Assert.True(filter.ShouldLog("b", T0));
    }
}
