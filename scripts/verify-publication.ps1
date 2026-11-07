$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$tracked=@(& git -C $root -c core.quotepath=false ls-files)
if($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0){throw 'Stage or commit the intended source files first.'}
foreach($required in @('LICENSE','.gitignore','README.md','THIRD_PARTY_NOTICES.md','build.ps1','setup-runtime.ps1','packaging/USER_GUIDE.html','packaging/models/README.txt')){if($required -notin $tracked){throw "Missing publication input: $required"}}
$issues=New-Object 'System.Collections.Generic.List[string]'
$bytes=0L
foreach($name in $tracked){
    $file=Get-Item -LiteralPath (Join-Path $root $name)
    $bytes+=$file.Length
    if($name -match '^assets/' -and $name -notin @('assets/cover.png','assets/MakroPlus.ico')){$issues.Add("Unreviewed design intermediary: $name");continue}
    if($name -match '^(dist|release|runtime|models)/' -or $file.Extension -in @('.gguf','.safetensors','.onnx','.pt','.pth','.exe','.dll','.zip','.pfx','.pem','.key') -or $file.Length -gt 5MB){$issues.Add("Generated/private/oversized input: $name");continue}
    if($file.Extension -notin @('.png','.ico')){
        $content=[IO.File]::ReadAllText($file.FullName)
        if($content -match '(?i)(?:github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)'){$issues.Add("Possible credential: $name")}
        if($content -match '(?i)[A-Z]:[\\/]+Users[\\/]+(?!Public[\\/]|USERNAME[\\/]|Example[\\/])[^\\/\s"\u0027]+[\\/]'){$issues.Add("Development-computer path: $name")}
    }
}
if($issues.Count -gt 0){$issues|ForEach-Object {Write-Output $_};throw 'Publication content checks failed.'}
if(-not ([IO.File]::ReadAllText((Join-Path $root 'LICENSE')).Contains('GNU GENERAL PUBLIC LICENSE'))){throw 'GPL license text missing.'}
Write-Output ('PASS: '+$tracked.Count+' reviewed source files; '+$bytes+' bytes; no model/runtime binaries, generated releases, recognized credential formats or development-computer paths.')
