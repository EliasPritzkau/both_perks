Set-StrictMode -Version Latest

function Import-ModPaths {
    [CmdletBinding()]
    param(
        [string]$Path = (Join-Path (Split-Path $PSScriptRoot -Parent) 'ModPaths.local.psd1')
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Path configuration not found: $Path. Copy ModPaths.example.psd1 to ModPaths.local.psd1 and edit it."
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $config = Import-PowerShellDataFile -LiteralPath $resolvedPath
    $required = @(
        'DefaultGameVersion',
        'Bannerlord',
        'Tools',
        'SteamApps',
        'SteamAppId',
        'WorkshopDir',
        'Sources',
        'Releases',
        'CompatibilityBuilds',
        'DecompiledSources',
        'ReferenceArchives'
    )

    foreach ($key in $required) {
        if (-not $config.ContainsKey($key)) {
            throw "Path configuration '$resolvedPath' is missing '$key'."
        }
    }

    if (-not $config.Bannerlord.ContainsKey($config.DefaultGameVersion)) {
        throw "DefaultGameVersion '$($config.DefaultGameVersion)' is not defined under Bannerlord."
    }

    $config['_ConfigPath'] = $resolvedPath
    return $config
}

function Resolve-ConfiguredTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [hashtable]$Config,
        [Parameter(Mandatory)] [string]$Name
    )

    if (-not $Config.Tools.ContainsKey($Name)) {
        throw "Tool '$Name' is not configured under Tools."
    }
    $configuredCommand = [string]$Config.Tools[$Name]
    if ([string]::IsNullOrWhiteSpace($configuredCommand)) {
        throw "Configured tool '$Name' is empty."
    }
    $command = Get-Command $configuredCommand -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Configured tool '$Name' was not found: $configuredCommand"
    }
    return $command.Source
}

function Get-BannerlordInstallation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [hashtable]$Config,
        [Parameter(Mandatory)] [string]$Version,
        [string]$RootOverride
    )

    $root = $RootOverride
    if ([string]::IsNullOrWhiteSpace($root)) {
        if (-not $Config.Bannerlord.ContainsKey($Version)) {
            throw "Bannerlord version key '$Version' is not configured."
        }
        $root = [string]$Config.Bannerlord[$Version]
    }

    if ([string]::IsNullOrWhiteSpace($root)) {
        throw "Bannerlord path for '$Version' is empty in '$($Config._ConfigPath)'."
    }

    $root = [System.IO.Path]::GetFullPath($root).TrimEnd('\')
    $bin = Join-Path $root 'bin\Win64_Shipping_Client'
    $versionFile = Join-Path $bin 'Version.xml'
    if (-not (Test-Path -LiteralPath $bin -PathType Container)) {
        throw "Bannerlord bin directory not found for '$Version': $bin"
    }
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Bannerlord version file not found: $versionFile"
    }

    [xml]$versionXml = Get-Content -LiteralPath $versionFile -Raw
    $actualVersion = [string]$versionXml.Version.Singleplayer.Value
    $expectedPrefix = 'v' + ($Version.TrimStart('v') -replace '_', '.') + '.'
    if (-not $actualVersion.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Configured '$Version' path contains $actualVersion, expected $expectedPrefix*. Path: $root"
    }

    [pscustomobject]@{
        Key = $Version
        Version = $actualVersion
        Root = $root
        Bin = $bin
    }
}

Export-ModuleMember -Function Import-ModPaths, Get-BannerlordInstallation, Resolve-ConfiguredTool
