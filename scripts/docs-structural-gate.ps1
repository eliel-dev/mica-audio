[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ChangedFilesPath,
    [object]$IsPullRequest = $false,
    [object]$HasDocsExemptLabel = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-ToBoolean {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string]$ParameterName
    )

    if ($Value -is [bool]) {
        return [bool]$Value
    }

    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $false
    }

    switch -Regex ($text.Trim().ToLowerInvariant()) {
        '^(1|true|yes|y|on)$' { return $true }
        '^(0|false|no|n|off)$' { return $false }
        default {
            throw "Valor invalido para ${ParameterName}: '$Value'. Use true/false ou 1/0."
        }
    }
}

$isPullRequestBool = Convert-ToBoolean -Value $IsPullRequest -ParameterName 'IsPullRequest'
$hasDocsExemptLabelBool = Convert-ToBoolean -Value $HasDocsExemptLabel -ParameterName 'HasDocsExemptLabel'

if (-not (Test-Path -LiteralPath $ChangedFilesPath)) {
    throw "Arquivo de changed files nao encontrado: $ChangedFilesPath"
}

$changedFiles = @(Get-Content -LiteralPath $ChangedFilesPath |
    ForEach-Object { $_.Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_ -replace '\\', '/' })

if ($changedFiles.Count -eq 0) {
    Write-Host '[docs-structural-gate] Nenhum arquivo alterado. Gate passou.' -ForegroundColor Green
    exit 0
}

$structuralPatterns = @(
    '^src/',
    '^firmware/',
    '^matrixportal-s3/',
    '^scripts/',
    '^MicaAudio\.sln$',
    '^global\.json$',
    '^\.github/workflows/'
)

$docsEvidencePatterns = @(
    '^docs/wiki/',
    '^docs/adr/',
    '^README\.md$',
    '^docs/handoffs/',
    '^AGENTS\.md$'
)

$structuralFiles = @()
$docsEvidenceFiles = @()

foreach ($file in $changedFiles) {
    if ($structuralPatterns | Where-Object { $file -match $_ }) {
        $structuralFiles += $file
    }

    if ($docsEvidencePatterns | Where-Object { $file -match $_ }) {
        $docsEvidenceFiles += $file
    }
}

$hasStructural = $structuralFiles.Count -gt 0
$hasDocsEvidence = $docsEvidenceFiles.Count -gt 0

Write-Host "[docs-structural-gate] changed_total=$($changedFiles.Count) structural=$($structuralFiles.Count) docs_evidence=$($docsEvidenceFiles.Count)"

if (-not $hasStructural) {
    Write-Host '[docs-structural-gate] Sem mudanca estrutural. Gate passou.' -ForegroundColor Green
    exit 0
}

if ($hasDocsEvidence) {
    Write-Host '[docs-structural-gate] Mudanca estrutural com evidencia de docs. Gate passou.' -ForegroundColor Green
    exit 0
}

if ($isPullRequestBool -and $hasDocsExemptLabelBool) {
    Write-Host '[docs-structural-gate] Bypass aplicado por label docs-exempt (apenas PR).' -ForegroundColor Yellow
    Write-Host '[docs-structural-gate] Arquivos estruturais sem docs:' -ForegroundColor Yellow
    $structuralFiles | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
    exit 0
}

Write-Host '[docs-structural-gate] Falha: mudanca estrutural sem atualizacao de docs.' -ForegroundColor Red
Write-Host '[docs-structural-gate] Arquivos estruturais detectados:' -ForegroundColor Red
$structuralFiles | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
Write-Host '[docs-structural-gate] Adicione docs em docs/wiki/, docs/adr/, docs/handoffs/, README.md ou AGENTS.md.' -ForegroundColor Red

exit 1



