# DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---versionamento-do-release-oficial
# DOCS: docs/wiki/modules/server-build-and-artifacts.md#manifesto-oficial
# DOCS: docs/wiki/reference/code-index.md
# DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
# DOCS: docs/handoffs/2026-04-14-versioning-semver-e-ota-stages.md
param(
    [switch]$SkipToolInstall,
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$firmwareRoot = Join-Path $repoRoot 'firmware/esp32s3-devkitc1'
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot 'src/App.WinUI/AppData/Firmware'
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
$autoVersionHeaderPath = Join-Path $firmwareRoot 'src/firmware_version.auto.h'
$target = [pscustomobject]@{ Env = 'esp32s3_devkitc1_dma_exp'; OutputFile = 'esp32s3-devkitc1-128x64-dma_exp_merged.bin'; OtaFile = 'esp32s3-devkitc1-128x64-dma_exp_ota.bin' }
$manifestPath = Join-Path $resolvedOutputRoot 'esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json'
$platformIoCommand = @()

function Invoke-External {
    param(
        [Parameter(Mandatory)]
        [string[]]$Args,
        [Parameter(Mandatory)]
        [string]$ErrorMessage
    )

    if ($Args.Length -eq 0) {
        throw "Invoke-External recebeu lista de argumentos vazia."
    }

    if ($Args.Length -eq 1) {
        & $Args[0] | Out-Host
    }
    else {
        & $Args[0] $Args[1..($Args.Length - 1)] | Out-Host
    }
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage (exit code=$LASTEXITCODE)"
    }
}

function Resolve-PlatformIoCommand {
    $platformioCmd = Get-Command -Name 'platformio' -ErrorAction SilentlyContinue
    if ($platformioCmd) {
        return ,$platformioCmd.Source
    }

    $pioCmd = Get-Command -Name 'pio' -ErrorAction SilentlyContinue
    if ($pioCmd) {
        return ,$pioCmd.Source
    }

    $localPlatformIo = Join-Path $env:USERPROFILE '.platformio\penv\Scripts\platformio.exe'
    if (Test-Path $localPlatformIo) {
        return ,$localPlatformIo
    }

    $python = Get-Command -Name 'python' -ErrorAction SilentlyContinue
    if ($python) {
        try {
            & python -m platformio --version *> $null
            if ($LASTEXITCODE -eq 0) {
                return @('python', '-m', 'platformio')
            }
        }
        catch {
            # fallback para erro mais claro no final
        }
    }

    throw 'PlatformIO nao encontrado. Instale PlatformIO Core ou garanta "~/.platformio/penv/Scripts" no PATH.'
}

function Ensure-Tools {
    if ($SkipToolInstall) {
        return
    }

    $alreadyAvailable = $false
    try {
        $null = Resolve-PlatformIoCommand
        $alreadyAvailable = $true
    }
    catch {
        $alreadyAvailable = $false
    }

    if ($alreadyAvailable) {
        return
    }

    $python = Get-Command -Name 'python' -ErrorAction SilentlyContinue
    if (-not $python) {
        return
    }

    Write-Host '[build-precompiled-firmware] PlatformIO nao encontrado. Instalando no Python atual...'
    Invoke-External -Args @('python', '-m', 'pip', 'install', '--upgrade', 'platformio') -ErrorMessage 'Falha ao instalar platformio'
}

function Invoke-Pio {
    param(
        [Parameter(Mandatory)]
        [string[]]$Args,
        [Parameter(Mandatory)]
        [string]$ErrorMessage
    )

    $commandArgs = @($script:platformIoCommand) + $Args
    Invoke-External -Args $commandArgs -ErrorMessage $ErrorMessage
}

function Invoke-PioBuild {
    param([Parameter(Mandatory)][string]$Env)

    Write-Host "[build-precompiled-firmware] Build iniciado: $Env"
    Invoke-Pio -Args @('run', '-e', $Env, '--project-dir', $firmwareRoot) -ErrorMessage "Falha no build do firmware ($Env)"
}

function Merge-Firmware {
    param(
        [Parameter(Mandatory)][string]$Env,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $buildPath = Join-Path $firmwareRoot ".pio/build/$Env"
    $bootloader = Join-Path $buildPath 'bootloader.bin'
    $partitions = Join-Path $buildPath 'partitions.bin'
    $firmware = Join-Path $buildPath 'firmware.bin'

    foreach ($file in @($bootloader, $partitions, $firmware)) {
        if (-not (Test-Path $file)) {
            throw "Build concluido, mas nao encontrou binario esperado: $file"
        }
    }

    Write-Host "[build-precompiled-firmware] Merge iniciado: $Env -> $DestinationPath"
    Invoke-Pio -Args @(
        'pkg', 'exec', '-p', 'tool-esptoolpy', '--', 'esptool.py',
        '--chip', 'esp32s3', 'merge_bin',
        '--flash_mode', 'keep', '--flash_freq', 'keep', '--flash_size', 'keep',
        '0x0', $bootloader,
        '0x8000', $partitions,
        '0x10000', $firmware,
        '--output', $DestinationPath) -ErrorMessage "Falha no merge do firmware ($Env)"

    if (-not (Test-Path $DestinationPath)) {
        throw "Falha ao gerar merged bin: $DestinationPath"
    }

    $size = (Get-Item $DestinationPath).Length
    if ($size -le 0) {
        throw "Merged bin invalido (tamanho zero): $DestinationPath"
    }

    Write-Host "[build-precompiled-firmware] OK: $DestinationPath ($size bytes)"
}

function Normalize-VersionToken {
    param([string]$Value)

    $trimmed = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return 'unknown'
    }

    $normalized = $trimmed -replace '[^0-9A-Za-z._-]', '_'
    $normalized = $normalized.Trim('_')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return 'unknown'
    }

    return $normalized
}

function Try-GetGitLine {
    param(
        [Parameter(Mandatory)]
        [string[]]$Args
    )

    $git = Get-Command -Name 'git' -ErrorAction SilentlyContinue
    if (-not $git) {
        return $null
    }

    try {
        $output = & $git.Source @Args 2>$null
        if ($LASTEXITCODE -ne 0 -or $null -eq $output) {
            return $null
        }
    }
    catch {
        return $null
    }

    if ($output -is [array]) {
        foreach ($line in $output) {
            $text = "$line".Trim()
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                return $text
            }
        }

        return $null
    }

    $single = "$output".Trim()
    if ([string]::IsNullOrWhiteSpace($single)) {
        return $null
    }

    return $single
}

function Resolve-FirmwareVersion {
    # SemVer via git describe: deterministic, same commit = same version.
    # With tag at HEAD:        "v1.0.0"
    # Without tag, N ahead:    "v1.0.0-14-gb116aea"
    # No tags in repo:         "v0.0.0-0-g<sha>"
    # Dirty working tree:      appends "-dirty" (scoped to firmware/ only)

    $version = $null

    $described = Try-GetGitLine -Args @('-C', $repoRoot, 'describe', '--tags', '--long', '--always')
    if (-not [string]::IsNullOrWhiteSpace($described)) {
        # git describe --tags --long outputs: <tag>-<N>-g<sha> (e.g. v1.0.0-0-gb116aea)
        # --always without tags outputs just the short sha (e.g. b116aea)
        if ($described -match '^(.+)-(\d+)-g([0-9a-f]+)$') {
            $tag = $Matches[1]
            $distance = [int]$Matches[2]
            $sha = $Matches[3]
            if ($distance -eq 0) {
                $version = $tag
            }
            else {
                $normalizedTag = Normalize-VersionToken -Value $tag
                $version = "$normalizedTag-$distance-g$sha"
            }
        }
        else {
            # Fallback: no tags at all, git describe --always returns bare sha
            $version = "v0.0.0-0-g$described"
        }
    }

    if ($null -eq $version) {
        # Git unavailable or describe failed
        $sha = Try-GetGitLine -Args @('-C', $repoRoot, 'rev-parse', '--short', 'HEAD')
        if (-not [string]::IsNullOrWhiteSpace($sha)) {
            $version = "v0.0.0-0-g$sha"
        }
        else {
            $version = 'v0.0.0-0-gnocommit'
        }
    }

    # Append -dirty when firmware/ has uncommitted changes (staged or unstaged).
    # Scoped to firmware/ only — C#/docs changes don't affect firmware version.
    $dirtyCheck = Try-GetGitLine -Args @('-C', $repoRoot, 'status', '--porcelain', '--', 'firmware/')
    if (-not [string]::IsNullOrWhiteSpace($dirtyCheck)) {
        $version = "$version-dirty"
    }

    return $version
}

function Resolve-GitSha {
    $commit = Try-GetGitLine -Args @('-C', $repoRoot, 'rev-parse', '--short', 'HEAD')
    if ([string]::IsNullOrWhiteSpace($commit)) {
        return 'nocommit'
    }

    return $commit
}

function Write-AutoFirmwareVersionHeader {
    param(
        [Parameter(Mandatory)][string]$HeaderPath,
        [Parameter(Mandatory)][string]$Version
    )

    $escapedVersion = $Version.Replace('\', '\\').Replace('"', '\"')
    $content = @(
        '#pragma once',
        '',
        '// Arquivo gerado por scripts/build-precompiled-firmware.ps1',
        "#define MICA_FIRMWARE_VERSION `"$escapedVersion`""
    )

    Set-Content -Path $HeaderPath -Value $content -Encoding ascii
}

function Write-FirmwareManifest {
    param(
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)][string]$FirmwarePath,
        [Parameter(Mandatory)][string]$FirmwareVersion,
        [Parameter(Mandatory)][string]$GitSha,
        [string]$OtaFilePath = ''
    )

    $sha256 = (Get-FileHash -Path $FirmwarePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $fileSizeBytes = (Get-Item $FirmwarePath).Length

    $manifest = [ordered]@{
        schemaVersion   = 3
        firmwareVersion = $FirmwareVersion
        gitSha          = $GitSha
        profile         = 'dma_exp'
        boardModel      = 'esp32s3_devkitc1'
        panelType       = 'hub75_p2_5_128x64_smd2121_scan32'
        controlPlane    = 'mqtt'
        builtAtUtc      = (Get-Date).ToUniversalTime().ToString('o')
        sha256          = $sha256
        fileSizeBytes   = $fileSizeBytes
    }

    if (-not [string]::IsNullOrWhiteSpace($OtaFilePath) -and (Test-Path $OtaFilePath)) {
        $otaSha256 = (Get-FileHash -Path $OtaFilePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $otaFileSizeBytes = (Get-Item $OtaFilePath).Length
        $manifest['otaFileName'] = [System.IO.Path]::GetFileName($OtaFilePath)
        $manifest['otaSha256'] = $otaSha256
        $manifest['otaFileSizeBytes'] = $otaFileSizeBytes
    }

    $json = $manifest | ConvertTo-Json
    Set-Content -Path $DestinationPath -Value $json -Encoding utf8
    Write-Host "[build-precompiled-firmware] Manifesto atualizado: $DestinationPath"
}

$destinationPath = Join-Path $resolvedOutputRoot $target.OutputFile
$firmwareVersion = Resolve-FirmwareVersion
$gitSha = Resolve-GitSha

try {
    if (-not (Test-Path $firmwareRoot)) {
        throw "Pasta de firmware nao encontrada: $firmwareRoot"
    }

    Write-AutoFirmwareVersionHeader -HeaderPath $autoVersionHeaderPath -Version $firmwareVersion
    Write-Host "[build-precompiled-firmware] Firmware version: $firmwareVersion"

    Ensure-Tools
    $platformIoCommand = @(Resolve-PlatformIoCommand)
    Write-Host "[build-precompiled-firmware] PlatformIO: $($platformIoCommand -join ' ')"
    Invoke-Pio -Args @('--version') -ErrorMessage 'Falha ao consultar versao do PlatformIO'

    if (-not (Test-Path $resolvedOutputRoot)) {
        New-Item -Path $resolvedOutputRoot -ItemType Directory | Out-Null
    }

    Invoke-PioBuild -Env $target.Env
    Merge-Firmware -Env $target.Env -DestinationPath $destinationPath

    $otaSourcePath = Join-Path $firmwareRoot ".pio/build/$($target.Env)/firmware.bin"
    $otaDestinationPath = Join-Path $resolvedOutputRoot $target.OtaFile
    if (Test-Path $otaSourcePath) {
        Copy-Item -Path $otaSourcePath -Destination $otaDestinationPath -Force
        $otaSize = (Get-Item $otaDestinationPath).Length
        if ($otaSize -le 0) {
            throw "Binario OTA invalido (tamanho zero): $otaDestinationPath"
        }
        Write-Host "[build-precompiled-firmware] OTA binary: $otaDestinationPath ($otaSize bytes)"
    } else {
        Write-Host "[build-precompiled-firmware] AVISO: firmware.bin nao encontrado para OTA; manifesto sera gerado sem campos OTA."
        $otaDestinationPath = ''
    }

    Write-FirmwareManifest -DestinationPath $manifestPath -FirmwarePath $destinationPath -FirmwareVersion $firmwareVersion -GitSha $gitSha -OtaFilePath $otaDestinationPath

    Write-Host '[build-precompiled-firmware] Concluido com sucesso.'
    $size = (Get-Item $destinationPath).Length
    Write-Host " - $($target.OutputFile) ($size bytes)"
}
finally {
    if (Test-Path $autoVersionHeaderPath) {
        Remove-Item -Path $autoVersionHeaderPath -Force
        Write-Host '[build-precompiled-firmware] Removido arquivo temporario firmware_version.auto.h'
    }
}
