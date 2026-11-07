param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$out = Join-Path $base $OutputDirectory
New-Item -ItemType Directory -Path $out -Force | Out-Null
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Xaml.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll','UIAutomationClient.dll','UIAutomationTypes.dll') | ForEach-Object { '/reference:' + (Join-Path $wpf $_) }
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /utf8output ('/out:' + (Join-Path $out 'MakroPlus.exe')) ('/win32manifest:' + (Join-Path $base 'app.manifest')) ('/win32icon:' + (Join-Path $base 'assets\MakroPlus.ico')) ('/resource:' + (Join-Path $base 'assets\cover.png') + ',MakroPlus.Cover.png') ('/resource:' + (Join-Path $base 'assets\MakroPlus.ico') + ',MakroPlus.Icon.ico') @refs (Join-Path $base 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$runtimeSource = Join-Path $base 'runtime\llama'
if (Test-Path -LiteralPath $runtimeSource) {
    $runtimeOutput = Join-Path $out 'runtime\llama'
    New-Item -ItemType Directory -Path $runtimeOutput -Force | Out-Null
    Copy-Item -Path (Join-Path $runtimeSource '*') -Destination $runtimeOutput -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $base 'MakroPlus.exe.config') -Destination $out -Force
New-Item -ItemType Directory -Path (Join-Path $out 'models') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $base 'packaging\models\README.txt') -Destination (Join-Path $out 'models\README.txt') -Force
Copy-Item -LiteralPath (Join-Path $base 'packaging\USER_GUIDE.html') -Destination $out -Force
foreach($name in @('LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $base $name) -Destination $out -Force}
Write-Host "Ready: $out\MakroPlus.exe"
