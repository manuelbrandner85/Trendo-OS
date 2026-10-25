# T-0220: echte Deinstallation mit der Datenfrage (Velopack-Hook). Sichert Daten vorher und stellt sie am Ende byte-gleich wieder her.
# Fall A: keine Antwort -> Daten bleiben. Fall B: Ja (per BM_CLICK auf den Ja-Knopf) -> Daten weg, Projekt-Unterordner bleiben.
# Aufruf: powershell -ExecutionPolicy Bypass -File tools/messung/deinstallation-datenfrage.ps1 -Setup <Setup.exe> -Work <leerer Temp-Ordner> -Log <Logdatei>
param([string]$Setup, [string]$Work, [string]$Log)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr h, int id);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
}
"@
$inst = Join-Path $env:LOCALAPPDATA 'TrendoOS'; $data = Join-Path $env:APPDATA 'TrendoOS'
$hookLog = Join-Path $env:TEMP 'trendo-deinstallation.log'
New-Item -ItemType Directory -Force $Work | Out-Null
$script:ok = $true
function Say([string]$m) { $line = ($m -replace [regex]::Escape($env:USERPROFILE), 'C:\Users\<user>'); Add-Content -Path $Log -Value $line -Encoding utf8; Write-Output $line }
function Check([string]$n, [bool]$c, [string]$d = '') { if (-not $c) { $script:ok = $false }; Say ("{0}  {1}{2}" -f ($(if ($c) { 'PASS' } else { 'FAIL' })), $n, $(if ($d) { " - $d" } else { '' })) }
function Version() { $e = Join-Path $inst 'current\TrendoOS.exe'; if (Test-Path $e) { (Get-Item $e).VersionInfo.ProductVersion -replace '\+.*$', '' } else { '' } }
function Hashes([string]$root) { $h = @{}; if (Test-Path $root) { Get-ChildItem $root -Recurse -File | ForEach-Object { $h[$_.FullName.Substring($root.Length)] = (Get-FileHash $_.FullName).Hash } }; return $h }
function Same($a, $b) { if ($a.Count -ne $b.Count) { return $false }; foreach ($k in $a.Keys) { if ($b[$k] -ne $a[$k]) { return $false } }; return $true }
function StopMine([datetime]$since) { Start-Sleep -Seconds 4; Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" -and $_.StartTime -ge $since } | ForEach-Object { Stop-Process -Id $_.Id -Force } }
function Install([string]$step) { $since = Get-Date; & $Setup --silent | Out-Null; StopMine $since; Check "$step`: Version $(Version) installiert" ((Version) -ne '') }
function Uninstall([string]$step, [bool]$clickYes) {
    if (Test-Path $hookLog) { Remove-Item $hookLog }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process (Join-Path $inst 'Update.exe') -ArgumentList 'uninstall' -PassThru
    $seen = $false
    while (-not $p.HasExited -and $sw.Elapsed.TotalSeconds -lt 60) {
        # Wichtig: [NullString]::Value – PowerShell macht aus $null sonst einen leeren Klassennamen
        $h = [W]::FindWindow([NullString]::Value, 'Trendo OS deinstallieren')
        if ($h -ne [IntPtr]::Zero -and -not $seen) { $seen = $true; Say "  $step`: Frage erschienen nach $([math]::Round($sw.Elapsed.TotalSeconds,1)) s"
            if ($clickYes) { Start-Sleep -Milliseconds 800; $b = [W]::GetDlgItem($h, 6); [W]::PostMessage($b, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Say "  $step`: Ja geklickt" } }
        # Velopack zeigt nach der Deinstallation ein eigenes Abschlussfenster: bestätigen
        $v = [W]::FindWindow([NullString]::Value, 'Trendo OS Deinstallation')
        # Aufgabendialog (Deinstallation abgeschlossen): OK per TDM_CLICK_BUTTON (WM_USER+102, IDOK)
        if ($v -ne [IntPtr]::Zero) { Say "  $step`: Velopack meldet Deinstallation abgeschlossen nach $([math]::Round($sw.Elapsed.TotalSeconds,1)) s – OK"; [W]::SendMessage($v, 0x0466, [IntPtr]1, [IntPtr]::Zero) | Out-Null; Start-Sleep -Seconds 1 }
        Start-Sleep -Milliseconds 300
    }
    Start-Sleep -Seconds 3
    Check "$step`: Frage erschienen" $seen
    Check "$step`: Deinstallation beendet nach $([math]::Round($sw.Elapsed.TotalSeconds,1)) s" $p.HasExited
    Check "$step`: Programmordner entfernt" (-not (Test-Path (Join-Path $inst 'current')))
    if (Test-Path $hookLog) { Get-Content $hookLog -Encoding UTF8 | ForEach-Object { Say "  Hook: $($_.Substring(20))" } }
}

Say "Deinstallation mit Datenfrage $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') · Setup $(Split-Path $Setup -Leaf)"
if (Get-Process TrendoOS -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$inst\*" }) { Say 'ABBRUCH: Trendo OS läuft gerade.'; exit 2 }
$bData = Join-Path $Work 'sicherung-daten'; Copy-Item $data $bData -Recurse -Force
$before = Hashes $data
Say "Ausgangslage: installiert $(Version), Daten $($before.Count) Dateien gesichert"

Install '1 Update auf neue Version'
# Testprojekt im Datenordner: muss den Fall B überleben
$proj = Join-Path $data 'projects\deinstall-test'; New-Item -ItemType Directory -Force $proj | Out-Null; Set-Content (Join-Path $proj 'notiz.txt') 'bleibt' -Encoding utf8

Uninstall '2 Deinstallieren ohne Antwort' $false
Check '2: Benutzerdaten bleiben unverändert' ((Hashes $data).Count -eq $before.Count + 1)

Install '3 Neu installieren'
Uninstall '4 Deinstallieren mit Ja' $true
$left = if (Test-Path $data) { Get-ChildItem $data -Force | ForEach-Object Name } else { @() }
Check '4: Einstellungen, Protokolle, Sicherungen gelöscht' (-not (Test-Path (Join-Path $data 'settings.json')) -and -not (Test-Path (Join-Path $data 'audit'))) ("übrig: " + ($left -join ', '))
Check '4: Projekt-Unterordner bleibt' (Test-Path (Join-Path $proj 'notiz.txt'))

# Ausgangszustand: Daten byte-gleich zurück, neue Version installiert
if (Test-Path $data) { Remove-Item $data -Recurse -Force }
Copy-Item $bData $data -Recurse -Force
Install '5 Endzustand'
Check '5: Benutzerdaten byte-gleich wie vorher' (Same $before (Hashes $data)) "$($before.Count) Dateien"
Say $(if ($script:ok) { 'ERGEBNIS: ALLE BESTANDEN' } else { 'ERGEBNIS: FEHLER – siehe FAIL-Zeilen' })
