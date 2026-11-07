param([string]$OutputDirectory = 'dist', [switch]$WithModel)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$outputFolder = Join-Path $PSScriptRoot $OutputDirectory
$exe = Join-Path $outputFolder 'MakroPlus.exe'
Set-Content -LiteralPath (Join-Path $outputFolder 'test-results.txt') -Value 'RUNNING'
Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait
$report = Get-Content -LiteralPath (Join-Path $outputFolder 'test-results.txt') -Raw
Write-Host $report
if (-not $report.StartsWith('PASS:')) { throw 'Tests failed.' }
if ($WithModel) {
    $modelReport = Join-Path $outputFolder 'llm-test-results.json'
    Set-Content -LiteralPath $modelReport -Value '{"status":"RUNNING"}'
    Start-Process -FilePath $exe -ArgumentList '--llm-test' -WindowStyle Hidden -Wait
    $modelResult = Get-Content -LiteralPath $modelReport -Raw | ConvertFrom-Json
    Write-Host "Model test: $($modelResult.status) — $($modelResult.completed) scenarios"
    if ($modelResult.status -ne 'PASS') { throw "Real-model test failed. Details: $modelReport" }
}
