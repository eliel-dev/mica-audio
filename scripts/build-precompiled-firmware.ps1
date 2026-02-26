param(
    [switch]$SkipToolInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$firmwareRoot = Join-Path $repoRoot 'firmware/matrixportal-s3'
$outputRoot = Join-Path $repoRoot 'src/App.WinUI/AppData/Firmware'

$targets = @(
    [pscustomobject]@{ Env = 'esp32s3_devkitc1_stable'; OutputFile = 'esp32s3-devkitc1-stable_merged.bin' },
    [pscustomobject]@{ Env = 'esp32s3_devkitc1_dma_exp'; OutputFile = 'esp32s3-devkitc1-dma_exp_merged.bin' }
)

function Invoke-External {
    param(
        [Parameter(Mandatory)]
        [string[]]$Args,
        [Parameter(Mandatory)]
        [string]$ErrorMessage
    )

    & $Args[0] $Args[1..($Args.Length - 1)] | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage (exit code=$LASTEXITCODE)"
    }
}

function Require-Python {
    try {
        & py --version | Out-Host
    }
    catch {
        throw 'Python launcher "py" nao encontrado. Instale Python para continuar.'
    }
}

function Ensure-Tools {
    if ($SkipToolInstall) {
        return
    }

    Write-Host '[build-precompiled-firmware] Garantindo PlatformIO e esptool no Python atual...'
    Invoke-External -Args @('py', '-m', 'pip', 'install', '--upgrade', 'platformio', 'esptool') -ErrorMessage 'Falha ao instalar platformio/esptool'
}

function Invoke-PioBuild {
    param([Parameter(Mandatory)][string]$Env)

    Write-Host "[build-precompiled-firmware] Build iniciado: $Env"
    Invoke-External -Args @('py', '-m', 'platformio', 'run', '-e', $Env, '--project-dir', $firmwareRoot) -ErrorMessage "Falha no build do firmware ($Env)"
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
    Invoke-External -Args @(
        'py', '-m', 'esptool', '--chip', 'esp32s3', 'merge-bin',
        '--flash-mode', 'keep', '--flash-freq', 'keep', '--flash-size', 'keep',
        '0x0', $bootloader,
        '0x8000', $partitions,
        '0x10000', $firmware,
        '-o', $DestinationPath) -ErrorMessage "Falha no merge do firmware ($Env)"

    if (-not (Test-Path $DestinationPath)) {
        throw "Falha ao gerar merged bin: $DestinationPath"
    }

    $size = (Get-Item $DestinationPath).Length
    if ($size -le 0) {
        throw "Merged bin invalido (tamanho zero): $DestinationPath"
    }

    Write-Host "[build-precompiled-firmware] OK: $DestinationPath ($size bytes)"
}

Require-Python
Ensure-Tools

if (-not (Test-Path $firmwareRoot)) {
    throw "Pasta de firmware nao encontrada: $firmwareRoot"
}

if (-not (Test-Path $outputRoot)) {
    New-Item -Path $outputRoot -ItemType Directory | Out-Null
}

foreach ($target in $targets) {
    Invoke-PioBuild -Env $target.Env
    $destinationPath = Join-Path $outputRoot $target.OutputFile
    Merge-Firmware -Env $target.Env -DestinationPath $destinationPath
}

Write-Host '[build-precompiled-firmware] Concluido com sucesso.'
foreach ($target in $targets) {
    $path = Join-Path $outputRoot $target.OutputFile
    $size = (Get-Item $path).Length
    Write-Host " - $($target.OutputFile) ($size bytes)"
}