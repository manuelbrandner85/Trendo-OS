# T-0724 (Audit Phase E): abgeschotteter Rundlauf Installation -> Start -> Update -> Deinstallation mit dem PORTABLEN Paket
# (TrendoOS-win-Portable.zip, wird mit jedem Release ausgeliefert, siehe installer/build-setup.sh).
#
# Warum portabel und nicht Setup.exe: Velopacks Setup.exe schreibt immer einen Deinstallations-Eintrag in die Registry (HKCU\...\Uninstall)
# und startet die App danach ohne Argumente mit dem echten Datenordner (%APPDATA%\TrendoOS). Beides laesst sich nicht abschalten
# -> der Setup.exe-Weg bleibt hier BLOCKIERT (siehe scripts/setup-rundlauf.ps1, nur bewusst mit -Ausfuehren).
# Dieser Lauf beruehrt weder Registry noch Startmenue, Desktop oder echtes Benutzerprofil: Installationsziel und Datenordner liegen unter
# %TEMP%\trendo-t0724-*; die App wird nur mit --data-root <Temp> gestartet; Update.exe laeuft mit --norestart (kein Start ohne Argumente).
# Vorher/Nachher-Vergleich der echten Orte (nur lesen) belegt, dass nichts veraendert wurde.
#
# Aufruf: powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf-portabel.ps1 -Beleg reports/T-0724/setup
# Voraussetzungen: .NET 10 SDK, vpk (dotnet tool install -g vpk), kein Admin.
param(
    [string]$Work = (Join-Path $env:TEMP ("trendo-t0724-setup-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$Beleg = '',
    [string]$AltVersion = '0.0.1',
    [string]$NeuVersion = '0.0.2',
    [switch]$Behalten
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$packId = 'TrendoOSRundlauf'
$inst = Join-Path $Work 'installiert'
$daten = Join-Path $Work 'daten'
New-Item -ItemType Directory -Force $Work, $daten | Out-Null
if (-not $Beleg) { $Beleg = $Work }
New-Item -ItemType Directory -Force $Beleg | Out-Null
$Log = Join-Path $Beleg 'setup-rundlauf-portabel.log'
if (Test-Path $Log) { Remove-Item $Log }
$script:ok = $true
function Clean([string]$m) {
    $m = $m -replace [regex]::Escape($env:USERPROFILE), 'C:\Users\<user>'
    $m = $m -replace [regex]::Escape($env:USERPROFILE.Replace('\', '/')), 'C:/Users/<user>'
    if ($env:COMPUTERNAME) { $m = $m -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
    return $m
}
function Say([string]$m) { $line = Clean $m; Add-Content -Path $Log -Value $line -Encoding utf8; Write-Output $line }
function Check([string]$name, [bool]$cond, [string]$detail = '') { if (-not $cond) { $script:ok = $false }; Say ("{0}  {1}{2}" -f ($(if ($cond) { 'PASS' } else { 'FAIL' })), $name, $(if ($detail) { " - $detail" } else { '' })) }
function Exe() { Join-Path $inst 'current\TrendoOS.exe' }
function Version() { $e = Exe; if (Test-Path $e) { (Get-Item $e).VersionInfo.ProductVersion -replace '\+.*$', '' } else { '' } }
function Hashes([string]$root) {
    $h = @{}; if (-not (Test-Path $root)) { return $h }
    Get-ChildItem $root -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object { try { $h[$_.FullName.Substring($root.Length)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } catch { $h[$_.FullName.Substring($root.Length)] = 'gesperrt' } }
    return $h
}
# Echte Orte nur lesen: Registry-Deinstallationseintraege (HKCU), Startmenue, Desktop, %APPDATA%\TrendoOS, %LOCALAPPDATA% (oberste Ebene, Trendo/Velopack)
function Snapshot() {
    $s = [ordered]@{}
    $s['registry-uninstall'] = @(Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue | ForEach-Object PSChildName | Sort-Object)
    $s['startmenue-trendo'] = @(Get-ChildItem (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') -Recurse -Filter '*Trendo*' -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
    $s['desktop'] = @(Get-ChildItem ([Environment]::GetFolderPath('Desktop')), ([Environment]::GetFolderPath('CommonDesktopDirectory')) -Force -ErrorAction SilentlyContinue | ForEach-Object Name | Sort-Object)
    $s['localappdata-trendo-velopack'] = @(Get-ChildItem $env:LOCALAPPDATA -Directory -Force -ErrorAction SilentlyContinue | Where-Object Name -match 'Trendo|velopack|Rundlauf' | ForEach-Object Name | Sort-Object)
    $s['appdata-trendo'] = @(Get-ChildItem $env:APPDATA -Directory -Force -ErrorAction SilentlyContinue | Where-Object Name -match 'Trendo|Rundlauf' | ForEach-Object Name | Sort-Object)
    $real = Join-Path $env:APPDATA 'TrendoOS'
    # echter Datenordner kann sehr gross sein (KI-Modelle): Pfad + Groesse + Aenderungszeit statt Pruefsumme
    $s['appdata-trendoos-dateien'] = @(Get-ChildItem $real -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object { "$($_.FullName.Substring($real.Length))=$($_.Length)@$($_.LastWriteTimeUtc.Ticks)" } | Sort-Object)
    return $s
}
function SelfTest([string]$step) {
    $out = Join-Path $Work "$step-selftest.txt"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process (Exe) -ArgumentList @('--api-selftest', '--offline', '--no-first-run', '--data-root', "`"$daten`"") -Wait -PassThru -NoNewWindow -RedirectStandardOutput $out -RedirectStandardError "$out.err"
    $txt = (Get-Content $out -Raw -ErrorAction SilentlyContinue) + (Get-Content "$out.err" -Raw -ErrorAction SilentlyContinue)
    Add-Content -Path $Log -Value (Clean ("  | " + (($txt -split "`r?`n" | Where-Object { $_ } | Select-Object -Last 25) -join "`n  | "))) -Encoding utf8
    Check "$step`: Selbsttest --api-selftest --offline --no-first-run --data-root <temp>" ($p.ExitCode -eq 0) "Exit $($p.ExitCode), $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s"
}
function GuiStart([string]$step) {
    $slog = Join-Path $Work "$step-start.txt"
    if (Test-Path $slog) { Remove-Item $slog }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process (Exe) -ArgumentList @('--no-first-run', '--data-root', "`"$daten`"", '--startup-log', "`"$slog`"") -PassThru
    while (-not (Test-Path $slog) -and $sw.Elapsed.TotalSeconds -lt 90 -and -not $p.HasExited) { Start-Sleep -Milliseconds 100 }
    $wall = $sw.Elapsed.TotalMilliseconds
    Start-Sleep -Seconds 2; $p.Refresh()
    $marks = if (Test-Path $slog) { (Get-Content $slog | Where-Object { $_ -match '^(main|window\.opened|first\.frame|startup\.total)\s' } | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }) -join ' | ' } else { 'kein Startprotokoll' }
    Check "$step`: Fenster startet (eigener Datenordner)" ((Test-Path $slog) -and -not $p.HasExited) "$marks | Startprotokoll nach $([math]::Round($wall)) ms (Wanduhr)"
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Say "  eigene Test-App beendet (PID $($p.Id))" }
    Start-Sleep -Seconds 1
}

Say "Setup-Rundlauf PORTABEL $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Windows $([Environment]::OSVersion.VersionString)"
Say "Arbeitsordner: $Work - Paketkennung: $packId - Versionen $AltVersion -> $NeuVersion"
$vpk = (Get-Command vpk -ErrorAction SilentlyContinue).Source
if (-not $vpk) { Say 'BLOCKIERT: vpk fehlt (dotnet tool install -g vpk).'; exit 3 }
$before = Snapshot

# 1 Zwei Testpakete bauen (gleiche App, nur Versionsnummer verschieden), wie installer/build-setup.sh, aber eigene Kennung und ohne Verknuepfungen
foreach ($v in @($AltVersion, $NeuVersion)) {
    $pub = Join-Path $Work "publish-$v"; $out = Join-Path $Work "pakete-$v"
    Say "Baue App $v ..."
    & dotnet publish (Join-Path $repo 'src\Trendo.App\Trendo.App.csproj') -c Release -r win-x64 --self-contained true -p:Version=$v -o $pub --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { Say "FAIL dotnet publish $v (Exit $LASTEXITCODE)"; exit 1 }
    & $vpk '[win]' pack -u $packId -v $v -p $pub -e TrendoOS.exe -r win-x64 -i (Join-Path $repo 'src\Trendo.App\Assets\logo\trendo.ico') --packTitle 'Trendo OS Rundlauf' --packAuthors 'Trendo OS Projekt' --shortcuts None --delta None -o $out --yes 2>&1 | ForEach-Object { Add-Content -Path $Log -Value (Clean "$_") -Encoding utf8 }
    if ($LASTEXITCODE -ne 0) { Say "FAIL vpk pack $v (Exit $LASTEXITCODE)"; exit 1 }
    Remove-Item $pub -Recurse -Force
}
$zipOld = Join-Path (Join-Path $Work "pakete-$AltVersion") "$packId-win-Portable.zip"
$nupkgNew = Join-Path (Join-Path $Work "pakete-$NeuVersion") "$packId-$NeuVersion-full.nupkg"
Check '1 Pakete gebaut: Portable-ZIP alt und Update-Paket neu' ((Test-Path $zipOld) -and (Test-Path $nupkgNew)) "$([math]::Round((Get-Item $zipOld).Length / 1MB)) MB / $([math]::Round((Get-Item $nupkgNew).Length / 1MB)) MB"
Say "  SHA256 $((Get-FileHash $zipOld -Algorithm SHA256).Hash.ToLower())  $(Split-Path $zipOld -Leaf)"
Say "  SHA256 $((Get-FileHash $nupkgNew -Algorithm SHA256).Hash.ToLower())  $(Split-Path $nupkgNew -Leaf)"

# 2 Installieren = portables Paket in den eigenen Ordner auspacken
$sw = [Diagnostics.Stopwatch]::StartNew()
Expand-Archive -Path $zipOld -DestinationPath $inst -Force
$files = (Get-ChildItem $inst -Recurse -File | Measure-Object -Sum Length)
Check "2 Installieren: Version $(Version)" ((Version) -eq $AltVersion) "$($files.Count) Dateien, $([math]::Round($files.Sum / 1MB)) MB in $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s; Update.exe vorhanden: $(Test-Path (Join-Path $inst 'Update.exe'))"

# 3 Starten: Selbsttest und Fenster (kalt = erster Start nach dem Auspacken, warm = zweiter Start)
SelfTest '3-alt'
GuiStart '3-alt-kalt'
GuiStart '3-alt-warm'
# eigene Nutzerdaten anlegen, die das Update ueberleben muessen
$marker = Join-Path $daten 'Dokumente\Rundlauf-Notiz.txt'
New-Item -ItemType Directory -Force (Split-Path $marker) | Out-Null
Set-Content -Path $marker -Value "Notiz vor dem Update $(Get-Date -Format o)" -Encoding utf8
$datenVorher = Hashes $daten
Say "  Datenordner vor dem Update: $($datenVorher.Count) Dateien (inkl. eigener Notiz)"

# 4 Update auf die neuere Version (lokales Paket, wie der eingebaute Updater), ohne Neustart
$upd = Join-Path $inst 'Update.exe'
$help = (& $upd --help 2>&1 | Out-String)
Add-Content -Path $Log -Value ("  Update.exe --help:`n" + (Clean $help)) -Encoding utf8
$sw = [Diagnostics.Stopwatch]::StartNew()
$args4 = @('apply', '--norestart', '--package', "`"$nupkgNew`"")
Say ("$ Update.exe " + ($args4 -join ' '))
$p = Start-Process $upd -ArgumentList $args4 -Wait -PassThru -NoNewWindow
Say "  Exit $($p.ExitCode) nach $([math]::Round($sw.Elapsed.TotalSeconds, 1)) s"
Start-Sleep -Seconds 3
$stray = @(Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" })
foreach ($s in $stray) { Say "  WARNUNG: Update hat die App gestartet (PID $($s.Id)) - eigener Prozess beendet"; Stop-Process -Id $s.Id -Force }
Check "4 Update: Version $(Version)" ((Version) -eq $NeuVersion) "Exit $($p.ExitCode)"
Check '4 Update: App nicht selbst neu gestartet (--norestart)' ($stray.Count -eq 0)
$datenNach = Hashes $daten
$lost = @($datenVorher.Keys | Where-Object { -not $datenNach.ContainsKey($_) })
$changed = @($datenVorher.Keys | Where-Object { $datenNach.ContainsKey($_) -and $datenNach[$_] -ne $datenVorher[$_] })
Check '4 Update: Nutzerdaten erhalten' (($lost.Count -eq 0) -and (Test-Path $marker) -and $datenNach[$marker.Substring($daten.Length)] -eq $datenVorher[$marker.Substring($daten.Length)]) "fehlend $($lost.Count), veraendert $($changed.Count): $(($changed | Select-Object -First 8) -join ', ')"
SelfTest '4-neu'
GuiStart '4-neu-kalt'
GuiStart '4-neu-warm'
Check '4 Update: Notiz nach Start der neuen Version unveraendert' ((Get-FileHash $marker -Algorithm SHA256).Hash -eq $datenVorher[$marker.Substring($daten.Length)])

# 5 Deinstallieren = portablen Ordner entfernen (das portable Paket hat keinen Deinstallations-Eintrag); Datenordner bleibt wie beim echten Setup (Standard: behalten)
$running = @(Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" })
Check '5 Deinstallieren: keine Test-App laeuft mehr' ($running.Count -eq 0)
Remove-Item $inst -Recurse -Force
Check '5 Deinstallieren: Programmordner entfernt' (-not (Test-Path $inst))
Say "  Erlaubter Rest: eigener Datenordner $($datenNach.Count) Dateien (wird am Ende mit dem Arbeitsordner geloescht)"

# 6 Echtes System unberuehrt?
$after = Snapshot
foreach ($k in $before.Keys) {
    $a = @($before[$k]); $b = @($after[$k])
    $diff = @(Compare-Object $a $b -ErrorAction SilentlyContinue)
    Check "6 Echtes System unveraendert: $k" ($diff.Count -eq 0) "vorher $($a.Count), nachher $($b.Count) Eintraege$(if ($diff.Count) { ': ' + (($diff | Select-Object -First 6 | ForEach-Object { $_.SideIndicator + $_.InputObject }) -join ', ') })"
}
Say $(if ($script:ok) { 'ERGEBNIS: ALLE BESTANDEN' } else { 'ERGEBNIS: FEHLER - siehe FAIL-Zeilen' })
foreach ($f in Get-ChildItem $Work -File -Filter '*-start.txt') { Copy-Item $f.FullName (Join-Path $Beleg $f.Name) }
foreach ($f in Get-ChildItem $Work -File -Filter '*-selftest.txt') { Set-Content (Join-Path $Beleg $f.Name) (Clean (Get-Content $f.FullName -Raw)) -Encoding utf8 }
Get-ChildItem $Beleg -File | Where-Object Name -ne 'SHA256SUMS' | ForEach-Object { "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" } | Set-Content (Join-Path $Beleg 'SHA256SUMS') -Encoding ascii
if (-not $Behalten) { Remove-Item $Work -Recurse -Force -ErrorAction SilentlyContinue }
if ($script:ok) { exit 0 } else { exit 1 }
