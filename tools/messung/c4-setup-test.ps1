# C4 / T-0008: Installation, Update, Rückfall und Deinstallation des Windows-Setups (Velopack, nur Benutzer, kein Admin).
# Sichert vorher die Benutzerdaten (%APPDATA%\TrendoOS) und die vorhandene Installation und stellt die Daten am Ende byte-gleich wieder her.
# Beendet nur Trendo-Prozesse aus dem Installationsordner, die dieses Skript selbst gestartet hat (Setup startet die App nach dem Installieren).
# Aufruf: powershell -ExecutionPolicy Bypass -File tools/messung/c4-setup-test.ps1 -OldSetup <Setup alt.exe> -NewSetup <Setup neu.exe> -NewPackage <neu-full.nupkg> -Work <leerer Temp-Ordner> -Log <Logdatei>
param([string]$OldSetup, [string]$NewSetup, [string]$NewPackage, [string]$Work, [string]$Log)
$ErrorActionPreference = 'Stop'
$inst = Join-Path $env:LOCALAPPDATA 'TrendoOS'
$data = Join-Path $env:APPDATA 'TrendoOS'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TrendoOS'
$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Trendo OS.lnk'
New-Item -ItemType Directory -Force $Work | Out-Null
$script:ok = $true
function Say([string]$m) { $line = ($m -replace [regex]::Escape($env:USERPROFILE), 'C:\Users\<user>'); Add-Content -Path $Log -Value $line -Encoding utf8; Write-Output $line }
function Check([string]$name, [bool]$cond, [string]$detail = '') { if (-not $cond) { $script:ok = $false }; Say ("{0}  {1}{2}" -f ($(if ($cond) { 'PASS' } else { 'FAIL' })), $name, $(if ($detail) { " – $detail" } else { '' })) }
function Version() { $exe = Join-Path $inst 'current\TrendoOS.exe'; if (Test-Path $exe) { (Get-Item $exe).VersionInfo.ProductVersion -replace '\+.*$', '' } else { '' } }
function RegVersion() { if (Test-Path $key) { (Get-ItemProperty $key).DisplayVersion } else { '' } }
function StopMine([datetime]$since) {
    Start-Sleep -Seconds 4
    Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" -and $_.StartTime -ge $since } | ForEach-Object { Say "  gestartete App beendet (PID $($_.Id))"; Stop-Process -Id $_.Id -Force }
}
function Hashes([string]$root) {
    if (-not (Test-Path $root)) { return @{} }
    $h = @{}; Get-ChildItem $root -Recurse -File | ForEach-Object { $h[$_.FullName.Substring($root.Length)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }; return $h
}
function Same($a, $b) { if ($a.Count -ne $b.Count) { return $false }; foreach ($k in $a.Keys) { if ($b[$k] -ne $a[$k]) { return $false } }; return $true }
function OpensWithRealData([string]$step) {
    $exe = Join-Path $inst 'current\TrendoOS.exe'; $since = Get-Date
    $logf = Join-Path $Work "$step-start.txt"
    $p = Start-Process $exe -ArgumentList @('--no-first-run', '--startup-log', "`"$logf`"") -PassThru
    $sw = [Diagnostics.Stopwatch]::StartNew(); while (-not (Test-Path $logf) -and $sw.Elapsed.TotalSeconds -lt 60) { Start-Sleep -Milliseconds 250 }
    Start-Sleep -Seconds 2; $p.Refresh()
    $first = if (Test-Path $logf) { (Get-Content $logf | Where-Object { $_ -match '^(window.opened|first.frame) ' }) -join ' | ' } else { 'kein Startprotokoll' }
    Check "$step`: App startet mit den echten Daten" ((Test-Path $logf) -and -not $p.HasExited) $first
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; StopMine $since
}

Say "C4 Setup-Test $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') · Windows $([Environment]::OSVersion.VersionString)"
Say "Alt: $(Split-Path $OldSetup -Leaf) · Neu: $(Split-Path $NewSetup -Leaf) · Paket: $(Split-Path $NewPackage -Leaf)"
if (Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" }) { Say 'ABBRUCH: Trendo OS läuft gerade. Bitte schließen.'; exit 2 }

# 0 Sicherung
$bData = Join-Path $Work 'sicherung-daten'; $bInst = Join-Path $Work 'sicherung-installation'
if (Test-Path $data) { Copy-Item $data $bData -Recurse -Force }
if (Test-Path $inst) { Copy-Item $inst $bInst -Recurse -Force }
$before = Hashes $data
Say "Ausgangslage: installiert $(Version) (Registry $(RegVersion)), Daten $($before.Count) Dateien gesichert"

# 1 Deinstallieren der vorhandenen Version
if (Test-Path (Join-Path $inst 'Update.exe')) {
    & (Join-Path $inst 'Update.exe') uninstall --silent | Out-Null; Start-Sleep -Seconds 3
    Check '1 Deinstallieren: Programmordner entfernt' (-not (Test-Path (Join-Path $inst 'current')))
    Check '1 Deinstallieren: Registry-Eintrag entfernt' (-not (Test-Path $key))
    Check '1 Deinstallieren: Startmenü-Verknüpfung entfernt' (-not (Test-Path $lnk))
    Check '1 Deinstallieren: Benutzerdaten bleiben erhalten' (Same $before (Hashes $data)) "$((Hashes $data).Count) Dateien"
}

# 2 Installieren der alten Version
$since = Get-Date; & $OldSetup --silent | Out-Null; StopMine $since
$v = Version; Check "2 Installieren alt: Version $v" ($v -ne '') "Registry $(RegVersion)"
Check '2 Installieren alt: Startmenü-Verknüpfung da' (Test-Path $lnk)
OpensWithRealData '2-alt'

# 3 Update auf die neue Version (lokales Paket, wie der eingebaute Updater)
$since = Get-Date; & (Join-Path $inst 'Update.exe') apply --silent --norestart -p $NewPackage | Out-Null; Start-Sleep -Seconds 3; StopMine $since
$v3 = Version; Check "3 Update: Version $v3" ($v3 -ne '' -and $v3 -ne $v) "Registry $(RegVersion)"
Check '3 Update: Benutzerdaten noch da' ((Hashes $data).Count -ge $before.Count)
OpensWithRealData '3-update'

# 4 Rückfall: ältere Version über die neuere installieren (Weg aus RUECKFALL.md)
$since = Get-Date; & $OldSetup --silent | Out-Null; StopMine $since
$v4 = Version; Check "4 Rückfall: Version $v4" ($v4 -eq $v) "Registry $(RegVersion)"
OpensWithRealData '4-rueckfall'

# 5 Deinstallieren
& (Join-Path $inst 'Update.exe') uninstall --silent | Out-Null; Start-Sleep -Seconds 3
Check '5 Deinstallieren: Programmordner entfernt' (-not (Test-Path (Join-Path $inst 'current')))
Check '5 Deinstallieren: Registry-Eintrag entfernt' (-not (Test-Path $key))
Check '5 Deinstallieren: Startmenü-Verknüpfung entfernt' (-not (Test-Path $lnk))
Check '5 Deinstallieren: Benutzerdaten bleiben erhalten' (Test-Path $data)

# 6 Ausgangszustand: neue Version installieren, Daten byte-gleich zurück
$since = Get-Date; & $NewSetup --silent | Out-Null; StopMine $since
if (Test-Path $data) { Remove-Item $data -Recurse -Force }
if (Test-Path $bData) { Copy-Item $bData $data -Recurse -Force }
Check "6 Ausgangszustand: Version $(Version) installiert" ((Version) -ne '') "Registry $(RegVersion)"
Check '6 Ausgangszustand: Benutzerdaten byte-gleich wie vorher' (Same $before (Hashes $data)) "$($before.Count) Dateien"
Say $(if ($script:ok) { 'ERGEBNIS: ALLE BESTANDEN' } else { 'ERGEBNIS: FEHLER – siehe FAIL-Zeilen' })
