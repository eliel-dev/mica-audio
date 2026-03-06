# DOCS: docs/wiki/guides/visual-studio-web-headless-dev.md#passos
# DOCS: docs/handoffs/2026-03-06-vs-green-button-web-dev.md#decisoes-tomadas

param(
    [ValidateSet('dev', 'prod')]
    [string]$Mode = 'dev',
    [switch]$NoOpen,
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$webPath = Join-Path $repoRoot 'src\Web.Headless'
$headlessProject = Join-Path $repoRoot 'src\App.Headless\App.Headless.csproj'
$childProcesses = @()
$packageJsonPath = Join-Path $webPath 'package.json'
$packageLockPath = Join-Path $webPath 'package-lock.json'
$nodeModulesPath = Join-Path $webPath 'node_modules'
$installedLockPath = Join-Path $nodeModulesPath '.package-lock.json'

function Add-ChildProcess {
    param([System.Diagnostics.Process]$Process)
    if ($null -ne $Process) {
        $script:childProcesses += $Process
    }
}

function Stop-ChildProcesses {
    foreach ($process in $script:childProcesses) {
        try {
            if ($null -ne $process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
        }
    }
}

function Open-UrlIfNeeded {
    param([string]$Url)
    if (-not $NoOpen) {
        Start-Sleep -Seconds 2
        Start-Process $Url | Out-Null
    }
}

function Wait-UntilAnyExit {
    param([System.Diagnostics.Process[]]$Processes)
    while ($true) {
        foreach ($process in $Processes) {
            if ($process.HasExited) {
                return
            }
        }

        Start-Sleep -Milliseconds 500
    }
}

function Test-WebInstallRequired {
    if (-not (Test-Path $packageJsonPath)) {
        throw "Web package.json not found: $packageJsonPath"
    }

    if (-not (Test-Path $packageLockPath)) {
        throw "Web package-lock.json not found: $packageLockPath"
    }

    if (-not (Test-Path $nodeModulesPath)) {
        Write-Host '[headless-web-run] node_modules missing; npm ci required.'
        return $true
    }

    if (-not (Test-Path $installedLockPath)) {
        Write-Host '[headless-web-run] node_modules install stamp missing; npm ci required.'
        return $true
    }

    $installedLockTime = (Get-Item $installedLockPath).LastWriteTimeUtc
    $packageLockTime = (Get-Item $packageLockPath).LastWriteTimeUtc
    $packageJsonTime = (Get-Item $packageJsonPath).LastWriteTimeUtc

    if ($packageLockTime -gt $installedLockTime -or $packageJsonTime -gt $installedLockTime) {
        Write-Host '[headless-web-run] package metadata newer than node_modules; npm ci required.'
        return $true
    }

    return $false
}

try {
    Write-Host "[headless-web-run] Mode: $Mode"

    if ($SkipInstall) {
        Write-Host '[headless-web-run] Skipping web dependency installation by flag.'
    }
    elseif (Test-WebInstallRequired) {
        Write-Host '[headless-web-run] Installing web dependencies (auto mode)...'
        Push-Location $webPath
        try {
            npm.cmd ci
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host '[headless-web-run] Web dependencies already up to date.'
    }

    if ($Mode -eq 'prod') {
        Write-Host '[headless-web-run] Building web frontend...'
        Push-Location $webPath
        try {
            npm.cmd run build
        }
        finally {
            Pop-Location
        }
    }

    Write-Host '[headless-web-run] Building backend...'
    dotnet build $headlessProject -c Debug

    if ($Mode -eq 'dev') {
        Write-Host '[headless-web-run] Starting backend on http://127.0.0.1:5175 ...'
        $backendProcess = Start-Process dotnet -ArgumentList @('run', '--project', $headlessProject, '-c', 'Debug', '--no-build') -WorkingDirectory $repoRoot -PassThru
        Add-ChildProcess -Process $backendProcess

        Write-Host '[headless-web-run] Starting Vite dev server on http://127.0.0.1:5173 ...'
        $viteProcess = Start-Process npm.cmd -ArgumentList @('run', 'dev', '--', '--host', '127.0.0.1', '--port', '5173') -WorkingDirectory $webPath -PassThru
        Add-ChildProcess -Process $viteProcess

        Open-UrlIfNeeded -Url 'http://127.0.0.1:5173'
        Wait-UntilAnyExit -Processes @($backendProcess, $viteProcess)
        return
    }

    Write-Host '[headless-web-run] Starting backend with static dist on http://127.0.0.1:5175 ...'
    $backendProcess = Start-Process dotnet -ArgumentList @('run', '--project', $headlessProject, '-c', 'Debug', '--no-build') -WorkingDirectory $repoRoot -PassThru
    Add-ChildProcess -Process $backendProcess

    Open-UrlIfNeeded -Url 'http://127.0.0.1:5175'
    Wait-Process -Id $backendProcess.Id
}
finally {
    Stop-ChildProcesses
}