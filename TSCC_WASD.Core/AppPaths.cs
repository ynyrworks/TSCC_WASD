namespace TSCC_WASD.Core;

/// <summary>
/// Where TSCC_WASD keeps profiles, settings, calibration and the HidHide restore record.
/// The project was called YnyrWASD before 0.4.1; its data folder is moved over once.
/// </summary>
public static class AppPaths
{
    public const string Name = "TSCC_WASD";
    /// <summary>Name used before the rename (data folder, Run value, single-instance mutex).</summary>
    public const string LegacyName = "YnyrWASD";

    private static string RealAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>Tests set this to a temporary folder so nothing touches the real %APPDATA%.</summary>
    public static string? RootOverride { get; set; }

    private static string AppData => RootOverride ?? RealAppData;

    public static string DataDirectory => Path.Combine(AppData, Name);

    /// <summary>The user's real data folder, whatever <see cref="RootOverride"/> says.</summary>
    public static string RealDataDirectory => Path.Combine(RealAppData, Name);

    public static string File(string fileName) => Path.Combine(DataDirectory, fileName);

    /// <summary>
    /// Moves %APPDATA%\YnyrWASD to %APPDATA%\TSCC_WASD if only the old folder exists.
    /// Returns true when data was moved. Never merges or overwrites an existing new folder.
    /// </summary>
    /// <param name="root">Folder holding both data folders; defaults to %APPDATA%.</param>
    public static bool MigrateLegacyData(string? root = null)
    {
        root ??= AppData;
        string legacy = Path.Combine(root, LegacyName), current = Path.Combine(root, Name);
        try
        {
            if (!Directory.Exists(legacy) || Directory.Exists(current)) return false;
            Directory.Move(legacy, current);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // The old folder stays intact; defaults are used instead.
        }
    }
}
