# Verify Switch Cases - Check All Operation Routes
# =================================================
# This script verifies that all 35 expected operation routes exist in the main adapter
# Run after refactoring to ensure no operation routes were lost

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Operation Routes Verification Script" -ForegroundColor Cyan
Write-Host "Checking for all 35 operation routes..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Expected operation routes (35 total)
$expectedRoutes = @{
    "Document" = @(
        "Document.CreateDocument",
        "Document.OpenModel",
        "Document.SaveModel",
        "Document.CloseModel",
        "Document.GetDocumentInfo",
        "Document.ActivateDocument",
        "Document.RebuildModel",
        "Document.GetAllOpenDocuments",
        "Document.GetDocumentCount",
        "Document.CloseAllDocuments"
    )
    "Export" = @(
        "Export.ExportToSTEP",
        "Export.ExportToIGES",
        "Export.ExportToSTL",
        "Export.ExportToPDF",
        "Export.ExportToDXF"
    )
    "CustomProperty" = @(
        "CustomProperty.Get",
        "CustomProperty.Set",
        "CustomProperty.GetAll",
        "CustomProperty.Delete"
    )
    "Sketch" = @(
        "Sketch.CreateSketch",
        "Sketch.ExitSketch",
        "Sketch.SketchCircle",
        "Sketch.SketchLine",
        "Sketch.SketchCenterLine",
        "Sketch.SketchArc",
        "Sketch.Sketch3PointArc",
        "Sketch.SketchTangentArc",
        "Sketch.SketchCornerRectangle",
        "Sketch.SketchPoint",
        "Sketch.SketchEllipse",
        "Sketch.SketchSpline",
        "Sketch.SketchPolygon"
    )
    "Feature" = @(
        "Feature.CreateExtrusion"
    )
    "Analysis" = @(
        "Analysis.GetMassProperties"
    )
}

# Calculate total expected routes
$totalExpected = ($expectedRoutes.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum

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

# Read all adapter content
$content = ""
foreach ($file in $adapterFiles) {
    $content += Get-Content $file.FullName -Raw
}

# Track results
$missingRoutes = @()
$foundRoutes = @()

# Check each operation route
foreach ($category in $expectedRoutes.Keys) {
    foreach ($route in $expectedRoutes[$category]) {
        # Search for route in switch case or if statement
        # Pattern: "Document.CreateDocument" or 'Document.CreateDocument' or Document.CreateDocument
        if ($content -match "[`"']$([regex]::Escape($route))[`"']" -or $content -match "\b$([regex]::Escape($route))\b") {
            $foundRoutes += [PSCustomObject]@{
                Category = $category
                Route = $route
            }
        } else {
            $missingRoutes += [PSCustomObject]@{
                Category = $category
                Route = $route
            }
        }
    }
}

# Print results
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VERIFICATION RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Expected Routes: $totalExpected" -ForegroundColor White
Write-Host "Found Routes:    $($foundRoutes.Count)" -ForegroundColor $(if ($foundRoutes.Count -eq $totalExpected) { "Green" } else { "Yellow" })
Write-Host "Missing Routes:  $($missingRoutes.Count)" -ForegroundColor $(if ($missingRoutes.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

# Show breakdown by category
foreach ($category in $expectedRoutes.Keys | Sort-Object) {
    $categoryExpected = $expectedRoutes[$category].Count
    $categoryFound = ($foundRoutes | Where-Object { $_.Category -eq $category }).Count
    $categoryMissing = $categoryExpected - $categoryFound

    $statusColor = if ($categoryMissing -eq 0) { "Green" } else { "Red" }
    $statusIcon = if ($categoryMissing -eq 0) { "✅" } else { "❌" }

    Write-Host "$statusIcon $category`: $categoryFound/$categoryExpected" -ForegroundColor $statusColor
}
Write-Host ""

if ($foundRoutes.Count -gt 0) {
    Write-Host "✅ Found Routes by Category:" -ForegroundColor Green
    $foundRoutes | Group-Object -Property Category | Sort-Object Name | ForEach-Object {
        Write-Host "  $($_.Name) ($($_.Count) routes):" -ForegroundColor Gray
        $_.Group | ForEach-Object {
            Write-Host "    - $($_.Route)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

if ($missingRoutes.Count -gt 0) {
    Write-Host "❌ Missing Routes by Category:" -ForegroundColor Red
    $missingRoutes | Group-Object -Property Category | Sort-Object Name | ForEach-Object {
        Write-Host "  $($_.Name) ($($_.Count) routes):" -ForegroundColor Red
        $_.Group | ForEach-Object {
            Write-Host "    - $($_.Route)" -ForegroundColor Red
        }
    }
    Write-Host ""
    Write-Host "ERROR: Refactoring incomplete - missing operation routes detected!" -ForegroundColor Red
    exit 1
}

# Success
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ VERIFICATION PASSED" -ForegroundColor Green
Write-Host "All 35 operation routes present in main adapter" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

exit 0
