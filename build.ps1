# Runs tests, publishes self-contained builds, and packages Inno Setup installers in out\installer.
# Usage: .\build.ps1 [-Arch x64|arm64|all] [-SkipTests]
# Requires .NET 10 SDK and Inno Setup 6.7+ (ISCC.exe).
param(
    [ValidateSet("x64", "arm64", "all")] [string] $Arch = "all",
    [switch] $SkipTests,
    [string] $ReleaseNotes = ""
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$props = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup
$version = $props.Version
$updateSource = if ($props.UpdateSource) { $props.UpdateSource.Trim() } else { "M-H-AFIFI/Siraj" }

function Find-Iscc {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path $candidate) { return $candidate }
    }
    throw "Inno Setup 6 (ISCC.exe) not found. Install Inno Setup 6.7+ first."
}

$iscc = Find-Iscc

if (-not $SkipTests) {
    dotnet test tests/Siraj.Core.Tests -c Release
    if ($LASTEXITCODE) { throw "tests failed" }
}

$keepLocales = @("en-US", "en-GB", "ar-SA", "tr-TR", "ur-PK")

$archs = if ($Arch -eq "all") { @("x64", "arm64") } else { @($Arch) }
foreach ($a in $archs) {
    $out = "out/$a"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    $platform = if ($a -eq "arm64") { "ARM64" } else { "x64" }
    dotnet publish src/Siraj.App/Siraj.App.csproj -c Release -r "win-$a" -p:Platform=$platform -o $out
    if ($LASTEXITCODE) { throw "publish $a failed" }
    if (-not (Test-Path "$out/Siraj.pri")) { throw "publish $a is missing Siraj.pri" }

    Get-ChildItem $out -Filter "Microsoft.DiaSymReader.Native.*.dll" | Remove-Item -Force
    Get-ChildItem $out -Directory | Where-Object {
        $_.Name -match '^[a-z]{2,3}-[A-Za-z0-9-]+$' -and ($keepLocales -notcontains $_.Name)
    } | Remove-Item -Recurse -Force

    & $iscc "/DArch=$a" "/DAppVersion=$version" installer/Siraj.iss
    if ($LASTEXITCODE) { throw "installer $a failed" }
}

$installerDir = "out/installer"
$exes = Get-ChildItem $installerDir -Filter "SirajSetup-*.exe" | Sort-Object Name
$hashes = @{}
$checksumLines = foreach ($exe in $exes) {
    $hash = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashes[$exe.Name] = $hash
    "$hash  $($exe.Name)"
}
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllLines((Join-Path (Resolve-Path $installerDir) "SHA256SUMS.txt"), [string[]]$checksumLines, $utf8NoBom)

$manifestAssets = [ordered]@{}
foreach ($a in @("x64", "arm64")) {
    $name = "SirajSetup-$version-$a.exe"
    $match = $exes | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($match) {
        $downloadUrl = if ($updateSource -match '^https?://') {
            $baseUrl = $updateSource.Substring(0, $updateSource.LastIndexOf('/') + 1)
            "$baseUrl$name"
        } else {
            "https://github.com/$updateSource/releases/download/v$version/$name"
        }
        $manifestAssets[$a] = [ordered]@{
            fileName = $name
            url      = $downloadUrl
            sha256   = $hashes[$name]
            size     = $match.Length
        }
    }
}
if ($manifestAssets.Count -gt 0) {
    $manifest = [ordered]@{
        version     = $version
        publishedAt = (Get-Date).ToUniversalTime().ToString("o")
        notes       = $ReleaseNotes
        assets      = $manifestAssets
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path (Resolve-Path $installerDir) "windows-latest.json"), $manifestJson, $utf8NoBom)
}

Write-Host ""
Write-Host "Built installers in ${installerDir}:"
$exes | ForEach-Object { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
