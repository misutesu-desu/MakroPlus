param([string]$OutputDirectory = 'release\MakroPlus-2.6.2-win-x64', [switch]$Archive, [switch]$SkipBuild, [string]$BuildDirectory = 'dist\makroplus-2.6.2')
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$build = Join-Path $base $BuildDirectory
if (-not $SkipBuild) { & (Join-Path $base 'build.ps1') -OutputDirectory $BuildDirectory }
$destination = [IO.Path]::GetFullPath((Join-Path $base $OutputDirectory))
if (Test-Path -LiteralPath $destination) { throw "Output already exists; choose a new folder: $destination" }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in @('MakroPlus.exe','MakroPlus.exe.config')) { Copy-Item -LiteralPath (Join-Path $build $name) -Destination $destination }
foreach ($name in @('LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $base $name) -Destination $destination }
$engine = Join-Path $destination 'runtime\llama'
New-Item -ItemType Directory -Path $engine -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $base 'runtime\llama') -File | Where-Object { $_.Extension -eq '.dll' -or $_.Name -eq 'llama-server.exe' -or $_.Name.StartsWith('LICENSE') -or $_.Name -eq 'runtime-info.json' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $engine }
Get-ChildItem -LiteralPath (Join-Path $base 'packaging') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination }
New-Item -ItemType Directory -Path (Join-Path $destination 'models') -Force | Out-Null
# Copy only the folder instructions, never developer or user model weights.
Copy-Item -LiteralPath (Join-Path $base 'packaging\models\README.txt') -Destination (Join-Path $destination 'models\README.txt')
$prerequisite = Join-Path $base 'runtime\prerequisites\vc_redist.x64.exe'
if (Test-Path -LiteralPath $prerequisite) { New-Item -ItemType Directory -Path (Join-Path $destination 'prerequisites') -Force | Out-Null; Copy-Item -LiteralPath $prerequisite -Destination (Join-Path $destination 'prerequisites') }
if (Get-ChildItem -LiteralPath $destination -Recurse -File | Where-Object { $_.Extension -in @('.gguf','.safetensors','.pt','.pth') }) { throw 'Language model weights must not be included in the release.' }
$report = Get-Content -LiteralPath (Join-Path $build 'test-results.txt') -Raw
if (-not $report.StartsWith('PASS:')) { throw 'Successful test report required.' }
Set-Content -LiteralPath (Join-Path $destination 'TEST-RESULTS.txt') -Value $report -Encoding UTF8
foreach ($name in @('device-model-test.json','activation-model-test.json','features-model-test.json','app-model-test.json','llm-test-results.json','custom-model-test.json','gesture-model-test.json','model-folder-test.json')) {
    $source = Join-Path $build $name
    if (Test-Path -LiteralPath $source) {
        $modelResult = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
        if ($modelResult.status -ne 'PASS') { throw "Unsuccessful model report: $name" }
        $count = if ($modelResult.results) { $modelResult.results.Count } else { $modelResult.completed }
        Add-Content -LiteralPath (Join-Path $destination 'TEST-RESULTS.txt') -Value "$name : PASS ($count cases)" -Encoding UTF8
    }
}
$performance = Join-Path $build 'performance-test.json'
if (Test-Path -LiteralPath $performance) { Copy-Item -LiteralPath $performance -Destination $destination }
$timing = Join-Path $build 'execution-timing.json'
if (Test-Path -LiteralPath $timing) { Copy-Item -LiteralPath $timing -Destination $destination }
$gesturePerformance = Join-Path $build 'gesture-performance-test.json'
if (Test-Path -LiteralPath $gesturePerformance) { Copy-Item -LiteralPath $gesturePerformance -Destination $destination }
$conflictPerformance = Join-Path $build 'conflict-performance-test.json'
if (Test-Path -LiteralPath $conflictPerformance) { Copy-Item -LiteralPath $conflictPerformance -Destination $destination }
$dispatchTiming = Join-Path $build 'input-dispatch-timing.json'
if (Test-Path -LiteralPath $dispatchTiming) { Copy-Item -LiteralPath $dispatchTiming -Destination $destination }
foreach($name in @('guide-test.json','model-install-merge-test.json')) {
    $source=Join-Path $build $name
    if(Test-Path -LiteralPath $source){$result=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json;if($result.status -ne 'PASS'){throw "Unsuccessful report: $name"};Add-Content -LiteralPath (Join-Path $destination 'TEST-RESULTS.txt') -Value "$name : PASS" -Encoding UTF8}
}
$compatibility = @(Get-ChildItem -LiteralPath $build -Filter 'compat-*.json' -File | ForEach-Object {
    $result=Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json
    if($result.status -ne 'PASS'){throw "Unsuccessful compatibility report: $($_.Name)"}
    Add-Content -LiteralPath (Join-Path $destination 'TEST-RESULTS.txt') -Value "$($_.Name) : PASS ($($result.results.Count) cases)" -Encoding UTF8
    [pscustomobject]@{model=$result.model;mode=$(if($_.Name.StartsWith('compat-api-')){'Compatible API via local llama.cpp fixture'}else{'Local GGUF'});status=$result.status;cases=@($result.results | Select-Object prompt,structured,passed,milliseconds)}
})
if($compatibility.Count -gt 0){ConvertTo-Json -InputObject $compatibility -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'model-compatibility-summary.json') -Encoding UTF8}
$files = Get-ChildItem -LiteralPath $destination -Recurse -File | ForEach-Object { [pscustomobject]@{path=$_.FullName.Substring($destination.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} }
@{name='Makro+';version='2.6.2';platform='Windows 10/11 x64';modelIncluded=$false;files=@($files)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'package-manifest.json') -Encoding UTF8
if ($Archive) {
    Add-Type -AssemblyName System.IO.Compression
    $zipPath = $destination + '.zip'
    if (Test-Path -LiteralPath $zipPath) { throw 'Archive already exists.' }
    $zipFile = [IO.File]::Create($zipPath)
    $zip = [IO.Compression.ZipArchive]::new($zipFile,[IO.Compression.ZipArchiveMode]::Create)
    try { foreach ($file in Get-ChildItem -LiteralPath $destination -Recurse -File) { $entry = $zip.CreateEntry($file.FullName.Substring($destination.Length+1).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal);$inputFile=[IO.File]::OpenRead($file.FullName);$outputFile=$entry.Open();try{$inputFile.CopyTo($outputFile)}finally{$inputFile.Dispose();$outputFile.Dispose()} } } finally { $zip.Dispose();$zipFile.Dispose() }
    (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zipPath) | Set-Content -LiteralPath ($zipPath+'.sha256') -Encoding ASCII
    Write-Host "Archive: $zipPath"
}
Write-Host "Package: $destination"
