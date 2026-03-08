# DOCS: docs/wiki/modules/settings-presets-persistence.md#migracao-de-registro-de-devices
[CmdletBinding()]
param(
    [string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Path)) {
    $appDataRoot = Join-Path $env:APPDATA 'MicaAudio'
    $Path = Join-Path $appDataRoot 'devices.json'
}

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Host "devices.json nao encontrado em $Path"
    exit 0
}

$raw = Get-Content -LiteralPath $Path -Raw
if ([string]::IsNullOrWhiteSpace($raw)) {
    Write-Host "devices.json vazio em $Path"
    exit 0
}

$records = $raw | ConvertFrom-Json
if ($null -eq $records) {
    Write-Host "Nenhum registro encontrado em $Path"
    exit 0
}

if ($records -isnot [System.Collections.IEnumerable] -or $records -is [string]) {
    $records = @($records)
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = "$Path.bak-$timestamp"
Copy-Item -LiteralPath $Path -Destination $backupPath -Force

$changed = 0

function Test-ValidTimestamp {
    param([object]$Value)
    if ($null -eq $Value) { return $false }
    try {
        $parsed = [DateTimeOffset]::Parse($Value.ToString())
        return $parsed -ne [DateTimeOffset]::MinValue
    }
    catch {
        return $false
    }
}

function Resolve-SeedTimestamp {
    param(
        [object]$LastSeenUtc,
        [object]$CreatedAtUtc
    )

    if (Test-ValidTimestamp $LastSeenUtc) {
        return [DateTimeOffset]::Parse($LastSeenUtc.ToString()).ToString('o')
    }

    if (Test-ValidTimestamp $CreatedAtUtc) {
        return [DateTimeOffset]::Parse($CreatedAtUtc.ToString()).ToString('o')
    }

    return [DateTimeOffset]::UtcNow.ToString('o')
}

foreach ($record in $records) {
    $recordChanged = $false
    $props = $record.PSObject.Properties

    if (-not $props.Match('IsRegistered')) {
        Add-Member -InputObject $record -MemberType NoteProperty -Name IsRegistered -Value $true
        $recordChanged = $true
    }

    $seedTimestamp = Resolve-SeedTimestamp -LastSeenUtc $record.LastSeenUtc -CreatedAtUtc $record.CreatedAtUtc

    if (-not $props.Match('FirstSeenUtc') -or -not (Test-ValidTimestamp $record.FirstSeenUtc)) {
        if ($props.Match('FirstSeenUtc')) {
            $record.FirstSeenUtc = $seedTimestamp
        }
        else {
            Add-Member -InputObject $record -MemberType NoteProperty -Name FirstSeenUtc -Value $seedTimestamp
        }
        $recordChanged = $true
    }

    if (-not $props.Match('LastAuthUtc') -or -not (Test-ValidTimestamp $record.LastAuthUtc)) {
        if ($props.Match('LastAuthUtc')) {
            $record.LastAuthUtc = $seedTimestamp
        }
        else {
            Add-Member -InputObject $record -MemberType NoteProperty -Name LastAuthUtc -Value $seedTimestamp
        }
        $recordChanged = $true
    }

    if (-not $props.Match('ConfigState')) {
        Add-Member -InputObject $record -MemberType NoteProperty -Name ConfigState -Value 0
        $recordChanged = $true
    }

    if ($recordChanged) {
        $changed++
    }
}

$records | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding UTF8

Write-Host "Backup criado em: $backupPath"
Write-Host "Registros lidos: $($records.Count)"
Write-Host "Registros alterados: $changed"

