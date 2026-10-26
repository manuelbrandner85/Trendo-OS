# T-0222 am PC: Setup aus dem Vollpaket-Ordner installieren und prüfen, ob Trendo beim ersten Start die Programme ohne Internet selbst einrichtet.
# Sichert vorher die Benutzerdaten (%APPDATA%\TrendoOS) nach -Work und vergleicht sie danach. Beendet keine Prozesse – die App bleibt offen.
# Aufruf: powershell -ExecutionPolicy Bypass -File tools/messung/vollpaket-setup-test.ps1 -Medium <Ordner mit TrendoOS-Setup.exe und Programme\> -Work <leerer Temp-Ordner> -Log <Logdatei>
param([string]$Medium, [string]$Work, [string]$Log, [int]$WaitMinutes = 20)
$ErrorActionPreference = 'Stop'
function Say($t) { $line = "$(Get-Date -Format 'HH:mm:ss') $t"; $line | Tee-Object -FilePath $Log -Append }
$inst = Join-Path $env:LOCALAPPDATA 'TrendoOS'
$data = Join-Path $env:APPDATA 'TrendoOS'
$tools = Join-Path $env:LOCALAPPDATA 'TrendoWerkzeuge'
$audit = Join-Path $data 'audit\audit.jsonl'
New-Item -ItemType Directory -Force $Work | Out-Null
Say "Vollpaket-Setup-Test $(Get-Date -Format 'yyyy-MM-dd') · Windows $([Environment]::OSVersion.VersionString)"
$manifest = Get-Content (Join-Path $Medium 'Programme\manifest.json') -Raw | ConvertFrom-Json
Say "Vollpaket: $($manifest.programs.Count) Pakete, Setup $((Get-Item (Join-Path $Medium 'TrendoOS-Setup.exe')).VersionInfo.ProductVersion)"
$before = if (Test-Path "$inst\current\TrendoOS.exe") { (Get-Item "$inst\current\TrendoOS.exe").VersionInfo.ProductVersion } else { 'nicht installiert' }
Say "Vorher installiert: $before"

# 1. Benutzerdaten sichern und Prüfsummen merken
function Hashes($root) { if (-not (Test-Path $root)) { return @{} }; $h = @{}; Get-ChildItem $root -Recurse -File | ForEach-Object { $h[$_.FullName.Substring($root.Length)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }; $h }
$backup = Join-Path $Work 'sicherung-appdata-TrendoOS'
if (Test-Path $data) { robocopy $data $backup /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null }
$dataBefore = Hashes $data
Say "1. Daten gesichert: $($dataBefore.Count) Dateien"
$auditLinesBefore = if (Test-Path $audit) { (Get-Content $audit).Count } else { 0 }
$toolsBefore = if (Test-Path $tools) { (Get-ChildItem $tools -Directory).Name -join ', ' } else { '(kein Ordner)' }
Say "   TrendoWerkzeuge vorher: $toolsBefore"

# 2. alte Fassung still deinstallieren (Daten bleiben)
if (Test-Path (Join-Path $inst 'Update.exe')) { & (Join-Path $inst 'Update.exe') uninstall --silent | Out-Null; Start-Sleep -Seconds 3 }
Say "2. Deinstalliert: $(-not (Test-Path "$inst\current\TrendoOS.exe"))"

# 3. Setup aus dem Vollpaket-Ordner starten – wie ein Doppelklick
$t0 = Get-Date
Start-Process (Join-Path $Medium 'TrendoOS-Setup.exe') -WorkingDirectory $Medium
for ($i = 0; $i -lt 180 -and -not (Test-Path "$inst\current\TrendoOS.exe"); $i++) { Start-Sleep -Seconds 1 }
Say "3. Installiert: $((Get-Item "$inst\current\TrendoOS.exe" -ErrorAction SilentlyContinue).VersionInfo.ProductVersion) nach $([int]((Get-Date) - $t0).TotalSeconds) s"

# 4. auf die Einrichtung der Programme warten (Audit-Log und Werkzeugordner)
$deadline = (Get-Date).AddMinutes($WaitMinutes); $last = -1
while ((Get-Date) -lt $deadline) {
    $new = if (Test-Path $audit) { Get-Content $audit | Select-Object -Skip $auditLinesBefore } else { @() }
    $installed = @($new | Where-Object { $_ -match '"vollpaket\.install"' }).Count
    $notFound = @($new | Where-Object { $_ -match '"vollpaket\.not_found"' }).Count
    if ($installed -ne $last) { Say "   eingerichtet bisher: $installed"; $last = $installed }
    if ($notFound -gt 0) { Say "   Trendo meldet: kein Vollpaket gefunden"; break }
    $missing = @($manifest.programs | Where-Object { -not (Test-Path (Join-Path $tools (Join-Path $_.id $_.exe))) }).Count
    if ($missing -eq 0) { break }
    Start-Sleep -Seconds 5
}
$dur = [int]((Get-Date) - $t0).TotalSeconds
Say "4. Ende Einrichtung nach $dur s"
foreach ($p in $manifest.programs) { $ok = Test-Path (Join-Path $tools (Join-Path $p.id $p.exe)); Say ("   {0,-12} {1}" -f $p.id, $(if ($ok) { 'eingerichtet' } else { 'FEHLT' })) }
$newAudit = if (Test-Path $audit) { Get-Content $audit | Select-Object -Skip $auditLinesBefore } else { @() }
Say "   Audit (neu, Auszug):"; $newAudit | Where-Object { $_ -match 'vollpaket|app.start|first' } | Select-Object -First 30 | ForEach-Object { Say "     $_" }

# 5. Benutzerdaten: alte Dateien unverändert?
$dataAfter = Hashes $data
$changed = @($dataBefore.Keys | Where-Object { $dataAfter[$_] -ne $dataBefore[$_] })
Say "5. Alte Dateien: $($dataBefore.Count), davon verändert/fehlend: $($changed.Count) $(($changed | Select-Object -First 10) -join ', ')"
Say "Fertig. App bleibt geöffnet."
