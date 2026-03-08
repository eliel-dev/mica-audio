# DOCS: docs/wiki/guides/release-1.0-installer.md
# DOCS: docs/wiki/modules/app-winui.md
[CmdletBinding()]
param(
    [string]$AppProject = "src\App.WinUI\App.WinUI.csproj",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [string]$PublishProfile,
    [switch]$SkipPublish,
    [string]$CertSubject = "CN=MicaAudio Dev",
    [int]$CertYears = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[sign-dev] $Message" -ForegroundColor Cyan
}

function Get-OrCreateDevCertificate {
    param(
        [string]$Subject,
        [int]$Years
    )

    $existing = Get-ChildItem -Path "Cert:\CurrentUser\My" |
        Where-Object {
            $_.Subject -eq $Subject -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(14)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -ne $existing) {
        Write-Step "Using existing certificate: $($existing.Thumbprint)"
        return $existing
    }

    Write-Step "Creating development code-signing certificate in CurrentUser\\My"
    $newCert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears([Math]::Max(1, $Years))

    return $newCert
}

function Ensure-CertInStore {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$StorePath
    )

    $present = Get-ChildItem -Path $StorePath |
        Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } |
        Select-Object -First 1

    if ($null -ne $present) {
        return
    }

    $tempCer = Join-Path $env:TEMP ("micaaudio-dev-{0}.cer" -f $Certificate.Thumbprint)
    Export-Certificate -Cert $Certificate -FilePath $tempCer -Type CERT | Out-Null
    Import-Certificate -FilePath $tempCer -CertStoreLocation $StorePath | Out-Null
    Remove-Item -Path $tempCer -Force -ErrorAction SilentlyContinue
}

function Resolve-PublishDir {
    param(
        [string]$ProjectPath,
        [string]$Config,
        [string]$Rid,
        [string]$TargetFramework
    )

    $projectDir = Split-Path -Path $ProjectPath -Parent
    $candidates = @(
        (Join-Path $projectDir ".artifacts\publish\$Rid"),
        (Join-Path $projectDir "bin\$Config\$TargetFramework\$Rid\publish"),
        (Join-Path $projectDir "bin\$Config\$TargetFramework\$Rid")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "App.WinUI.exe")) {
            return (Resolve-Path $candidate).Path
        }
    }

    $foundExe = Get-ChildItem -Path $projectDir -Recurse -File -Filter "App.WinUI.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $foundExe) {
        throw "Could not find publish output. Run publish first or remove -SkipPublish."
    }

    return $foundExe.DirectoryName
}

function Get-ProjectTargetFramework {
    param([string]$ProjectPath)

    [xml]$project = Get-Content -Path $ProjectPath
    $framework = $project.Project.PropertyGroup.TargetFramework | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($framework)) {
        $framework = $project.Project.PropertyGroup.TargetFrameworks | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($framework)) {
        throw "Could not resolve TargetFramework from '$ProjectPath'."
    }

    return ($framework -split ';')[0].Trim()
}

function Get-FilesToSign {
    param([string]$PublishDir)

    $preferredNames = @(
        "App.WinUI.exe",
        "App.WinUI.dll",
        "MicaAudio.Core.dll",
        "Audio.Loopback.dll",
        "Analyzer.Dsp.dll",
        "Visual.Win2D.dll",
        "Output.dll"
    )

    $files = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $preferredNames) {
        $directPath = Join-Path $PublishDir $name
        if (Test-Path $directPath) {
            [void]$files.Add((Resolve-Path $directPath).Path)
            continue
        }

        $match = Get-ChildItem -Path $PublishDir -Recurse -File -Filter $name |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -ne $match) {
            [void]$files.Add($match.FullName)
        }
    }

    if ($files.Count -eq 0) {
        throw "No signable app binaries found in '$PublishDir'."
    }

    $result = @()
    foreach ($filePath in $files) {
        $result += [string]$filePath
    }

    return $result
}

function Sign-File {
    param(
        [string]$Path,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )

    $result = Set-AuthenticodeSignature -FilePath $Path -Certificate $Certificate -HashAlgorithm SHA256
    if ($result.Status -ne "Valid") {
        throw "Signature failed for '$Path' (Status: $($result.Status), Message: $($result.StatusMessage))"
    }
}

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$projectPathCandidate = if ([System.IO.Path]::IsPathRooted($AppProject)) {
    $AppProject
}
else {
    Join-Path $repoRoot $AppProject
}

$resolvedProject = (Resolve-Path $projectPathCandidate).Path
$rid = "win-$Platform"
$profile = if ([string]::IsNullOrWhiteSpace($PublishProfile)) { "win-$Platform" } else { $PublishProfile }
$targetFramework = Get-ProjectTargetFramework -ProjectPath $resolvedProject

$certificate = Get-OrCreateDevCertificate -Subject $CertSubject -Years $CertYears
Write-Step "Ensuring certificate trust in CurrentUser stores"
Ensure-CertInStore -Certificate $certificate -StorePath "Cert:\CurrentUser\Root"
Ensure-CertInStore -Certificate $certificate -StorePath "Cert:\CurrentUser\TrustedPublisher"

if (-not $SkipPublish) {
    Write-Step "Publishing App.WinUI ($Configuration, profile '$profile')"
    $publishArgs = @(
        "publish",
        $resolvedProject,
        "-c", $Configuration,
        "-p:Platform=$Platform",
        "-p:PublishProfile=$profile"
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Step "Skipping publish because -SkipPublish was specified"
}

$publishDir = Resolve-PublishDir -ProjectPath $resolvedProject -Config $Configuration -Rid $rid -TargetFramework $targetFramework
Write-Step "Publish directory: $publishDir"

$filesToSign = @(Get-FilesToSign -PublishDir $publishDir)
Write-Step ("Signing {0} files" -f $filesToSign.Count)
foreach ($file in $filesToSign) {
    Sign-File -Path $file -Certificate $certificate
    Write-Host ("  signed: {0}" -f $file)
}

$appExe = Join-Path $publishDir "App.WinUI.exe"
if (Test-Path $appExe) {
    $sig = Get-AuthenticodeSignature -FilePath $appExe
    Write-Step ("App signature status: {0}" -f $sig.Status)
}

Write-Step "Done."