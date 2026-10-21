<#  Entfernt die Benutzer-Installation von Trendo OS. Benutzerdaten (%LOCALAPPDATA%\TrendoOS) bleiben erhalten, außer mit -RemoveData. #>
param([switch]$RemoveData)
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\TrendoOS'
$lnk    = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Trendo OS.lnk'
if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
if (Test-Path -LiteralPath $target) {
  if ($PSScriptRoot -and ($PSScriptRoot.TrimEnd('\') -ieq $target.TrimEnd('\'))) { Set-Location $env:TEMP }
  Remove-Item -LiteralPath $target -Recurse -Force
}
if ($RemoveData) { $d = Join-Path $env:LOCALAPPDATA 'TrendoOS'; if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force } }
"Trendo OS deinstalliert$(if($RemoveData){' (inkl. Daten)'})."
