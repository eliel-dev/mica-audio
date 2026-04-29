<#!
DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---higiene-de-warnings-platformio
DOCS: docs/handoffs/2026-04-29-build-warning-cleanup-and-remove-device-button.md
#>

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem'
$propertyName = 'LongPathsEnabled'
$currentValue = (Get-ItemProperty -Path $registryPath -Name $propertyName -ErrorAction SilentlyContinue).$propertyName

if ($currentValue -eq 1) {
    Write-Host 'Windows Long Path Support ja esta habilitado.'
    exit 0
}

if ($DryRun) {
    Write-Host "Dry-run: habilitaria $propertyName=1 em $registryPath."
    exit 0
}

if (-not (Test-IsAdministrator)) {
    throw 'Execute este script em um PowerShell aberto como Administrador para alterar HKLM.'
}

New-ItemProperty -Path $registryPath -Name $propertyName -Value 1 -PropertyType DWord -Force | Out-Null
Write-Host 'Windows Long Path Support habilitado. Reinicie o Windows antes do proximo build completo do PlatformIO.'
