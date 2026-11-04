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
        Assert.True(filter.ShouldLog("等待手把（虛擬 DualShock 4 已連線）：a", T0));
        Assert.False(filter.ShouldLog("等待手把（虛擬 DualShock 4 已連線）：a", T0.AddSeconds(59)));
        Assert.True(filter.ShouldLog("等待手把（虛擬 DualShock 4 已連線）：a", T0.AddSeconds(60)));
    }

    [Fact]
    public void AlternatingTextsDoNotFloodTheLog()
    {
        var filter = new StatusLogFilter();
        int logged = 0;
        for (int second = 0; second < 120; second++)
            if (filter.ShouldLog(second % 2 == 0 ? "等待手把（虛擬 DualShock 4 已連線）：a" : "Waiting for a controller (virtual DualShock 4 connected): b", T0.AddSeconds(second))) logged++;
        Assert.Equal(4, logged); // a and b, each at 0 s and again after 60 s.
    }

    [Fact]
    public void DifferentTextsAreAllLogged()
    {
        var filter = new StatusLogFilter();
        Assert.True(filter.ShouldLog("等待手把（虛擬 DualShock 4 已連線）：a", T0));
        Assert.True(filter.ShouldLog("Waiting for a controller (virtual DualShock 4 connected): b", T0));
    }

    [Fact]
    public void OneOffEventsAreLoggedEveryTime()
    {
        var filter = new StatusLogFilter();
        Assert.True(filter.ShouldLog("映射已停止。", T0));
        Assert.True(filter.ShouldLog("映射已停止。", T0.AddSeconds(43)));
        Assert.True(filter.ShouldLog("Mapping stopped.", T0.AddSeconds(44)));
        Assert.True(filter.ShouldLog("Mapping stopped.", T0.AddSeconds(45)));
    }
}
