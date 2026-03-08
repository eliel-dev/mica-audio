[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$errors = New-Object 'System.Collections.Generic.List[string]'

function Add-Error {
    param([string]$Message)
    $errors.Add($Message)
}

$viewModelsDir = Join-Path $repoRoot 'src/App.WinUI/ViewModels'
$viewModelFiles = Get-ChildItem -Path $viewModelsDir -File -Filter '*.cs' -ErrorAction SilentlyContinue

foreach ($file in $viewModelFiles) {
    $text = Get-Content -Raw -Path $file.FullName

    if ($text -match '\bINotifyPropertyChanged\b') {
        Add-Error "ViewModel com INotifyPropertyChanged manual: $($file.FullName)"
    }

    if ($text -notmatch 'using\s+CommunityToolkit\.Mvvm\.ComponentModel\s*;') {
        Add-Error "ViewModel sem using do Toolkit (ComponentModel): $($file.FullName)"
    }

    if ($text -notmatch ':\s*ObservableObject\b') {
        Add-Error "ViewModel sem heranca de ObservableObject: $($file.FullName)"
    }
}

$viewsPath = Join-Path $repoRoot 'src/App.WinUI/Views'
$pageFiles = Get-ChildItem -Path $viewsPath -File -Filter '*.xaml.cs' -ErrorAction SilentlyContinue

$forbiddenStatics = @(
    'App.Resolve<',
    'App.Services',
    'App.DeviceOps',
    'App.DeviceIntegration',
    'App.AppCatalog',
    'App.AppDeployment',
    'App.AppModifierStore',
    'App.CityAutocomplete',
    'App.FirmwareService'
)

foreach ($file in $pageFiles) {
    $text = Get-Content -Raw -Path $file.FullName

    if ($text -match 'Page\s*\(\s*IServiceProvider\s+services\s*\)') {
        Add-Error "Construtor service-locator em pagina: $($file.FullName)"
    }

    foreach ($token in $forbiddenStatics) {
        if ($text.Contains($token)) {
            Add-Error "Uso de service locator estatico detectado em $($file.FullName): $token"
        }
    }
}

Write-Host '[mvvm-validate] Summary:' -ForegroundColor Cyan
Write-Host " - viewmodels_scanned: $($viewModelFiles.Count)" -ForegroundColor Cyan
Write-Host " - pages_scanned: $($pageFiles.Count)" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host '[mvvm-validate] Falhas encontradas:' -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host " - $error" -ForegroundColor Red
    }

    exit 1
}

Write-Host '[mvvm-validate] OK: nenhuma falha encontrada.' -ForegroundColor Green
exit 0
