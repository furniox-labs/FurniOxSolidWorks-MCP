param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedRelativePath {
    param(
        [string]$BasePath,
        [string]$FullPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = New-Object System.Uri($baseFullPath)
    $fullUri = New-Object System.Uri([System.IO.Path]::GetFullPath($FullPath))
    $relative = [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fullUri).ToString())
    return $relative.Replace('\', '/')
}

$resolvedRoot = (Resolve-Path $RepoRoot).Path
$errors = [System.Collections.Generic.List[string]]::new()

$requiredPaths = @(
    "FurniOxSolidWorks-MCP.sln",
    "src/FurniOx.SolidWorks.Shared/FurniOx.SolidWorks.Shared.csproj",
    "src/FurniOx.SolidWorks.Core/FurniOx.SolidWorks.Core.csproj",
    "src/FurniOx.SolidWorks.MCP/FurniOx.SolidWorks.MCP.csproj",
    "src/FurniOx.SolidWorks.MCP/Tools/AssemblyBrowserTools.cs",
    "tests/FurniOx.SolidWorks.Core.Tests/FurniOx.SolidWorks.Core.Tests.csproj",
    "tests/FurniOx.SolidWorks.Tools.Tests/FurniOx.SolidWorks.Tools.Tests.csproj",
    "tests/FurniOx.SolidWorks.Integration.Tests/FurniOx.SolidWorks.Integration.Tests.csproj"
)

foreach ($relativePath in $requiredPaths) {
    $candidate = Join-Path $resolvedRoot $relativePath
    if (-not (Test-Path -LiteralPath $candidate)) {
        $errors.Add("Required public path missing: $relativePath")
    }
}

$bannedSolutionTokens = @(
    "FurniOx.SolidWorks.Bridge.Protocol",
    "FurniOx.SolidWorks.Bridge",
    "FurniOx.SolidWorks.Core.Private",
    "FurniOx.SolidWorks.MCP.Private",
    "FurniOx.SolidWorks.Core.Private.Tests",
    "FurniOx.SolidWorks.MCP.Private.Tests"
)

$solutionPath = Join-Path $resolvedRoot "FurniOxSolidWorks-MCP.sln"
if (Test-Path -LiteralPath $solutionPath) {
    $solutionText = Get-Content -LiteralPath $solutionPath -Raw
    foreach ($token in $bannedSolutionTokens) {
        if ($solutionText.IndexOf($token, [System.StringComparison]::Ordinal) -ge 0) {
            $errors.Add("Public solution leaks private project token: $token")
        }
    }
}

$bannedPatterns = @(
    ".mcp.json",
    "*/bin/*",
    "*/obj/*",
    ".artifacts/*",
    "src/FurniOx.SolidWorks.Bridge*",
    "src/FurniOx.SolidWorks.Core.Private*",
    "src/FurniOx.SolidWorks.MCP.Private*",
    "tests/FurniOx.SolidWorks.Core.Private.Tests*",
    "tests/FurniOx.SolidWorks.MCP.Private.Tests*",
    "FurniOxSolidWorks-MCP.Private.sln",
    "src/FurniOx.SolidWorks.Shared/Models/AssemblyAnalysisResult.cs",
    "src/FurniOx.SolidWorks.Shared/Models/BatchAnalysisModels.cs",
    "src/FurniOx.SolidWorks.Shared/Models/ComponentDocumentProperties.cs",
    "src/FurniOx.SolidWorks.Shared/Models/DocumentSummaryInfo.cs",
    "src/FurniOx.SolidWorks.Shared/Models/DrawingAnalysisResult.cs",
    "src/FurniOx.SolidWorks.Shared/Models/PartAnalysisResult.cs",
    "src/FurniOx.SolidWorks.Core/Bridge*",
    "src/FurniOx.SolidWorks.Core/DocManager*",
    "src/FurniOx.SolidWorks.Core/Adapters/Analysis*",
    "src/FurniOx.SolidWorks.Core/Adapters/BatchCustomProperty*.cs",
    "src/FurniOx.SolidWorks.Core/Adapters/CustomProperty*.cs",
    "src/FurniOx.SolidWorks.Core/Adapters/SummaryInfoOperations.cs",
    "src/FurniOx.SolidWorks.Core/Adapters/TargetDocumentResolutionSupport.cs",
    "src/FurniOx.SolidWorks.Core/Adapters/Document/*Rename*.cs",
    "src/FurniOx.SolidWorks.Core/Operations/AnalysisOperationNames.cs",
    "src/FurniOx.SolidWorks.Core/Operations/CustomPropertyOperationNames.cs",
    "src/FurniOx.SolidWorks.Core/Operations/SummaryInfoOperationNames.cs",
    "src/FurniOx.SolidWorks.MCP/BridgeBootstrapService.cs",
    "src/FurniOx.SolidWorks.MCP/Tools/AnalysisTools.cs",
    "src/FurniOx.SolidWorks.MCP/Tools/BatchAnalysisTools.cs",
    "src/FurniOx.SolidWorks.MCP/Tools/BatchCustomPropertyTools.cs",
    "src/FurniOx.SolidWorks.MCP/Tools/CustomPropertyTools.cs",
    "src/FurniOx.SolidWorks.MCP/Tools/SummaryInfoTools.cs"
)

$items = Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Force | Sort-Object FullName
foreach ($item in $items) {
    $relativePath = Get-NormalizedRelativePath -BasePath $resolvedRoot -FullPath $item.FullName
    foreach ($pattern in $bannedPatterns) {
        if ($relativePath -like $pattern) {
            $errors.Add("Banned private path present in public tree: $relativePath")
            break
        }
    }
}

$publicProjectChecks = @(
    @{
        Path = "src/FurniOx.SolidWorks.Core/FurniOx.SolidWorks.Core.csproj"
        Forbidden = @("FurniOx.SolidWorks.Bridge.Protocol", "FurniOx.SolidWorks.Core.Private", "AnalysisOperationNames.cs", "CustomPropertyOperationNames.cs", "SummaryInfoOperationNames.cs")
    },
    @{
        Path = "src/FurniOx.SolidWorks.MCP/FurniOx.SolidWorks.MCP.csproj"
        Forbidden = @("BridgeBootstrapService.cs", "AnalysisTools.cs", "BatchAnalysisTools.cs", "BatchCustomPropertyTools.cs", "CustomPropertyTools.cs", "SummaryInfoTools.cs", "FurniOx.SolidWorks.MCP.Private")
    },
    @{
        Path = "src/FurniOx.SolidWorks.Shared/FurniOx.SolidWorks.Shared.csproj"
        Forbidden = @("AssemblyAnalysisResult.cs", "BatchAnalysisModels.cs", "DocumentSummaryInfo.cs", "DrawingAnalysisResult.cs", "PartAnalysisResult.cs")
    }
)

foreach ($check in $publicProjectChecks) {
    $projectPath = Join-Path $resolvedRoot $check.Path
    if (-not (Test-Path -LiteralPath $projectPath)) {
        continue
    }

    $projectText = Get-Content -LiteralPath $projectPath -Raw
    foreach ($token in $check.Forbidden) {
        if ($projectText.IndexOf($token, [System.StringComparison]::Ordinal) -ge 0 -and $check.Path -notlike "*.Shared.csproj") {
            if ($check.Path -eq "src/FurniOx.SolidWorks.Core/FurniOx.SolidWorks.Core.csproj" -and $token -in @("AnalysisOperationNames.cs", "CustomPropertyOperationNames.cs", "SummaryInfoOperationNames.cs")) {
                continue
            }

            if ($check.Path -eq "src/FurniOx.SolidWorks.MCP/FurniOx.SolidWorks.MCP.csproj" -and $token -in @("BridgeBootstrapService.cs", "AnalysisTools.cs", "BatchAnalysisTools.cs", "BatchCustomPropertyTools.cs", "CustomPropertyTools.cs", "SummaryInfoTools.cs")) {
                continue
            }

            $errors.Add("Unexpected private token '$token' still referenced in $($check.Path)")
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Error ("Public boundary check failed:`n - " + ($errors -join "`n - "))
    exit 1
}

Write-Host "Public boundary check passed for $resolvedRoot"
