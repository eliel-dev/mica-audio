# DOCS: docs/wiki/modules/server-build-and-artifacts.md#server-standalone
# DOCS: docs/handoffs/2026-04-28-docker-local-redeploy-script.md
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$ImageName = "micaaudio-server:dev",

    [ValidateNotNullOrEmpty()]
    [string]$ContainerName = "mica-audio-server",

    [string]$AdminToken = "dev-token",

    [string]$PublicHost = "",

    [ValidateRange(1, 65535)]
    [int]$HttpPort = 5272,

    [ValidateRange(1, 65535)]
    [int]$ContainerHttpPort = 8080,

    [ValidateRange(1, 65535)]
    [int]$MqttPort = 5273,

    [ValidateRange(1, 65535)]
    [int]$VisualUdpPort = 5274,

    [ValidateRange(1, 65535)]
    [int]$DiscoveryUdpPort = 5275,

    [ValidateNotNullOrEmpty()]
    [string]$StorageVolume = "mica-audio-server-data",

    [switch]$NoCache,

    [switch]$FollowLogs,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[docker-server-redeploy] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  OK  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  WARN $Message" -ForegroundColor Yellow
}

function Format-CommandLine {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    $items = @($FileName)
    foreach ($argument in $Arguments) {
        if ($null -eq $argument) {
            continue
        }

        $text = [string]$argument
        if ($text.Length -eq 0 -or $text.IndexOfAny([char[]]" `"`t") -ge 0) {
            $text = '"' + ($text -replace '"', '\"') + '"'
        }

        $items += $text
    }

    return ($items -join " ")
}

function Invoke-External {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrEmpty()]
        [string]$FileName,

        [string[]]$Arguments,

        [string[]]$DisplayArguments = $null,

        [switch]$CaptureOutput,

        [switch]$RunInDryRun
    )

    if ($null -eq $DisplayArguments) {
        $DisplayArguments = $Arguments
    }

    Write-Step (Format-CommandLine -FileName $FileName -Arguments $DisplayArguments)

    if ($DryRun -and -not $RunInDryRun) {
        return @()
    }

    if ($CaptureOutput) {
        $output = & $FileName @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $details = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            throw "Comando falhou com exit code ${exitCode}: $(Format-CommandLine -FileName $FileName -Arguments $DisplayArguments)$([Environment]::NewLine)$details"
        }

        return $output
    }

    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Comando falhou com exit code ${LASTEXITCODE}: $(Format-CommandLine -FileName $FileName -Arguments $DisplayArguments)"
    }

    return @()
}

function Assert-DockerReady {
    $cliVersion = @(Invoke-External -FileName "docker" -Arguments @("--version") -CaptureOutput -RunInDryRun)
    if ($cliVersion.Count -gt 0) {
        Write-Ok "Docker CLI: $($cliVersion[0])"
    }

    if ($DryRun) {
        Write-Warn "Dry-run ativo: check do daemon Docker sera pulado."
        return
    }

    $serverVersion = @(Invoke-External -FileName "docker" -Arguments @("version", "--format", "{{.Server.Version}}") -CaptureOutput)
    if ($serverVersion.Count -eq 0 -or [string]::IsNullOrWhiteSpace($serverVersion[0])) {
        throw "Docker daemon nao respondeu. Abra o Docker Desktop e tente novamente."
    }

    Write-Ok "Docker daemon: $($serverVersion[0])"
}

function Test-PrivateIpv4 {
    param([System.Net.IPAddress]$Address)

    if ($null -eq $Address -or $Address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
        return $false
    }

    $bytes = $Address.GetAddressBytes()
    return $bytes[0] -eq 10 `
        -or ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) `
        -or ($bytes[0] -eq 192 -and $bytes[1] -eq 168)
}

function Test-LoopbackOrApipa {
    param([System.Net.IPAddress]$Address)

    if ($null -eq $Address) {
        return $true
    }

    $bytes = $Address.GetAddressBytes()
    return [System.Net.IPAddress]::IsLoopback($Address) `
        -or ($bytes.Length -ge 2 -and $bytes[0] -eq 169 -and $bytes[1] -eq 254)
}

function Test-LikelyVirtualAdapter {
    param([System.Net.NetworkInformation.NetworkInterface]$Adapter)

    $keywords = @(
        "virtual",
        "vethernet",
        "hyper-v",
        "docker",
        "wsl",
        "vmware",
        "virtualbox",
        "loopback",
        "tunnel",
        "tap"
    )

    $descriptor = "$($Adapter.Name) $($Adapter.Description)"
    foreach ($keyword in $keywords) {
        if ($descriptor.IndexOf($keyword, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Test-PreferredPhysicalAdapter {
    param([System.Net.NetworkInformation.NetworkInterface]$Adapter)

    return $Adapter.NetworkInterfaceType -in @(
        [System.Net.NetworkInformation.NetworkInterfaceType]::Wireless80211,
        [System.Net.NetworkInformation.NetworkInterfaceType]::Ethernet,
        [System.Net.NetworkInformation.NetworkInterfaceType]::GigabitEthernet,
        [System.Net.NetworkInformation.NetworkInterfaceType]::FastEthernetFx,
        [System.Net.NetworkInformation.NetworkInterfaceType]::FastEthernetT
    )
}

function Resolve-PublicEndpoint {
    param([string]$ExplicitHost)

    $trimmed = if ($null -eq $ExplicitHost) { "" } else { $ExplicitHost.Trim() }
    if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
        if ($trimmed.Equals("localhost", [StringComparison]::OrdinalIgnoreCase) -or $trimmed.StartsWith("127.", [StringComparison]::OrdinalIgnoreCase)) {
            throw "PublicHost nao pode ser localhost/127.x para ESP fisico. Use o IP LAN do PC, por exemplo -PublicHost 192.168.1.50."
        }

        return [pscustomobject]@{
            Address = $trimmed
            Adapter = "manual"
            Score = 1000
        }
    }

    $candidates = @()
    foreach ($adapter in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
        if ($adapter.OperationalStatus -ne [System.Net.NetworkInformation.OperationalStatus]::Up) {
            continue
        }

        if ($adapter.NetworkInterfaceType -eq [System.Net.NetworkInformation.NetworkInterfaceType]::Loopback `
            -or $adapter.NetworkInterfaceType -eq [System.Net.NetworkInformation.NetworkInterfaceType]::Tunnel `
            -or (Test-LikelyVirtualAdapter -Adapter $adapter)) {
            continue
        }

        foreach ($unicast in $adapter.GetIPProperties().UnicastAddresses) {
            $address = $unicast.Address
            if ($address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork `
                -or (Test-LoopbackOrApipa -Address $address) `
                -or -not (Test-PrivateIpv4 -Address $address)) {
                continue
            }

            $score = 0
            if (Test-PreferredPhysicalAdapter -Adapter $adapter) {
                $score += 100
            }

            if ($adapter.NetworkInterfaceType -eq [System.Net.NetworkInformation.NetworkInterfaceType]::Wireless80211) {
                $score += 20
            }
            elseif ($adapter.NetworkInterfaceType -eq [System.Net.NetworkInformation.NetworkInterfaceType]::Ethernet) {
                $score += 10
            }

            $candidates += [pscustomobject]@{
                Address = $address.ToString()
                Adapter = $adapter.Name
                Score = $score
            }
        }
    }

    $selected = $candidates |
        Sort-Object -Property @{ Expression = { $_.Score }; Descending = $true }, @{ Expression = { $_.Address }; Ascending = $true } |
        Select-Object -First 1

    if ($null -eq $selected) {
        throw "Nao foi possivel detectar um IPv4 LAN privado. Rode novamente com -PublicHost <IP_DO_PC>."
    }

    return $selected
}

function Get-DockerContainerNames {
    param([switch]$RunningOnly)

    $arguments = @("ps", "--format", "{{.Names}}")
    if (-not $RunningOnly) {
        $arguments = @("ps", "-a", "--format", "{{.Names}}")
    }

    $names = Invoke-External -FileName "docker" -Arguments $arguments -CaptureOutput
    return @($names | ForEach-Object { $_.ToString().Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Remove-ExistingContainer {
    param([string]$Name)

    if ($DryRun) {
        Write-Step "Dry-run: pararia e removeria somente o container '$Name' se ele existisse."
        return
    }

    $allContainers = Get-DockerContainerNames
    if ($allContainers -notcontains $Name) {
        Write-Ok "Nenhum container existente com nome '$Name'."
        return
    }

    $runningContainers = Get-DockerContainerNames -RunningOnly
    if ($runningContainers -contains $Name) {
        Invoke-External -FileName "docker" -Arguments @("stop", $Name) | Out-Null
    }

    Invoke-External -FileName "docker" -Arguments @("rm", $Name) | Out-Null
}

function Ensure-StorageVolume {
    param([string]$Name)

    if ($DryRun) {
        Write-Step "Dry-run: garantiria o volume Docker '$Name'."
        return
    }

    $volumes = Invoke-External -FileName "docker" -Arguments @("volume", "ls", "--format", "{{.Name}}") -CaptureOutput
    $volumeNames = @($volumes | ForEach-Object { $_.ToString().Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($volumeNames -contains $Name) {
        Write-Ok "Volume persistente existente: $Name"
        return
    }

    Invoke-External -FileName "docker" -Arguments @("volume", "create", $Name) | Out-Null
}

function Build-ServerImage {
    param(
        [string]$RepoRoot,
        [string]$Tag
    )

    $dockerfile = Join-Path $RepoRoot "src\MicaAudio.Server\Dockerfile"
    if (-not (Test-Path $dockerfile)) {
        throw "Dockerfile nao encontrado: $dockerfile"
    }

    $arguments = @("build", "-f", $dockerfile, "-t", $Tag)
    if ($NoCache) {
        $arguments += "--no-cache"
    }

    $arguments += $RepoRoot
    Invoke-External -FileName "docker" -Arguments $arguments | Out-Null
}

function Start-ServerContainer {
    param(
        [string]$Name,
        [string]$Tag,
        [string]$LanHost,
        [string]$VolumeName
    )

    $httpBase = "http://${LanHost}:$HttpPort"
    $arguments = @(
        "run",
        "-d",
        "--name", $Name,
        "--restart", "unless-stopped",
        "-e", "PORT=$ContainerHttpPort",
        "-e", "MICA_SERVER__STORAGEROOT=/data",
        "-e", "MICA_SERVER__ADMINTOKEN=$AdminToken",
        "-e", "MICA_SERVER__RESTRICTTOPRIVATENETWORKS=false",
        "-e", "MICA_SERVER__TRUSTEDLANAUTOREGISTRATION=true",
        "-e", "MICA_SERVER__DISCOVERYUDPPORT=$DiscoveryUdpPort",
        "-e", "MICA_SERVER__MAXMEDIAUPLOADBYTES=20971520",
        "-e", "MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true",
        "-e", "MICA_SERVER__VISUALUDPPORT=$VisualUdpPort",
        "-e", "MICA_SERVER__MQTTPORT=$MqttPort",
        "-e", "MICA_SERVER__STARTUPPAIRCODETTLSECONDS=0",
        "-e", "MICA_SERVER__PUBLICHTTPBASEADDRESS=$httpBase",
        "-e", "MICA_SERVER__PUBLICHOST=$LanHost",
        "-p", "${HttpPort}:${ContainerHttpPort}",
        "-p", "${MqttPort}:${MqttPort}",
        "-p", "${VisualUdpPort}:${VisualUdpPort}/udp",
        "-p", "${DiscoveryUdpPort}:${DiscoveryUdpPort}/udp",
        "-v", "${VolumeName}:/data",
        $Tag
    )

    $displayArguments = @($arguments)
    for ($i = 0; $i -lt $displayArguments.Count; $i++) {
        if ($displayArguments[$i] -eq "MICA_SERVER__ADMINTOKEN=$AdminToken") {
            $displayArguments[$i] = "MICA_SERVER__ADMINTOKEN=***"
        }
    }

    Invoke-External -FileName "docker" -Arguments $arguments -DisplayArguments $displayArguments | Out-Null
}

function Wait-Health {
    param(
        [string]$Uri,
        [string]$Name,
        [int]$TimeoutSeconds = 30
    )

    if ($DryRun) {
        Write-Step "Dry-run: aguardaria health check em $Uri."
        return
    }

    Write-Step "Aguardando health check em $Uri"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = ""

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Ok "Health check respondeu HTTP $($response.StatusCode)."
                return
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    Write-Warn "Ultimas linhas do container:"
    & docker logs --tail 80 $Name 2>&1 | ForEach-Object { Write-Host "    $_" }
    throw "Servidor nao respondeu health check em ${TimeoutSeconds}s. Ultimo erro: $lastError"
}

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$AdminToken = if ($null -eq $AdminToken) { "" } else { $AdminToken.Trim() }
if ([string]::IsNullOrWhiteSpace($AdminToken)) {
    throw "AdminToken nao pode ser vazio. Para dev local use -AdminToken dev-token."
}

Assert-DockerReady
$endpoint = Resolve-PublicEndpoint -ExplicitHost $PublicHost
$resolvedPublicHost = $endpoint.Address
Write-Ok "IP LAN anunciado: $resolvedPublicHost ($($endpoint.Adapter))"

Ensure-StorageVolume -Name $StorageVolume
Remove-ExistingContainer -Name $ContainerName
Build-ServerImage -RepoRoot $repoRoot -Tag $ImageName
Start-ServerContainer -Name $ContainerName -Tag $ImageName -LanHost $resolvedPublicHost -VolumeName $StorageVolume

$localHealth = "http://127.0.0.1:${HttpPort}/api/v1/health"
Wait-Health -Uri $localHealth -Name $ContainerName

$lanBase = "http://${resolvedPublicHost}:$HttpPort"
Write-Host ""
Write-Host "MicaAudio.Server Docker local" -ForegroundColor White
Write-Host "  Container:       $ContainerName"
Write-Host "  Image:           $ImageName"
Write-Host "  Storage volume:  $StorageVolume -> /data"
Write-Host "  Local health:    $localHealth"
Write-Host "  LAN base:        $lanBase"
Write-Host "  MQTT:            ${resolvedPublicHost}:$MqttPort"
Write-Host "  Visual UDP:      ${resolvedPublicHost}:$VisualUdpPort/udp"
Write-Host "  Discovery UDP:   ${resolvedPublicHost}:$DiscoveryUdpPort/udp"
Write-Host "  Logs:            docker logs -f $ContainerName"
Write-Host ""
Write-Host "No portal AP do ESP, use Servidor=$lanBase apenas como fallback tecnico."

if ($FollowLogs) {
    if ($DryRun) {
        Write-Step "Dry-run: seguiria logs do container '$ContainerName'."
    }
    else {
        & docker logs -f $ContainerName
    }
}
