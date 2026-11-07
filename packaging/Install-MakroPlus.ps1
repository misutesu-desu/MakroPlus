param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Makro+ requires 64-bit Windows 10/11.' }
$framework = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
if (-not $framework -or $framework.Release -lt 528040) { throw '.NET Framework 4.8 is required: https://dotnet.microsoft.com/download/dotnet-framework/net48' }
$vc = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
if (-not $vc) { $vc = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue }
if ($CheckOnly) { [pscustomobject]@{Windows64=$true;Framework=$framework.Release;VisualCppInstalled=($vc -and $vc.Installed -eq 1)} | ConvertTo-Json; exit 0 }
if (-not $vc -or $vc.Installed -ne 1) {
    $installer = Join-Path $PSScriptRoot 'prerequisites\vc_redist.x64.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'Microsoft runtime signature validation failed.' }
    $process = Start-Process -FilePath $installer -ArgumentList '/install','/passive','/norestart' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0,1638,3010)) { throw "Visual C++ runtime installation failed: $($process.ExitCode)" }
}
$destination = Join-Path $env:LOCALAPPDATA 'Programs\MakroPlus'
$expected = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\MakroPlus'))
if ([IO.Path]::GetFullPath($destination) -ne $expected) { throw 'Invalid installation directory.' }
$executable = Join-Path $destination 'MakroPlus.exe'
$active = Get-Process -Name MakroPlus -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }
if ($active) { throw 'Close Makro+ before updating.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
if ([IO.Path]::GetFullPath($PSScriptRoot) -ne $expected) {
    Get-ChildItem -LiteralPath $PSScriptRoot -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
}
$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'),(Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'))) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'Makro+.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $destination
    $shortcut.IconLocation = $executable + ',0'
    $shortcut.Save()
}
Write-Host "Installed: $executable"
Start-Process -FilePath $executable -WorkingDirectory $destination -WindowStyle Hidden
