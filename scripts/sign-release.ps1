[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CertPath,

    [Parameter(Mandatory = $true)]
    [string]$CertPassword,

    [Parameter(Mandatory = $true)]
    [string[]]$InputPaths,

    [string]$TimestampUrl = 'http://timestamp.digicert.com',

    [string]$ExpectedSubject
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "[sign-release] $Message" -ForegroundColor Cyan
}

function Resolve-SignToolPath {
    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    $kitRoots = @(
        'C:\Program Files (x86)\Windows Kits\10\bin',
        'C:\Program Files\Windows Kits\10\bin'
    )

    foreach ($root in $kitRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $candidate = Get-ChildItem -Path $root -Filter signtool.exe -Recurse -File -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw 'signtool.exe nao encontrado. Instale Windows SDK Build Tools no runner.'
}

function Resolve-FilesToSign {
    param([string[]]$Patterns)

    $files = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($pattern in $Patterns) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        $expanded = @(Get-ChildItem -Path $pattern -File -Recurse -ErrorAction SilentlyContinue)
        if ($expanded.Count -eq 0 -and (Test-Path -LiteralPath $pattern -PathType Leaf)) {
            $expanded = @((Get-Item -LiteralPath $pattern))
        }

        foreach ($item in $expanded) {
            [void]$files.Add($item.FullName)
        }
    }

    return @($files)
}

if (-not (Test-Path -LiteralPath $CertPath)) {
    throw "Arquivo PFX nao encontrado: $CertPath"
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedSubject)) {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $cert.Import($CertPath, $CertPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)

    if (-not [string]::Equals($cert.Subject, $ExpectedSubject, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Certificado de assinatura com Subject inesperado. Esperado='$ExpectedSubject' Atual='$($cert.Subject)'"
    }

    Write-Step "Certificado validado por Subject: $ExpectedSubject"
}

$signTool = Resolve-SignToolPath
$filesToSign = Resolve-FilesToSign -Patterns $InputPaths
if ($filesToSign.Count -eq 0) {
    throw 'Nenhum arquivo encontrado para assinatura.'
}

Write-Step "signtool: $signTool"
Write-Step "Arquivos para assinar: $($filesToSign.Count)"

foreach ($file in $filesToSign) {
    & $signTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $CertPath /p $CertPassword $file | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao assinar arquivo: $file"
    }

    $signature = Get-AuthenticodeSignature -FilePath $file
    if ($signature.Status -ne 'Valid') {
        throw "Assinatura invalida apos assinatura: $file (Status=$($signature.Status))"
    }

    Write-Host "  signed: $file"
}

Write-Step 'Assinatura concluida com sucesso.'
