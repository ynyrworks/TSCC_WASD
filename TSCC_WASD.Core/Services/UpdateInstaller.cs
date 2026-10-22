using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace TSCC_WASD.Core.Services;

/// <summary>A downloaded release ZIP whose SHA-256 matched, unpacked and ready to copy over this install.</summary>
public sealed record DownloadedUpdate(Version Version, string Tag, string PackageDirectory);

/// <summary>A downloaded update failed a check; whatever was downloaded has been deleted.</summary>
public sealed class UpdateVerificationException(string message) : IOException(message);

/// <summary>
/// Downloads, verifies and installs a release ZIP in place of the running copy. Only runs when
/// the user agrees to install an update. A running EXE can be renamed but not overwritten, so the
/// current one is moved to <c>TSCC_WASD.exe.old</c>, the new files are copied in, and the new
/// version is started; it deletes the old EXE once this process has exited.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UpdateInstaller
{
    public const string ExecutableName = AppPaths.Name + ".exe";
    /// <summary>Passed to the new version with this process ID, so it waits for the old one to exit.</summary>
    public const string UpdatedFromArgument = "--updated-from";
    private const string BackupSuffix = ".old";
    private const long MaxPackageBytes = 512L * 1024 * 1024;
    private const int MaxChecksumBytes = 4096;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static string WorkDirectory => Path.Combine(Path.GetTempPath(), AppPaths.Name, "update");

    private static string? AppDirectory => Path.GetDirectoryName(Environment.ProcessPath);

    /// <summary>Why this copy can't replace itself (developer build, read-only folder), or null when it can.</summary>
    public static string? InPlaceProblem()
    {
        var dir = AppDirectory;
        if (dir is null || !Path.GetFileName(Environment.ProcessPath)!.Equals(ExecutableName, StringComparison.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(dir, AppPaths.Name + ".Core.dll")))
            return L.T("這不是發行版 ZIP 的 TSCC_WASD（例如開發版本），無法在程式內更新。",
                "This TSCC_WASD is not from a release ZIP (for example a developer build), so it can't update itself.");
        try
        {
            string probe = Path.Combine(dir, $".update-check-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return L.T($"無法寫入程式資料夾 {dir}（例如放在 Program Files），無法在程式內更新。請把 TSCC_WASD 資料夾移到自己的資料夾（例如「文件」）。",
                $"Can't write to the program folder {dir} (for example under Program Files), so TSCC_WASD can't update itself. Move the TSCC_WASD folder somewhere of your own, such as Documents.");
        }
    }

    /// <summary>Downloads and unpacks the release, then checks the new EXE's version and signature.</summary>
    public static async Task<DownloadedUpdate> PrepareAsync(UpdatePackage package, IProgress<double>? progress = null, CancellationToken token = default)
    {
        var update = await DownloadAsync(package, progress, token).ConfigureAwait(false);
        var current = Environment.ProcessPath is { } path ? Authenticode.Check(path) : new SignatureInfo(SignatureStatus.Unsigned);
        try { VerifyExecutable(Path.Combine(update.PackageDirectory, ExecutableName), update.Version, current); }
        catch (UpdateVerificationException)
        {
            TryDeleteDirectory(Path.Combine(WorkDirectory, update.Version.ToString(3)));
            throw;
        }
        return update;
    }

    /// <summary>
    /// Downloads the release ZIP (reusing an earlier download whose hash still matches), checks its
    /// SHA-256 against the release's .sha256 file and GitHub's digest, and unpacks it.
    /// </summary>
    public static async Task<DownloadedUpdate> DownloadAsync(UpdatePackage package, IProgress<double>? progress = null,
        CancellationToken token = default, HttpClient? client = null, string? workDirectory = null)
    {
        var release = package.Release;
        var http = client ?? Http;
        string dir = Path.Combine(workDirectory ?? WorkDirectory, release.Version.ToString(3));
        Directory.CreateDirectory(dir);
        string expected = await ExpectedHashAsync(http, package, token).ConfigureAwait(false);
        string zip = Path.Combine(dir, UpdateChecker.PackageFileName(release.Version));
        if (!(File.Exists(zip) && HashMatches(zip, expected)))
        {
            string partial = zip + ".part";
            try { await DownloadFileAsync(http, package.Zip, partial, progress, token).ConfigureAwait(false); }
            catch
            {
                File.Delete(partial);
                throw;
            }
            if (!HashMatches(partial, expected))
            {
                File.Delete(partial);
                throw new UpdateVerificationException(L.T($"下載的 {package.Zip.Name} 與發行版公布的 SHA-256 不符，已刪除，沒有安裝任何東西。",
                    $"The downloaded {package.Zip.Name} does not match the release's SHA-256. It was deleted and nothing was installed."));
            }
            File.Move(partial, zip, overwrite: true);
        }
        progress?.Report(1);

        string extracted = Path.Combine(dir, "package");
        if (Directory.Exists(extracted)) Directory.Delete(extracted, recursive: true);
        try
        {
            // ExtractToDirectory refuses entries that would land outside the target folder.
            ZipFile.ExtractToDirectory(zip, extracted);
        }
        catch (InvalidDataException)
        {
            TryDeleteDirectory(dir);
            throw new UpdateVerificationException(L.T("下載的更新檔無法解壓縮，已刪除。", "The downloaded update could not be unpacked and was deleted."));
        }
        string packageName = Path.GetFileNameWithoutExtension(zip);
        string? packageDir = new[] { Path.Combine(extracted, packageName), extracted }
            .FirstOrDefault(d => File.Exists(Path.Combine(d, ExecutableName)));
        if (packageDir is null)
        {
            TryDeleteDirectory(dir);
            throw new UpdateVerificationException(L.T($"下載的更新檔裡沒有 {ExecutableName}，已刪除。",
                $"The downloaded update has no {ExecutableName}. It was deleted."));
        }
        return new DownloadedUpdate(release.Version, release.Tag, packageDir);
    }

    /// <summary>The ZIP's SHA-256 from the .sha256 asset and/or GitHub's digest; they must agree, and at least one must exist.</summary>
    private static async Task<string> ExpectedHashAsync(HttpClient http, UpdatePackage package, CancellationToken token)
    {
        string? fromFile = null;
        if (package.Checksum is { } checksum)
        {
            using var response = await GetAsync(http, checksum.Url, token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > MaxChecksumBytes) throw ChecksumUnreadable();
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            fromFile = (text.Length <= MaxChecksumBytes ? ParseChecksum(text, package.Zip.Name) : null) ?? throw ChecksumUnreadable();
        }
        string? digest = package.Zip.Sha256 is { } d && IsSha256(d) ? d : null;
        if (fromFile is not null && digest is not null && !fromFile.Equals(digest, StringComparison.OrdinalIgnoreCase))
            throw new UpdateVerificationException(L.T("發行版的 .sha256 檔與 GitHub 記錄的檔案雜湊不一致，沒有下載更新。",
                "The release's .sha256 file and GitHub's recorded file hash disagree, so the update was not downloaded."));
        return fromFile ?? digest ?? throw new UpdateVerificationException(L.T(
            "這個發行版沒有提供 SHA-256，無法驗證下載內容，所以不在程式內安裝。",
            "This release publishes no SHA-256, so the download can't be verified and won't be installed from TSCC_WASD."));

        static UpdateVerificationException ChecksumUnreadable() => new(L.T(
            "無法讀取發行版的 .sha256 檔，沒有下載更新。", "The release's .sha256 file could not be read, so the update was not downloaded."));
    }

    /// <summary>
    /// Reads a <c>sha256sum</c>-style line ("hash  file.zip", as package.ps1 writes it). The hash
    /// must be 64 hex digits, and a file name, when given, must be the ZIP's.
    /// </summary>
    public static string? ParseChecksum(string text, string fileName)
    {
        var line = text.TrimStart('﻿').Split('\n', 2)[0].Trim();
        var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !IsSha256(parts[0])) return null;
        if (parts.Length == 2 && !parts[1].Trim().TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase)) return null;
        return parts[0].ToLowerInvariant();
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool HashMatches(string path, string sha256)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient http, string url, CancellationToken token)
    {
        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            // GitHub redirects release downloads to its own storage; never accept a plain-HTTP hop.
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new UpdateVerificationException(L.T("更新下載被轉向到非 HTTPS 網址，已停止。", "The update download was redirected away from HTTPS and was stopped."));
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task DownloadFileAsync(HttpClient http, ReleaseAsset asset, string target, IProgress<double>? progress, CancellationToken token)
    {
        using var response = await GetAsync(http, asset.Url, token).ConfigureAwait(false);
        long total = response.Content.Headers.ContentLength ?? asset.Size;
        long limit = asset.Size > 0 ? asset.Size : MaxPackageBytes;
        if (total > limit || limit > MaxPackageBytes) throw TooLarge();
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var file = File.Create(target);
        var buffer = new byte[81920];
        long done = 0;
        int lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            done += read;
            if (done > limit) throw TooLarge();
            await file.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            if (total > 0 && (int)(done * 100 / total) is var percent && percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(Math.Min(1, (double)done / total));
            }
        }
        if (asset.Size > 0 && done != asset.Size)
            throw new UpdateVerificationException(L.T("下載的更新檔大小與發行版不符，已刪除。", "The downloaded update's size does not match the release. It was deleted."));

        static UpdateVerificationException TooLarge() => new(L.T("更新檔比發行版公布的大小還大，已停止下載。",
            "The update is larger than the release says it is, so the download was stopped."));
    }

    /// <summary>The unpacked EXE must carry the release's version and keep (or gain) a valid signature.</summary>
    public static void VerifyExecutable(string exe, Version expected, SignatureInfo current)
    {
        var info = FileVersionInfo.GetVersionInfo(exe);
        var actual = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
        if (actual != expected)
            throw new UpdateVerificationException(L.T($"下載的 {ExecutableName} 版本是 {actual}，不是 {expected.ToString(3)}，沒有安裝。",
                $"The downloaded {ExecutableName} is version {actual}, not {expected.ToString(3)}, so it was not installed."));
        if (SignatureProblem(current, Authenticode.Check(exe)) is { } problem) throw new UpdateVerificationException(problem);
    }

    /// <summary>
    /// A broken signature always fails. Once the running copy is signed, the update must be signed
    /// by the same publisher; unsigned previews may update to unsigned or signed builds.
    /// </summary>
    public static string? SignatureProblem(SignatureInfo current, SignatureInfo candidate)
    {
        if (candidate.Status == SignatureStatus.Invalid)
            return L.T("新版本的數位簽章無效（檔案可能被修改過），沒有安裝。", "The new version's digital signature is invalid (the file may have been modified), so it was not installed.");
        if (current.Status != SignatureStatus.Valid) return null;
        if (candidate.Status != SignatureStatus.Valid)
            return L.T("目前的 TSCC_WASD 有數位簽章，但下載的新版本沒有，沒有安裝。", "This TSCC_WASD is signed but the downloaded version is not, so it was not installed.");
        if (!string.Equals(current.Publisher, candidate.Publisher, StringComparison.Ordinal))
            return L.T($"新版本的簽署者（{candidate.Publisher}）與目前版本（{current.Publisher}）不同，沒有安裝。",
                $"The new version is signed by {candidate.Publisher}, not {current.Publisher} like this one, so it was not installed.");
        return null;
    }

    /// <summary>
    /// Replaces the files in <paramref name="appDirectory"/> with the unpacked release. The running
    /// EXE is renamed to .old first and put back if copying fails. Stop mapping before calling this.
    /// </summary>
    public static void Apply(string packageDirectory, string appDirectory)
    {
        string exe = Path.Combine(appDirectory, ExecutableName), backup = exe + BackupSuffix;
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(exe, backup);
        try
        {
            var files = Directory.GetFiles(packageDirectory, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(packageDirectory, f))
                // The EXE goes last, so a failure part-way never leaves a new EXE with old files beside it.
                .OrderBy(f => f.Equals(ExecutableName, StringComparison.OrdinalIgnoreCase));
            foreach (var relative in files)
            {
                string target = Path.Combine(appDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(packageDirectory, relative), target, overwrite: true);
            }
        }
        catch
        {
            // Put the running version back so TSCC_WASD still starts; any docs or driver installers already copied are harmless.
            File.Move(backup, exe, overwrite: true);
            throw;
        }
    }

    /// <summary>Installs the update over the running copy. Then call <see cref="Restart"/> and exit.</summary>
    public static void Install(DownloadedUpdate update) => Apply(update.PackageDirectory, RequireAppDirectory());

    /// <summary>Starts the (new) EXE, which waits for this process to exit before it opens.</summary>
    public static void Restart()
    {
        string dir = RequireAppDirectory();
        Process.Start(new ProcessStartInfo(Path.Combine(dir, ExecutableName))
        {
            UseShellExecute = false,
            WorkingDirectory = dir,
            ArgumentList = { UpdatedFromArgument, Environment.ProcessId.ToString() }
        })?.Dispose();
    }

    /// <summary>
    /// At startup: when launched by an update, waits for the old process to exit, then removes
    /// the old EXE and the downloaded files. Returns true when this start follows an update.
    /// </summary>
    public static bool FinishUpdate(string[] args)
    {
        int index = Array.FindIndex(args, a => a.Equals(UpdatedFromArgument, StringComparison.OrdinalIgnoreCase));
        bool updated = index >= 0;
        if (updated && index + 1 < args.Length && int.TryParse(args[index + 1], out int pid))
        {
            try
            {
                using var old = Process.GetProcessById(pid);
                if (old.ProcessName.Equals(AppPaths.Name, StringComparison.OrdinalIgnoreCase)) old.WaitForExit(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { } // Already gone.
        }
        if (Environment.ProcessPath is { } path)
        {
            try { File.Delete(path + BackupSuffix); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        if (updated) TryDeleteDirectory(WorkDirectory);
        return updated;
    }

    private static string RequireAppDirectory() =>
        AppDirectory ?? throw new IOException(L.T("無法取得程式路徑。", "Could not determine the program path."));

    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
