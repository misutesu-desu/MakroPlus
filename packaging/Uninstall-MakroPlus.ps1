$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\MakroPlus'))
if ($target -ne [IO.Path]::GetFullPath($PSScriptRoot)) { throw 'Run the uninstaller from the installed MakroPlus folder.' }
$executable = Join-Path $target 'MakroPlus.exe'
if (Get-Process -Name MakroPlus -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }) { throw 'Close Makro+ before uninstalling.' }
$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'),(Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'))) {
    $link = Join-Path $folder 'Makro+.lnk'
    if ((Test-Path -LiteralPath $link) -and $shell.CreateShortcut($link).TargetPath -eq $executable) { Remove-Item -LiteralPath $link }
}
# The absolute target above is fixed to this user's installed application directory.
Remove-Item -LiteralPath $target -Recurse -Force
Write-Host 'Makro+ removed. User settings and exported macros were preserved.'
