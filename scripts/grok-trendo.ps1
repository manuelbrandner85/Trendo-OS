<#
  Startet Grok Build für einen Trendo-Auftrag (von Grok Bot aufgerufen, Manuel kann es auch selbst nutzen).
  - Vertraut nur dem Repo-Ordner (--trust), Rechte nach .grok/config.toml, alles andere abgelehnt (dontAsk).
  - Lädt NUR die Projektregeln (AGENTS.md): Claude-Kompatibilität ist für diesen Prozess abgeschaltet;
    die globalen Claude-Dateien bleiben unverändert.
  - Wartet höchstens -TimeoutMin Minuten (Standard 60) und beendet danach nur diesen Grok-Prozess samt Kindern.
  Beispiel:  & ".\scripts\grok-trendo.ps1" -Task "tasks\T-0002.md"
             & ".\scripts\grok-trendo.ps1" -Prompt "Erkläre die Repo-Struktur" -ReadOnly
             & ".\scripts\grok-trendo.ps1" -Task "tasks\T-0002.md" -TimeoutMin 60
#>
param([string]$Task, [string]$Prompt, [switch]$ReadOnly, [int]$MaxTurns = 80, [string]$Model, [int]$TimeoutMin = 60)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$grok = Join-Path $env:USERPROFILE '.grok\bin\grok.exe'
foreach ($k in 'SKILLS', 'RULES', 'AGENTS', 'MCPS', 'HOOKS') { Set-Item "env:GROK_CLAUDE_${k}_ENABLED" 'false'; Set-Item "env:GROK_CURSOR_${k}_ENABLED" 'false' }
$log = Join-Path $repo 'tasks\logs'; New-Item -ItemType Directory $log -Force | Out-Null
$id = if ($Task) { [IO.Path]::GetFileNameWithoutExtension($Task) } else { 'adhoc' }
$out = Join-Path $log ("{0}-{1:yyyyMMdd-HHmmss}.jsonl" -f $id, (Get-Date))
$a = @('--trust', '--cwd', $repo, '--permission-mode', 'dontAsk', '--max-turns', $MaxTurns, '--output-format', 'streaming-json')
if ($ReadOnly) { $a += @('--disallowed-tools', 'run_terminal_command,monitor,get_command_or_subagent_output,kill_command_or_subagent,search_replace,spawn_subagent,scheduler_create,workflow,use_tool,image_gen,image_edit,image_to_video,reference_to_video') }
if ($Model) { $a += @('-m', $Model) }
if ($Task) { $a += @('--prompt-file', (Resolve-Path $Task)) } elseif ($Prompt) { $a += @('-p', $Prompt) } else { throw 'Bitte -Task oder -Prompt angeben.' }
if ($TimeoutMin -lt 1) { throw 'Parameter -TimeoutMin muss mindestens 1 sein.' }

function ConvertTo-GrokArgument([string]$Value) {
  if ($Value -notmatch '[\s"]') { return $Value }
  $escaped = $Value -replace '(\\*)"', '$1$1\"'
  if ($escaped.EndsWith('\')) {
    $trimmed = $escaped.TrimEnd('\')
    $escaped = $trimmed + ('\' * (($escaped.Length - $trimmed.Length) * 2))
  }
  return '"' + $escaped + '"'
}
function Start-GrokPipeReader($reader, $writer, $gate) {
  $ps = [powershell]::Create()
  [void]$ps.AddScript({
    param($reader, $writer, $gate)
    try {
      while ($null -ne ($line = $reader.ReadLine())) {
        [System.Threading.Monitor]::Enter($gate)
        try { $writer.WriteLine($line) } finally { [System.Threading.Monitor]::Exit($gate) }
      }
    } catch { }
  }).AddArgument($reader).AddArgument($writer).AddArgument($gate)
  return @{ Ps = $ps; Handle = $ps.BeginInvoke() }
}
function Complete-GrokPipeReader($reader) {
  if (-not $reader) { return }
  $ps = $reader.Ps
  $handle = $reader.Handle
  if ($handle -and -not $handle.IsCompleted) { [void]$handle.AsyncWaitHandle.WaitOne(10000) }
  if ($handle -and $handle.IsCompleted) {
    try { [void]$ps.EndInvoke($handle) } catch { }
    $ps.Dispose()
  } else {
    try { $ps.Stop() } catch { }
  }
}

$argLine = (($a | ForEach-Object { ConvertTo-GrokArgument ([string]$_) }) -join ' ')
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $grok
$psi.Arguments = $argLine
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
if ($psi.GetType().GetProperty('StandardOutputEncoding')) {
  $utf8In = New-Object System.Text.UTF8Encoding $false
  $psi.StandardOutputEncoding = $utf8In
  $psi.StandardErrorEncoding = $utf8In
}
$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $psi
$utf8 = New-Object System.Text.UTF8Encoding $true
$writer = New-Object System.IO.StreamWriter($out, $false, $utf8)
$writer.AutoFlush = $true
$gate = New-Object System.Object
$runOut = $null
$runErr = $null
$timedOut = $false
$code = 1
try {
  [void]$proc.Start()
  $runOut = Start-GrokPipeReader $proc.StandardOutput $writer $gate
  $runErr = Start-GrokPipeReader $proc.StandardError $writer $gate
  $timeoutMsLong = [int64]$TimeoutMin * [int64]60000
  if ($timeoutMsLong -gt [int]::MaxValue) { $timeoutMsLong = [int]::MaxValue }
  $timedOut = -not $proc.WaitForExit([int]$timeoutMsLong)
  if ($timedOut) {
    $code = 2
    if ($proc.Id -gt 0) {
      # Nur diese PID und ihre Kindprozesse. Keine anderen Prozesse.
      # stderr von taskkill darf das Skript nicht abbrechen (sonst fehlt Exit-Code 2).
      $taskkill = Join-Path $env:SystemRoot 'System32\taskkill.exe'
      $killPreference = $ErrorActionPreference
      $ErrorActionPreference = 'Continue'
      try { & $taskkill /T /F /PID $proc.Id 2>&1 | Out-Null } catch { }
      finally { $ErrorActionPreference = $killPreference }
      if (-not $proc.WaitForExit(15000)) {
        try { $proc.StandardOutput.BaseStream.Close() } catch { }
        try { $proc.StandardError.BaseStream.Close() } catch { }
      }
    }
  } elseif ($proc.HasExited) { $code = $proc.ExitCode }
} finally {
  Complete-GrokPipeReader $runOut
  Complete-GrokPipeReader $runErr
  if ($writer) { $writer.Dispose() }
  if ($proc) { $proc.Dispose() }
}

# Antworttext und Werkzeugaufrufe lesbar zusammenfassen (Rohprotokoll bleibt als .jsonl)
$text = ''; $calls = @(); $end = $null
foreach ($l in Get-Content $out -Encoding UTF8) {
  try { $j = $l | ConvertFrom-Json } catch { continue }
  if ($j.type -eq 'text') { $text += $j.data }
  elseif ($j.type -eq 'tool_call') { $calls += "$($j.toolName): $($j.title)" }
  elseif ($j.type -eq 'end') { $end = $j }
}
$sum = [IO.Path]::ChangeExtension($out, '.txt')
$endeZeile = if ($timedOut) { "Ende: ZEITLIMIT nach $TimeoutMin min" } else { "Ende: $($end.stopReason), Runden $($end.num_turns)" }
@("Werkzeugaufrufe ($($calls.Count)):") + ($calls | ForEach-Object { "  $_" }) + @('', 'Antwort:', $text, '', $endeZeile) | Set-Content $sum -Encoding utf8
Get-Content $sum
"Protokoll: $out"
$global:LASTEXITCODE = $code
exit $code
