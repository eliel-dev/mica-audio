[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [ValidateSet("auto", "dotnet", "publish")]
    [string]$RunMode = "publish",
    [bool]$SingleFile = $false,
    [switch]$SkipDoctor,
    [switch]$SkipPublish,
    [switch]$NoSign,
    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[dev-run] $Message" -ForegroundColor Cyan
}

function Resolve-PublishDir {
    param(
        [string]$RepoRoot,
        [string]$Config
    )

    $candidates = @(
        (Join-Path $RepoRoot "src\App.WinUI\.artifacts\publish\win-x64"),
        (Join-Path $RepoRoot "src\App.WinUI\bin\$Config\net8.0-windows10.0.19041.0\win-x64\publish"),
        (Join-Path $RepoRoot "src\App.WinUI\bin\$Config\net8.0-windows10.0.19041.0\win-x64")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "App.WinUI.exe")) {
            return (Resolve-Path $candidate).Path
        }
    }

    $fallbackExe = Get-ChildItem -Path (Join-Path $RepoRoot "src\App.WinUI") -Recurse -File -Filter "App.WinUI.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $fallbackExe) {
        throw "Nao foi possivel localizar App.WinUI.exe apos publish."
    }

    return $fallbackExe.DirectoryName
}

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$appProject = Join-Path $repoRoot "src\App.WinUI\App.WinUI.csproj"

if (-not (Test-Path $appProject)) {
    throw "Projeto nao encontrado: $appProject"
}

if (-not $SkipDoctor) {
    Write-Step "Executando diagnostico rapido"
    & (Join-Path $PSScriptRoot "dev-doctor.ps1") -CrashTailLines 12
}

$effectiveRunMode = $RunMode
if ($RunMode -eq "auto") {
    $policyPath = "HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy"
    $state = $null
    if (Test-Path $policyPath) {
        $policy = Get-ItemProperty -Path $policyPath -ErrorAction SilentlyContinue
        $state = $policy.VerifiedAndReputablePolicyState
    }

    if ($state -eq 1) {
        $effectiveRunMode = "publish"
        Write-Step "RunMode=auto -> publish (SAC/WDAC rigoroso detectado: VerifiedAndReputablePolicyState=1)"
    }
    else {
        $effectiveRunMode = "dotnet"
        if ($null -eq $state) {
            Write-Step "RunMode=auto -> dotnet (estado de politica nao encontrado)"
        }
        else {
            Write-Step "RunMode=auto -> dotnet (VerifiedAndReputablePolicyState=$state)"
        }
    }
}

if ($effectiveRunMode -eq "dotnet") {
    if ($SkipPublish) {
        Write-Step "Aviso: -SkipPublish nao se aplica em RunMode=dotnet."
    }

    if (-not $NoSign) {
        Write-Step "Aviso: assinatura nao e necessaria em RunMode=dotnet."
    }

    if ($NoLaunch) {
        Write-Step "Compilando app (RunMode=dotnet, -NoLaunch)"
        & dotnet build $appProject -c $Configuration -p:Platform=x64
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build falhou com exit code $LASTEXITCODE"
        }

        Write-Step "Build concluido. Launch pulado (-NoLaunch)."
        Write-Host "Execute manualmente: dotnet run --project $appProject -c $Configuration -p:Platform=x64"
        return
    }

    $crashLogDotnet = Join-Path $env:LocalAppData "MicaAudio\crash.log"
    if (Test-Path $crashLogDotnet) {
        $archiveName = "crash.previous-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date)
        $archivePath = Join-Path (Split-Path $crashLogDotnet -Parent) $archiveName
        Copy-Item -Path $crashLogDotnet -Destination $archivePath -Force
        Write-Step "Crash log anterior arquivado em: $archivePath"
    }

    Write-Step "Iniciando app via dotnet run (modo dev resiliente)"
    Start-Process -FilePath "dotnet" -ArgumentList @(
        "run",
        "--project", $appProject,
        "-c", $Configuration,
        "-p:Platform=x64"
    ) -WorkingDirectory $repoRoot
    Write-Step "App iniciado. Se falhar no startup, verifique: $crashLogDotnet"
    return
}

if (-not $SkipPublish) {
    Write-Step "Publicando App.WinUI ($Configuration, x64)"
    $publishArgs = @(
        "publish",
        $appProject,
        "-c", $Configuration,
        "-p:Platform=x64",
        "-p:PublishProfile=win-x64"
    )
    if ($SingleFile) {
        Write-Step "Aplicando PublishSingleFile=true (observacao: pode extrair DLL em %TEMP% dependendo do runtime)"
        $publishArgs += @(
            "-p:PublishSingleFile=true",
            "-p:SelfContained=false",
            "-p:PublishTrimmed=false"
        )
    }
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish falhou com exit code $LASTEXITCODE"
    }
}
else {
    Write-Step "Pulando publish (-SkipPublish)"
}

if (-not $NoSign) {
    Write-Step "Assinando binarios para reduzir bloqueio por politica de seguranca"
    & (Join-Path $PSScriptRoot "sign-dev.ps1") -SkipPublish -Configuration $Configuration -Platform x64
    if ($LASTEXITCODE -ne 0) {
        throw "sign-dev.ps1 falhou com exit code $LASTEXITCODE"
    }
}
else {
    Write-Step "Pulando assinatura (-NoSign)"
}

$publishDir = Resolve-PublishDir -RepoRoot $repoRoot -Config $Configuration
$appExe = Join-Path $publishDir "App.WinUI.exe"
if (-not (Test-Path $appExe)) {
    throw "Executavel nao encontrado: $appExe"
}

Write-Step "Diretorio de publicacao: $publishDir"
Write-Step "Removendo marca de arquivo bloqueado (se existir)"
Get-ChildItem -Path $publishDir -Recurse -File -Include *.exe, *.dll |
    ForEach-Object { Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue }

$crashLog = Join-Path $env:LocalAppData "MicaAudio\crash.log"
if (Test-Path $crashLog) {
    $archiveName = "crash.previous-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date)
    $archivePath = Join-Path (Split-Path $crashLog -Parent) $archiveName
    Copy-Item -Path $crashLog -Destination $archivePath -Force
    Write-Step "Crash log anterior arquivado em: $archivePath"
}

if ($NoLaunch) {
    Write-Step "Publicacao pronta. Launch pulado (-NoLaunch)."
    Write-Host "Execute manualmente: $appExe"
    return
}

Write-Step "Iniciando app"
Start-Process -FilePath $appExe -WorkingDirectory $publishDir

# Give startup a short window and surface common App Control failure clearly.
Start-Sleep -Seconds 2
if (Test-Path $crashLog) {
    $recentCrashTail = Get-Content $crashLog -Tail 40
    $appControlBlocked = $recentCrashTail | Where-Object { $_ -match "0x800711C7|Controle de Aplicativo bloqueou|App Control" } | Select-Object -First 1
    if ($null -ne $appControlBlocked) {
        Write-Host "  WARN Politica de Controle de Aplicativo bloqueou um modulo do app (0x800711C7)." -ForegroundColor Yellow
        Write-Host "       Com SAC/WDAC ativo, assinatura dev local pode nao ser suficiente para DLLs carregadas em runtime." -ForegroundColor Yellow
        Write-Host "       Opcoes: desativar SAC na maquina de dev, usar certificado corporativo confiavel pela politica, ou usar VM sem SAC." -ForegroundColor Yellow
    }
}
Write-Step "App iniciado. Se falhar no startup, verifique: $crashLog"



