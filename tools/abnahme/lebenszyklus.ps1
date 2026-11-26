# T-0770: Abnahme Installieren, Update, Deinstallieren, Leistung - wiederholbar und abgeschottet.
# Baut zwei Abnahme-Pakete mit EIGENER Paket-Id (Standard TrendoOSAbnahme), installiert still in einen eigenen Ordner,
# aktualisiert A->B ueber den echten Update-Weg der App (lokale Quelle per TRENDO_UPDATE_QUELLE), deinstalliert ueber Update.exe
# und misst Start, Speicher und grosse Dateien. Eine echte Trendo-Installation (Paket-Id TrendoOS) wird nie angefasst:
#  - eigene Paket-Id und eigener Titel (Verknuepfung "Trendo OS Abnahme"), eigener Installationsordner unter -Work
#  - TRENDO_INSTANZ legt Daten, Werkzeuge und Altordner unter -Work (auch in den Velopack-Hooks), TEMP/TMP ebenfalls
#  - Prozesse werden nur per PID beendet, und nur wenn ihre Datei unter -Work liegt
#  - vorher/nachher wird der Stand der echten Ordner (nur lesend) verglichen
# Aufruf (PowerShell 5.1 oder 7):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/abnahme/lebenszyklus.ps1 -Repo <Repo> -Work <leerer eigener Ordner> [-Phase Alles]
# Phasen: Bauen, Installieren, Update, Leistung, Gross, Deinstallieren, Alles (= alle in dieser Reihenfolge)
param(
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][string]$Work,
    [string]$Phase = 'Alles',
    [string]$VersionA = '0.9.110-abnahme1',
    [string]$VersionB = '0.9.110-abnahme2',
    [string]$PackId = 'TrendoOSAbnahme',
    [string]$Title = 'Trendo OS Abnahme',
    [int]$Runs = 3
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path $Repo).Path
if ($PackId -ieq 'TrendoOS') { throw 'Abbruch: Die Paket-Id der echten Installation (TrendoOS) ist verboten.' }
$realLocal = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TrendoOS'
$realData = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'TrendoOS'
$realTools = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TrendoWerkzeuge'
# Schutz (Korrektur M6): -Work darf weder ein echter Ordner sein noch darin noch darueber liegen (sonst wuerde z. B. -Work %LOCALAPPDATA%
# die echte Installation einschliessen). Geprueft wird VOR dem Anlegen und vor jedem anderen Schritt.
function Test-WorkSafe([string]$w) {
    $full = [IO.Path]::GetFullPath($w).TrimEnd('\', '/') + '\'
    foreach ($r in @($realLocal, $realData, $realTools)) {
        $rr = [IO.Path]::GetFullPath($r).TrimEnd('\', '/') + '\'
        if ($full.StartsWith($rr, [StringComparison]::OrdinalIgnoreCase) -or $rr.StartsWith($full, [StringComparison]::OrdinalIgnoreCase)) { return $r }
    }
    return $null
}
$bad = Test-WorkSafe $Work
if ($bad) { Write-Output "Abbruch: -Work ist ein echter Ordner, liegt darin oder darueber ($bad). Nichts wurde gestartet oder beendet."; exit 90 }
New-Item -ItemType Directory -Force $Work | Out-Null
$Work = (Resolve-Path $Work).Path

$Feed = Join-Path $Work 'feed'; $Pakete = Join-Path $Work 'pakete'; $Inst = Join-Path $Work 'installiert'
$Instanz = Join-Path $Work 'instanz'; $Tmp = Join-Path $Work 'tmp'; $LogDir = Join-Path $Work 'log'; $Gross = Join-Path $Work 'gross'
$Data = Join-Path $Instanz 'TrendoOS'
foreach ($d in @($Feed, $Pakete, $Instanz, $Tmp, $LogDir)) { New-Item -ItemType Directory -Force $d | Out-Null }

# Abschottung fuer alle von hier gestarteten Prozesse (Setup, Update.exe, Hooks, App)
$env:TEMP = $Tmp; $env:TMP = $Tmp
$env:TRENDO_INSTANZ = $Instanz
$env:TRENDO_UPDATE_QUELLE = $Feed
$env:TRENDO_TEMP_AUFRAEUMEN = '0'

$script:fails = 0
$LogFile = Join-Path $LogDir 'abnahme.log'
$userProfile = [Environment]::GetFolderPath('UserProfile')
function Anon([string]$s) { if ($null -eq $s) { return '' }; $s = $s -replace [regex]::Escape($userProfile), 'C:\Users\<user>'; return ($s -replace [regex]::Escape($env:COMPUTERNAME), '<pc>') }
function Say([string]$m) {
    $line = "{0} {1}" -f (Get-Date -Format 'HH:mm:ss'), (Anon $m)
    for ($t = 0; $t -lt 20; $t++) { try { [IO.File]::AppendAllText($LogFile, $line + [Environment]::NewLine, [Text.Encoding]::UTF8); break } catch [IO.IOException] { Start-Sleep -Milliseconds 100 } }
    Write-Output $line
}
function Check([string]$name, [bool]$ok, [string]$detail = '') { if (-not $ok) { $script:fails++ }; Say ("{0}  {1}{2}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $(if ($detail) { " - $detail" } else { '' })) }
function Sha([string]$f) { (Get-FileHash -Algorithm SHA256 $f).Hash.ToLowerInvariant() }

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class AbnWin {
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
}
"@
Add-Type -AssemblyName System.Drawing

# --- echte Installation: nur lesend festhalten ---
function RealState([string]$name = '') {
    $o = [ordered]@{}
    # Korrektur N1: rekursiv jede Datei und jeder Ordner mit relativem Pfad, Groesse und Zeitstempel; daraus eine SHA-256 der Liste
    foreach ($p in @($realLocal, $realData, $realTools)) {
        if (Test-Path $p) {
            $lines = New-Object System.Collections.Generic.List[string]
            $bytes = [long]0
            foreach ($i in @(Get-ChildItem -Force -Recurse $p -ErrorAction SilentlyContinue)) {
                $rel = $i.FullName.Substring($p.Length)
                if ($i.PSIsContainer) { $lines.Add(("D {0} {1:o}" -f $rel, $i.LastWriteTimeUtc)) } else { $bytes += $i.Length; $lines.Add(("F {0} {1} {2:o}" -f $rel, $i.Length, $i.LastWriteTimeUtc)) }
            }
            $lines.Sort([StringComparer]::OrdinalIgnoreCase)
            if ($name) { [IO.File]::WriteAllLines((Join-Path $LogDir ("echte-liste-{0}-{1}-{2}.txt" -f $name, (Split-Path (Split-Path $p -Parent) -Leaf), (Split-Path $p -Leaf))), $lines) } # relative Pfade, fuer den Vergleich
            $text = [string]::Join("`n", $lines)
            $sha = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '').ToLowerInvariant()
            $o[(Anon $p)] = "{0} Eintraege rekursiv, {1} Bytes, Ordner {2:o}, Liste-SHA256 {3}" -f $lines.Count, $bytes, (Get-Item -Force $p).LastWriteTimeUtc, $sha
        } else { $o[(Anon $p)] = 'fehlt' }
    }
    $desk = [Environment]::GetFolderPath('Desktop'); $sm = Join-Path ([Environment]::GetFolderPath('Programs')) ''
    foreach ($l in @(Get-ChildItem -Force $desk -Filter '*.lnk' -ErrorAction SilentlyContinue) + @(Get-ChildItem -Force $sm -Filter '*.lnk' -ErrorAction SilentlyContinue)) {
        if ($l.Name -like 'Trend*' -and $l.Name -notlike "$Title*") { $o[(Anon $l.FullName)] = "{0:o} {1} Bytes" -f $l.LastWriteTimeUtc, $l.Length }
    }
    $k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TrendoOS'
    $o['HKCU Uninstall\TrendoOS'] = $(if (Test-Path $k) { $v = Get-ItemProperty $k; "DisplayVersion=$($v.DisplayVersion)" } else { 'fehlt' })
    $o['Prozesse der echten Installation'] = (ProcsUnder $realLocal).Count
    return $o
}
function SaveRealState([string]$name) { $s = RealState $name; ($s | ConvertTo-Json) | Set-Content -Encoding UTF8 (Join-Path $LogDir "echte-installation-$name.json"); return $s }

# --- eigene Prozesse ---
function ProcsUnder([string]$dir) { $pat = $dir.TrimEnd('\').Replace('\', '\\') + '\\%'; @(Get-CimInstance Win32_Process -Filter "ExecutablePath LIKE '$pat'" -ErrorAction SilentlyContinue | ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue } | Where-Object { $_ -and $_.Path -and $_.Path.StartsWith($dir, [StringComparison]::OrdinalIgnoreCase) }) }
function MyProcs() { ProcsUnder $Work }
function StopMine([int]$procId) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($p -and $p.Path -and $p.Path.StartsWith($Work, [StringComparison]::OrdinalIgnoreCase)) { Stop-Process -Id $procId -Force; $p.WaitForExit(15000) | Out-Null; return $true }
    return $false
}
function StopAllMine() { foreach ($p in MyProcs) { if (StopMine $p.Id) { Say "  beendet (eigene PID): $($p.Id) $($p.ProcessName)" } } }

function WaitWindow([System.Diagnostics.Process]$p, [int]$timeoutSec = 120) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        if ($p.HasExited) { return $null }
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero -and [AbnWin]::IsWindowVisible($p.MainWindowHandle)) {
            return [pscustomobject]@{ Ms = [math]::Round(((Get-Date) - $p.StartTime).TotalMilliseconds); Title = [AbnWin]::Title($p.MainWindowHandle); Handle = $p.MainWindowHandle }
        }
        Start-Sleep -Milliseconds 50
    }
    return $null
}
function FindAppProcess([datetime]$since, [int]$timeoutSec = 90) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        $p = MyProcs | Where-Object { $_.ProcessName -eq 'TrendoOS' -and $_.StartTime -ge $since } | Sort-Object StartTime -Descending | Select-Object -First 1
        if ($p) { return $p }
        Start-Sleep -Milliseconds 200
    }
    return $null
}
function Shot([IntPtr]$h, [string]$file) {
    $r = New-Object AbnWin+RECT; [AbnWin]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = [math]::Max(1, $r.Right - $r.Left); $hh = [math]::Max(1, $r.Bottom - $r.Top)
    $bmp = New-Object System.Drawing.Bitmap $w, $hh; $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
    $ok = [AbnWin]::PrintWindow($h, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
    # Anteil nicht-schwarzer Pixel als einfacher Render-Beleg
    $lit = 0; $n = 0; for ($y = 0; $y -lt $hh; $y += [math]::Max(1, [int]($hh / 40))) { for ($x = 0; $x -lt $w; $x += [math]::Max(1, [int]($w / 40))) { $c = $bmp.GetPixel($x, $y); $n++; if (($c.R + $c.G + $c.B) -gt 30) { $lit++ } } }
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return [pscustomobject]@{ Ok = $ok; Size = "${w}x${hh}"; Lit = [math]::Round(100.0 * $lit / [math]::Max(1, $n)) }
}
function InstalledVersion() { $f = Join-Path $Inst 'current\sq.version'; if (Test-Path $f) { ([xml](Get-Content $f -Raw)).package.metadata.version } else { '' } }
function UninstallKey() { "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$PackId" }
function OwnShortcuts() { @(Get-ChildItem -Force ([Environment]::GetFolderPath('Desktop')), ([Environment]::GetFolderPath('Programs')) -Filter "$Title*.lnk" -ErrorAction SilentlyContinue) + @(Get-ChildItem -Force ([Environment]::GetFolderPath('Programs')) -Recurse -Filter "$Title*.lnk" -ErrorAction SilentlyContinue) | Sort-Object FullName -Unique }

# ================= Phasen =================
function Phase-Bauen {
    Say "== Bauen: $PackId $VersionA und $VersionB =="
    $ico = Join-Path $Repo 'src\Trendo.App\Assets\logo\trendo.ico'
    foreach ($v in @($VersionA, $VersionB)) {
        $pub = Join-Path $Pakete "publish-$v"
        if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
        $sw = [Diagnostics.Stopwatch]::StartNew()
        & dotnet publish (Join-Path $Repo 'src\Trendo.App\Trendo.App.csproj') -c Release -r win-x64 --self-contained true --disable-build-servers "-p:Version=$v" -o $pub *> (Join-Path $LogDir "publish-$v.log")
        Check "Release-Build $v veroeffentlicht" ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $pub 'TrendoOS.exe'))) "$([math]::Round($sw.Elapsed.TotalSeconds)) s"
        & vpk pack -u $PackId -v $v -p $pub -e TrendoOS.exe -i $ico --packTitle $Title --packAuthors 'Trendo OS Projekt' -o $Feed --yes *> (Join-Path $LogDir "vpk-$v.log")
        $setup = Join-Path $Feed "$PackId-win-Setup.exe"
        Check "Velopack-Paket $v gepackt" ($LASTEXITCODE -eq 0 -and (Test-Path $setup) -and (Test-Path (Join-Path $Feed "$PackId-$v-full.nupkg")))
        Copy-Item $setup (Join-Path $Pakete "Setup-$v.exe") -Force
        Remove-Item -Recurse -Force $pub
    }
    $sums = Get-ChildItem $Feed, $Pakete -File | Where-Object { $_.Extension -in '.exe', '.nupkg', '.json', '.zip' -or $_.Name -eq 'RELEASES' } | ForEach-Object { "{0}  {1}  {2}" -f (Sha $_.FullName), $_.Length, $_.Name }
    $sums | Set-Content -Encoding UTF8 (Join-Path $LogDir 'pakete-sha256.txt'); $sums | ForEach-Object { Say "  $_" }
}

function Phase-Installieren {
    Say "== Installieren: Setup $VersionA still nach eigenem Ordner =="
    if (Test-Path $Inst) { throw "Abbruch: $Inst existiert schon (alte Abnahme zuerst deinstallieren)." }
    $setup = Join-Path $Pakete "Setup-$VersionA.exe"
    $since = Get-Date
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $setup -ArgumentList @('--silent', '--installto', "`"$Inst`"", '--log', "`"$(Join-Path $LogDir 'setup-a.log')`"") -PassThru -Wait
    Check "Setup still beendet (Exitcode $($p.ExitCode))" ($p.ExitCode -eq 0) "$([math]::Round($sw.Elapsed.TotalSeconds,1)) s"
    Check "Installationsordner liegt unter dem eigenen Ordner" (Test-Path (Join-Path $Inst 'Update.exe')) (Anon $Inst)
    Check "Programm vorhanden (current\TrendoOS.exe)" (Test-Path (Join-Path $Inst 'current\TrendoOS.exe'))
    Check "installierte Version ist $VersionA" ((InstalledVersion) -eq $VersionA) (InstalledVersion)
    $files = @(Get-ChildItem -Recurse -File $Inst); Say ("  {0} Dateien, {1:0} MB im Installationsordner" -f $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB))
    Get-ChildItem -Force $Inst | ForEach-Object { Say "  Inhalt: $($_.Name)" }
    $sc = OwnShortcuts; foreach ($s in $sc) { Say "  Verknuepfung: $($s.FullName)" }
    Check "eigene Verknuepfungen angelegt" ($sc.Count -ge 1) "$($sc.Count)"
    $k = UninstallKey; Check "HKCU-Uninstall-Eintrag der eigenen Id" (Test-Path $k) $(if (Test-Path $k) { $v = Get-ItemProperty $k; "DisplayName=$($v.DisplayName) DisplayVersion=$($v.DisplayVersion) InstallLocation=$(Anon $v.InstallLocation)" } else { '' })
    # Erststart durch Setup (Velopack startet die App nach der Installation)
    $first = FindAppProcess $since 30
    if ($first) {
        $win = WaitWindow $first 120
        Check "Erststart nach Setup: Hauptfenster erscheint" ($null -ne $win) $(if ($win) { "PID $($first.Id), $($win.Ms) ms, Titel '$($win.Title)'" } else { '' })
        if ($win) { Start-Sleep -Seconds 4; $s = Shot $win.Handle (Join-Path $LogDir 'fenster-erststart-a.png'); Say "  Bild vom Fenster: $($s.Size), $($s.Lit) % nicht schwarz" }
        Check "Erststart sauber per PID beendet" (StopMine $first.Id)
    } else { Say '  Setup hat die App nicht selbst gestartet.' }
    # eigener Start
    $since = Get-Date
    $p = Start-Process (Join-Path $Inst 'current\TrendoOS.exe') -ArgumentList @('--no-first-run', '--offline') -PassThru
    $win = WaitWindow $p 120
    Check "Start A: Prozess laeuft und Hauptfenster erscheint" ($null -ne $win) $(if ($win) { "PID $($p.Id), $($win.Ms) ms, Titel '$($win.Title)'" } else { '' })
    if ($win) { Start-Sleep -Seconds 5; $s = Shot $win.Handle (Join-Path $LogDir 'fenster-a.png'); Check "Render-Beleg A (Fensterbild nicht leer)" ($s.Lit -ge 20) "$($s.Size), $($s.Lit) % nicht schwarz" }
    Check "Start A sauber per PID beendet" (StopMine $p.Id)
    Check "Daten liegen in der Pruef-Instanz" (Test-Path (Join-Path $Data 'settings.json')) (Anon $Data)
}

function Phase-Update {
    Say "== Update $VersionA -> $VersionB ueber den Update-Weg der App (Quelle: lokaler Ordner) =="
    Check "Ausgangslage: $VersionA installiert" ((InstalledVersion) -eq $VersionA) (InstalledVersion)
    $test = Join-Path $Data 'abnahme-testdatei.txt'; $proj = Join-Path $Data 'projects\abnahme\notiz.txt'
    New-Item -ItemType Directory -Force (Split-Path $proj) | Out-Null
    Set-Content $test "Abnahme T-0770 $(Get-Date -Format o)" -Encoding UTF8; Set-Content $proj 'Projekt bleibt' -Encoding UTF8
    $h1 = Sha $test; $h2 = Sha $proj; $settingsHash = Sha (Join-Path $Data 'settings.json')
    Say "  Testdateien angelegt: $h1 / $h2"
    $since = Get-Date
    # Start ohne --offline: die App sucht beim Start selbst, laedt B und installiert nach 30 s Countdown (Einstellung "sofort")
    $p = Start-Process (Join-Path $Inst 'current\TrendoOS.exe') -ArgumentList @('--no-first-run') -PassThru
    $win = WaitWindow $p 120
    Check "A gestartet (PID $($p.Id))" ($null -ne $win) $(if ($win) { "Titel '$($win.Title)'" } else { '' })
    $sw = [Diagnostics.Stopwatch]::StartNew(); $newP = $null
    # Neustart durch Update.exe abwarten: Hook-Prozesse (--veloapp-...) zaehlen nicht, gesucht ist der Start mit --nach-update
    while ($sw.Elapsed.TotalSeconds -lt 240) {
        if ((InstalledVersion) -eq $VersionB) {
            foreach ($c in @(MyProcs | Where-Object { $_.ProcessName -eq 'TrendoOS' -and $_.Id -ne $p.Id -and $_.StartTime -ge $since })) {
                $cl = (Get-CimInstance Win32_Process -Filter "ProcessId=$($c.Id)" -ErrorAction SilentlyContinue).CommandLine
                if ($cl -and $cl -notmatch '--veloapp-') { $newP = $c; break }
            }
            if ($newP) { break }
        }
        Start-Sleep -Milliseconds 300
    }
    $u = MyProcs | Where-Object { $_.ProcessName -eq 'Update' }; foreach ($x in $u) { $x.WaitForExit(60000) | Out-Null }
    $bk = Join-Path $Data 'updates\vorherige-version'
    $bkFile = @(Get-ChildItem $bk -Filter "*-$VersionA-full.nupkg" -ErrorAction SilentlyContinue)
    Check "Sicherung der alten Version fuer die Rueckkehr (U5) vorhanden" ($bkFile.Count -eq 1) $(if ($bkFile.Count) { "$($bkFile[0].Name), $($bkFile[0].Length) Bytes, SHA-256 $(Sha $bkFile[0].FullName)" } else { 'fehlt' })
    Check "A hat sich beendet (Update angewendet)" ($p.HasExited) $(if ($p.HasExited) { "nach $([math]::Round(($p.ExitTime - $p.StartTime).TotalSeconds)) s" } else { '' })
    Check "installierte Version ist jetzt $VersionB" ((InstalledVersion) -eq $VersionB) (InstalledVersion)
    if ($newP) {
        $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId=$($newP.Id)").CommandLine
        Say "  neu gestartet: PID $($newP.Id), Befehl: $(Anon $cmd)"
        $win = WaitWindow $newP 120
        Check "B laeuft nach dem Update mit Hauptfenster" ($null -ne $win) $(if ($win) { "PID $($newP.Id), Titel '$($win.Title)'" } else { '' })
        if ($win) { Start-Sleep -Seconds 5; $s = Shot $win.Handle (Join-Path $LogDir 'fenster-b-nach-update.png'); Say "  Bild vom Fenster: $($s.Size), $($s.Lit) % nicht schwarz" }
        Check "B sauber per PID beendet" (StopMine $newP.Id)
    } else { Check "B wurde nach dem Update neu gestartet" $false }
    Check "Testdatei im Datenordner unveraendert" ((Test-Path $test) -and (Sha $test) -eq $h1)
    Check "Projektdatei im Datenordner unveraendert" ((Test-Path $proj) -and (Sha $proj) -eq $h2)
    Check "Einstellungen vorhanden" (Test-Path (Join-Path $Data 'settings.json')) $(if ((Sha (Join-Path $Data 'settings.json')) -eq $settingsHash) { 'unveraendert' } else { 'von der App fortgeschrieben' })
    $audit = Get-ChildItem -Recurse -File $Data -Filter '*.jsonl' -ErrorAction SilentlyContinue | Select-String -Pattern 'update\.(downloaded|countdown|apply)' -ErrorAction SilentlyContinue
    foreach ($a in $audit) { Say "  Protokoll: $($a.Line.Substring(0, [math]::Min(200, $a.Line.Length)))" }
    $bk = Join-Path $Data 'updates'; if (Test-Path $bk) { Get-ChildItem -Recurse -File $bk | ForEach-Object { Say "  Update-Ordner: $($_.FullName.Substring($Data.Length)) $($_.Length) Bytes" } }
    $vl = Join-Path $Inst 'velopack.log'; if (Test-Path $vl) { Copy-Item $vl (Join-Path $LogDir 'velopack-nach-update.log') -Force }
    StopAllMine
}

function StartMeasure([string]$root, [string]$name, [string[]]$extra = @()) {
    $log = Join-Path $LogDir "start-$name.txt"; if (Test-Path $log) { Remove-Item $log }
    $p = Start-Process (Join-Path $Inst 'current\TrendoOS.exe') -ArgumentList (@('--offline', '--no-first-run', '--data-root', "`"$root`"", '--startup-log', "`"$log`"") + $extra) -PassThru
    $win = WaitWindow $p 120
    # Die App schreibt das Startprotokoll beim ersten Bild; auf die Zeile warten, nicht nur auf die Datei
    $ff = $null; $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($null -eq $ff -and $sw.Elapsed.TotalSeconds -lt 60) {
        if (Test-Path $log) { $m = Select-String -Path $log -Pattern '^first\.frame\s+(\d+)' -ErrorAction SilentlyContinue | Select-Object -First 1; if ($m) { $ff = [int]$m.Matches[0].Groups[1].Value } }
        if ($null -eq $ff) { Start-Sleep -Milliseconds 100 }
    }
    return [pscustomobject]@{ P = $p; Win = $win; FirstFrame = $ff; Log = $log }
}
function Median($xs) { $s = @($xs | Where-Object { $null -ne $_ } | Sort-Object); if ($s.Count -eq 0) { return $null }; return $s[[int][math]::Floor(($s.Count - 1) / 2)] }

function Phase-Leistung {
    Say "== Leistung (Release-Build $(InstalledVersion), installiert) =="
    $res = [ordered]@{ version = (InstalledVersion); hinweis = 'Kaltstart = neuer, leerer Datenordner (erster Start eines Nutzers); Dateicache von Windows nicht geleert (dafuer braeuchte es Adminrechte).' }
    foreach ($kind in @('kalt', 'warm')) {
        $win = @(); $ff = @()
        $warmRoot = Join-Path $Work 'mess\warm'
        if ($kind -eq 'warm') { $r = StartMeasure $warmRoot 'warm-vorlauf'; Start-Sleep -Seconds 8; StopMine $r.P.Id | Out-Null }
        for ($i = 1; $i -le $Runs; $i++) {
            $root = if ($kind -eq 'kalt') { Join-Path $Work "mess\kalt$i" } else { $warmRoot }
            $r = StartMeasure $root "$kind$i"
            Say ("  {0}start {1}: Fenster sichtbar nach {2} ms, erstes Bild (App-Messung) {3} ms, PID {4}" -f $kind, $i, $(if ($r.Win) { $r.Win.Ms } else { 'NIE' }), $r.FirstFrame, $r.P.Id)
            if ($r.Win) { $win += $r.Win.Ms }; if ($r.FirstFrame) { $ff += $r.FirstFrame }
            Start-Sleep -Seconds 6; StopMine $r.P.Id | Out-Null
        }
        $res["${kind}start_fenster_ms"] = $win; $res["${kind}start_fenster_median_ms"] = (Median $win)
        $res["${kind}start_erstesbild_ms"] = $ff; $res["${kind}start_erstesbild_median_ms"] = (Median $ff)
        Check "${kind}start: $Runs Laeufe mit Fenster" ($win.Count -eq $Runs) "Median Fenster $(Median $win) ms, Median erstes Bild $(Median $ff) ms"
    }
    # Speicher im Leerlauf und nach 5 Programmen (Fenster-Wiederherstellung der App: updates/fenster.json)
    $memRoot = Join-Path $Work 'mess\speicher'
    $r = StartMeasure $memRoot 'leerlauf'; Start-Sleep -Seconds 30; $r.P.Refresh()
    $res['leerlauf_ws_mb'] = [math]::Round($r.P.WorkingSet64 / 1MB); $res['leerlauf_privat_mb'] = [math]::Round($r.P.PrivateMemorySize64 / 1MB)
    Say "  Leerlauf (30 s nach Start): Working Set $($res['leerlauf_ws_mb']) MB, privat $($res['leerlauf_privat_mb']) MB"
    StopMine $r.P.Id | Out-Null
    $progs = @('text', 'tabelle', 'praesentation', 'pdf', 'bild')
    $wins = @(); $x = 40; foreach ($pg in $progs) { $wins += [ordered]@{ Id = "w-$pg"; Page = $pg; X = $x; Y = $x; W = 900; H = 640; Minimized = $false; Maximized = $false; Moved = $true }; $x += 40 }
    New-Item -ItemType Directory -Force (Join-Path $memRoot 'updates') | Out-Null
    ([ordered]@{ Windows = $wins; Stack = @($progs | ForEach-Object { "w-$_" }); Active = 'bild' } | ConvertTo-Json -Depth 5) | Set-Content -Encoding UTF8 (Join-Path $memRoot 'updates\fenster.json')
    $r = StartMeasure $memRoot 'fuenf-programme'; Start-Sleep -Seconds 30; $r.P.Refresh()
    $res['fuenf_programme'] = $progs; $res['fuenf_ws_mb'] = [math]::Round($r.P.WorkingSet64 / 1MB); $res['fuenf_privat_mb'] = [math]::Round($r.P.PrivateMemorySize64 / 1MB); $res['fuenf_spitze_ws_mb'] = [math]::Round($r.P.PeakWorkingSet64 / 1MB)
    if ($r.Win) { $s = Shot $r.Win.Handle (Join-Path $LogDir 'fenster-fuenf-programme.png'); Say "  Bild: $($s.Size), $($s.Lit) % nicht schwarz" }
    Check "fuenf Programme geoeffnet (Wiederherstellungsdatei verbraucht)" (-not (Test-Path (Join-Path $memRoot 'updates\fenster.json')))
    Say "  nach 5 Programmen ($($progs -join ', ')): Working Set $($res['fuenf_ws_mb']) MB, privat $($res['fuenf_privat_mb']) MB, Spitze $($res['fuenf_spitze_ws_mb']) MB"
    StopMine $r.P.Id | Out-Null
    ($res | ConvertTo-Json -Depth 5) | Set-Content -Encoding UTF8 (Join-Path $LogDir 'leistung.json')
}

function Phase-Gross {
    Say '== Grosse Dateien (Release-Build, Avalonia headless ueber tools/Trendo.Render) =='
    New-Item -ItemType Directory -Force $Gross | Out-Null
    $gen = Join-Path $Work 'gross-erzeugen.py'
    @'
import sys, os, zipfile, struct, zlib
out = sys.argv[1]
# 1) Textdokument mit 500 Seiten (je Seite eine Ueberschrift + 6 Absaetze ~ 45 Zeilen)
paras = []
for s in range(1, 501):
    paras.append('<w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Kapitel %d</w:t></w:r></w:p>' % s)
    for a in range(6):
        paras.append('<w:p><w:r><w:t xml:space="preserve">Seite %d, Absatz %d: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</w:t></w:r></w:p>' % (s, a + 1))
    paras.append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
doc = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + ''.join(paras) + '</w:body></w:document>'
with zipfile.ZipFile(os.path.join(out, 'gross-500-seiten.docx'), 'w', zipfile.ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml', '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
    z.writestr('_rels/.rels', '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
    z.writestr('word/document.xml', doc)
# 2) Tabelle mit 100.000 Zeilen x 6 Spalten (CSV)
with open(os.path.join(out, 'gross-100000-zeilen.csv'), 'w', encoding='utf-8', newline='') as f:
    f.write('Nr;Datum;Kunde;Artikel;Menge;Betrag\n')
    for i in range(1, 100001):
        f.write('%d;2026-%02d-%02d;Kunde %d;Artikel %d;%d;%d,%02d\n' % (i, i % 12 + 1, i % 28 + 1, i % 977, i % 313, i % 50 + 1, i % 9999, i % 100))
# 3) Bild 8000 x 8000 (PNG, Farbverlauf)
w = h = 8000
raw = bytearray()
rowcache = {}
for y in range(h):
    g = (y * 255) // (h - 1)
    if g not in rowcache:
        row = bytearray(b'\x00')
        for x in range(w):
            row += bytes(((x * 255) // (w - 1), g, 128))
        rowcache[g] = bytes(row)
    raw += rowcache[g]
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 6)) + chunk(b'IEND', b'')
open(os.path.join(out, 'gross-8000x8000.png'), 'wb').write(png)
'@ | Set-Content -Encoding ASCII $gen
    if (-not (Test-Path (Join-Path $Gross 'gross-8000x8000.png'))) { & python -I $gen $Gross; Check 'grosse Testdateien erzeugt' ($LASTEXITCODE -eq 0) }
    Get-ChildItem $Gross -File | ForEach-Object { Say ("  {0}: {1:0.0} MB, SHA-256 {2}" -f $_.Name, ($_.Length / 1MB), (Sha $_.FullName)) }
    & dotnet build (Join-Path $Repo 'tools\Trendo.Render') -c Release *> (Join-Path $LogDir 'render-build.log')
    $exe = Get-ChildItem (Join-Path $Repo 'tools\Trendo.Render\bin\Release') -Recurse -Filter 'Trendo.Render.exe' | Select-Object -First 1
    Check 'Trendo.Render (Release) gebaut' ($null -ne $exe)
    $rows = @()
    foreach ($f in @('gross-500-seiten.docx', 'gross-100000-zeilen.csv', 'gross-8000x8000.png')) {
        for ($i = 1; $i -le $Runs; $i++) {
            $env:TRENDO_RENDER_ROOT = Join-Path $Work "render-$i"; $env:TRENDO_SHOT = 'seite'; $env:TRENDO_GROSS = Join-Path $Gross $f
            $png = Join-Path $LogDir ("gross-{0}-{1}.png" -f [IO.Path]::GetFileNameWithoutExtension($f), $i)
            $sw = [Diagnostics.Stopwatch]::StartNew()
            $out = & $exe.FullName $png 1600 1000 'start' 2>&1 | Out-String
            $line = ($out -split "`r?`n" | Where-Object { $_ -like 'GROSS *' } | Select-Object -First 1)
            Say "  Lauf $i ($([math]::Round($sw.Elapsed.TotalSeconds,1)) s Prozess): $line"
            if (-not $line) { Say "  Ausgabe: $(($out -split "`r?`n" | Select-Object -Last 5) -join ' | ')" }
            $rows += [pscustomobject]@{ Datei = $f; Lauf = $i; Zeile = $line }
        }
    }
    Remove-Item Env:TRENDO_GROSS, Env:TRENDO_SHOT, Env:TRENDO_RENDER_ROOT -ErrorAction SilentlyContinue
    ($rows | ConvertTo-Json -Depth 4) | Set-Content -Encoding UTF8 (Join-Path $LogDir 'gross.json')
}

function Phase-Deinstallieren {
    Say '== Deinstallieren ueber Update.exe (still) =='
    StopAllMine
    $upd = Join-Path $Inst 'Update.exe'
    Check 'Update.exe vorhanden' (Test-Path $upd)
    $hookLog = Join-Path $Tmp 'trendo-deinstallation.log'; if (Test-Path $hookLog) { Remove-Item $hookLog }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $upd -ArgumentList @('--uninstall', '--silent') -PassThru -Wait
    Check "Update.exe --uninstall --silent beendet (Exitcode $($p.ExitCode))" ($p.ExitCode -eq 0) "$([math]::Round($sw.Elapsed.TotalSeconds,1)) s"
    Start-Sleep -Seconds 5
    if (Test-Path $hookLog) { Get-Content $hookLog -Encoding UTF8 | ForEach-Object { Say "  Hook: $_" } } else { Say '  Hook-Protokoll fehlt (Deinstallations-Hook nicht gelaufen oder anderer Temp-Ordner)' }
    $left = @(); if (Test-Path $Inst) { $left = @(Get-ChildItem -Force -Recurse $Inst) }
    foreach ($l in $left) { Say "  REST im Installationsordner: $($l.FullName.Substring($Inst.Length))" }
    Check 'Installationsordner leer oder entfernt' ($left.Count -eq 0) $(if (Test-Path $Inst) { "Ordner da, $($left.Count) Eintraege" } else { 'Ordner entfernt' })
    $sc = OwnShortcuts; foreach ($s in $sc) { Say "  REST Verknuepfung: $($s.FullName)" }
    Check 'eigene Verknuepfungen entfernt' ($sc.Count -eq 0)
    Check 'HKCU-Uninstall-Eintrag der eigenen Id entfernt' (-not (Test-Path (UninstallKey)))
    $other = @(Get-ChildItem 'HKCU:\Software' -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*$PackId*" }) + @(Get-ChildItem 'HKCU:\Software\Classes' -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*$PackId*" })
    foreach ($o in $other) { Say "  REST Registry: $($o.Name)" }
    Check 'keine weiteren HKCU-Schluessel mit der eigenen Id' ($other.Count -eq 0)
    $vel = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) $PackId
    Check "kein Ordner %LOCALAPPDATA%\$PackId (Standardort)" (-not (Test-Path $vel))
    $dataLeft = @(); if (Test-Path $Data) { $dataLeft = @(Get-ChildItem -Force $Data) }
    Say "  Datenordner der Instanz nach dem Deinstallieren: $(if (Test-Path $Data) { "$($dataLeft.Count) Eintraege (Absicht: stille Deinstallation behaelt Daten)" } else { 'fehlt' })"
    Check 'Nutzerdaten bleiben bei stiller Deinstallation (Absicht)' ((Test-Path (Join-Path $Data 'abnahme-testdatei.txt')))
    Check 'keine eigenen Prozesse mehr' ((MyProcs).Count -eq 0)
}

# ================= Ablauf =================
Say "Abnahme T-0770 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') Phase $Phase, Paket-Id $PackId, Arbeitsordner $Work"
Say "PowerShell $($PSVersionTable.PSVersion), Windows $([Environment]::OSVersion.Version), CPU-Kerne $([Environment]::ProcessorCount), RAM $([math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB)) GB"
$before = SaveRealState "vorher-$Phase"
$phases = if ($Phase -eq 'Alles') { @('Bauen', 'Installieren', 'Update', 'Leistung', 'Gross', 'Deinstallieren') } else { @($Phase -split ',') }
try { foreach ($ph in $phases) { & "Phase-$ph" } }
finally {
    StopAllMine
    $after = SaveRealState "nachher-$Phase"
    $same = ($before | ConvertTo-Json) -eq ($after | ConvertTo-Json)
    Check 'echte Installation unveraendert (Ordnerstand, Verknuepfungen, HKCU-Eintrag, Prozesse; nur lesend verglichen)' $same
    if (-not $same) {
        foreach ($k in $before.Keys) { if ("$($before[$k])" -ne "$($after[$k])") { Say "  geaendert: $k :: $($before[$k]) -> $($after[$k])" } }
        foreach ($f in @(Get-ChildItem $LogDir -Filter "echte-liste-vorher-$Phase-*.txt" -ErrorAction SilentlyContinue)) {
            $leaf = $f.Name.Substring(("echte-liste-vorher-$Phase-").Length) -replace '\.txt$', ''
            $n = Join-Path $LogDir "echte-liste-nachher-$Phase-$leaf.txt"
            if (Test-Path $n) { Compare-Object (Get-Content $f.FullName) (Get-Content $n) | Select-Object -First 20 | ForEach-Object { Say ("  Unterschied {0} {1}: {2}" -f $leaf, $_.SideIndicator, $_.InputObject) } }
        }
    }
    Say "Ende Phase $Phase - Fehler: $script:fails"
}
exit $script:fails
