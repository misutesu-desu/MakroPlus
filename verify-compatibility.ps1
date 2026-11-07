param([Parameter(Mandatory=$true)][string[]]$ModelPaths, [string]$OutputDirectory='dist\makroplus-2.6.2')
$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot $OutputDirectory
$exe=Join-Path $folder 'MakroPlus.exe'
foreach($mode in @('--compat-model-test','--compat-api-test')) {
    foreach($model in $ModelPaths) {
        $resolved=(Resolve-Path -LiteralPath $model).Path
        $prefix=if($mode -eq '--compat-api-test'){'compat-api-'}else{'compat-'}
        $report=Join-Path $folder ($prefix+[IO.Path]::GetFileNameWithoutExtension($resolved)+'.json')
        Write-Host "Testing $mode : $([IO.Path]::GetFileName($resolved))"
        $process=Start-Process -FilePath $exe -ArgumentList ($mode+' "'+$resolved+'"') -WindowStyle Hidden -PassThru -Wait
        $result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or $result.status -ne 'PASS'){throw "Compatibility test failed: $report"}
        Write-Host "PASS: $($result.results.Count) cases"
    }
}
