# T-0727 B1: Kaltstart messen. Kopiert das veröffentlichte Programm in einen frischen Ordner (neue Dateien wie nach dem Auspacken)
# und startet das Fenster mit eigenem Datenordner und --startup-log. Modus:
#   kalt      = erster Start direkt nach dem Kopieren
#   vorgelesen = nach dem Kopieren alle Dateien einmal lesen (öffnet jede Datei wie der Virenscanner beim ersten Zugriff), dann starten
#   warm      = zweiter Start im selben Ordner
param([string]$Pub, [string]$Work, [string]$Tag, [string]$Mode = 'kalt')
$ErrorActionPreference = 'Stop'
$dir = Join-Path $Work "kopie-$Tag"; $daten = Join-Path $Work "daten-$Tag"
if ($Mode -ne 'warm') {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    $sw = [Diagnostics.Stopwatch]::StartNew(); Copy-Item $Pub $dir -Recurse; $copyMs = $sw.ElapsedMilliseconds
    if ($Mode -eq 'vorgelesen') {
        $sw.Restart(); $n = 0
        foreach ($f in Get-ChildItem $dir -Recurse -File) { $fs = [IO.File]::OpenRead($f.FullName); $buf = New-Object byte[] 65536; while ($fs.Read($buf, 0, $buf.Length) -gt 0) {}; $fs.Dispose(); $n++ }
        "vorgelesen: $n Dateien in $($sw.ElapsedMilliseconds) ms"
    }
    "kopiert in $copyMs ms"
}
New-Item -ItemType Directory -Force $daten | Out-Null
$slog = Join-Path $Work "start-$Tag-$Mode.txt"; if (Test-Path $slog) { Remove-Item $slog }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process (Join-Path $dir 'TrendoOS.exe') -ArgumentList @('--no-first-run', '--data-root', "`"$daten`"", '--startup-log', "`"$slog`"") -PassThru
while ($sw.Elapsed.TotalSeconds -lt 90 -and -not $p.HasExited) {
    if ((Test-Path $slog) -and (Select-String -Path $slog -Pattern '^first\.frame' -Quiet)) { break }
    Start-Sleep -Milliseconds 100
}
$wall = $sw.ElapsedMilliseconds
Start-Sleep -Milliseconds 500
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
"Wanduhr bis Startprotokoll: $wall ms"
if (Test-Path $slog) { Get-Content $slog | Where-Object { $_ -match '^(main|avalonia\.start|app\.init|app\.xaml|window\.ctor|ctor\.\S+|d\.\S+|window\.opened|first\.frame)\s' } }
