[CmdletBinding()]
param(
    [string]$ChangedFilesPath,
    [switch]$Fast
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot 'docs/wiki/reference/ai-contract.v1.yaml'
$schemaPath = Join-Path $repoRoot 'docs/wiki/reference/ai-contract.schema.json'
$agentsPath = Join-Path $repoRoot 'AGENTS.md'

$errors = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'

function Normalize-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = $Path.Trim().Replace('\\', '/')
    while ($normalized.StartsWith('./')) {
        $normalized = $normalized.Substring(2)
    }

    return $normalized.TrimStart('/')
}

function Test-PathPattern {
    param(
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $f = Normalize-RepoPath -Path $File
    $p = Normalize-RepoPath -Path $Pattern

    if ($p.EndsWith('/')) {
        return $f.StartsWith($p, [System.StringComparison]::OrdinalIgnoreCase)
    }

    return [string]::Equals($f, $p, [System.StringComparison]::OrdinalIgnoreCase)
}

function Ensure-Array {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return $Value
    }

    return @($Value)
}

function Invoke-GitNameOnlyDiff {
    param(
        [Parameter(Mandatory = $true)][string]$Left,
        [Parameter(Mandatory = $true)][string]$Right
    )

    $output = & git -c core.safecrlf=false diff --name-only $Left $Right 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }

    return @($output)
}

function Resolve-ChangedFiles {
    param(
        [string]$ChangedFileList,
        [switch]$FastMode
    )

    if (-not [string]::IsNullOrWhiteSpace($ChangedFileList)) {
        $resolvedPath = if ([System.IO.Path]::IsPathRooted($ChangedFileList)) {
            $ChangedFileList
        }
        else {
            Join-Path $repoRoot $ChangedFileList
        }

        if (-not (Test-Path -LiteralPath $resolvedPath)) {
            throw "ChangedFilesPath nao encontrado: $ChangedFileList"
        }

        return @(Get-Content -LiteralPath $resolvedPath |
            ForEach-Object { Normalize-RepoPath -Path $_ } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique)
    }

    $hasHead = $true
    & git -c core.safecrlf=false rev-parse --verify HEAD 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        $hasHead = $false
    }

    if (-not $hasHead) {
        $untrackedOnly = @(& git -c core.safecrlf=false ls-files --others --exclude-standard 2>$null |
            ForEach-Object { Normalize-RepoPath -Path $_ } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique)

        if ($untrackedOnly.Count -eq 0) {
            return @()
        }

        return $untrackedOnly
    }

    $changed = @()

    if ($FastMode) {
        $changed = Invoke-GitNameOnlyDiff -Left 'HEAD' -Right '--'
    }
    else {
        $changed = @(& git -c core.safecrlf=false diff --name-only HEAD 2>$null)

        if ($changed.Count -eq 0) {
            & git -c core.safecrlf=false rev-parse --verify HEAD~1 1>$null 2>$null
            if ($LASTEXITCODE -eq 0) {
                $changed = Invoke-GitNameOnlyDiff -Left 'HEAD~1' -Right 'HEAD'
            }
            else {
                $changed = @(& git -c core.safecrlf=false show --pretty='' --name-only HEAD 2>$null)
            }
        }
    }

    $untracked = @(& git -c core.safecrlf=false ls-files --others --exclude-standard 2>$null)

    return @($changed + $untracked |
        ForEach-Object { Normalize-RepoPath -Path $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique)
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Manifesto IA ausente: $manifestPath"
}

if (-not (Test-Path -LiteralPath $schemaPath)) {
    throw "Schema IA ausente: $schemaPath"
}

if (-not (Test-Path -LiteralPath $agentsPath)) {
    $errors.Add('AGENTS.md ausente na raiz do repositorio.')
}

$manifestRaw = Get-Content -LiteralPath $manifestPath -Raw
$schemaRaw = Get-Content -LiteralPath $schemaPath -Raw

try {
    # ai-contract.v1.yaml esta armazenado no repositorio em formato JSON valido.
    $manifest = $manifestRaw | ConvertFrom-Json
}
catch {
    throw "Falha ao parsear ai-contract.v1.yaml: $($_.Exception.Message)"
}

try {
    $schema = $schemaRaw | ConvertFrom-Json
}
catch {
    throw "Falha ao parsear ai-contract.schema.json: $($_.Exception.Message)"
}

foreach ($requiredKey in (Ensure-Array -Value $schema.required)) {
    if ($null -eq $manifest.PSObject.Properties[[string]$requiredKey]) {
        $errors.Add("Manifesto IA sem chave obrigatoria: $requiredKey")
    }
}

$versionValue = [string]$manifest.version
if ([string]::IsNullOrWhiteSpace($versionValue)) {
    $errors.Add('Manifesto IA sem versao valida em `version`.')
}

$structuralPaths = @(Ensure-Array -Value $manifest.structural_paths | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$docsEvidencePaths = @(Ensure-Array -Value $manifest.docs_evidence_paths | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$requiredHandoffSections = @(Ensure-Array -Value $manifest.required_handoff_sections | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$aiQualityPolicy = $manifest.ai_quality_policy
$moduleEntrypoints = @(Ensure-Array -Value $manifest.module_entrypoints)
$decisionSources = @(Ensure-Array -Value $manifest.decision_sources | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$forbiddenCommands = @(Ensure-Array -Value $manifest.forbidden_commands | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

if ($structuralPaths.Count -eq 0) { $errors.Add('Manifesto IA sem `structural_paths`.') }
if ($docsEvidencePaths.Count -eq 0) { $errors.Add('Manifesto IA sem `docs_evidence_paths`.') }
if ($requiredHandoffSections.Count -eq 0) { $errors.Add('Manifesto IA sem `required_handoff_sections`.') }
if ($null -eq $aiQualityPolicy) { $errors.Add('Manifesto IA sem `ai_quality_policy`.') }
if ($moduleEntrypoints.Count -eq 0) { $errors.Add('Manifesto IA sem `module_entrypoints`.') }
if ($decisionSources.Count -eq 0) { $errors.Add('Manifesto IA sem `decision_sources`.') }
if ($forbiddenCommands.Count -eq 0) { $errors.Add('Manifesto IA sem `forbidden_commands`.') }

if ($null -ne $aiQualityPolicy) {
    if (-not [bool]$aiQualityPolicy.require_current_best_practices) {
        $errors.Add('Manifesto IA exige `ai_quality_policy.require_current_best_practices = true`.')
    }

    if (-not [bool]$aiQualityPolicy.require_current_date_check_for_temporal_guidance) {
        $errors.Add('Manifesto IA exige `ai_quality_policy.require_current_date_check_for_temporal_guidance = true`.')
    }

    $dotnetPrimarySources = @(Ensure-Array -Value $aiQualityPolicy.dotnet_primary_sources | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($dotnetPrimarySources.Count -eq 0) {
        $errors.Add('Manifesto IA sem `ai_quality_policy.dotnet_primary_sources`.')
    }
}

if (Test-Path -LiteralPath $agentsPath) {
    $agentsRaw = Get-Content -LiteralPath $agentsPath -Raw

    if ($agentsRaw -notmatch 'Regra obrigatoria de qualidade para IA') {
        $errors.Add('AGENTS.md sem secao "Regra obrigatoria de qualidade para IA".')
    }

    if ($agentsRaw -notmatch 'melhores praticas atuais da stack alvo') {
        $errors.Add('AGENTS.md sem regra sobre melhores praticas atuais da stack alvo.')
    }

    if ($agentsRaw -notmatch 'data atual do ambiente') {
        $errors.Add('AGENTS.md sem regra sobre consulta da data atual do ambiente.')
    }

    if ($agentsRaw -notmatch 'Microsoft/CommunityToolkit') {
        $errors.Add('AGENTS.md sem regra sobre fonte primaria Microsoft/CommunityToolkit para .NET/C#.')
    }
}

foreach ($source in $decisionSources) {
    $sourcePath = Join-Path $repoRoot (Normalize-RepoPath -Path $source)
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        $errors.Add("decision_source ausente: $source")
    }
}

$changedFiles = @(Resolve-ChangedFiles -ChangedFileList $ChangedFilesPath -FastMode:$Fast)
$changedTotal = $changedFiles.Count

if ($changedTotal -eq 0) {
    $warnings.Add('Nenhum arquivo alterado detectado. Executando apenas validacao do manifesto.')
}

$structuralFiles = @()
$docsEvidenceFiles = @()
$handoffFiles = @()

foreach ($file in $changedFiles) {
    foreach ($pattern in $structuralPaths) {
        if (Test-PathPattern -File $file -Pattern $pattern) {
            $structuralFiles += $file
            break
        }
    }

    foreach ($pattern in $docsEvidencePaths) {
        if (Test-PathPattern -File $file -Pattern $pattern) {
            $docsEvidenceFiles += $file
            break
        }
    }

    if ($file.StartsWith('docs/handoffs/', [System.StringComparison]::OrdinalIgnoreCase) -and $file.EndsWith('.md', [System.StringComparison]::OrdinalIgnoreCase)) {
        $handoffFiles += $file
    }
}

$structuralFiles = @($structuralFiles | Sort-Object -Unique)
$docsEvidenceFiles = @($docsEvidenceFiles | Sort-Object -Unique)
$handoffFiles = @($handoffFiles | Sort-Object -Unique | Where-Object { $_ -notlike 'docs/handoffs/README.md' })

if ($structuralFiles.Count -gt 0 -and $docsEvidenceFiles.Count -eq 0) {
    $errors.Add('Mudanca estrutural detectada sem evidencia de docs (docs/wiki, docs/adr, docs/handoffs, README.md, AGENTS.md).')
}

if ($structuralFiles.Count -gt 0 -and $handoffFiles.Count -eq 0) {
    $errors.Add('Mudanca estrutural detectada sem handoff em docs/handoffs/YYYY-MM-DD-<slug>.md.')
}

foreach ($handoff in $handoffFiles) {
    $handoffPath = Join-Path $repoRoot $handoff
    if (-not (Test-Path -LiteralPath $handoffPath)) {
        $errors.Add("Handoff listado mas ausente: $handoff")
        continue
    }

    $content = Get-Content -LiteralPath $handoffPath -Raw
    foreach ($section in $requiredHandoffSections) {
        if ($content -notmatch [regex]::Escape([string]$section)) {
            $errors.Add("Handoff sem secao obrigatoria: $handoff -> $section")
        }
    }
}

foreach ($entry in $moduleEntrypoints) {
    $pathProp = [string]$entry.path
    if ([string]::IsNullOrWhiteSpace($pathProp)) {
        $errors.Add('module_entrypoints contem item sem path.')
        continue
    }

    $normalizedPath = Normalize-RepoPath -Path $pathProp
    $fullPath = Join-Path $repoRoot $normalizedPath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        $errors.Add("module_entrypoint ausente: $normalizedPath")
        continue
    }

    $minDocs = 2
    if ($entry.PSObject.Properties['min_docs_markers']) {
        $minDocs = [int]$entry.min_docs_markers
    }

    if ($changedFiles | Where-Object { [string]::Equals($_, $normalizedPath, [System.StringComparison]::OrdinalIgnoreCase) }) {
        $count = (Select-String -Path $fullPath -Pattern 'DOCS:\s+([^\s]+)' -AllMatches).Matches.Count
        if ($count -lt $minDocs) {
            $errors.Add("Arquivo-chave alterado sem DOCS minimo: $normalizedPath (atual=$count minimo=$minDocs)")
        }
    }
}

Write-Host '[ai-governance-check] Summary:' -ForegroundColor Cyan
Write-Host " - changed_files: $changedTotal" -ForegroundColor Cyan
Write-Host " - structural_changed: $($structuralFiles.Count)" -ForegroundColor Cyan
Write-Host " - docs_evidence: $($docsEvidenceFiles.Count)" -ForegroundColor Cyan
Write-Host " - handoffs_changed: $($handoffFiles.Count)" -ForegroundColor Cyan

if ($errors.Count -gt 0) {
    Write-Host '[ai-governance-check] Falhas encontradas:' -ForegroundColor Red
    foreach ($issue in $errors) {
        Write-Host " - $issue" -ForegroundColor Red
    }

    if ($warnings.Count -gt 0) {
        Write-Host '[ai-governance-check] Warnings:' -ForegroundColor Yellow
        foreach ($warning in $warnings) {
            Write-Host " - $warning" -ForegroundColor Yellow
        }
    }

    exit 1
}

Write-Host '[ai-governance-check] OK: governanca IA valida.' -ForegroundColor Green
if ($warnings.Count -gt 0) {
    Write-Host '[ai-governance-check] Warnings:' -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

exit 0

