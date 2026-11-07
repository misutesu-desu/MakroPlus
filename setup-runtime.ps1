$ErrorActionPreference = 'Stop'
$runtimeFolder = Join-Path $PSScriptRoot 'runtime\llama'
$archivePath = Join-Path $PSScriptRoot 'runtime\llama-b12132-vulkan.zip'
$packageUrl = 'https://github.com/ggml-org/llama.cpp/releases/download/b12132/llama-b12132-bin-win-vulkan-x64.zip'
$expectedHash = 'a2bbf7c46695bca2d3baa1919c64975061234dc9ea56a571f2f070704bbd5516'
New-Item -ItemType Directory -Path $runtimeFolder -Force | Out-Null
Invoke-WebRequest -Uri $packageUrl -OutFile $archivePath
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw 'Runtime SHA-256 validation failed.' }
Expand-Archive -LiteralPath $archivePath -DestinationPath $runtimeFolder -Force
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b12132/LICENSE' -OutFile (Join-Path $runtimeFolder 'LICENSE-llama.cpp.txt')
@{ version = 'b12132'; source = $packageUrl; sha256 = $expectedHash; backend = 'Vulkan'; installed = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtimeFolder 'runtime-info.json') -Encoding utf8
Write-Host "Runtime ready: $runtimeFolder"
