# T-0687 (Audit 6, Abschnitt 7): Setup-Rundlauf als Skript – Installieren -> Starten -> Update auf eine neuere Testversion -> Rueckfall -> Deinstallieren.
# Baut dafuer ZWEI eigene Testpakete mit eigener Paketkennung "TrendoOSRundlauf" (Version 0.0.1 und 0.0.2), installiert sie mit "--installto"
# in einen eigenen Temp-Ordner und ruehrt die echte Installation "TrendoOS" nie an (anderer Registry-Schluessel, anderer Ordner, keine Verknuepfungen).
#
# WICHTIG – warum der Lauf standardmaessig NICHT ausgefuehrt wird:
#   Velopack startet die App direkt nach dem Installieren selbst, ohne Argumente. Trendo nimmt dann den echten Datenordner
#   (%APPDATA%\TrendoOS, ueber SHGetKnownFolderPath – laesst sich nicht per Umgebungsvariable umlenken) und laeuft dort mit dem
#   Startwaechter (BootGuard). Ausserdem laufen die Installations- und Deinstallations-Haken der App (Vollpaket merken, Datenfrage).
#   Das ist ein Eingriff in die echten Daten des Nutzers. Deshalb baut das Skript ohne -Ausfuehren nur die Pakete und prueft die
#   Setup-Optionen; den eigentlichen Rundlauf startet der Nutzer selbst bewusst mit -Ausfuehren (am besten mit geschlossenem Trendo
#   und nach einer Sicherung von %APPDATA%\TrendoOS). Das Skript sichert diesen Ordner vorher und vergleicht ihn danach (nur Bericht,
#   es stellt NICHTS selbst zurueck).
#
# Aufruf:
#   powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf.ps1                 # nur bauen und pruefen (sicher)
#   powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf.ps1 -Ausfuehren     # ganzer Rundlauf (siehe oben)
# Voraussetzungen: .NET 10 SDK, vpk (dotnet tool install -g vpk), kein Admin. Ergebnis: Logdatei + SHA256SUMS im Arbeitsordner (Beleg-Ordner mit -Beleg).
param(
    [string]$Work = (Join-Path $env:TEMP ("trendo-setup-rundlauf-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$Beleg = '',
    [switch]$Ausfuehren,
    [string]$AltVersion = '0.0.1',
    [string]$NeuVersion = '0.0.2'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$packId = 'TrendoOSRundlauf'
$inst = Join-Path $Work 'installiert'
$daten = Join-Path $Work 'daten'
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$packId"
$realKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TrendoOS'
$realData = Join-Path $env:APPDATA 'TrendoOS'
New-Item -ItemType Directory -Force $Work, $daten | Out-Null
if (-not $Beleg) { $Beleg = $Work }
New-Item -ItemType Directory -Force $Beleg | Out-Null
$Log = Join-Path $Beleg 'setup-rundlauf.log'
$script:ok = $true
function Clean([string]$m) {
    $m = $m -replace [regex]::Escape($env:USERPROFILE), 'C:\Users\<user>'
    if ($env:COMPUTERNAME) { $m = $m -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
    return $m
}
function Say([string]$m) { $line = Clean $m; Add-Content -Path $Log -Value $line -Encoding utf8; Write-Output $line }
function Check([string]$name, [bool]$cond, [string]$detail = '') { if (-not $cond) { $script:ok = $false }; Say ("{0}  {1}{2}" -f ($(if ($cond) { 'PASS' } else { 'FAIL' })), $name, $(if ($detail) { " - $detail" } else { '' })) }
function Exe() { Join-Path $inst 'current\TrendoOS.exe' }
function Version() { $e = Exe; if (Test-Path $e) { (Get-Item $e).VersionInfo.ProductVersion -replace '\+.*$', '' } else { '' } }
function RegVersion([string]$k) { if (Test-Path $k) { (Get-ItemProperty $k).DisplayVersion } else { '' } }
function StopMine([datetime]$since) {
    Start-Sleep -Seconds 4
    Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" -and $_.StartTime -ge $since } | ForEach-Object { Say "  von Setup gestartete Test-App beendet (PID $($_.Id))"; Stop-Process -Id $_.Id -Force }
}
function Hashes([string]$root) {
    if (-not (Test-Path $root)) { return @{} }
    $h = @{}; Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { try { $h[$_.FullName.Substring($root.Length)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } catch { $h[$_.FullName.Substring($root.Length)] = 'gesperrt' } }; return $h
}
function Run([string]$file, [string[]]$a) { Say ("$ " + (Split-Path $file -Leaf) + ' ' + ($a -join ' ')); $p = Start-Process $file -ArgumentList $a -Wait -PassThru -NoNewWindow; Say "  Exit $($p.ExitCode)"; return $p.ExitCode }
function StartsWithOwnData([string]$step) {
    $since = Get-Date; $slog = Join-Path $Work "$step-start.txt"
    $p = Start-Process (Exe) -ArgumentList @('--no-first-run', '--data-root', "`"$daten`"", '--startup-log', "`"$slog`"") -PassThru
    $sw = [Diagnostics.Stopwatch]::StartNew(); while (-not (Test-Path $slog) -and $sw.Elapsed.TotalSeconds -lt 90) { Start-Sleep -Milliseconds 250 }
    Start-Sleep -Seconds 3; $p.Refresh()
    $first = if (Test-Path $slog) { (Get-Content $slog | Where-Object { $_ -match '^(window.opened|first.frame) ' }) -join ' | ' } else { 'kein Startprotokoll' }
    Check "$step`: App startet (eigener Datenordner)" ((Test-Path $slog) -and -not $p.HasExited) $first
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; StopMine $since
}

Say "Setup-Rundlauf $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Windows $([Environment]::OSVersion.VersionString)"
Say "Arbeitsordner: $Work - Paketkennung: $packId - Versionen $AltVersion -> $NeuVersion"
$vpk = (Get-Command vpk -ErrorAction SilentlyContinue).Source
if (-not $vpk) { Say 'BLOCKIERT: vpk fehlt (dotnet tool install -g vpk).'; exit 3 }

# 1 Zwei Testpakete bauen (gleiche App, nur Versionsnummer verschieden). Keine Verknuepfungen, eigene Kennung, je Version eigener Ausgabeordner.
foreach ($v in @($AltVersion, $NeuVersion)) {
    $pub = Join-Path $Work "publish-$v"; $out = Join-Path $Work "pakete-$v"
    Say "Baue App $v ..."
    & dotnet publish (Join-Path $repo 'src\Trendo.App\Trendo.App.csproj') -c Release -r win-x64 --self-contained true -p:Version=$v -o $pub --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { Say "FAIL dotnet publish $v (Exit $LASTEXITCODE)"; exit 1 }
    & $vpk '[win]' pack -u $packId -v $v -p $pub -e TrendoOS.exe -i (Join-Path $repo 'src\Trendo.App\Assets\logo\trendo.ico') --packTitle 'Trendo OS Rundlauf' --packAuthors 'Trendo OS Projekt' --shortcuts None --delta None -o $out --yes 2>&1 | ForEach-Object { Add-Content -Path $Log -Value (Clean "$_") -Encoding utf8 }
    if ($LASTEXITCODE -ne 0) { Say "FAIL vpk pack $v (Exit $LASTEXITCODE)"; exit 1 }
    Remove-Item $pub -Recurse -Force # Platz sparen: das Paket enthaelt alles
}
$setupOld = Join-Path (Join-Path $Work "pakete-$AltVersion") "$packId-win-Setup.exe"
$nupkgNew = Join-Path (Join-Path $Work "pakete-$NeuVersion") "$packId-$NeuVersion-full.nupkg"
Check 'Pakete gebaut: Setup alt und Update-Paket neu' ((Test-Path $setupOld) -and (Test-Path $nupkgNew)) "$([math]::Round((Get-Item $setupOld).Length / 1MB)) MB / $([math]::Round((Get-Item $nupkgNew).Length / 1MB)) MB"
Say 'Hinweis: Setup.exe wird mit "--silent --installto <Ordner>" aufgerufen (Velopack-Option fuer einen eigenen Zielordner); geprueft wird das erst im Rundlauf selbst.'

if (-not $Ausfuehren) {
    Say 'NICHT AUSGEFUEHRT: Der Rundlauf startet die App nach dem Installieren mit dem echten Datenordner (siehe Kopf des Skripts).'
    Say 'Bewusst starten mit: powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf.ps1 -Ausfuehren'
    Get-ChildItem $Beleg -File | Where-Object Name -ne 'SHA256SUMS' | ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" } | Set-Content (Join-Path $Beleg 'SHA256SUMS') -Encoding ascii
    exit 0
}

if (Get-Process TrendoOS -ErrorAction SilentlyContinue) { Say 'ABBRUCH: Trendo OS laeuft gerade. Bitte schliessen.'; exit 2 }
$realBefore = Hashes $realData; $realRegBefore = RegVersion $realKey
Say "Echte Installation: Registry TrendoOS $realRegBefore, echter Datenordner $($realBefore.Count) Dateien (nur Vergleich, keine Aenderung durch das Skript)"
if (Test-Path $realData) { Copy-Item $realData (Join-Path $Work 'sicherung-echte-daten') -Recurse -Force; Say 'Sicherung der echten Daten im Arbeitsordner angelegt.' }

# 2 Installieren (alte Testversion) in den eigenen Ordner
$since = Get-Date; Run $setupOld @('--silent', '--installto', "`"$inst`"") | Out-Null; StopMine $since
$v = Version; Check "2 Installieren: Version $v" ($v -eq $AltVersion) "Registry $(RegVersion $key)"
Check '2 Installieren: im eigenen Ordner' (Test-Path (Exe)) $inst
StartsWithOwnData '2-installiert'

# 3 Update auf die neuere Testversion (lokales Paket, wie der eingebaute Updater)
$since = Get-Date; Run (Join-Path $inst 'Update.exe') @('apply', '--silent', '--norestart', '-p', "`"$nupkgNew`"") | Out-Null; Start-Sleep -Seconds 3; StopMine $since
$v3 = Version; Check "3 Update: Version $v3" ($v3 -eq $NeuVersion) "Registry $(RegVersion $key)"
StartsWithOwnData '3-update'

# 4 Rueckfall: aeltere Version ueber die neuere installieren
$since = Get-Date; Run $setupOld @('--silent', '--installto', "`"$inst`"") | Out-Null; StopMine $since
$v4 = Version; Check "4 Rueckfall: Version $v4" ($v4 -eq $AltVersion) "Registry $(RegVersion $key)"
StartsWithOwnData '4-rueckfall'

# 5 Deinstallieren und Reste pruefen
Run (Join-Path $inst 'Update.exe') @('uninstall', '--silent') | Out-Null; Start-Sleep -Seconds 3
Check '5 Deinstallieren: Programm entfernt' (-not (Test-Path (Exe)))
Check '5 Deinstallieren: Registry-Eintrag entfernt' (-not (Test-Path $key))
$lnk = Get-ChildItem (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') -Filter 'Trendo OS Rundlauf*.lnk' -Recurse -ErrorAction SilentlyContinue
Check '5 Deinstallieren: keine Verknuepfung uebrig' (-not $lnk)
$rest = if (Test-Path $inst) { (Get-ChildItem $inst -Recurse -File | Measure-Object).Count } else { 0 }
Say "  Reste im Installationsordner: $rest Datei(en)"

# 6 Echte Installation unberuehrt?
Check '6 Echte Installation: Registry unveraendert' ((RegVersion $realKey) -eq $realRegBefore) "vorher $realRegBefore, nachher $(RegVersion $realKey)"
$realAfter = Hashes $realData
$changed = @($realAfter.Keys | Where-Object { $realBefore[$_] -ne $realAfter[$_] }) + @($realBefore.Keys | Where-Object { -not $realAfter.ContainsKey($_) })
Say "  Echter Datenordner: $($changed.Count) Datei(en) neu/geaendert/fehlend (Auto-Start der App durch Setup): $(($changed | Select-Object -First 15) -join ', ')"
Say $(if ($script:ok) { 'ERGEBNIS: ALLE BESTANDEN' } else { 'ERGEBNIS: FEHLER - siehe FAIL-Zeilen' })
Get-ChildItem $Beleg -File | Where-Object Name -ne 'SHA256SUMS' | ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" } | Set-Content (Join-Path $Beleg 'SHA256SUMS') -Encoding ascii
if ($script:ok) { exit 0 } else { exit 1 }
