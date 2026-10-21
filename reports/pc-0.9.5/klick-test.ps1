param([string]$Out)
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Windows.Forms,System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);}
"@
$audit = "$env:APPDATA\TrendoOS\audit\audit.jsonl"
$p = Get-Process TrendoOS | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
$root = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
function All { $root.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) }
function ById($id) { All | Where-Object { $_.Current.AutomationId -eq $id } | Select-Object -First 1 }
function Ids($prefix) { All | Where-Object { $_.Current.AutomationId -like "$prefix*" } | ForEach-Object { $_.Current.AutomationId } }
function Click($el) {
  $r = $el.Current.BoundingRectangle
  $scr = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  if ($r.Bottom -gt $scr.Height - 120 -or $r.Y -lt 0) { try { $el.GetCurrentPattern([Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView(); Start-Sleep -Milliseconds 400; $r = $el.Current.BoundingRectangle } catch {} }
  [M]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  [M]::SetCursorPos([int]($r.X + $r.Width/2), [int]($r.Y + $r.Height/2)); Start-Sleep -Milliseconds 150
  [M]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [M]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
}
function Shot($f) { $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height; $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [Drawing.Point]::Empty, $b.Size); $s = New-Object Drawing.Bitmap $bmp, 1365, 720; $s.Save($f); $g.Dispose(); $bmp.Dispose(); $s.Dispose() }
function Run($back, $id) {
  Click (ById $back); Start-Sleep -Milliseconds 1200
  $el = ById $id; if (-not $el) { return "{0,-14} {1,-34} NICHT GEFUNDEN" -f $back, $id }
  $a0 = @(Get-Content $audit).Count
  Click $el; Start-Sleep -Seconds 3
  $still = [bool](ById $id)
  $new = @(Get-Content $audit | Select-Object -Skip $a0 | ForEach-Object { $j = $_ | ConvertFrom-Json; "$($j.Action) $($j.Detail)" }) -join ' ; '
  Shot (Join-Path $Out ("{0}.png" -f ($id -replace '[^A-Za-z0-9-]', '')))
  $ok = (-not $still) -and ($new -match 'studio.open')
  "{0} {1,-14} {2,-34} Liste verlassen={3} | {4}" -f ($(if ($ok) {'OK  '} else {'FEHL'})), $back, $id, (-not $still), $new
}
# Startseite
Click (ById 'side-start'); Start-Sleep 1
foreach ($t in @(Ids 'tile-')) { Run 'side-start' $t }
# Seitenleisten-Bereiche mit Studio-Karten
foreach ($cat in 'side-alle-studios','side-online-business','side-3d-und-games','side-kreativ-und-medien','side-tools-und-entwickler','side-system-und-sicherheit','side-ki-assistent','side-kommunikation') {
  Click (ById $cat); Start-Sleep 1
  $cards = @(Ids 'studio-card-')
  "== $cat : $($cards.Count) Karten"
  if ($cat -ne 'side-alle-studios') { foreach ($c in $cards) { Run $cat $c } }
}
