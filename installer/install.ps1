<#  Installiert Trendo OS nur für den aktuellen Benutzer (keine Adminrechte, keine Registry-/Systemänderung).
    Ziel: %LOCALAPPDATA%\Programs\TrendoOS  ·  Startmenü-Verknüpfung im Benutzer-Startmenü. #>
param([string]$Source = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\TrendoOS'
$menu   = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$lnk    = Join-Path $menu 'Trendo OS.lnk'
if (-not (Test-Path -LiteralPath (Join-Path $Source 'TrendoOS.exe'))) { throw "TrendoOS.exe nicht im Quellordner gefunden." }
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $Source '*') -Destination $target -Recurse -Force
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk); $s.TargetPath = Join-Path $target 'TrendoOS.exe'; $s.WorkingDirectory = $target; $s.Description = 'Trendo OS'; $s.Save()
Set-Content -LiteralPath (Join-Path $target 'INSTALL_INFO.txt') -Value "Installiert: $(Get-Date -Format o)`nVersion: P1-Vorschau`nDeinstallation: uninstall.ps1 in diesem Ordner" -Encoding utf8
"Installiert nach %LOCALAPPDATA%\Programs\TrendoOS; Startmenü-Verknüpfung 'Trendo OS' angelegt."
