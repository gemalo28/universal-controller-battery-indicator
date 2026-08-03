param(
    [string]$ResultsPath = "TestResults",
    [ValidateRange(0, 100)]
    [double]$MinimumLineCoverage = 90
)

$coverageFiles = @(Get-ChildItem -Path $ResultsPath -Recurse -Filter "coverage.cobertura.xml" -File)
if ($coverageFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml report was found under '$ResultsPath'."
}

$coveredLines = 0L
$validLines = 0L
foreach ($coverageFile in $coverageFiles) {
    [xml]$report = Get-Content -LiteralPath $coverageFile.FullName -Raw
    $coverage = $report.coverage
    $coveredLines += [long]$coverage.'lines-covered'
    $validLines += [long]$coverage.'lines-valid'
}

if ($validLines -eq 0) {
    throw "Coverage reports contain no valid source lines."
}

$lineCoverage = 100.0 * $coveredLines / $validLines
Write-Host ("Line coverage: {0:N2}% ({1}/{2}); required: {3:N2}%" -f `
    $lineCoverage, $coveredLines, $validLines, $MinimumLineCoverage)

if ($lineCoverage -lt $MinimumLineCoverage) {
    throw ("Line coverage {0:N2}% is below the mandatory {1:N2}% threshold." -f `
        $lineCoverage, $MinimumLineCoverage)
}
