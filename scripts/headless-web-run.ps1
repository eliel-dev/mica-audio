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

try {
    Write-Host "[headless-web-run] Mode: $Mode"

    if (-not $SkipInstall) {
        Write-Host '[headless-web-run] Installing web dependencies...'
        Push-Location $webPath
        try {
            npm.cmd ci
        }
        finally {
            Pop-Location
        }
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
