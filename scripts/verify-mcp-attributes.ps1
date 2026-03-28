# Verify that all MCP tools have [McpServerTool] attribute

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  MCP Attribute Verification" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$toolFiles = Get-ChildItem 'src/FurniOx.SolidWorks.MCP/Tools' -Filter '*Tools.cs' | Where-Object { $_.Name -ne 'SolidWorksTools.cs' -and $_.Name -ne 'ToolsBase.cs' }
$totalTools = 0
$toolsWithAttribute = 0
$missingAttribute = @()

foreach ($file in $toolFiles) {
    $content = Get-Content $file.FullName -Raw
    $methods = [regex]::Matches($content, 'public static async Task<string> (\w+)\(')

    Write-Host "Checking $($file.Name)..." -ForegroundColor Gray

    foreach ($method in $methods) {
        $totalTools++
        $methodName = $method.Groups[1].Value

        # Check if [McpServerTool appears before this method
        # Pattern: [McpServerTool, Description("...")] or [McpServerTool]
        if ($content -match "(?s)\[McpServerTool[^\]]*\].*?public static async Task<string> $methodName\(") {
            $toolsWithAttribute++
            Write-Host "  [OK] $methodName has [McpServerTool]" -ForegroundColor Green
        } else {
            $missingAttribute += "$methodName ($($file.Name))"
            Write-Host "  [MISSING] $methodName lacks [McpServerTool]" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Verification Results" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total MCP tools:               $totalTools" -ForegroundColor Yellow
Write-Host "Tools with [McpServerTool]:    $toolsWithAttribute" -ForegroundColor $(if ($toolsWithAttribute -eq $totalTools) { "Green" } else { "Red" })
Write-Host "Tools missing attribute:       $($missingAttribute.Count)" -ForegroundColor $(if ($missingAttribute.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($missingAttribute.Count -gt 0) {
    Write-Host "MISSING [McpServerTool] ATTRIBUTE:" -ForegroundColor Red
    foreach ($item in $missingAttribute) {
        Write-Host "  - $item" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "[FAIL] Some tools missing [McpServerTool] attribute" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[PASS] All $totalTools MCP tools have [McpServerTool] attribute!" -ForegroundColor Green
    exit 0
}
