# T-0727 B1: abwechselnd R2R (wie ausgeliefert) und ohne R2R – je Runde frischer Ordner (kalt), danach zweiter Start (warm), Ordner danach gelöscht
param([int]$Runden = 3)
$M = Join-Path $env:TEMP 'trendo-auftrag-T-0727-mess'; $s = Join-Path $M 'py\kaltstart.ps1'; $w = Join-Path $M 'b1'
$out = Join-Path $w 'vergleich.log'
for ($r = 1; $r -le $Runden; $r++) {
    foreach ($v in @('pub-nachher', 'pub-ohne-r2r')) {
        $tag = "$v-r$r"
        $k = & $s -Pub (Join-Path $w $v) -Work $w -Tag $tag -Mode kalt
        $wa = & $s -Pub (Join-Path $w $v) -Work $w -Tag $tag -Mode warm
        $pick = { param($lines) ($lines | Where-Object { $_ -match '^(main|app\.init|window\.ctor|first\.frame)\s' } | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }) -join ' | ' }
        $line = "Runde $r $v kalt: $(& $pick $k) || warm: $(& $pick $wa)"
        $line; Add-Content -Path $out -Value $line -Encoding utf8
        Remove-Item (Join-Path $w "kopie-$tag") -Recurse -Force; Remove-Item (Join-Path $w "daten-$tag") -Recurse -Force
    }
}
