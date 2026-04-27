[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipBuild,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Get-FriendlyPmcVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    [xml]$versionProps = Get-Content -Raw (Join-Path $RepoRoot 'client-spt4\FriendlyPMC.Version.props')
    $resolvedVersion = $versionProps.Project.PropertyGroup.FriendlyPmcVersion
    if ([string]::IsNullOrWhiteSpace($resolvedVersion)) {
        throw 'FriendlyPmcVersion was not found in client-spt4\FriendlyPMC.Version.props'
    }

    return $resolvedVersion.Trim()
}

function Invoke-DotNetBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed: dotnet $($Arguments -join ' ')"
    }
}

function Get-BinaryVersionText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinaryPath
    )

    if (-not (Test-Path -LiteralPath $BinaryPath -PathType Leaf)) {
        throw "Required binary does not exist: $BinaryPath"
    }

    $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($BinaryPath).FileVersion
    if (-not [string]::IsNullOrWhiteSpace($fileVersion)) {
        return $fileVersion
    }

    return [System.Reflection.AssemblyName]::GetAssemblyName($BinaryPath).Version.ToString()
}

function Assert-AssemblyVersionMatches {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $actualVersion = Get-BinaryVersionText -BinaryPath $AssemblyPath
    $expectedAssemblyVersion = '{0}.0' -f $ExpectedVersion
    if ($actualVersion -ne $expectedAssemblyVersion) {
        throw "$Label version mismatch. Expected $expectedAssemblyVersion but found $actualVersion at $AssemblyPath"
    }
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required release artifact is missing: $Source"
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-FriendlyPmcVersion -RepoRoot $repoRoot
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'dist'
}

$clientProject = Join-Path $repoRoot 'client-spt4\FriendlyPMC.CoreFollowers\FriendlyPMC.CoreFollowers.csproj'
$serverProject = Join-Path $repoRoot 'server-spt4\FriendlyPMC.Server\FriendlyPMC.Server.csproj'
$clientOutput = Join-Path $repoRoot 'client-spt4\FriendlyPMC.CoreFollowers\bin\Release\netstandard2.1'
$serverOutput = Join-Path $repoRoot 'server-spt4\FriendlyPMC.Server\bin\Release\PMCSquadmates.Server'
$stageRoot = Join-Path $OutputDirectory "PMCSquadmates-$Version"
$zipPath = Join-Path $OutputDirectory "PMCSquadmates-$Version-spt4.zip"

if (-not $SkipBuild) {
    Invoke-DotNetBuild -Arguments @('build', $clientProject, '-c', 'Release', '-f', 'netstandard2.1', '-v', 'minimal')
    Invoke-DotNetBuild -Arguments @('build', $serverProject, '-c', 'Release', '-v', 'minimal')
}

$clientAssemblyPath = Join-Path $clientOutput 'PMCSquadmates.Client.dll'
$serverAssemblyPath = Join-Path $serverOutput 'PMCSquadmates.Server.dll'
Assert-AssemblyVersionMatches -AssemblyPath $clientAssemblyPath -ExpectedVersion $Version -Label 'Built client'
Assert-AssemblyVersionMatches -AssemblyPath $serverAssemblyPath -ExpectedVersion $Version -Label 'Built server'

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Reset-Directory -Path $stageRoot
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$clientInstall = Join-Path $stageRoot 'BepInEx\plugins\PMCSquadmates.Client'
$serverInstall = Join-Path $stageRoot 'SPT\user\mods\PMCSquadmates.Server'
New-Item -ItemType Directory -Path $clientInstall -Force | Out-Null
New-Item -ItemType Directory -Path $serverInstall -Force | Out-Null

Copy-RequiredFile -Source $clientAssemblyPath -Destination $clientInstall
Copy-RequiredFile -Source (Join-Path $clientOutput 'PMCSquadmates.Client.deps.json') -Destination $clientInstall
Copy-RequiredFile -Source $serverAssemblyPath -Destination $serverInstall
Copy-RequiredFile -Source (Join-Path $serverOutput 'PMCSquadmates.Server.deps.json') -Destination $serverInstall
Copy-RequiredFile -Source (Join-Path $serverOutput 'PMCSquadmates.Server.staticwebassets.endpoints.json') -Destination $serverInstall
Copy-RequiredFile -Source (Join-Path $repoRoot 'LICENSE') -Destination $stageRoot
Copy-RequiredFile -Source (Join-Path $repoRoot 'README.md') -Destination $stageRoot

Compress-Archive -Path (Join-Path $stageRoot '*') -DestinationPath $zipPath -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$entries = [IO.Compression.ZipFile]::OpenRead($zipPath).Entries
$entryNames = $entries | ForEach-Object { $_.FullName }
$requiredEntries = @(
    'BepInEx\plugins\PMCSquadmates.Client\PMCSquadmates.Client.dll',
    'BepInEx\plugins\PMCSquadmates.Client\PMCSquadmates.Client.deps.json',
    'SPT\user\mods\PMCSquadmates.Server\PMCSquadmates.Server.dll',
    'SPT\user\mods\PMCSquadmates.Server\PMCSquadmates.Server.deps.json',
    'SPT\user\mods\PMCSquadmates.Server\PMCSquadmates.Server.staticwebassets.endpoints.json',
    'LICENSE',
    'README.md'
)

foreach ($requiredEntry in $requiredEntries) {
    if ($entryNames -notcontains $requiredEntry) {
        throw "Release archive is missing required entry: $requiredEntry"
    }
}

$badEntries = @($entryNames | Where-Object {
    $_ -like 'user\mods\*' -or
    $_ -like 'BepInEx\plugins\netstandard2.1\*' -or
    $_ -like 'SPT\user\mods\FriendlyPMC.Server\*' -or
    $_ -like 'BepInEx\plugins\FriendlyPMC.CoreFollowers\*'
})

if ($badEntries.Count -gt 0) {
    throw "Release archive contains obsolete install paths: $($badEntries -join ', ')"
}

Write-Host "Created release archive: $zipPath"
$entries | Select-Object FullName, Length | Format-Table -AutoSize
