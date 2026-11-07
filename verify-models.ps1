param([string]$OutputDirectory = 'dist\makroplus-2.6.2')
$ErrorActionPreference = 'Stop'
$folder = Join-Path $PSScriptRoot $OutputDirectory
$exe = Join-Path $folder 'MakroPlus.exe'
$cases = @(
    @{Name='GPU failure / CPU / mouse / reuse';Flag='--device-model-test';Report='device-model-test.json'},
    @{Name='Keyboard / text / missing information';Flag='--llm-test';Report='llm-test-results.json'},
    @{Name='Saved browser and media preferences';Flag='--features-model-test';Report='features-model-test.json'},
    @{Name='Spotify / Discord / missing trigger';Flag='--app-model-test';Report='app-model-test.json'},
    @{Name='YouTube timestamp / numpad';Flag='--activation-model-test';Report='activation-model-test.json'},
    @{Name='Custom API model / optional structured output / touchpad';Flag='--server-model-test';Report='custom-model-test.json'},
    @{Name='Touchpad actions without keyboard triggers';Flag='--gesture-model-test';Report='gesture-model-test.json'}
)
foreach ($case in $cases) {
    Write-Host "Testing: $($case.Name)"
    $process = Start-Process -FilePath $exe -ArgumentList $case.Flag -WindowStyle Hidden -PassThru -Wait
    $result = Get-Content -LiteralPath (Join-Path $folder $case.Report) -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or $result.status -ne 'PASS') { throw "Model test failed: $($case.Report)" }
    Write-Host "PASS: $($case.Name)"
}
