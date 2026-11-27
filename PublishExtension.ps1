<#
.SYNOPSIS
Publishes RDCore.Web.Host as the RDWeb extension of an RDCore platform.

.DESCRIPTION
The host is published to <PlatformRoot>/Extensions/RDCore.Web/, and `rdc describe-ext` writes its extension.manifest.json there: the language server
discovers the extension by that manifest, and loads it when "RDCore.Web" is in its Allowed list. The manifest records the SHA-512 of the executable, so
nothing may change the executable after this script runs.

The platform's rdc describes the extension, through the dotnet host (`dotnet rdc.dll`), so a tree published for another system is described the same way.
It describes it as an unsigned local build (`--unsafe-dev-mode`), as PlatformPublish.ps1 does for the extensions it publishes.
The platform is built on an RDCore.SDK at least as recent as the one RDCore.Web is built on.

.PARAMETER PlatformRoot
The folder of the platform: the one that holds RDCore.CLI/ and Extensions/.

.PARAMETER Configuration
The build configuration: Release unless given.

.PARAMETER RuntimeIdentifier
The system the extension is published for (win-x64, linux-x64, osx-arm64); the one running this script unless given.

.PARAMETER SdkVersion
The RDCore.SDK package version to build on, when it is not the one Directory.Build.props sets.

.PARAMETER Feed
A package folder or feed to restore from as well, for an RDCore.SDK package that is not published.

.EXAMPLE
./PublishExtension.ps1 -PlatformRoot C:/rdcore
#>
param(
    [Parameter(Mandatory)] [string] $PlatformRoot,
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier,
    [string] $SdkVersion,
    [string] $Feed
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path -LiteralPath $PlatformRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $root "RDCore.CLI/rdc.dll")))
{
    throw "'$root' is not the folder of a platform: it has no RDCore.CLI/rdc.dll."
}

$extensionFolder = Join-Path $root "Extensions/RDCore.Web"
$arguments = @("publish", (Join-Path $PSScriptRoot "RDCore.Web.Host/RDCore.Web.Host.csproj"), "-c", $Configuration, "-o", $extensionFolder)
if ($RuntimeIdentifier) { $arguments += @("-r", $RuntimeIdentifier, "--self-contained", "false") }
if ($SdkVersion) { $arguments += "-p:RDCoreSdkVersion=$SdkVersion" }
if ($Feed) { $arguments += "-p:RestoreAdditionalProjectSources=$Feed" }

& dotnet @arguments | Out-Host
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed (exit $LASTEXITCODE)."
}

# the executable has the .exe of Windows when it is published for Windows: for the system given, else for the one running this script.
$forWindows = if ($RuntimeIdentifier) { $RuntimeIdentifier.StartsWith("win", [StringComparison]::OrdinalIgnoreCase) } else { $env:OS -eq "Windows_NT" }
$executable = if ($forWindows) { "RDCore.Web.Host.exe" } else { "RDCore.Web.Host" }

# relative to the extension folder it runs in, so that no folder name of the platform root is in the path.
Push-Location -LiteralPath $extensionFolder
try
{
    & dotnet "../../RDCore.CLI/rdc.dll" describe-ext $executable --description "RDCore.Web: the diagnostics of the endpoints of RD-VBA controllers." `
        --overwrite --unsafe-dev-mode | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "describe-ext failed (exit $LASTEXITCODE)."
    }
}
finally
{
    Pop-Location
}

Write-Host "🧩 $(Join-Path $extensionFolder 'extension.manifest.json')"
