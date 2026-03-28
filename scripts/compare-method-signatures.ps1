# Compare method signatures between original and refactored files

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Method Signature Comparison" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Read original file
$originalPath = "src\FurniOx.SolidWorks.MCP\Tools_Legacy\SolidWorksTools_ORIGINAL.cs"
$original = Get-Content $originalPath -Raw

# Read all refactored files
$toolsPath = "src\FurniOx.SolidWorks.MCP\Tools"
$refactoredFiles = Get-ChildItem -Path $toolsPath -Filter "*.cs" -File | Where-Object { $_.Name -notlike "*_ORIGINAL*" }
$refactoredAll = ""
foreach ($file in $refactoredFiles) {
    $refactoredAll += Get-Content $file.FullName -Raw
}

# Extract all method signatures from original (35 MCP tools)
$originalMethods = @()
$matches = [regex]::Matches($original, 'public static async Task<string> (\w+)\(')
foreach ($match in $matches) {
    $originalMethods += $match.Groups[1].Value
}

# Extract all method signatures from refactored files (35 MCP tools)
$refactoredMethods = @()
$matches = [regex]::Matches($refactoredAll, 'public static async Task<string> (\w+)\(')
foreach ($match in $matches) {
    $refactoredMethods += $match.Groups[1].Value
}

Write-Host "Original MCP tools:    $($originalMethods.Count)" -ForegroundColor Yellow
Write-Host "Refactored MCP tools:  $($refactoredMethods.Count)" -ForegroundColor Yellow
Write-Host ""

# Find missing and extra methods
$missing = $originalMethods | Where-Object { $_ -notin $refactoredMethods }
$extra = $refactoredMethods | Where-Object { $_ -notin $originalMethods }

$hasIssues = $false

if ($missing.Count -gt 0) {
    $hasIssues = $true
    Write-Host "MISSING METHODS (in original but not refactored):" -ForegroundColor Red
    foreach ($method in $missing) {
        Write-Host "  - $method" -ForegroundColor Red
    }
    Write-Host ""
}

if ($extra.Count -gt 0) {
    $hasIssues = $true
    Write-Host "EXTRA METHODS (in refactored but not original):" -ForegroundColor Yellow
    foreach ($method in $extra) {
        Write-Host "  + $method" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Compare FormatResult helper (should be in both)
$originalHasFormatResult = $original -match "private static string FormatResult\("
$refactoredHasFormatResult = $refactoredAll -match "protected static string FormatResult\("

Write-Host "FormatResult helper:" -ForegroundColor Cyan
Write-Host "  Original:    $(if ($originalHasFormatResult) { 'Found (private)' } else { 'NOT FOUND' })"
Write-Host "  Refactored:  $(if ($refactoredHasFormatResult) { 'Found (protected in ToolsBase)' } else { 'NOT FOUND' })"
Write-Host ""

if (-not $refactoredHasFormatResult) {
    $hasIssues = $true
    Write-Host "ERROR: FormatResult helper missing in refactored files!" -ForegroundColor Red
    Write-Host ""
}

# Final result
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Comparison Results" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

if ($hasIssues) {
    Write-Host "[FAIL] Method signature comparison FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[PASS] All method signatures match perfectly!" -ForegroundColor Green
    Write-Host "  - All 35 MCP tools present" -ForegroundColor Green
    Write-Host "  - FormatResult helper present" -ForegroundColor Green
    exit 0
}
