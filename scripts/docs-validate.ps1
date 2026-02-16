[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Normalize-Anchor {
    param([Parameter(Mandatory)][string]$Text)

    $trimmed = $Text.Trim().ToLowerInvariant()
    $normalized = $trimmed.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object System.Text.StringBuilder
    foreach ($char in $normalized.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($char) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$sb.Append($char)
        }
    }

    $noDiacritics = $sb.ToString()
    $clean = $noDiacritics -replace '[`*_[\]{}()<>,.:;!?"''/\\|@#$%^&=+~]', ''
    $clean = $clean -replace '\s+', '-'
    $clean = $clean -replace '-+', '-'
    return $clean.Trim('-')
}

function Get-MarkdownAnchors {
    param([Parameter(Mandatory)][string]$FilePath)

    $anchors = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($line in (Get-Content -LiteralPath $FilePath)) {
        if ($line -match '^\s{0,3}#{1,6}\s+(.+?)\s*$') {
            $anchor = Normalize-Anchor -Text $Matches[1]
            if ($anchor) {
                [void]$anchors.Add($anchor)
            }
        }
    }

    return $anchors
}

function Resolve-LinkPath {
    param(
        [Parameter(Mandatory)][string]$RawPath,
        [Parameter(Mandatory)][string]$CurrentFile
    )

    $decoded = [System.Uri]::UnescapeDataString($RawPath)
    if ([IO.Path]::IsPathRooted($decoded)) {
        return $decoded
    }

    $currentDir = Split-Path -Parent $CurrentFile
    return [IO.Path]::GetFullPath((Join-Path $currentDir $decoded))
}

function To-RepoRelative {
    param([Parameter(Mandatory)][string]$Path)

    $full = (Resolve-Path -LiteralPath $Path).Path
    if ($full.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ($full.Substring($repoRoot.Length).TrimStart([char]'\', [char]'/')) -replace '\\', '/'
    }

    return $full -replace '\\', '/'
}

function Get-MarkdownLinks {
    param([Parameter(Mandatory)][string]$FilePath)

    $content = Get-Content -LiteralPath $FilePath -Raw
    $matches = [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')
    foreach ($match in $matches) {
        $rawLink = $match.Groups[1].Value.Trim()
        if ([string]::IsNullOrWhiteSpace($rawLink)) {
            continue
        }

        [pscustomobject]@{
            FilePath = $FilePath
            RawLink  = $rawLink
        }
    }
}

function Validate-Link {
    param(
        [Parameter(Mandatory)]$LinkItem,
        [System.Collections.Generic.List[string]]$Errors,
        [ref]$ValidatedCodeLinks
    )

    $raw = $LinkItem.RawLink
    $sourceFile = $LinkItem.FilePath

    if ($raw.StartsWith('http://') -or $raw.StartsWith('https://') -or $raw.StartsWith('mailto:')) {
        return
    }

    $pathPart = $raw
    $fragment = $null
    $hashIndex = $raw.IndexOf('#')
    if ($hashIndex -ge 0) {
        $pathPart = $raw.Substring(0, $hashIndex)
        $fragment = $raw.Substring($hashIndex + 1)
    }

    if ([string]::IsNullOrWhiteSpace($pathPart)) {
        if ([string]::IsNullOrWhiteSpace($fragment)) {
            return
        }

        $anchors = Get-MarkdownAnchors -FilePath $sourceFile
        $normalized = Normalize-Anchor -Text $fragment
        if (-not $anchors.Contains($normalized)) {
            $Errors.Add("Ancora interna invalida em $(To-RepoRelative -Path $sourceFile) -> #$fragment")
        }

        return
    }

    $resolved = Resolve-LinkPath -RawPath $pathPart -CurrentFile $sourceFile
    if (-not (Test-Path -LiteralPath $resolved)) {
        $Errors.Add("Link quebrado em $(To-RepoRelative -Path $sourceFile) -> $raw")
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($fragment)) {
        if ($fragment -match '^L(\d+)$') {
            $lineNumber = [int]$Matches[1]
            $lineCount = (Get-Content -LiteralPath $resolved).Count
            if ($lineNumber -lt 1 -or $lineNumber -gt $lineCount) {
                $Errors.Add("Linha invalida em link $raw (arquivo tem $lineCount linhas)")
                return
            }
        }
        elseif ([IO.Path]::GetExtension($resolved) -ieq '.md') {
            $anchors = Get-MarkdownAnchors -FilePath $resolved
            $normalized = Normalize-Anchor -Text $fragment
            if (-not $anchors.Contains($normalized)) {
                $Errors.Add("Ancora markdown invalida em $(To-RepoRelative -Path $sourceFile) -> $raw")
                return
            }
        }
    }

    $relativeResolved = To-RepoRelative -Path $resolved
    if ($relativeResolved.StartsWith('src/') -or $relativeResolved.StartsWith('firmware/') -or $relativeResolved.StartsWith('scripts/')) {
        $ValidatedCodeLinks.Value++
    }
}

$errors = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'

$expectedWikiFiles = @(
    'docs/wiki/README.md',
    'docs/wiki/architecture/01-system-overview.md',
    'docs/wiki/architecture/02-runtime-lifecycle.md',
    'docs/wiki/architecture/03-data-contracts.md',
    'docs/wiki/architecture/04-threading-concurrency.md',
    'docs/wiki/architecture/05-device-session-and-reconnect.md',
    'docs/wiki/architecture/06-errors-timeouts-and-recovery.md',
    'docs/wiki/modules/app-winui.md',
    'docs/wiki/modules/audio-loopback.md',
    'docs/wiki/modules/analyzer-dsp.md',
    'docs/wiki/modules/visual-win2d.md',
    'docs/wiki/modules/output-led.md',
    'docs/wiki/modules/device-server-protocol.md',
    'docs/wiki/modules/firmware-matrixportal-s3.md',
    'docs/wiki/modules/settings-presets-persistence.md',
    'docs/wiki/modules/device-operations-coordinator.md',
    'docs/wiki/modules/apps-catalog-deployment.md',
    'docs/wiki/modules/server-build-and-artifacts.md',
    'docs/wiki/guides/change-visualizer-settings.md',
    'docs/wiki/guides/add-new-renderer.md',
    'docs/wiki/guides/add-device-command.md',
    'docs/wiki/guides/build-export-firmware.md',
    'docs/wiki/guides/debug-no-visualization.md',
    'docs/wiki/guides/add-app-catalog-item.md',
    'docs/wiki/guides/operate-device-lifecycle.md',
    'docs/wiki/guides/debug-ota-http-failure.md',
    'docs/wiki/guides/release-doc-checklist.md',
    'docs/wiki/reference/code-index.md',
    'docs/wiki/reference/linking-conventions.md',
    'docs/wiki/reference/http-api-v1.md',
    'docs/wiki/reference/ws-protocol-v1.md',
    'docs/wiki/reference/troubleshooting-matrix.md',
    'docs/wiki/reference/docs-health.md',
    'docs/wiki/reference/glossary.md',
    'docs/wiki/ai/README.md',
    'docs/wiki/ai/agent-entrypoint.md',
    'docs/wiki/ai/task-lifecycle.md',
    'docs/wiki/ai/change-classification.md',
    'docs/wiki/ai/validation-matrix.md',
    'docs/wiki/ai/incident-playbooks.md',
    'docs/wiki/ai/mcp-viability.md',
    'docs/wiki/_templates/module-page-template.md',
    'docs/wiki/_templates/guide-template.md',
    'docs/wiki/_templates/ai-change-handoff-template.md'
)

foreach ($relative in $expectedWikiFiles) {
    $target = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $target)) {
        $errors.Add("Arquivo esperado ausente: $relative")
    }
}

$requiredGovernanceFiles = @(
    'AGENTS.md',
    'docs/wiki/reference/ai-contract.v1.yaml',
    'docs/wiki/reference/ai-contract.schema.json',
    'docs/handoffs/README.md'
)

foreach ($relative in $requiredGovernanceFiles) {
    $target = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $target)) {
        $errors.Add("Arquivo de governanca ausente: $relative")
    }
}

$handoffFolder = Join-Path $repoRoot 'docs/handoffs'
if (-not (Test-Path -LiteralPath $handoffFolder)) {
    $errors.Add('Pasta de handoff ausente: docs/handoffs')
}
else {
    $handoffFiles = @(Get-ChildItem -LiteralPath $handoffFolder -Filter '*.md' -File |
        Where-Object { $_.Name -ne 'README.md' })

    if ($handoffFiles.Count -eq 0) {
        $warnings.Add('Nenhum handoff encontrado em docs/handoffs (normal se ainda nao houve mudanca estrutural).')
    }
}

$aiContractPath = Join-Path $repoRoot 'docs/wiki/reference/ai-contract.v1.yaml'
$aiSchemaPath = Join-Path $repoRoot 'docs/wiki/reference/ai-contract.schema.json'
if ((Test-Path -LiteralPath $aiContractPath) -and (Test-Path -LiteralPath $aiSchemaPath)) {
    try {
        $aiContract = Get-Content -LiteralPath $aiContractPath -Raw | ConvertFrom-Json
    }
    catch {
        $errors.Add("Falha ao parsear ai-contract.v1.yaml como JSON/YAML valido: $($_.Exception.Message)")
        $aiContract = $null
    }

    try {
        $aiSchema = Get-Content -LiteralPath $aiSchemaPath -Raw | ConvertFrom-Json
    }
    catch {
        $errors.Add("Falha ao parsear ai-contract.schema.json: $($_.Exception.Message)")
        $aiSchema = $null
    }

    if ($null -ne $aiContract -and $null -ne $aiSchema) {
        foreach ($required in $aiSchema.required) {
            if ($null -eq $aiContract.PSObject.Properties[$required]) {
                $errors.Add("ai-contract sem chave obrigatoria definida no schema: $required")
            }
        }
    }
}

$markdownFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/wiki') -Filter '*.md' -File -Recurse)
$totalWikiFiles = $markdownFiles.Count

$validatedCodeLinks = 0
foreach ($mdFile in $markdownFiles) {
    foreach ($link in (Get-MarkdownLinks -FilePath $mdFile.FullName)) {
        Validate-Link -LinkItem $link -Errors $errors -ValidatedCodeLinks ([ref]$validatedCodeLinks)
    }
}

$moduleFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/wiki/modules') -Filter '*.md' -File)
foreach ($moduleFile in $moduleFiles) {
    $content = Get-Content -LiteralPath $moduleFile.FullName -Raw
    if ($content -notmatch '(?im)^##\s+Referencias de codigo\s*$') {
        $errors.Add("Secao obrigatoria ausente em modulo: $(To-RepoRelative -Path $moduleFile.FullName) -> '## Referencias de codigo'")
    }
}

$requiredGuideSections = @('## Objetivo', '## Passos', '## Referencias de codigo', '## Checklist rapido')
$guideFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/wiki/guides') -Filter '*.md' -File)
foreach ($guideFile in $guideFiles) {
    $content = Get-Content -LiteralPath $guideFile.FullName -Raw
    foreach ($section in $requiredGuideSections) {
        if ($content -notmatch [regex]::Escape($section)) {
            $errors.Add("Secao obrigatoria ausente em guia: $(To-RepoRelative -Path $guideFile.FullName) -> '$section'")
        }
    }
}

$requiredArchitectureSections = @('## Objetivo', '## Referencias de codigo')
$architectureFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/wiki/architecture') -Filter '*.md' -File)
foreach ($archFile in $architectureFiles) {
    $content = Get-Content -LiteralPath $archFile.FullName -Raw
    foreach ($section in $requiredArchitectureSections) {
        if ($content -notmatch [regex]::Escape($section)) {
            $errors.Add("Secao obrigatoria ausente em arquitetura: $(To-RepoRelative -Path $archFile.FullName) -> '$section'")
        }
    }
}

$adrReadme = Join-Path $repoRoot 'docs/adr/README.md'
$adrTemplate = Join-Path $repoRoot 'docs/adr/_template.md'
if (-not (Test-Path -LiteralPath $adrReadme)) {
    $errors.Add('Arquivo ADR obrigatorio ausente: docs/adr/README.md')
}
if (-not (Test-Path -LiteralPath $adrTemplate)) {
    $errors.Add('Arquivo ADR obrigatorio ausente: docs/adr/_template.md')
}

$adrFiles = @()
if (Test-Path -LiteralPath (Join-Path $repoRoot 'docs/adr')) {
    $adrFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/adr') -Filter '*.md' -File |
        Where-Object { $_.Name -ne 'README.md' -and $_.Name -ne '_template.md' } |
        Sort-Object Name)
}

if ($adrFiles.Count -eq 0) {
    $errors.Add('Nenhum ADR encontrado em docs/adr (esperado padrao NNNN-slug.md).')
}

$adrNumbers = New-Object 'System.Collections.Generic.List[int]'
$requiredAdrSections = @('## Contexto', '## Decisao', '## Consequencias', '## Status')
foreach ($adrFile in $adrFiles) {
    if ($adrFile.Name -notmatch '^(\d{4})-[a-z0-9][a-z0-9-]*\.md$') {
        $errors.Add("ADR fora do padrao NNNN-slug-kebab-case: docs/adr/$($adrFile.Name)")
        continue
    }

    $adrNumbers.Add([int]$Matches[1])

    $content = Get-Content -LiteralPath $adrFile.FullName -Raw
    foreach ($section in $requiredAdrSections) {
        if ($content -notmatch [regex]::Escape($section)) {
            $errors.Add("Secao obrigatoria ausente em ADR: docs/adr/$($adrFile.Name) -> '$section'")
        }
    }
}

for ($i = 1; $i -lt $adrNumbers.Count; $i++) {
    if ($adrNumbers[$i] -le $adrNumbers[$i - 1]) {
        $errors.Add('Ordem de ADRs invalida em docs/adr (numeracao deve ser crescente).')
        break
    }
}

if (Test-Path -LiteralPath $adrReadme) {
    $adrReadmeContent = Get-Content -LiteralPath $adrReadme -Raw

    foreach ($adrFile in $adrFiles) {
        if ($adrReadmeContent -notmatch [regex]::Escape($adrFile.Name)) {
            $errors.Add("ADR nao listado em docs/adr/README.md: $($adrFile.Name)")
        }
    }

    $listedAdrMatches = [regex]::Matches($adrReadmeContent, '\((\d{4}-[a-z0-9][a-z0-9-]*\.md)\)')
    $listedAdrNumbers = New-Object 'System.Collections.Generic.List[int]'
    foreach ($match in $listedAdrMatches) {
        $listedFile = $match.Groups[1].Value
        $listedPath = Join-Path $repoRoot ('docs/adr/' + $listedFile)
        if (-not (Test-Path -LiteralPath $listedPath)) {
            $errors.Add("README de ADR referencia arquivo inexistente: docs/adr/$listedFile")
            continue
        }

        if ($listedFile -match '^(\d{4})-') {
            $listedAdrNumbers.Add([int]$Matches[1])
        }
    }

    for ($i = 1; $i -lt $listedAdrNumbers.Count; $i++) {
        if ($listedAdrNumbers[$i] -le $listedAdrNumbers[$i - 1]) {
            $errors.Add('Ordem de ADRs invalida em docs/adr/README.md (numeracao deve ser crescente).')
            break
        }
    }
}

$indexFiles = @(
    (Join-Path $repoRoot 'docs/wiki/README.md'),
    (Join-Path $repoRoot 'docs/wiki/reference/code-index.md')
)
$indexedPages = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($indexFile in $indexFiles) {
    foreach ($link in (Get-MarkdownLinks -FilePath $indexFile)) {
        $raw = $link.RawLink
        if ($raw.StartsWith('http://') -or $raw.StartsWith('https://') -or $raw.StartsWith('mailto:')) {
            continue
        }

        $pathPart = $raw
        $hashIndex = $raw.IndexOf('#')
        if ($hashIndex -ge 0) {
            $pathPart = $raw.Substring(0, $hashIndex)
        }

        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }

        $resolved = Resolve-LinkPath -RawPath $pathPart -CurrentFile $indexFile
        if ((Test-Path -LiteralPath $resolved) -and ([IO.Path]::GetExtension($resolved) -ieq '.md')) {
            $relative = To-RepoRelative -Path $resolved
            if ($relative.ToLowerInvariant().StartsWith('docs/wiki/')) {
                [void]$indexedPages.Add((Resolve-Path -LiteralPath $resolved).Path)
            }
        }
    }
}

foreach ($wikiFile in $markdownFiles) {
    $full = (Resolve-Path -LiteralPath $wikiFile.FullName).Path
    $relative = To-RepoRelative -Path $full

    if ($relative -eq 'docs/wiki/README.md') {
        continue
    }

    if (-not $indexedPages.Contains($full)) {
        $errors.Add("Pagina wiki nao referenciada por indice: $relative")
    }
}

$codeIndexFile = Join-Path $repoRoot 'docs/wiki/reference/code-index.md'
$codeIndexLinks = Get-MarkdownLinks -FilePath $codeIndexFile
$codeIndexCodeLinks = 0
foreach ($link in $codeIndexLinks) {
    $raw = $link.RawLink
    if ($raw.StartsWith('http://') -or $raw.StartsWith('https://') -or $raw.StartsWith('mailto:')) {
        continue
    }

    $pathPart = $raw
    $fragment = $null
    $hash = $raw.IndexOf('#')
    if ($hash -ge 0) {
        $pathPart = $raw.Substring(0, $hash)
        $fragment = $raw.Substring($hash + 1)
    }

    if ([string]::IsNullOrWhiteSpace($pathPart)) {
        continue
    }

    $resolved = Resolve-LinkPath -RawPath $pathPart -CurrentFile $codeIndexFile
    if (-not (Test-Path -LiteralPath $resolved)) {
        $errors.Add("Code index com link quebrado: $raw")
        continue
    }

    $relativeResolved = To-RepoRelative -Path $resolved
    if ($relativeResolved.StartsWith('src/') -or $relativeResolved.StartsWith('firmware/') -or $relativeResolved.StartsWith('scripts/') -or $relativeResolved -eq 'AGENTS.md') {
        $codeIndexCodeLinks++

        if ($relativeResolved -eq 'AGENTS.md') {
            continue
        }

        if ([string]::IsNullOrWhiteSpace($fragment) -or ($fragment -notmatch '^L(\d+)$')) {
            $errors.Add("Code index deve usar ancora de linha (#LNN): $raw")
            continue
        }

        $line = [int]$Matches[1]
        $lineCount = (Get-Content -LiteralPath $resolved).Count
        if ($line -lt 1 -or $line -gt $lineCount) {
            $errors.Add("Code index aponta para linha invalida: $raw (arquivo tem $lineCount linhas)")
        }
    }
}

if ($codeIndexCodeLinks -eq 0) {
    $errors.Add('Code index sem links de codigo validos.')
}

$docsCoverageTargets = @(
    'src/App.WinUI/App.xaml.cs',
    'src/App.WinUI/Views/ShellPage.xaml.cs',
    'src/App.WinUI/Views/MainPage.xaml.cs',
    'src/App.WinUI/Services/AudioPipelineCoordinator.cs',
    'src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs',
    'src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs',
    'src/Visual.Win2D/Engine/VisualizerEngine.cs',
    'src/Output/Led/MatrixPortalLedOutput.cs',
    'src/Device.Server/Hosting/DeviceServerHost.cs',
    'src/Device.Server/Hosting/DeviceServerHost.Advanced.cs',
    'firmware/matrixportal-s3/src/main.cpp',
    'src/App.WinUI/Services/Devices/DeviceIntegrationService.cs',
    'src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs',
    'src/App.WinUI/Services/Devices/FirmwareBuildService.cs',
    'src/App.WinUI/Services/Apps/AppCatalogService.cs',
    'src/App.WinUI/Services/Apps/AppDeploymentService.cs',
    'src/App.WinUI/Views/DevicesPage.xaml.cs',
    'src/App.WinUI/Views/AppsPage.xaml.cs',
    'src/App.WinUI/Views/ServerPage.xaml.cs',
    'src/App.WinUI/Services/AppSettingsDomainService.cs',
    'src/Device.Protocol/Stream/StreamFrameV1.cs',
    'src/Device.Protocol/Models/DeviceCommandRequest.cs',
    'src/Device.Protocol/Models/DeviceCommandProgressMessage.cs'
)

$totalDocsBacklinks = 0
foreach ($relative in $docsCoverageTargets) {
    $full = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $full)) {
        $errors.Add("Arquivo-chave ausente para validacao DOCS: $relative")
        continue
    }

    $docLines = Select-String -Path $full -Pattern 'DOCS:\s+([^\s]+)' -AllMatches
    $references = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $docLines) {
        foreach ($match in $entry.Matches) {
            $references.Add($match.Groups[1].Value.Trim())
        }
    }

    $totalDocsBacklinks += $references.Count

    if ($references.Count -lt 2) {
        $errors.Add("Arquivo-chave sem minimo de 2 marcadores DOCS: $relative")
    }

    foreach ($reference in $references) {
        $docPathPart = $reference
        $anchorPart = $null
        $hash = $reference.IndexOf('#')
        if ($hash -ge 0) {
            $docPathPart = $reference.Substring(0, $hash)
            $anchorPart = $reference.Substring($hash + 1)
        }

        $docTarget = Join-Path $repoRoot $docPathPart
        if (-not (Test-Path -LiteralPath $docTarget)) {
            $errors.Add("DOCS quebrado em $relative -> $reference")
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($anchorPart)) {
            $anchors = Get-MarkdownAnchors -FilePath $docTarget
            $normalized = Normalize-Anchor -Text $anchorPart
            if (-not $anchors.Contains($normalized)) {
                $errors.Add("DOCS ancora invalida em $relative -> $reference")
            }
        }
    }
}

Write-Host '[docs-validate] Summary:' -ForegroundColor Cyan
Write-Host " - wiki_files_total: $totalWikiFiles" -ForegroundColor Cyan
Write-Host " - wiki_to_code_links_validated: $validatedCodeLinks" -ForegroundColor Cyan
Write-Host " - docs_backlinks_found: $totalDocsBacklinks" -ForegroundColor Cyan

if ($errors.Count -eq 0) {
    Write-Host '[docs-validate] OK: nenhuma falha encontrada.' -ForegroundColor Green
    if ($warnings.Count -gt 0) {
        Write-Host '[docs-validate] Warnings:' -ForegroundColor Yellow
        foreach ($warning in $warnings) {
            Write-Host " - $warning" -ForegroundColor Yellow
        }
    }

    exit 0
}

Write-Host '[docs-validate] Falhas encontradas:' -ForegroundColor Red
foreach ($issue in $errors) {
    Write-Host " - $issue" -ForegroundColor Red
}

if ($warnings.Count -gt 0) {
    Write-Host '[docs-validate] Warnings:' -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

exit 1

