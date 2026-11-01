#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) {
    throw 'Package on Windows using x64 PowerShell.'
}
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    function Invoke-DotNet([string[]] $Arguments) {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $Arguments" }
    }
    [xml] $props = Get-Content -Raw 'Directory.Build.props'
    $version = [string] $props.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part version.' }
    $artifactRoot = Join-Path $repoRoot 'artifacts'
    # Remove stage folders left by earlier runs; this run's stage is kept for the release workflow (signing).
    if (Test-Path -LiteralPath $artifactRoot) {
        Get-ChildItem -LiteralPath $artifactRoot -Directory -Filter 'stage-*' |
            Remove-Item -Recurse -Force
    }
    $stage = Join-Path $artifactRoot ('stage-' + [guid]::NewGuid().ToString('N'))
    $packageName = "TSCC_WASD-$version-win-x64"
    $packageDir = Join-Path $stage $packageName
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    Invoke-DotNet @('restore', 'TSCC_WASD.sln', '--locked-mode', '-p:NuGetAuditMode=all', '-warnaserror')
    Invoke-DotNet @('build', 'TSCC_WASD.sln', '-c', 'Release', '--no-restore', '-warnaserror')
    Invoke-DotNet @('test', 'TSCC_WASD.sln', '-c', 'Release', '--no-build', '--logger', 'trx')
    # Self-contained single file: users need no separate .NET Desktop Runtime.
    Invoke-DotNet @('publish', 'TSCC_WASD.App/TSCC_WASD.App.csproj', '-c', 'Release',
        '--no-restore', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $packageDir)

    # Official ViGEmBus and HidHide installers, pinned in drivers.json (the app checks the same hashes).
    $driverCache = Join-Path $artifactRoot 'driver-cache'
    $driverDir = Join-Path $packageDir 'drivers'
    New-Item -ItemType Directory -Path $driverCache, $driverDir -Force | Out-Null
    $drivers = Get-Content -Raw 'TSCC_WASD.Core/drivers.json' | ConvertFrom-Json
    foreach ($driver in $drivers) {
        $cached = Join-Path $driverCache $driver.fileName
        $valid = (Test-Path -LiteralPath $cached) -and
            ((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -eq $driver.sha256)
        if (-not $valid) {
            Invoke-WebRequest -Uri $driver.url -OutFile $cached
            $actual = (Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash
            if ($actual -ne $driver.sha256) {
                Remove-Item -LiteralPath $cached
                throw "$($driver.name) installer hash mismatch: $actual"
            }
        }
        Copy-Item -LiteralPath $cached -Destination $driverDir
    }
    foreach ($name in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md',
        'CONTRIBUTING.md', 'SECURITY.md', 'PLAN.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $packageDir
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $packageDir -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'examples') -Destination $packageDir -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $packageDir -Recurse
    $requiredFiles = @('TSCC_WASD.exe', 'LICENSE') + @($drivers | ForEach-Object { 'drivers/' + $_.fileName })
    foreach ($required in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDir $required))) {
            throw "Missing package file: $required"
        }
    }
    if (Get-ChildItem -LiteralPath $packageDir -Filter '*SharpDX*' -Recurse) {
        throw 'Obsolete SharpDX files found.'
    }
    $zip = Join-Path $artifactRoot ($packageName + '.zip')
    Compress-Archive -LiteralPath $packageDir -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $packageName.zip" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding utf8
    Write-Host "Package: $zip"
    Write-Host "SHA256: $hash"
} finally {
    Pop-Location
}
