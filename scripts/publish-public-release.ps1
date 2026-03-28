param(
    [string]$Runtime = "win-x64",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-VersionFromProps {
    param([string]$PropsPath)

    [xml]$props = Get-Content -LiteralPath $PropsPath
    $versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($versionNode -eq $null -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Unable to read Version from $PropsPath."
    }

    return $versionNode.InnerText
}

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

$privateRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$isPrivateSourceRepo = Test-Path -LiteralPath (Join-Path $privateRepoRoot "src/FurniOx.SolidWorks.MCP.Private\FurniOx.SolidWorks.MCP.Private.csproj")

$publicSourceRoot = $privateRepoRoot
if ($isPrivateSourceRepo) {
    $publicSourceRoot = Join-Path $privateRepoRoot "out/public-release-package-source"
    & powershell -ExecutionPolicy Bypass -File (Join-Path $privateRepoRoot "scripts/export-public.ps1") -DestinationPath $publicSourceRoot -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Public export failed."
    }
}

$version = Get-VersionFromProps -PropsPath (Join-Path $publicSourceRoot "Directory.Build.props")
$releaseName = "FurniOxSolidWorks-MCP-$version-$Runtime"
$releaseRoot = Join-Path $privateRepoRoot "out/releases"
$publishPath = Join-Path $releaseRoot "$releaseName-publish"
$packagePath = Join-Path $releaseRoot $releaseName
$zipPath = Join-Path $releaseRoot "$releaseName.zip"
$projectPath = Join-Path $publicSourceRoot "src/FurniOx.SolidWorks.MCP\FurniOx.SolidWorks.MCP.csproj"

if (-not (Test-Path -LiteralPath $releaseRoot)) {
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
}

if ((Test-Path -LiteralPath $publishPath) -or (Test-Path -LiteralPath $packagePath) -or (Test-Path -LiteralPath $zipPath)) {
    if (-not $Force) {
        throw "Release output already exists under $releaseRoot. Re-run with -Force to overwrite."
    }

    Remove-PathIfExists -Path $publishPath
    Remove-PathIfExists -Path $packagePath
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
}

$existingDotnetCliHome = $env:DOTNET_CLI_HOME
$existingHome = $env:HOME
$existingDotnetNoLogo = $env:DOTNET_NOLOGO
$existingGenerateAspNetCertificate = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
$existingAddGlobalToolsToPath = $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH

try {
    $dotnetHome = Join-Path $privateRepoRoot ".dotnet_cli\publish-public-release"
    if (-not (Test-Path -LiteralPath $dotnetHome)) {
        New-Item -ItemType Directory -Path $dotnetHome -Force | Out-Null
    }

    $env:DOTNET_CLI_HOME = $dotnetHome
    $env:HOME = $dotnetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_NOLOGO = "1"
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = "false"
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "false"

    Push-Location $publicSourceRoot

    & dotnet restore $projectPath -r $Runtime -p:NuGetAudit=false -p:MSBuildEnableWorkloadResolver=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }

    & dotnet publish $projectPath `
        -c Release `
        -r $Runtime `
        --self-contained true `
        --no-restore `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:NuGetAudit=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:MSBuildEnableWorkloadResolver=false `
        -o $publishPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }
}
finally {
    Pop-Location
    $env:DOTNET_CLI_HOME = $existingDotnetCliHome
    $env:HOME = $existingHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $null
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $null
    $env:DOTNET_NOLOGO = $existingDotnetNoLogo
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $existingGenerateAspNetCertificate
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = $existingAddGlobalToolsToPath
}

New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

$packagedFiles = @(
    "FurniOx.SolidWorks.MCP.exe",
    "appsettings.json",
    "appsettings.local.example.json"
)

foreach ($fileName in $packagedFiles) {
    $sourcePath = Join-Path $publishPath $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Expected published file not found: $sourcePath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $packagePath -Force
}

Copy-Item -LiteralPath (Join-Path $publicSourceRoot ".mcp.exe.example.json") -Destination $packagePath -Force
Copy-Item -LiteralPath (Join-Path $publicSourceRoot "README.md") -Destination $packagePath -Force
Copy-Item -LiteralPath (Join-Path $publicSourceRoot "LICENSE") -Destination $packagePath -Force

Compress-Archive -Path (Join-Path $packagePath "*") -DestinationPath $zipPath -Force

Write-Host "Published source: $publicSourceRoot"
Write-Host "Publish output: $publishPath"
Write-Host "Release folder: $packagePath"
Write-Host "Release archive: $zipPath"
