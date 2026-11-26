# T-0776: Fundorte und Versionen der Fremdprogramme – nur lesend (Trendo-Werkzeugordner, Programme-Ordner, PATH).
# Jedes Programm läuft für die Versionsabfrage mit eigenem Profil im angegebenen Arbeitsordner. Ausgabe ohne Benutzernamen.
param([Parameter(Mandatory = $true)][string]$Arbeit, [Parameter(Mandatory = $true)][string]$Ausgabe)
$ErrorActionPreference = 'Continue'
$home_ = [Environment]::GetFolderPath('UserProfile')
function Clean([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace [regex]::Escape($home_), 'C:\Users\<user>') -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
$tw = Join-Path $env:LOCALAPPDATA 'TrendoWerkzeuge'
$pf = $env:ProgramW6432; if (-not $pf) { $pf = $env:ProgramFiles }
$out = New-Object System.Collections.Generic.List[string]
$out.Add("# T-0776 Werkzeug-Fundorte - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$out.Add('## PATH')
foreach ($c in 'soffice', 'krita', 'inkscape', '7z', 'freecadcmd') { $g = Get-Command $c -ErrorAction SilentlyContinue; $out.Add("$c : " + $(if ($g) { Clean $g.Source } else { 'nicht im PATH' })) }
$out.Add('## Trendo-Werkzeugordner (%LOCALAPPDATA%\TrendoWerkzeuge)')
foreach ($f in 'libreoffice\App\libreoffice\program\soffice.exe', 'krita\bin\krita.exe', 'inkscape\bin\inkscape.exe', '7zip\7z.exe') {
    $p = Join-Path $tw $f
    if (Test-Path $p) { $out.Add("vorhanden: %LOCALAPPDATA%\TrendoWerkzeuge\$f  SHA-256 $((Get-FileHash $p -Algorithm SHA256).Hash.ToLower())") } else { $out.Add("fehlt: $f") }
}
$out.Add('## Programme-Ordner')
foreach ($f in 'FreeCAD 1.1\bin\freecadcmd.exe', '7-Zip\7z.exe', 'LibreOffice\program\soffice.exe', 'Krita (x64)\bin\krita.exe', 'Inkscape\bin\inkscape.exe') {
    $p = Join-Path $pf $f
    if (Test-Path $p) { $out.Add("vorhanden: C:\Program Files\$f  SHA-256 $((Get-FileHash $p -Algorithm SHA256).Hash.ToLower())") } else { $out.Add("fehlt: C:\Program Files\$f") }
}
$lo = Join-Path $pf 'LibreOffice\program'
if (Test-Path $lo) { $out.Add('Inhalt C:\Program Files\LibreOffice\program: ' + ((Get-ChildItem $lo -Force | ForEach-Object Name) -join ', ')) }
$out.Add('## Versionen (eigenes Profil im Arbeitsordner)')
New-Item -ItemType Directory -Force (Join-Path $Arbeit 'k\AppData\Roaming'), (Join-Path $Arbeit 'k\AppData\Local'), (Join-Path $Arbeit 'i'), (Join-Path $Arbeit 'f') | Out-Null
$uri = ([Uri](Join-Path $Arbeit 'lo')).AbsoluteUri
$out.Add('LibreOffice: ' + (Clean ((& (Join-Path $tw 'libreoffice\App\libreoffice\program\soffice.com') "-env:UserInstallation=$uri" --version 2>$null) -join ' ')).Trim())
$env:USERPROFILE = Join-Path $Arbeit 'k'; $env:APPDATA = Join-Path $Arbeit 'k\AppData\Roaming'; $env:LOCALAPPDATA_SAVE = $env:LOCALAPPDATA
$env:LOCALAPPDATA = Join-Path $Arbeit 'k\AppData\Local'
$kv = Join-Path $Arbeit 'krita-version.txt'  # krita.exe ist ein Fensterprogramm: Ausgabe nur über Umleitung
Start-Process -FilePath (Join-Path $tw 'krita\bin\krita.exe') -ArgumentList '--version' -Wait -NoNewWindow -RedirectStandardOutput $kv
$out.Add('Krita: ' + ((Get-Content $kv) -join ' ').Trim())
$env:LOCALAPPDATA = $env:LOCALAPPDATA_SAVE
$env:INKSCAPE_PROFILE_DIR = Join-Path $Arbeit 'i'; $env:XDG_CONFIG_HOME = $env:INKSCAPE_PROFILE_DIR; $env:XDG_CACHE_HOME = $env:INKSCAPE_PROFILE_DIR; $env:XDG_DATA_HOME = $env:INKSCAPE_PROFILE_DIR
$out.Add('Inkscape: ' + ((& (Join-Path $tw 'inkscape\bin\inkscape.com') --version 2>$null) -join ' ').Trim())
$out.Add('7-Zip (Werkzeugordner): ' + ((& (Join-Path $tw '7zip\7z.exe') 2>$null) | Select-Object -Skip 1 -First 1))
$out.Add('7-Zip (Programme): ' + ((& (Join-Path $pf '7-Zip\7z.exe') 2>$null) | Select-Object -Skip 1 -First 1))
$env:FREECAD_USER_HOME = Join-Path $Arbeit 'f'; $env:FREECAD_USER_DATA = $env:FREECAD_USER_HOME; $env:FREECAD_USER_TEMP = $env:FREECAD_USER_HOME
$out.Add('FreeCAD: ' + ((& (Join-Path $pf 'FreeCAD 1.1\bin\freecadcmd.exe') --version 2>$null) -join ' ').Trim())
[IO.File]::WriteAllLines($Ausgabe, $out, (New-Object Text.UTF8Encoding $false))
$out
