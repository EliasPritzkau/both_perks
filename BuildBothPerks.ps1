[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$ReferenceVersion = 'v1_2',
    [string]$BannerlordRoot,
    [string]$WorkshopDir
)

$ErrorActionPreference = 'Stop'

$pathsModule = Join-Path $PSScriptRoot 'tools\ModPaths.psm1'
Import-Module -Name $pathsModule -Force
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot 'ModPaths.local.psd1'
}
$config = Import-ModPaths -Path $ConfigPath
$dotnet = Resolve-ConfiguredTool -Config $config -Name 'DotNet'
$installation = Get-BannerlordInstallation -Config $config -Version $ReferenceVersion -RootOverride $BannerlordRoot
if ([string]::IsNullOrWhiteSpace($WorkshopDir)) {
    $WorkshopDir = $config.WorkshopDir
}

$bootstrapProject = Join-Path $PSScriptRoot 'src\Bootstrap\BothPerks.Bootstrap.csproj'
$coreProject = Join-Path $PSScriptRoot 'src\BothPerks.csproj'

& $dotnet build $bootstrapProject -c Release --no-incremental "-p:BannerlordRoot=$($installation.Root)"
if ($LASTEXITCODE -ne 0) {
    throw 'BothPerks bootstrap build failed.'
}

foreach ($target in 'v1_2', 'v1_3', 'v1_4') {
    $targetInstallation = Get-BannerlordInstallation -Config $config -Version $target
    & $dotnet build $coreProject -c Release --no-incremental `
        "-p:BannerlordRoot=$($targetInstallation.Root)" `
        "-p:WorkshopDir=$WorkshopDir" `
        "-p:BannerlordTarget=$target"
    if ($LASTEXITCODE -ne 0) {
        throw "BothPerks core build failed for $target."
    }
}

Write-Host "Built BothPerks bootstrap and v1_2/v1_3/v1_4 cores."
