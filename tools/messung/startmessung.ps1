# D7: Startzeit und Arbeitsspeicher eines Trendo-Starts messen (nur der selbst gestartete Prozess wird beendet).
# Aufruf: powershell -ExecutionPolicy Bypass -File tools/messung/startmessung.ps1 -Exe <TrendoOS.exe> -Root <leerer Temp-Ordner> -Name lauf1
param([string]$Exe, [string]$Root, [string]$Name)
$log = Join-Path $Root "$Name.txt"
if (Test-Path $log) { Remove-Item $log }
$p = Start-Process -FilePath $Exe -ArgumentList @('--offline','--no-first-run','--data-root', "`"$(Join-Path $Root 'data')`"", '--startup-log', "`"$log`"") -PassThru
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not (Test-Path $log) -and $sw.Elapsed.TotalSeconds -lt 90) { Start-Sleep -Milliseconds 200 }
Start-Sleep -Seconds 8
$p.Refresh()
$mb = [math]::Round($p.WorkingSet64 / 1MB)
$priv = [math]::Round($p.PrivateMemorySize64 / 1MB)
$lines = if (Test-Path $log) { (Get-Content $log | Where-Object { $_ -match '^(main|avalonia.start|app.init|window.opened|first.frame) ' }) -join ' | ' } else { 'KEIN LOG' }
"$Name pid=$($p.Id) RAM_working=${mb}MB RAM_privat=${priv}MB :: $lines"
Stop-Process -Id $p.Id -Force
