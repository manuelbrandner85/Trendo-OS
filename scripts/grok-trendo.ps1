<#
  Startet Grok Build für einen Trendo-Auftrag (von Grok Bot aufgerufen, Manuel kann es auch selbst nutzen).
  - Vertraut nur dem Repo-Ordner (--trust), Rechte nach .grok/config.toml, alles andere abgelehnt (dontAsk).
  - Lädt NUR die Projektregeln (AGENTS.md): Claude-Kompatibilität ist für diesen Prozess abgeschaltet;
    die globalen Claude-Dateien bleiben unverändert.
  Beispiel:  & ".\scripts\grok-trendo.ps1" -Task "tasks\T-0002.md"
             & ".\scripts\grok-trendo.ps1" -Prompt "Erkläre die Repo-Struktur" -ReadOnly
#>
param([string]$Task, [string]$Prompt, [switch]$ReadOnly, [int]$MaxTurns = 80, [string]$Model)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$grok = Join-Path $env:USERPROFILE '.grok\bin\grok.exe'
foreach ($k in 'SKILLS', 'RULES', 'AGENTS', 'MCPS', 'HOOKS') { Set-Item "env:GROK_CLAUDE_${k}_ENABLED" 'false'; Set-Item "env:GROK_CURSOR_${k}_ENABLED" 'false' }
$log = Join-Path $repo 'tasks\logs'; New-Item -ItemType Directory $log -Force | Out-Null
$id = if ($Task) { [IO.Path]::GetFileNameWithoutExtension($Task) } else { 'adhoc' }
$out = Join-Path $log ("{0}-{1:yyyyMMdd-HHmmss}.jsonl" -f $id, (Get-Date))
$a = @('--trust', '--cwd', $repo, '--permission-mode', 'dontAsk', '--max-turns', $MaxTurns, '--output-format', 'streaming-json')
if ($ReadOnly) { $a += @('--disallowed-tools', 'run_terminal_command,search_replace,spawn_subagent,scheduler_create,image_gen,image_edit,image_to_video,reference_to_video') }
if ($Model) { $a += @('-m', $Model) }
if ($Task) { $a += @('--prompt-file', (Resolve-Path $Task)) } elseif ($Prompt) { $a += @('-p', $Prompt) } else { throw 'Bitte -Task oder -Prompt angeben.' }
& $grok @a 2>&1 | Out-File -FilePath $out -Encoding utf8
# Antworttext und Werkzeugaufrufe lesbar zusammenfassen (Rohprotokoll bleibt als .jsonl)
$text = ''; $calls = @(); $end = $null
foreach ($l in Get-Content $out) {
  try { $j = $l | ConvertFrom-Json } catch { continue }
  if ($j.type -eq 'text') { $text += $j.data }
  elseif ($j.type -eq 'tool_call') { $calls += "$($j.toolName): $($j.title)" }
  elseif ($j.type -eq 'end') { $end = $j }
}
$sum = [IO.Path]::ChangeExtension($out, '.txt')
@("Werkzeugaufrufe ($($calls.Count)):") + ($calls | ForEach-Object { "  $_" }) + @('', 'Antwort:', $text, '', "Ende: $($end.stopReason), Runden $($end.num_turns)") | Set-Content $sum -Encoding utf8
Get-Content $sum
"Protokoll: $out"
