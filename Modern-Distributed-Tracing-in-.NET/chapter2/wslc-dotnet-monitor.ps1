[CmdletBinding()]
param(
    [ValidateSet("up", "down", "status", "logs")]
    [string]$Action = "up",

    [ValidateSet(
        "frontend",
        "storage",
        "mysql",
        "dotnetmonitor-frontend",
        "dotnetmonitor-storage",
        "prometheus"
    )]
    [string]$Service = "frontend",

    [switch]$SkipBuild,
    [switch]$RemoveVolumes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$networkName = "chapter2-dotnet-monitor"

$images = @{
    frontend = "chapter2-frontend"
    storage = "chapter2-storage"
    prometheus = "chapter2-prometheus"
}

$containers = @{
    frontend = "chapter2-frontend"
    storage = "chapter2-storage"
    mysql = "chapter2-mysql"
    "dotnetmonitor-frontend" = "chapter2-dotnetmonitor-frontend"
    "dotnetmonitor-storage" = "chapter2-dotnetmonitor-storage"
    prometheus = "chapter2-prometheus"
}

$volumes = @{
    mysql = "chapter2-mysql-data"
    frontendTmp = "chapter2-frontend-tmp"
    storageTmp = "chapter2-storage-tmp"
}

$serviceOrder = @(
    "prometheus",
    "dotnetmonitor-storage",
    "dotnetmonitor-frontend",
    "storage",
    "frontend",
    "mysql"
)

function Invoke-Wslc {
    param(
        [Parameter(Mandatory, ValueFromRemainingArguments)]
        [string[]]$WslcArguments
    )

    & wslc @WslcArguments
    if ($LASTEXITCODE -ne 0) {
        throw "wslc failed with exit code $LASTEXITCODE`: wslc $($WslcArguments -join ' ')"
    }
}

function Test-WslcObject {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("container", "network", "volume", "image")]
        [string]$Type,

        [Parameter(Mandatory)]
        [string]$Name
    )

    & wslc inspect --type $Type $Name *> $null
    return $LASTEXITCODE -eq 0
}

function Ensure-Network {
    if (-not (Test-WslcObject -Type network -Name $networkName)) {
        Invoke-Wslc network create $networkName
    }
}

function Ensure-Volume {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Test-WslcObject -Type volume -Name $Name)) {
        Invoke-Wslc volume create $Name
    }
}

function Remove-Container {
    param([Parameter(Mandatory)][string]$Name)

    if (Test-WslcObject -Type container -Name $Name) {
        Invoke-Wslc remove --force $Name
    }
}

function Build-Images {
    Push-Location $projectRoot
    try {
        Invoke-Wslc build --file ".\storage\Dockerfile" --tag $images.storage "."
        Invoke-Wslc build --file ".\frontend\Dockerfile" --tag $images.frontend "."
        Invoke-Wslc build --file ".\configs\Prometheus.Wslc.Dockerfile" --tag $images.prometheus ".\configs"
    }
    finally {
        Pop-Location
    }
}

function Start-Stack {
    if (-not $SkipBuild) {
        Build-Images
    }

    Ensure-Network
    foreach ($volumeName in $volumes.Values) {
        Ensure-Volume -Name $volumeName
    }

    foreach ($containerName in $containers.Values) {
        Remove-Container -Name $containerName
    }

    Invoke-Wslc run --detach `
        --name $containers.mysql `
        --network $networkName `
        --network-alias "mysql" `
        --publish "3306:3306" `
        --env "MYSQL_ROOT_PASSWORD=pwd" `
        --volume "$($volumes.mysql):/var/lib/mysql" `
        "docker.io/library/mysql:8.0"

    Invoke-Wslc run --detach `
        --name $containers.storage `
        --network $networkName `
        --network-alias "storage" `
        --cpus "0.1" `
        --memory "256M" `
        --publish "5050:5050" `
        --env "ASPNETCORE_URLS=http://+:5050" `
        --env "ConnectionStrings__MySql=server=mysql;user=root;password=pwd;database=memes" `
        --volume "$($volumes.storageTmp):/tmp" `
        $images.storage

    Invoke-Wslc run --detach `
        --name $containers.frontend `
        --network $networkName `
        --network-alias "frontend" `
        --cpus "0.1" `
        --memory "256M" `
        --publish "5051:5051" `
        --env "ASPNETCORE_URLS=http://+:5051" `
        --env "Storage__Endpoint=http://storage:5050" `
        --volume "$($volumes.frontendTmp):/tmp" `
        $images.frontend

    Invoke-Wslc run --detach `
        --name $containers["dotnetmonitor-frontend"] `
        --network $networkName `
        --network-alias "dotnetmonitor-frontend" `
        --publish "52343:52343" `
        --publish "52345:52345" `
        --env "DOTNET_URLS=http://+:52343" `
        --env "DOTNET_Metrics__Endpoints=http://+:52345" `
        --volume "$($volumes.frontendTmp):/tmp" `
        "mcr.microsoft.com/dotnet/monitor:6.2.2" `
        "--no-auth"

    Invoke-Wslc run --detach `
        --name $containers["dotnetmonitor-storage"] `
        --network $networkName `
        --network-alias "dotnetmonitor-storage" `
        --publish "52333:52333" `
        --publish "52335:52335" `
        --env "DOTNET_URLS=http://+:52333" `
        --env "DOTNET_Metrics__Endpoints=http://+:52335" `
        --volume "$($volumes.storageTmp):/tmp" `
        "mcr.microsoft.com/dotnet/monitor:6.2.2" `
        "--no-auth"

    Invoke-Wslc run --detach `
        --name $containers.prometheus `
        --network $networkName `
        --network-alias "prometheus" `
        --publish "9090:9090" `
        $images.prometheus

    Write-Host ""
    Write-Host "Stack started:"
    Write-Host "  Frontend:   http://localhost:5051"
    Write-Host "  Storage:    http://localhost:5050"
    Write-Host "  Prometheus: http://localhost:9090"
    Write-Host ""
    Write-Host "Run '.\wslc-dotnet-monitor.ps1 status' to inspect the containers."
}

function Stop-Stack {
    foreach ($serviceName in $serviceOrder) {
        Remove-Container -Name $containers[$serviceName]
    }

    if (Test-WslcObject -Type network -Name $networkName) {
        Invoke-Wslc network remove $networkName
    }

    if ($RemoveVolumes) {
        foreach ($volumeName in $volumes.Values) {
            Invoke-Wslc volume remove --force $volumeName
        }
        Write-Host "Stack stopped; persistent and diagnostic volumes removed."
    }
    else {
        Write-Host "Stack stopped; volumes retained. Use 'down -RemoveVolumes' to delete them."
    }
}

function Show-Logs {
    $containerName = $containers[$Service]
    if (-not (Test-WslcObject -Type container -Name $containerName)) {
        throw "Container '$containerName' does not exist. Start the stack first."
    }

    & wslc logs --follow --tail 200 $containerName
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read logs for '$containerName'."
    }
}

if (-not (Get-Command wslc -ErrorAction SilentlyContinue)) {
    throw "wslc.exe was not found on PATH. Install or update WSL before running this script."
}

switch ($Action) {
    "up" { Start-Stack }
    "down" { Stop-Stack }
    "status" { Invoke-Wslc container list --all }
    "logs" { Show-Logs }
}
