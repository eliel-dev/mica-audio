[CmdletBinding()]
param(
    [int]$CrashTailLines = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[dev-doctor] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  OK  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  WARN $Message" -ForegroundColor Yellow
}

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$slnPath = Join-Path $repoRoot "MicaAudio.sln"
$appProjectPath = Join-Path $repoRoot "src\App.WinUI\App.WinUI.csproj"
$mainPagePath = Join-Path $repoRoot "src\App.WinUI\Views\MainPage.xaml"
$globalJsonPath = Join-Path $repoRoot "global.json"
$crashLogPath = Join-Path $env:LocalAppData "MicaAudio\crash.log"

Write-Step "Checando SDK .NET"
$dotnetVersion = (& dotnet --version).Trim()
if ([string]::IsNullOrWhiteSpace($dotnetVersion)) {
    Write-Warn "Nao foi possivel determinar a versao do dotnet."
}
else {
    Write-Ok "dotnet SDK: $dotnetVersion"
}

if (Test-Path $globalJsonPath) {
    $global = Get-Content $globalJsonPath -Raw | ConvertFrom-Json
    $expectedSdk = $global.sdk.version
    if ($expectedSdk -eq $dotnetVersion) {
        Write-Ok "global.json alinhado com SDK instalado ($expectedSdk)."
    }
    else {
        Write-Warn "global.json pede $expectedSdk e a maquina esta em $dotnetVersion."
    }
}
else {
    Write-Warn "global.json nao encontrado."
}

Write-Step "Checando Windows App Runtime 1.8"
$winAppRuntime = Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.1.8" -ErrorAction SilentlyContinue |
    Sort-Object Version -Descending |
    Select-Object -First 1

if ($null -ne $winAppRuntime) {
    Write-Ok "Windows App Runtime 1.8 encontrado: $($winAppRuntime.Version)"
}
else {
    Write-Warn "Windows App Runtime 1.8 nao encontrado no sistema."
}

if (Test-Path $appProjectPath) {
    $projectXml = [xml](Get-Content $appProjectPath -Raw)
    $selfContainedNode = $projectXml.SelectSingleNode("//WindowsAppSDKSelfContained")
    $selfContained = if ($null -ne $selfContainedNode) { $selfContainedNode.InnerText.Trim() } else { "" }
    if ($selfContained -eq "true") {
        Write-Ok "Projeto com WindowsAppSDKSelfContained=true (runtime integrado na publicacao)."
    }
    else {
        Write-Warn "Projeto sem WindowsAppSDKSelfContained=true."
    }
}

Write-Step "Checando mapeamento de plataforma na solution"
if (Test-Path $slnPath) {
    $sln = Get-Content $slnPath
    $badMappings = $sln | Select-String -Pattern '\{8ED61A39-E2A6-4BB8-8E69-7605F1CD0D3F\}.*=\s*(Debug|Release)\|x86$'
    if ($badMappings) {
        Write-Warn "App.WinUI ainda mapeado para x86 em alguma configuracao."
    }
    else {
        Write-Ok "App.WinUI sem mapeamento x86 ativo na solution."
    }
}
else {
    Write-Warn "Solution nao encontrada em $slnPath"
}

Write-Step "Checando politica de seguranca (SAC/App Control)"
$policyPath = "HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy"
if (Test-Path $policyPath) {
    $policy = Get-ItemProperty -Path $policyPath
    $state = $policy.VerifiedAndReputablePolicyState
    if ($state -eq 1) {
        Write-Warn "Smart App Control/Verified policy ativo (valor=$state). Pode bloquear EXE/DLL mesmo com assinatura dev local."
    }
    else {
        Write-Ok "VerifiedAndReputablePolicyState=$state"
    }
}
else {
    Write-Warn "Chave de politica CI nao encontrada."
}

Write-Step "Checando risco de parse no XAML"
if (Test-Path $mainPagePath) {
    $xaml = Get-Content $mainPagePath -Raw
    if ($xaml -match 'x:Name="LinearBoostSlider"[\s\S]*Minimum="1\.0"') {
        Write-Warn "LinearBoostSlider ainda com Minimum=\"1.0\" no XAML."
    }
    else {
        Write-Ok "LinearBoostSlider sem literal decimal sensivel no XAML."
    }
}

Write-Step "Ultimo crash log"
if (Test-Path $crashLogPath) {
    Write-Warn "Crash log encontrado: $crashLogPath"
    Get-Content $crashLogPath -Tail $CrashTailLines | ForEach-Object { Write-Host "    $_" }
}
else {
    Write-Ok "Sem crash.log em %LocalAppData%\\MicaAudio."
}

Write-Step "Diagnostico concluido."
