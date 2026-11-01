using System.IO;
using System.Text.Json;
using Xunit;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

public class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "profiles.json");

    [Fact]
    public void RoundTripAndBackupPreservePreviousVersion()
    {
        var store = new ProfileStore(FilePath);
        var profiles = store.LoadProfiles();
        string original = File.ReadAllText(FilePath);
        profiles[0].Name = "中文設定";
        profiles[0].DeadZone = 0.25;
        store.SaveProfiles(profiles);
        Assert.Equal("中文設定", store.LoadProfiles()[0].Name);
        Assert.Equal(0.25, store.LoadProfiles()[0].DeadZone);
        Assert.Equal(original, File.ReadAllText(FilePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void MalformedFileIsNeverOverwritten()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ broken json");
        Assert.Throws<JsonException>(() => new ProfileStore(FilePath).LoadProfiles());
        Assert.Equal("{ broken json", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("[null]")]
    public void EmptyAndNullProfilesRejected(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, json);
        Assert.Throws<InvalidDataException>(() => new ProfileStore(FilePath).LoadProfiles());
        Assert.Equal(json, File.ReadAllText(FilePath));
    }

    [Fact]
    public void InvalidSaveLeavesExistingFileUntouched()
    {
        var store = new ProfileStore(FilePath);
        var profiles = store.LoadProfiles();
        string original = File.ReadAllText(FilePath);
        profiles[0].PollingRateHz = 0;
        Assert.Throws<ArgumentException>(() => store.SaveProfiles(profiles));
        Assert.Equal(original, File.ReadAllText(FilePath));
    }

    [Fact]
    public void DuplicateIdsRejected()
    {
        Assert.Throws<InvalidDataException>(() => new ProfileStore(FilePath).SaveProfiles(
            new[] { new MappingProfile { Id = "same" }, new MappingProfile { Id = "same" } }));
    }

    [Fact]
    public void LegacyNumericEnumsRemainReadable()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "[{\"id\":\"old\",\"name\":\"old\",\"inputType\":0,\"outputType\":0}]");
        Assert.Equal(OutputControllerType.DualShock4, new ProfileStore(FilePath).LoadProfiles()[0].OutputType);
    }

    [Fact]
    public void SessionSnapshotIsIndependent()
    {
        var original = new MappingProfile();
        var copy = original.Snapshot();
        original.DeadZone = 0.9;
        Assert.Equal(0.08, copy.DeadZone);
    }

    [Fact]
    public void RemovedLegacyKeysAreReadableAndDroppedOnSave()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath,
            "[{\"id\":\"old\",\"name\":\"old\",\"requireDrivers\":true,\"advanced\":{\"k\":\"v\"}}]");
        var store = new ProfileStore(FilePath);
        var profiles = store.LoadProfiles();
        Assert.Equal("old", profiles[0].Name);
        store.SaveProfiles(profiles);
        string saved = File.ReadAllText(FilePath);
        Assert.DoesNotContain("requireDrivers", saved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("advanced", saved, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
