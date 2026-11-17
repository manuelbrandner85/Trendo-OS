#!/usr/bin/env bash
# T-0732: Beleg mit echtem FFmpeg – zehn Windows-Systemklänge (echte Fremddateien) im Stapel nach MP3, Abbruch mitten in einer Datei,
# Ton-Kette mit einstellbaren Werten nach FLAC und zurück, Aufnahme bis Stopp mit Pegel. Jedes Ergebnis öffnet ffprobe (fremdes Programm).
# Ausführen im Repo-Wurzelordner (Git Bash). Ergebnis: reports/T-0732/rohlog.txt, beleg.log, aufnahme.wav, sha256.txt.
# MP3s und FLAC aus den Windows-Klängen werden nicht mitgeliefert (fremde Klänge) – Name, Prüfsumme und ffprobe-Ausgabe stehen in beleg.log.
set -u
cd "$(dirname "$0")/../.."
OUT=reports/T-0732
WORK="${TEMP:-/tmp}/trendo-t0732-beleg-$$"
mkdir -p "$WORK"
{
  echo "== T-0732 Beleg $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "== ffmpeg: $(command -v ffmpeg >/dev/null && echo vorhanden || echo FEHLT)"
  TRENDO_T0732_BELEG="$WORK" dotnet test tests/Trendo.Core.Tests -c Release --no-build --logger "console;verbosity=normal" \
    --filter "FullyQualifiedName~TonMedienT0732Tests" 2>&1
  echo "== beleg.log"
  cat "$WORK/beleg.log"
} > "$OUT/rohlog.raw.txt" 2>&1
sed -E 's#[A-Za-z]:[\\/]+Users[\\/]+[^\\/ ]+#C:\\Users\\<user>#g' "$OUT/rohlog.raw.txt" > "$OUT/rohlog.txt" && rm "$OUT/rohlog.raw.txt"
sed -E 's#[A-Za-z]:[\\/]+Users[\\/]+[^\\/ ]+#C:\\Users\\<user>#g' "$WORK/beleg.log" > "$OUT/beleg.log"
cp "$WORK/aufnahme.wav" "$OUT/"
rm -rf "$WORK"
(cd "$OUT" && sha256sum beleg.log rohlog.txt aufnahme.wav beleg.sh > sha256.txt)
tail -4 "$OUT/rohlog.txt"
