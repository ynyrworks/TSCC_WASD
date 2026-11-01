using System.ComponentModel;
using System.Text.Json;
using Xunit;
using TSCC_WASD.Core.Models;

namespace TSCC_WASD.Tests;

public class MatchProcessNameTests
{
    [Fact]
    public void SettingRaisesPropertyChangedOnlyOnChange()
    {
        var profile = new MappingProfile();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)profile).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        profile.MatchProcessName = "game";
        profile.MatchProcessName = "game";
        Assert.Equal([nameof(MappingProfile.MatchProcessName)], raised);
    }

    [Fact]
    public void ValidateLimitsLengthTo260()
    {
        var profile = new MappingProfile { MatchProcessName = new string('a', 260) };
        profile.Validate();
        profile.MatchProcessName = new string('a', 261);
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void SnapshotKeepsNameAndNullIsNotSerialized()
    {
        Assert.Equal("game", new MappingProfile { MatchProcessName = "game" }.Snapshot().MatchProcessName);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        Assert.DoesNotContain("matchProcessName", JsonSerializer.Serialize(new MappingProfile(), options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameBecomesNullAndIsNotSerialized(string blank)
    {
        var profile = new MappingProfile { MatchProcessName = "game" };
        profile.MatchProcessName = blank;
        Assert.Null(profile.MatchProcessName);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        Assert.DoesNotContain("matchProcessName", JsonSerializer.Serialize(profile, options));
    }
}
