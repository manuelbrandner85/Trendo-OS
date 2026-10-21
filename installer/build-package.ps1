<#  Erstellt ein selbstständiges Windows-Paket (win-x64, self-contained) ohne Adminrechte.
    Ausgabe: artifacts\TrendoOS-win-x64\ und artifacts\TrendoOS-win-x64.zip + SHA256SUMS.txt #>
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $repo 'artifacts\TrendoOS-win-x64'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
dotnet publish (Join-Path $repo 'src\Trendo.App\Trendo.App.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen ($LASTEXITCODE)" }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $out
$zip = "$out.zip"; if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
$sums = Join-Path $repo 'artifacts\SHA256SUMS.txt'
@($zip, (Join-Path $out 'TrendoOS.exe'), (Join-Path $out 'TrendoOS.dll'), (Join-Path $out 'Trendo.Core.dll')) |
  ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLower(), (Split-Path -Leaf $_) } |
  Set-Content -LiteralPath $sums -Encoding utf8
Get-Content -LiteralPath $sums
