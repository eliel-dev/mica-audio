[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$errors = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'

function Add-Error {
    param([string]$Message)
    $errors.Add($Message)
}

function Add-Warning {
    param([string]$Message)
    $warnings.Add($Message)
}

function Read-FirstNodeText {
    param(
        [Parameter(Mandatory = $true)][xml]$Xml,
        [Parameter(Mandatory = $true)][string]$XPath
    )

    $node = $Xml.SelectSingleNode($XPath)
    if ($null -eq $node) {
        return $null
    }

    return [string]$node.InnerText
}

function Read-CsprojPackageVersion {
    param(
        [Parameter(Mandatory = $true)][xml]$Xml,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    $nodes = $Xml.SelectNodes("//*[local-name()='PackageReference']")
    foreach ($node in $nodes) {
        $includeAttr = $node.Attributes['Include']
        $versionAttr = $node.Attributes['Version']
        $includeValue = if ($null -eq $includeAttr) { '' } else { [string]$includeAttr.Value }

        if ([string]::Equals($includeValue, $PackageId, [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($null -eq $versionAttr) {
                return ''
            }

            return [string]$versionAttr.Value
        }
    }

    return $null
}

$csprojPath = Join-Path $repoRoot 'src/Visual.Win2D/Visual.Win2D.csproj'
if (-not (Test-Path -LiteralPath $csprojPath)) {
    throw "Projeto Visual.Win2D nao encontrado: $csprojPath"
}

[xml]$csprojXml = Get-Content -LiteralPath $csprojPath

$dotnetVersion = (& dotnet --version 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($dotnetVersion)) {
    Add-Error 'SDK .NET nao encontrado no PATH.'
}
else {
    $major = 0
    if ([int]::TryParse(($dotnetVersion.Split('.')[0]), [ref]$major)) {
        if ($major -lt 8) {
            Add-Error "SDK .NET muito antigo para shader path (detectado: $dotnetVersion, esperado >= 8.0)."
        }
    }
    else {
        Add-Warning "Nao foi possivel interpretar versao do SDK .NET: $dotnetVersion"
    }
}

$targetFramework = Read-FirstNodeText -Xml $csprojXml -XPath "//*[local-name()='TargetFramework']"
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    Add-Error 'TargetFramework ausente em Visual.Win2D.csproj.'
}
elseif (-not $targetFramework.StartsWith('net8.0-windows', [System.StringComparison]::OrdinalIgnoreCase)) {
    Add-Warning "TargetFramework atual '$targetFramework' difere do baseline esperado net8.0-windows*."
}

$allowUnsafeBlocks = Read-FirstNodeText -Xml $csprojXml -XPath "//*[local-name()='AllowUnsafeBlocks']"
if (-not [string]::Equals($allowUnsafeBlocks, 'true', [System.StringComparison]::OrdinalIgnoreCase)) {
    Add-Error 'AllowUnsafeBlocks=true e obrigatorio para descriptors do ComputeSharp shader.'
}

$computeSharpVersion = Read-CsprojPackageVersion -Xml $csprojXml -PackageId 'ComputeSharp.D2D1'
if ([string]::IsNullOrWhiteSpace($computeSharpVersion)) {
    Add-Error 'PackageReference ComputeSharp.D2D1 ausente em Visual.Win2D.csproj.'
}

$computeSharpWinUiVersion = Read-CsprojPackageVersion -Xml $csprojXml -PackageId 'ComputeSharp.D2D1.WinUI'
if ([string]::IsNullOrWhiteSpace($computeSharpWinUiVersion)) {
    Add-Error 'PackageReference ComputeSharp.D2D1.WinUI ausente em Visual.Win2D.csproj.'
}

$uapPropsCandidates = @(
    "$env:ProgramFiles(x86)\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Microsoft\\VisualStudio\\v17.0\\AppxPackage\\Microsoft.AppXPackage.Targets",
    "$env:ProgramFiles(x86)\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Microsoft\\VisualStudio\\v17.0\\AppxPackage\\Microsoft.AppXPackage.Targets"
)

if (-not ($uapPropsCandidates | Where-Object { Test-Path -LiteralPath $_ })) {
    Add-Warning 'Componentes UAP/MSIX do VS nao detectados. Isso afeta build de solucao completa (APPX3217), mas nao bloqueia build isolado do Visual.Win2D.'
}

$buildOutput = & dotnet build $csprojPath -c Debug -nologo -v minimal 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -ne 0) {
    $combined = ($buildOutput | Out-String)

    if ($combined -match 'ID2D1PixelShaderDescriptor') {
        Add-Error 'Falha de descriptor shader (ID2D1PixelShaderDescriptor). Verifique AllowUnsafeBlocks e o pacote ComputeSharp.D2D1.WinUI.'
    }

    if ($combined -match 'UAP\.props|APPX3217') {
        Add-Error 'Falha de toolchain UAP detectada (APPX3217/UAP.props). Use MicaAudio.Dev.slnf para desenvolvimento local e mantenha CI para build completo.'
    }

    Add-Error "Build de preflight do Visual.Win2D falhou (exit=$buildExitCode)."
}

Write-Host '[validate-shader-toolchain] Summary:' -ForegroundColor Cyan
Write-Host " - dotnet_version: $dotnetVersion" -ForegroundColor Cyan
Write-Host " - visual_win2d_project: $csprojPath" -ForegroundColor Cyan
Write-Host " - computesharp_d2d1: $computeSharpVersion" -ForegroundColor Cyan
Write-Host " - computesharp_d2d1_winui: $computeSharpWinUiVersion" -ForegroundColor Cyan

if ($warnings.Count -gt 0) {
    Write-Host '[validate-shader-toolchain] Warnings:' -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

if ($errors.Count -gt 0) {
    Write-Host '[validate-shader-toolchain] Falhas encontradas:' -ForegroundColor Red
    foreach ($failure in $errors) {
        Write-Host " - $failure" -ForegroundColor Red
    }

    exit 1
}

Write-Host '[validate-shader-toolchain] OK: toolchain de shader valido.' -ForegroundColor Green
exit 0

