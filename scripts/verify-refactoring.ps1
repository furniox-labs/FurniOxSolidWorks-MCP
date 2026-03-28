# Verify Refactoring - Check All Methods Present
# ================================================
# This script verifies that all 39 expected methods exist in the refactored code
# Run after refactoring to ensure no functions were lost

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Refactoring Verification Script" -ForegroundColor Cyan
Write-Host "Checking for all 39 expected methods..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Expected methods (39 total)
$expectedMethods = @(
    # Document Operations (10)
    "CreateDocumentAsync",
    "OpenModelAsync",
    "SaveModelAsync",
    "CloseModelAsync",
    "GetDocumentInfoAsync",
    "ActivateDocumentAsync",
    "RebuildModelAsync",
    "GetAllOpenDocumentsAsync",
    "GetDocumentCountAsync",
    "CloseAllDocumentsAsync",

    # Export Operations (3 methods)
    "ExportModelAsync",
    "ExportToPDFAsync",
    "ExportToDXFAsync",

    # Custom Property Operations (4)
    "GetCustomPropertyAsync",
    "SetCustomPropertyAsync",
    "GetAllCustomPropertiesAsync",
    "DeleteCustomPropertyAsync",

    # Sketch Operations (13)
    "CreateSketchAsync",
    "ExitSketchAsync",
    "SketchCircleAsync",
    "SketchLineAsync",
    "SketchCenterLineAsync",
    "SketchArcAsync",
    "Sketch3PointArcAsync",
    "SketchTangentArcAsync",
    "SketchCornerRectangleAsync",
    "SketchPointAsync",
    "SketchEllipseAsync",
    "SketchSplineAsync",
    "SketchPolygonAsync",

    # Feature Operations (2)
    "CreateExtrusionAsync",
    "GetMassPropertiesAsync",

    # Helper Methods (5)
    "GetIntParam",
    "GetStringParam",
    "GetBoolParam",
    "MmToMeters",
    "MetersToMm",

    # Main Methods (2)
    "ExecuteAsync",
    "Dispose"
)

# Search paths
$adaptersPath = "src\FurniOx.SolidWorks.Core\Adapters"

if (-not (Test-Path $adaptersPath)) {
    Write-Host "ERROR: Adapters directory not found at: $adaptersPath" -ForegroundColor Red
    exit 1
}

# Get all C# files in Adapters directory
$adapterFiles = Get-ChildItem -Path $adaptersPath -Filter "*.cs" -File

Write-Host "Searching in files:" -ForegroundColor Yellow
$adapterFiles | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor Gray
}
Write-Host ""

# Track results
$missingMethods = @()
$foundMethods = @()

# Check each expected method
foreach ($method in $expectedMethods) {
    $found = $false

    foreach ($file in $adapterFiles) {
        $content = Get-Content $file.FullName -Raw

        # Search for method definition (private/public/protected with any return type)
        # Matches: async Task, Task<T>, void, int, string, bool, double, etc.
        if ($content -match "(private|public|protected)\s+(static\s+)?(async\s+)?(\w+(<[^>]+>)?)\s+$method\s*\(") {
            $found = $true
            $foundMethods += [PSCustomObject]@{
                Method = $method
                File = $file.Name
            }
            break
        }
    }

    if (-not $found) {
        $missingMethods += $method
    }
}

# Print results
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VERIFICATION RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Expected Methods: $($expectedMethods.Count)" -ForegroundColor White
Write-Host "Found Methods:    $($foundMethods.Count)" -ForegroundColor $(if ($foundMethods.Count -eq $expectedMethods.Count) { "Green" } else { "Yellow" })
Write-Host "Missing Methods:  $($missingMethods.Count)" -ForegroundColor $(if ($missingMethods.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($foundMethods.Count -gt 0) {
    Write-Host "✅ Found Methods:" -ForegroundColor Green
    $foundMethods | Group-Object -Property File | ForEach-Object {
        Write-Host "  $($_.Name):" -ForegroundColor Gray
        $_.Group | ForEach-Object {
            Write-Host "    - $($_.Method)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

if ($missingMethods.Count -gt 0) {
    Write-Host "❌ Missing Methods:" -ForegroundColor Red
    $missingMethods | ForEach-Object {
        Write-Host "  - $_" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "ERROR: Refactoring incomplete - missing methods detected!" -ForegroundColor Red
    exit 1
}

# Success
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ VERIFICATION PASSED" -ForegroundColor Green
Write-Host "All 39 methods present in refactored code" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

exit 0
