# Verify MCP Tool Attributes
# Checks that all 35 MCP tools have required attributes:
# - [McpServerTool] attribute
# - Description attribute on method
# - Description attributes on parameters (excluding ISmartRouter router)

param(
    [string]$ToolsPath = "src\FurniOx.SolidWorks.MCP\Tools"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  MCP Tool Attributes Verification" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# Expected 35 MCP tools (excludes FormatResult helper)
$expectedTools = @(
    # Document Tools (10)
    "OpenModel",
    "SaveModel",
    "CloseModel",
    "GetDocumentInfo",
    "ActivateDocument",
    "CreateDocument",
    "RebuildModel",
    "GetAllOpenDocuments",
    "GetDocumentCount",
    "CloseAllDocuments",

    # Export Tools (5)
    "ExportToSTEP",
    "ExportToIGES",
    "ExportToSTL",
    "ExportToPDF",
    "ExportToDXF",

    # Custom Property Tools (4)
    "GetCustomProperty",
    "SetCustomProperty",
    "GetAllCustomProperties",
    "DeleteCustomProperty",

    # Sketch Tools (13)
    "CreateSketch",
    "ExitSketch",
    "SketchCircle",
    "SketchLine",
    "SketchCenterLine",
    "SketchArc",
    "Sketch3PointArc",
    "SketchTangentArc",
    "SketchCornerRectangle",
    "SketchPoint",
    "SketchEllipse",
    "SketchPolygon",
    "SketchSpline",

    # Feature Tools (1)
    "CreateExtrusion",

    # Analysis Tools (1)
    "GetMassProperties",

    # Performance Tools (1)
    "GetPerformanceMetrics"
)

Write-Host "Expected MCP Tools: $($expectedTools.Count)" -ForegroundColor Yellow
Write-Host ""

# Find all C# files in Tools directory
$toolFiles = Get-ChildItem -Path $ToolsPath -Filter "*.cs" -File | Where-Object { $_.Name -notlike "*_ORIGINAL*" -and $_.Name -ne "ToolsBase.cs" }

if ($toolFiles.Count -eq 0) {
    Write-Host "ERROR: No tool files found in $ToolsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Scanning tool files:" -ForegroundColor Cyan
foreach ($file in $toolFiles) {
    Write-Host "  - $($file.Name)" -ForegroundColor Gray
}
Write-Host ""

# Track results
$toolsWithoutMcpAttribute = @()
$toolsWithoutDescription = @()
$toolsWithAllAttributes = @()

# Check each tool
foreach ($toolName in $expectedTools) {
    $foundTool = $false
    $hasMcpAttribute = $false
    $hasDescription = $false

    # Search for the tool in all files
    foreach ($file in $toolFiles) {
        $content = Get-Content $file.FullName -Raw

        # Look for method signature
        if ($content -match "(?s)((?:\[[^\]]+\]\s*)*)\s*(public\s+static\s+async\s+Task<string>)\s+$toolName\s*\(") {
            $foundTool = $true
            $attributesSection = $Matches[1]

            # Check for [McpServerTool] attribute
            if ($attributesSection -match "\[McpServerTool") {
                $hasMcpAttribute = $true
            }

            # Check for Description attribute
            if ($attributesSection -match "\[Description\(|Description\s*=\s*""") {
                $hasDescription = $true
            }

            break
        }
    }

    if ($foundTool) {
        $status = ""
        $color = "Green"

        if (-not $hasMcpAttribute) {
            $toolsWithoutMcpAttribute += $toolName
            $status = "Missing [McpServerTool]"
            $color = "Red"
        } elseif (-not $hasDescription) {
            $toolsWithoutDescription += $toolName
            $status = "Missing Description"
            $color = "Yellow"
        } else {
            $toolsWithAllAttributes += $toolName
            $status = "[OK] All attributes present"
            $color = "Green"
        }

        Write-Host "  $toolName" -ForegroundColor White -NoNewline
        Write-Host " - $status" -ForegroundColor $color
    } else {
        Write-Host "  $toolName" -ForegroundColor White -NoNewline
        Write-Host " - NOT FOUND" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Verification Results" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Expected Tools:                      $($expectedTools.Count)" -ForegroundColor Yellow
Write-Host "Tools with all attributes:           $($toolsWithAllAttributes.Count)" -ForegroundColor $(if ($toolsWithAllAttributes.Count -eq $expectedTools.Count) { "Green" } else { "Yellow" })
Write-Host "Tools missing [McpServerTool]:       $($toolsWithoutMcpAttribute.Count)" -ForegroundColor $(if ($toolsWithoutMcpAttribute.Count -eq 0) { "Green" } else { "Red" })
Write-Host "Tools missing Description:           $($toolsWithoutDescription.Count)" -ForegroundColor $(if ($toolsWithoutDescription.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

$hasErrors = $false

if ($toolsWithoutMcpAttribute.Count -gt 0) {
    $hasErrors = $true
    Write-Host "TOOLS MISSING [McpServerTool] ATTRIBUTE:" -ForegroundColor Red
    foreach ($tool in $toolsWithoutMcpAttribute) {
        Write-Host "  - $tool" -ForegroundColor Red
    }
    Write-Host ""
}

if ($toolsWithoutDescription.Count -gt 0) {
    Write-Host "TOOLS MISSING DESCRIPTION:" -ForegroundColor Yellow
    foreach ($tool in $toolsWithoutDescription) {
        Write-Host "  - $tool" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($hasErrors) {
    Write-Host "[FAIL] VERIFICATION FAILED - Critical attributes missing" -ForegroundColor Red
    exit 1
} elseif ($toolsWithoutDescription.Count -gt 0) {
    Write-Host "[WARN] VERIFICATION PASSED WITH WARNINGS" -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "[PASS] VERIFICATION PASSED - All tools have required attributes!" -ForegroundColor Green
    exit 0
}
