[CmdletBinding()]
param(
    [switch]$SkipSelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$hooksPath = '.githooks'
$preCommit = Join-Path $repoRoot '.githooks/pre-commit'
$prePush = Join-Path $repoRoot '.githooks/pre-push'

if (-not (Test-Path -LiteralPath $preCommit) -or -not (Test-Path -LiteralPath $prePush)) {
    throw 'Hooks versionados ausentes em .githooks (pre-commit/pre-push).'
}

git config core.hooksPath $hooksPath | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Falha ao configurar core.hooksPath.'
}

$configured = git config --get core.hooksPath
if ($configured -ne $hooksPath) {
    throw "core.hooksPath inesperado: $configured"
}

# Windows costuma gerar warning de LF/CRLF aqui; nao deve quebrar o bootstrap.
cmd /c "git update-index --chmod=+x .githooks/pre-commit .githooks/pre-push >nul 2>nul"

Write-Host "[git-hooks-bootstrap] core.hooksPath=$configured" -ForegroundColor Cyan

if (-not $SkipSelfTest) {
    Write-Host '[git-hooks-bootstrap] Self-test: docs-validate' -ForegroundColor Cyan
    powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1

    Write-Host '[git-hooks-bootstrap] Self-test: ai-governance-check -Fast' -ForegroundColor Cyan
    powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -Fast
}

Write-Host '[git-hooks-bootstrap] OK: hooks ativos e validados.' -ForegroundColor Green

