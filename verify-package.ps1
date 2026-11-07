param([string]$Directory='release\MakroPlus-2.6.2-win-x64')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
$folder=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Directory))
$manifest=Get-Content -LiteralPath (Join-Path $folder 'package-manifest.json') -Raw|ConvertFrom-Json
$files=@(Get-ChildItem -LiteralPath $folder -Recurse -File)
foreach($required in @('models\README.txt','USER_GUIDE.html')){if(-not(Test-Path -LiteralPath (Join-Path $folder $required) -PathType Leaf)){throw "Required model instructions or guide missing: $required"}}
if($manifest.modelIncluded -ne $false -or ($files|Where-Object {$_.Extension -in @('.gguf','.safetensors','.pt','.pth')})){throw 'Model weights detected in package.'}
if($files.Count -ne $manifest.files.Count+1){throw 'Manifest file count mismatch.'}
foreach($entry in $manifest.files){
    $path=[IO.Path]::GetFullPath((Join-Path $folder $entry.path))
    if(-not $path.StartsWith($folder+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path leaves package directory.'}
    if((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256){throw "Manifest mismatch: $($entry.path)"}
}
$exe=Get-Item -LiteralPath (Join-Path $folder 'MakroPlus.exe')
if($exe.VersionInfo.FileVersion -ne $manifest.version+'.0'){throw 'Executable version mismatch.'}
$zipPath=$folder+'.zip'
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if((Get-Content -LiteralPath ($zipPath+'.sha256') -Raw).Trim() -ne $hash+'  '+[IO.Path]::GetFileName($zipPath)){throw 'Archive sidecar hash mismatch.'}
$stream=[IO.File]::OpenRead($zipPath)
$zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read)
try{
    if($zip.Entries.Count -ne $files.Count){throw 'Archive file count mismatch.'}
    $seen=@{}
    foreach($entry in $zip.Entries){
        if($seen.ContainsKey($entry.FullName)){throw 'Duplicate archive entry.'};$seen[$entry.FullName]=$true
        $expected=$files|Where-Object {$_.FullName.Substring($folder.Length+1).Replace('\','/') -ceq $entry.FullName}
        if(@($expected).Count -ne 1 -or $expected.Length -ne $entry.Length){throw "Archive entry mismatch: $($entry.FullName)"}
        $inputStream=$entry.Open();$algorithm=[Security.Cryptography.SHA256]::Create()
        try{$entryHash=[BitConverter]::ToString($algorithm.ComputeHash($inputStream)).Replace('-','').ToLowerInvariant()}finally{$inputStream.Dispose();$algorithm.Dispose()}
        if($entryHash -ne (Get-FileHash -LiteralPath $expected.FullName -Algorithm SHA256).Hash.ToLowerInvariant()){throw "Archive content mismatch: $($entry.FullName)"}
    }
}finally{$zip.Dispose();$stream.Dispose()}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $folder 'Install-MakroPlus.ps1') -CheckOnly
if($LASTEXITCODE -ne 0){throw 'Installer prerequisite check failed.'}
$sourceHashes=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File|ForEach-Object {[pscustomobject]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$verification=[ordered]@{status='PASS';version=$manifest.version;files=$files.Count;modelIncluded=$false;archiveBytes=(Get-Item -LiteralPath $zipPath).Length;sha256=$hash;executableSHA256=(Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant();verifiedAt=(Get-Date).ToString('o');source=$sourceHashes}
$verification|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path (Split-Path $folder -Parent) ('verification-'+$manifest.version+'.json')) -Encoding UTF8
Write-Host "PASS: $($files.Count) package files, every ZIP entry, hashes, model exclusion, executable version and prerequisite check."
