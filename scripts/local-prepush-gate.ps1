[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[local-prepush-gate] $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Etapa '$Name' falhou com exit code $LASTEXITCODE."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try
{
    Invoke-Step -Name 'docs-validate' -Action { powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 }
    Invoke-Step -Name 'ai-governance-check' -Action { powershell -ExecutionPolicy Bypass -File .\\scripts\\ai-governance-check.ps1 }
    Invoke-Step -Name 'mvvm-validate' -Action { powershell -ExecutionPolicy Bypass -File .\\scripts\\mvvm-validate.ps1 }
    Invoke-Step -Name 'build App.WinUI (Debug)' -Action { dotnet build src/App.WinUI/App.WinUI.csproj -c Debug }
    Invoke-Step -Name 'test Output.Tests (Debug)' -Action { dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build }
}
finally
{
    Pop-Location
}

