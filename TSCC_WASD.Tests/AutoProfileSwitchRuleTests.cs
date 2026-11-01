using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Mapping;
using Xunit;

namespace TSCC_WASD.Tests;

public class AutoProfileSwitchRuleTests
{
    private static MappingProfile P(string id, string? match = null) => new() { Id = id, Name = id, MatchProcessName = match };

    private readonly List<MappingProfile> _profiles = [P("home"), P("a", "GameA.exe"), P("b", "gameb"), P("a2", "gamea")];
    private readonly HashSet<int> _alive = [];
    private readonly AutoProfileSwitchRule _rule = new();
    private string _current = "home";

    private string? Step(string? name, int pid = 0)
    {
        if (pid != 0) _alive.Add(pid);
        var target = _rule.Evaluate(_profiles, _current, name is null ? null : new ForegroundProcess(name, pid), _alive.Contains);
        if (target is not null) _current = target;
        return target;
    }

    private void Manual(string id) { _current = id; _rule.NotifyManualSelection(); }

    [Fact]
    public void SwitchesWhenGameComesToForeground()
    {
        Assert.Null(Step("explorer.exe", 1));
        Assert.Equal("a", Step("GameA.exe", 10));
        Assert.Equal("a", _current);
    }

    [Fact]
    public void AltTabAwayDoesNotSwitchBack()
    {
        Step("GameA.exe", 10);
        Assert.Null(Step("explorer.exe", 1));
        Assert.Null(Step("explorer.exe", 1));
        Assert.Equal("a", _current);
    }

    [Fact]
    public void RestoresOriginalOnlyWhenGameEnds()
    {
        Step("GameA.exe", 10);
        Step("explorer.exe", 1);
        Assert.Null(Step("explorer.exe", 1));
        _alive.Remove(10);
        Assert.Equal("home", Step("explorer.exe", 1));
        Assert.Null(Step("explorer.exe", 1));
    }

    [Fact]
    public void NoRepeatedSwitchWhenAlreadyOnProfile()
    {
        Assert.Equal("a", Step("GameA.exe", 10));
        Assert.Null(Step("GameA.exe", 10));
        Assert.Null(Step("GameA.exe", 10));
    }

    [Fact]
    public void GameAToGameBRestoresOriginalNotA()
    {
        Step("GameA.exe", 10);
        Assert.Equal("b", Step("gameb", 20));
        _alive.Remove(10);
        Assert.Null(Step("gameb", 20));
        _alive.Remove(20);
        Assert.Equal("home", Step("explorer", 1));
    }

    [Theory]
    [InlineData("GAMEA")]
    [InlineData("gamea.EXE")]
    [InlineData(@"C:\Games\Sub Dir\GameA.exe")]
    [InlineData("D:/x/gamea")]
    public void MatchesNameIgnoringCaseExtensionAndPath(string foreground)
    {
        Assert.Equal("a", Step(foreground, 10));
    }

    [Fact]
    public void FirstProfileWinsForDuplicateNames()
    {
        Assert.Equal("a", Step("gamea", 10));
    }

    [Fact]
    public void BlankMatchValuesAreIgnored()
    {
        _profiles.Add(P("blank", "  "));
        _profiles.Add(P("empty", ""));
        Assert.Null(Step("", 5));
        Assert.Null(Step(" ", 5));
        Assert.False(AutoProfileSwitchRule.HasAnyMatch([P("x"), P("y", " ")]));
        Assert.True(AutoProfileSwitchRule.HasAnyMatch(_profiles));
    }

    [Fact]
    public void OwnProcessIsNeverAGame()
    {
        _profiles.Add(P("self", "TSCC_WASD"));
        Assert.Null(Step("TSCC_WASD.exe", 99));
        Assert.Null(Step(@"C:\x\tscc_wasd.exe", 99));
    }

    [Fact]
    public void UnreadableForegroundIsNoMatch()
    {
        Assert.Null(Step(null));
        Step("GameA.exe", 10);
        Assert.Null(Step(null));
        Assert.Equal("a", _current);
    }

    [Fact]
    public void ManualSelectionIsKeptWhenGameEnds()
    {
        Step("GameA.exe", 10);
        Manual("b");
        _alive.Remove(10);
        Assert.Null(Step("explorer", 1));
        Assert.Equal("b", _current);
        // The next game starts fresh and remembers the manual choice as the original.
        Assert.Equal("a", Step("GameA.exe", 11));
        _alive.Remove(11);
        Assert.Equal("b", Step("explorer", 1));
    }

    [Fact]
    public void ManualSelectionWinsWhileGameStaysForeground()
    {
        Step("GameA.exe", 10);
        Manual("b");
        Assert.Null(Step("GameA.exe", 10));
        Assert.Equal("b", _current);
    }

    [Fact]
    public void NextGameForegroundAfterManualSelectionSwitchesAgain()
    {
        Step("GameA.exe", 10);
        Step("TSCC_WASD", 99);
        Manual("b");
        Assert.Null(Step("GameA.exe", 10));
        Assert.Equal("b", _current);
        Assert.Equal("a", Step("GameA.exe", 11));
    }

    [Fact]
    public void ManualSelectionSurvivesAltTabBackToSameGame()
    {
        Step("GameA.exe", 10);
        Step("explorer", 1);
        Manual("b");
        Assert.Null(Step("explorer", 1));
        Assert.Null(Step("GameA.exe", 10));
        Assert.Equal("b", _current);
        _alive.Remove(10);
        Assert.Null(Step("explorer", 1));
        Assert.Equal("b", _current);
    }

    [Fact]
    public void ManualSelectionEndsWhenAnotherUntrackedGameComesToForeground()
    {
        Step("GameA.exe", 10);
        Manual("a2");
        Assert.Equal("b", Step("gameb", 20));
    }

    [Fact]
    public void OriginalSurvivesGameEndingWhileNextGameAlreadyInForeground()
    {
        Step("GameA.exe", 10);
        Step("explorer", 1);
        _alive.Add(20);
        _alive.Remove(10);
        Assert.Equal("b", Step("gameb", 20));
        _alive.Remove(20);
        Assert.Equal("home", Step("explorer", 1));
    }

    [Fact]
    public void StopRestoresOriginalAndResetsState()
    {
        Step("GameA.exe", 10);
        Assert.Equal("home", _rule.Stop(_profiles, _current));
        Assert.False(_rule.IsTracking);
        _current = "home";
        Assert.Null(_rule.Stop(_profiles, _current));
        _alive.Remove(10);
        Assert.Null(Step("explorer", 1));
    }

    [Fact]
    public void StopWithoutGameChangesNothing()
    {
        Assert.Null(_rule.Stop(_profiles, _current));
    }

    [Fact]
    public void DeletedOriginalDoesNotThrowOrSwitch()
    {
        Step("GameA.exe", 10);
        _profiles.RemoveAll(p => p.Id == "home");
        _alive.Remove(10);
        Assert.Null(Step("explorer", 1));
        Step("GameA.exe", 11);
        _profiles.RemoveAll(p => p.Id == "home");
        Assert.Null(_rule.Stop(_profiles, _current));
    }

    [Fact]
    public void DeletedTargetDoesNotThrow()
    {
        _profiles.RemoveAll(p => p.Id == "a" || p.Id == "a2");
        Assert.Null(Step("GameA.exe", 10));
        Assert.Equal("home", _current);
    }

    [Theory]
    [InlineData(OutputControllerType.DualShock4, OutputControllerType.Xbox360, true, true)]
    [InlineData(OutputControllerType.Xbox360, OutputControllerType.DualShock4, true, true)]
    [InlineData(OutputControllerType.Xbox360, OutputControllerType.Xbox360, true, false)]
    [InlineData(OutputControllerType.DualShock4, OutputControllerType.Xbox360, false, false)]
    [InlineData(OutputControllerType.DualShock4, OutputControllerType.DualShock4, false, false)]
    public void OutputChangeNoticeOnlyForAutomaticTypeChange(OutputControllerType oldType, OutputControllerType newType, bool automatic, bool expected)
    {
        Assert.Equal(expected, AutoProfileSwitchRule.ShouldNotifyOutputChange(oldType, newType, automatic));
    }
}
