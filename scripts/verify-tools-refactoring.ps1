# Verify SolidWorksTools Refactoring
# Checks that all 36 expected methods exist across refactored tool files
# Expected: 35 MCP tools + 1 helper method (FormatResult)

param(
    [string]$ToolsPath = "src\FurniOx.SolidWorks.MCP\Tools"
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  SolidWorksTools Refactoring Verification" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Expected 36 methods (35 tools + 1 helper)
$expectedMethods = @(
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
    "GetPerformanceMetrics",

    # Helper Methods (1)
    "FormatResult"
)

Write-Host "Expected Methods: $($expectedMethods.Count)" -ForegroundColor Yellow
Write-Host ""

# Find all C# files in Tools directory
$toolFiles = Get-ChildItem -Path $ToolsPath -Filter "*.cs" -File | Where-Object { $_.Name -notlike "*_ORIGINAL*" }

if ($toolFiles.Count -eq 0) {
    Write-Host "ERROR: No tool files found in $ToolsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Scanning tool files:" -ForegroundColor Cyan
foreach ($file in $toolFiles) {
    Write-Host "  - $($file.Name)" -ForegroundColor Gray
}
Write-Host ""

# Read all file contents
$allContent = ""
foreach ($file in $toolFiles) {
    $allContent += Get-Content $file.FullName -Raw
}

# Track results
$foundMethods = @{}
$missingMethods = @()

# Search for each expected method
foreach ($method in $expectedMethods) {
    # Match method signature (public/private/protected, static/async optional, return type, method name)
    # Examples:
    # public static async Task<string> OpenModel(
    # private static string FormatResult(
    # public static async Task<string> GetMassProperties(
    if ($allContent -match "(private|public|protected)\s+(static\s+)?(async\s+)?(\w+(<[^>]+>)?)\s+$method\s*\(") {
        $foundMethods[$method] = $true

        # Find which file contains it
        $foundInFile = ""
        foreach ($file in $toolFiles) {
            $content = Get-Content $file.FullName -Raw
            if ($content -match "(private|public|protected)\s+(static\s+)?(async\s+)?(\w+(<[^>]+>)?)\s+$method\s*\(") {
                $foundInFile = $file.Name
                break
            }
        }
        Write-Host "  [OK] $method" -ForegroundColor Green -NoNewline
        Write-Host " (in $foundInFile)" -ForegroundColor DarkGray
    } else {
        $missingMethods += $method
        Write-Host "  [MISSING] $method" -ForegroundColor Red -NoNewline
        Write-Host " (MISSING)" -ForegroundColor DarkRed
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Verification Results" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Expected Methods:  $($expectedMethods.Count)" -ForegroundColor Yellow
Write-Host "Found Methods:     $($foundMethods.Count)" -ForegroundColor $(if ($foundMethods.Count -eq $expectedMethods.Count) { "Green" } else { "Red" })
Write-Host "Missing Methods:   $($missingMethods.Count)" -ForegroundColor $(if ($missingMethods.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($missingMethods.Count -gt 0) {
    Write-Host "MISSING METHODS:" -ForegroundColor Red
    foreach ($method in $missingMethods) {
        Write-Host "  - $method" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "[FAIL] VERIFICATION FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[PASS] VERIFICATION PASSED - All 36 methods found!" -ForegroundColor Green
    exit 0
}
