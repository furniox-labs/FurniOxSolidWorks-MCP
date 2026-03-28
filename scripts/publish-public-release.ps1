param(
    [string]$Runtime = "win-x64",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repoRoot "FurniOxSolidWorks-MCP.sln"
$propsPath = Join-Path $repoRoot "Directory.Build.props"
$buildOutputPath = Join-Path $repoRoot "src/FurniOx.SolidWorks.MCP\bin\Release\net10.0-windows"

[xml]$props = Get-Content -LiteralPath $propsPath
$versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
$version = $null
if ($versionNode -ne $null) {
    $version = $versionNode.InnerText
}

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read Version from Directory.Build.props."
}

$releaseName = "FurniOxSolidWorks-MCP-$version-$Runtime"
$releaseRoot = Join-Path $repoRoot "out/releases"
$packagePath = Join-Path $releaseRoot $releaseName
$zipPath = Join-Path $releaseRoot "$releaseName.zip"

if (Test-Path -LiteralPath $packagePath) {
    if (-not $Force) {
        throw "Release folder already exists: $packagePath. Re-run with -Force to overwrite."
    }

    Remove-Item -LiteralPath $packagePath -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    if (-not $Force) {
        throw "Release archive already exists: $zipPath. Re-run with -Force to overwrite."
    }

    Remove-Item -LiteralPath $zipPath -Force
}

if (-not (Test-Path -LiteralPath $releaseRoot)) {
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
}

& dotnet build $solutionPath -c Release -m:1 -p:MSBuildEnableWorkloadResolver=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

if (-not (Test-Path -LiteralPath $buildOutputPath)) {
    throw "Expected build output path not found: $buildOutputPath"
}

New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
Get-ChildItem -LiteralPath $buildOutputPath -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $packagePath -Recurse -Force
}

Copy-Item -LiteralPath (Join-Path $repoRoot ".mcp.exe.example.json") -Destination $packagePath -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $packagePath -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $packagePath -Force

Compress-Archive -Path (Join-Path $packagePath "*") -DestinationPath $zipPath -Force

Write-Host "Release folder: $packagePath"
Write-Host "Release archive: $zipPath"
