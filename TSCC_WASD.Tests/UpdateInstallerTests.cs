using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

public sealed class UpdateInstallerTests : IDisposable
{
    private const string Tag = "v1.2.3";
    private const string ZipName = "TSCC_WASD-1.2.3-win-x64.zip";
    private const string Base = "https://github.com/ynyrworks/TSCC_WASD/releases/download/" + Tag + "/";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));

    public UpdateInstallerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static byte[] ReleaseZip(bool withExe = true)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(text);
            }
            if (withExe) Add("TSCC_WASD-1.2.3-win-x64/TSCC_WASD.exe", "new exe");
            Add("TSCC_WASD-1.2.3-win-x64/README.md", "readme");
            Add("TSCC_WASD-1.2.3-win-x64/drivers/HidHide.exe", "driver");
        }
        return buffer.ToArray();
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static UpdatePackage Package(byte[] zip, string? checksum, string? digest, long? size = null)
    {
        var release = new UpdateInfo(new Version(1, 2, 3), Tag, "https://x", true, []);
        var zipAsset = new ReleaseAsset(ZipName, Base + ZipName, size ?? zip.Length, digest);
        var checksumAsset = checksum is null ? null : new ReleaseAsset(ZipName + ".sha256", Base + ZipName + ".sha256", checksum.Length, null);
        return new UpdatePackage(release, zipAsset, checksumAsset);
    }

    private FakeServer Serve(byte[] zip, string? checksum)
    {
        var server = new FakeServer();
        server.Files[Base + ZipName] = zip;
        if (checksum is not null) server.Files[Base + ZipName + ".sha256"] = Encoding.UTF8.GetBytes(checksum);
        return server;
    }

    [Fact]
    public async Task DownloadsVerifiesAndUnpacksTheRelease()
    {
        var zip = ReleaseZip();
        string checksum = $"{Sha256(zip)}  {ZipName}\n";
        using var server = Serve(zip, checksum);
        using var client = new HttpClient(server);
        var reports = new List<double>();
        var update = await UpdateInstaller.DownloadAsync(Package(zip, checksum, Sha256(zip)), new SyncProgress(reports.Add), default, client, _root);

        Assert.Equal(new Version(1, 2, 3), update.Version);
        Assert.Equal("new exe", File.ReadAllText(Path.Combine(update.PackageDirectory, "TSCC_WASD.exe")));
        Assert.True(File.Exists(Path.Combine(update.PackageDirectory, "drivers", "HidHide.exe")));
        Assert.Equal(1, reports[^1]);
        Assert.False(File.Exists(Path.Combine(_root, "1.2.3", ZipName + ".part")));
    }

    [Fact]
    public async Task GitHubsDigestAloneIsEnough()
    {
        var zip = ReleaseZip();
        using var server = Serve(zip, checksum: null);
        using var client = new HttpClient(server);
        var update = await UpdateInstaller.DownloadAsync(Package(zip, null, Sha256(zip).ToUpperInvariant()), null, default, client, _root);
        Assert.True(File.Exists(Path.Combine(update.PackageDirectory, "TSCC_WASD.exe")));
    }

    [Fact]
    public async Task HashMismatchDeletesTheDownload()
    {
        var zip = ReleaseZip();
        string checksum = $"{new string('0', 64)}  {ZipName}";
        using var server = Serve(zip, checksum);
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, checksum, null), null, default, client, _root));
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ReleaseWithoutAnyChecksumIsNotDownloaded()
    {
        var zip = ReleaseZip();
        using var server = Serve(zip, checksum: null);
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, null, null), null, default, client, _root));
        Assert.DoesNotContain(Base + ZipName, server.Requested);
    }

    [Fact]
    public async Task ChecksumFileAndDigestMustAgree()
    {
        var zip = ReleaseZip();
        string checksum = $"{Sha256(zip)}  {ZipName}";
        using var server = Serve(zip, checksum);
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, checksum, new string('b', 64)), null, default, client, _root));
        Assert.DoesNotContain(Base + ZipName, server.Requested);
    }

    [Fact]
    public async Task ChecksumForAnotherFileIsRejected()
    {
        var zip = ReleaseZip();
        string checksum = $"{Sha256(zip)}  TSCC_WASD-1.2.2-win-x64.zip";
        using var server = Serve(zip, checksum);
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, checksum, null), null, default, client, _root));
    }

    [Fact]
    public async Task DownloadLargerThanTheReleaseSaysIsStopped()
    {
        var zip = ReleaseZip();
        string checksum = $"{Sha256(zip)}  {ZipName}";
        using var server = Serve(zip, checksum);
        server.HideLength = true; // Without Content-Length the size is only caught while reading.
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, checksum, null, size: zip.Length - 10), null, default, client, _root));
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RedirectToPlainHttpIsRefused()
    {
        var zip = ReleaseZip();
        string checksum = $"{Sha256(zip)}  {ZipName}";
        using var server = Serve(zip, checksum);
        server.RedirectTo = "http://github.com/";
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, checksum, null), null, default, client, _root));
    }

    [Fact]
    public async Task PackageWithoutTheExeIsRejected()
    {
        var zip = ReleaseZip(withExe: false);
        using var server = Serve(zip, checksum: null);
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            UpdateInstaller.DownloadAsync(Package(zip, null, Sha256(zip)), null, default, client, _root));
        Assert.False(Directory.Exists(Path.Combine(_root, "1.2.3")));
    }

    [Theory]
    [InlineData("{0}  TSCC_WASD-1.2.3-win-x64.zip", true)]
    [InlineData("﻿{0}  TSCC_WASD-1.2.3-win-x64.zip\r\n", true)]
    [InlineData("{0} *TSCC_WASD-1.2.3-win-x64.zip", true)]
    [InlineData("{0}", true)]
    [InlineData("{0}  TSCC_WASD-1.2.2-win-x64.zip", false)]
    [InlineData("abc  TSCC_WASD-1.2.3-win-x64.zip", false)]
    [InlineData("", false)]
    public void ParsesSha256SumLines(string format, bool valid)
    {
        string hash = new('C', 64);
        Assert.Equal(valid ? hash.ToLowerInvariant() : null, UpdateInstaller.ParseChecksum(string.Format(format, hash), ZipName));
    }

    [Fact]
    public void ExeMustCarryTheReleaseVersion()
    {
        string coreDll = typeof(UpdateChecker).Assembly.Location;
        var unsigned = new SignatureInfo(SignatureStatus.Unsigned);
        UpdateInstaller.VerifyExecutable(coreDll, UpdateChecker.CurrentVersion, unsigned);
        Assert.Throws<UpdateVerificationException>(() => UpdateInstaller.VerifyExecutable(coreDll, new Version(99, 0, 0), unsigned));
    }

    public static TheoryData<SignatureStatus, string?, SignatureStatus, string?, bool> SignatureCases => new()
    {
        { SignatureStatus.Unsigned, null, SignatureStatus.Unsigned, null, true },
        { SignatureStatus.Unsigned, null, SignatureStatus.Valid, "CN=SignPath Foundation", true },
        { SignatureStatus.Unsigned, null, SignatureStatus.Invalid, null, false },
        { SignatureStatus.Valid, "CN=SignPath Foundation", SignatureStatus.Valid, "CN=SignPath Foundation", true },
        { SignatureStatus.Valid, "CN=SignPath Foundation", SignatureStatus.Unsigned, null, false },
        { SignatureStatus.Valid, "CN=SignPath Foundation", SignatureStatus.Invalid, null, false },
        { SignatureStatus.Valid, "CN=SignPath Foundation", SignatureStatus.Valid, "CN=Someone Else", false },
    };

    [Theory]
    [MemberData(nameof(SignatureCases))]
    public void SignedInstallsOnlyAcceptTheSamePublisher(SignatureStatus current, string? currentBy, SignatureStatus candidate, string? candidateBy, bool ok)
        => Assert.Equal(ok, UpdateInstaller.SignatureProblem(new(current, currentBy), new(candidate, candidateBy)) is null);

    [Fact]
    public void AuthenticodeTellsSignedUnsignedAndTamperedApart()
    {
        Assert.Equal(SignatureStatus.Unsigned, Authenticode.Check(typeof(UpdateChecker).Assembly.Location).Status);

        string signed = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll");
        var info = Authenticode.Check(signed);
        Assert.Equal(SignatureStatus.Valid, info.Status);
        Assert.Contains("Microsoft", info.Publisher);

        string tampered = Path.Combine(_root, "coreclr.dll");
        var bytes = File.ReadAllBytes(signed);
        bytes[bytes.Length / 2] ^= 0xFF;
        File.WriteAllBytes(tampered, bytes);
        Assert.Equal(SignatureStatus.Invalid, Authenticode.Check(tampered).Status);
    }

    private (string app, string package) InstallFolders()
    {
        string app = Path.Combine(_root, "app"), package = Path.Combine(_root, "package");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(Path.Combine(package, "drivers"));
        File.WriteAllText(Path.Combine(app, "TSCC_WASD.exe"), "old exe");
        File.WriteAllText(Path.Combine(app, "README.md"), "old readme");
        File.WriteAllText(Path.Combine(package, "TSCC_WASD.exe"), "new exe");
        File.WriteAllText(Path.Combine(package, "README.md"), "new readme");
        File.WriteAllText(Path.Combine(package, "drivers", "HidHide.exe"), "driver");
        return (app, package);
    }

    [Fact]
    public void InstallMovesTheRunningExeAsideAndCopiesTheRelease()
    {
        var (app, package) = InstallFolders();
        File.WriteAllText(Path.Combine(app, "TSCC_WASD.exe.old"), "left over from the last update");
        UpdateInstaller.Apply(package, app);
        Assert.Equal("new exe", File.ReadAllText(Path.Combine(app, "TSCC_WASD.exe")));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(app, "TSCC_WASD.exe.old")));
        Assert.Equal("new readme", File.ReadAllText(Path.Combine(app, "README.md")));
        Assert.Equal("driver", File.ReadAllText(Path.Combine(app, "drivers", "HidHide.exe")));
    }

    [Fact]
    public void FailedInstallPutsTheCurrentExeBack()
    {
        var (app, package) = InstallFolders();
        using (new FileStream(Path.Combine(app, "README.md"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => UpdateInstaller.Apply(package, app));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(app, "TSCC_WASD.exe")));
        Assert.False(File.Exists(Path.Combine(app, "TSCC_WASD.exe.old")));
    }

    [Fact]
    public void NormalStartIsNotAnUpdate() => Assert.False(UpdateInstaller.FinishUpdate(["--startup"]));

    [Fact]
    public void TestHostCannotUpdateItself() => Assert.NotNull(UpdateInstaller.InPlaceProblem());

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public List<string> Requested { get; } = new();
        public bool HideLength { get; set; }
        public string? RedirectTo { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string url = request.RequestUri!.AbsoluteUri;
            Requested.Add(url);
            if (!Files.TryGetValue(url, out var body)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            HttpContent content = HideLength ? new StreamContent(new UnknownLengthStream(body)) : new ByteArrayContent(body);
            if (RedirectTo is not null) request.RequestUri = new Uri(RedirectTo);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }

    /// <summary>A stream that can't report its length, like a chunked response.</summary>
    private sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
