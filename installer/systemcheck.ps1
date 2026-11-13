<#  Trendo OS – Systemcheck VOR der Installation (Commercial Desktop Edition, 13.11.2026).
    Nur lesend: fragt Windows nach Prozessor, Arbeitsspeicher, Laufwerk, Grafikkarte und Bildschirm.
    Ändert nichts, installiert nichts, sendet nichts. Keine Seriennummern, keine Benutzernamen.
    Aufruf: powershell -ExecutionPolicy Bypass -File systemcheck.ps1 [-Json]
    Die Profilgrenzen sind VORLÄUFIG und müssen mit PerformanceProfiles.Specs in src/Trendo.Core/Compat/HardwareCompat.cs übereinstimmen (Test prüft das). #>
param([switch]$Json)
$ErrorActionPreference = 'SilentlyContinue'

# Profil: Name, Kerne, RAM (GB), freier Platz (GB), Grafikspeicher (GB)
$profiles = @(
    @{ Name = 'Basic';        Cores = 4; RamGb = 8;  DiskGb = 20;  VramGb = 0 },
    @{ Name = 'Standard';     Cores = 6; RamGb = 16; DiskGb = 50;  VramGb = 4 },
    @{ Name = 'Professional'; Cores = 8; RamGb = 32; DiskGb = 200; VramGb = 8 }
)

$os    = Get-CimInstance Win32_OperatingSystem
$cpu   = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpus  = @(Get-CimInstance Win32_VideoController)
$build = [int]$os.BuildNumber
$ramGb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
$sys   = $env:SystemDrive
$disk  = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$sys'"
$freeGb  = [math]::Round($disk.FreeSpace / 1GB, 0)
$totalGb = [math]::Round($disk.Size / 1GB, 0)
$media = $null
$part = Get-Partition -DriveLetter $sys.TrimEnd(':')
if ($part) { $media = (Get-PhysicalDisk | Where-Object { $_.DeviceId -eq [string]$part.DiskNumber } | Select-Object -First 1).MediaType }

# Grafikspeicher: AdapterRAM ist bei Karten über 4 GB oft falsch (32 Bit), darum zusätzlich die Registrierung lesen (nur lesen)
$vramGb = 0; $gpuName = $null; $driver = $null
$cls = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
Get-ChildItem $cls | Where-Object { $_.PSChildName -match '^\d+$' } | ForEach-Object {
    $k = Get-ItemProperty $_.PSPath
    $mem = $k.'HardwareInformation.qwMemorySize'
    if (-not $mem) { $mem = [uint32]$k.'HardwareInformation.MemorySize' }
    $gb = [math]::Round([double]$mem / 1GB, 1)
    if ($gb -gt $vramGb -or -not $gpuName) { if ($gb -gt $vramGb) { $vramGb = $gb }; $gpuName = $k.DriverDesc; $driver = $k.DriverVersion }
}
if (-not $gpuName -and $gpus.Count -gt 0) { $gpuName = $gpus[0].Name; $driver = $gpus[0].DriverVersion }
$screen = $gpus | Where-Object { $_.CurrentHorizontalResolution } | Select-Object -First 1

$cores = [int]$cpu.NumberOfLogicalProcessors
$have = 'unter Basic'
foreach ($p in $profiles) {
    if ($cores -ge $p.Cores -and $ramGb -ge ($p.RamGb * 0.94) -and $vramGb -ge ($p.VramGb * 0.94)) { $have = $p.Name } else { break }
}

$result = [ordered]@{
    windows       = "$($os.Caption) (Build $build)"
    windows11     = ($build -ge 22000)
    architektur   = $env:PROCESSOR_ARCHITECTURE
    prozessor     = ($cpu.Name -replace '\s+', ' ').Trim()
    kerne         = $cores
    ram_gb        = $ramGb
    laufwerk      = "$sys $freeGb von $totalGb GB frei"
    laufwerk_art  = if ($media) { "$media" } else { 'nicht ermittelt' }
    grafikkarte   = $gpuName
    grafik_gb     = $vramGb
    treiber       = $driver
    bildschirm    = if ($screen) { "$($screen.CurrentHorizontalResolution) x $($screen.CurrentVerticalResolution)" } else { 'nicht ermittelt' }
    profil        = $have
    vorlaeufig    = $true
}

if ($Json) { $result | ConvertTo-Json; exit 0 }
Write-Host ''
Write-Host 'Trendo OS – Systemcheck (nur lesend, nichts wird gesendet)'
Write-Host '-----------------------------------------------------------'
foreach ($k in $result.Keys) { Write-Host ('{0,-14} {1}' -f $k, $result[$k]) }
Write-Host ''
if (-not $result.windows11) { Write-Host 'Hinweis: Trendo OS ist für Windows 11 gebaut. Andere Versionen sind nicht geprüft.' }
if ($freeGb -lt 20) { Write-Host "Hinweis: Nur $freeGb GB frei auf $sys. Für Trendo werden mindestens 20 GB empfohlen, für 3D, Video und KI deutlich mehr." }
if ($have -eq 'unter Basic') { Write-Host 'Hinweis: Dieser PC liegt unter dem Basis-Profil. Trendo startet, viele Studios werden aber langsam sein.' }
Write-Host 'Die Profilgrenzen sind vorläufig. Welche Studios laufen, zeigt Trendo nach der Installation unter Einstellungen > Hardware & Leistung.'
