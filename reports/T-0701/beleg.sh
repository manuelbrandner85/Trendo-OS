#!/usr/bin/env bash
# T-0701: Beleg mit echtem LibreOffice – Trendo speichert die Fremd-Testdateien (tests/Daten/office), LibreOffice öffnet sie,
# macht PDF/XLSX/ODP daraus, Trendo liest die LibreOffice-Ergebnisse zurück. Ausführen im Repo-Wurzelordner (Git Bash).
# Ergebnis: reports/T-0701/libreoffice/* , rohlog.txt , sha256.txt (Benutzerpfade werden zu C:\Users\<user>).
set -u
cd "$(dirname "$0")/../.."
OUT=reports/T-0701
mkdir -p "$OUT/libreoffice"
SOFF="${TRENDO_WERKZEUGE:-$LOCALAPPDATA/TrendoWerkzeuge}/libreoffice/App/libreoffice/program/soffice.exe"
{
  echo "== T-0701 Beleg $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "== LibreOffice (soffice.exe vorhanden: $([ -f "$SOFF" ] && echo ja || echo nein)), version.ini:"
  grep -aE "^(buildid|ProductMajor|ProductMinor|Version|MsiProductVersion)" "$(dirname "$SOFF")/version.ini" 2>/dev/null
  echo "== Test mit echtem LibreOffice (T0701_TabellePraesentation.LibreOffice_rundreise) und Importbericht-Tests"
  TRENDO_T0701_BELEG="$(pwd)/$OUT/libreoffice" dotnet test tests/Trendo.Core.Tests -c Release --no-build --logger "console;verbosity=normal" \
    --filter "FullyQualifiedName~T0701|FullyQualifiedName~T0682|DisplayName~Abgleich mit LibreOffice" 2>&1
  echo "== Seitenzahl der PDFs (LibreOffice)"
  for f in "$OUT"/libreoffice/*.pdf; do python -c "import re,sys;d=open(sys.argv[1],'rb').read();print(sys.argv[1].split('/')[-1], len(re.findall(rb'/Type\s*/Page[^s]',d)), 'Seite(n)', len(d), 'Bytes')" "$f"; done
} > "$OUT/rohlog.raw.txt" 2>&1
sed -E 's#[A-Za-z]:[\\/]+Users[\\/]+[^\\/ ]+#C:\\Users\\<user>#g' "$OUT/rohlog.raw.txt" > "$OUT/rohlog.txt" && rm "$OUT/rohlog.raw.txt"
(cd "$OUT" && sha256sum libreoffice/* rohlog.txt ../../tests/Daten/office/fremd-tabelle.xlsx ../../tests/Daten/office/fremd-praesentation.pptx > sha256.txt)
tail -3 "$OUT/rohlog.txt"
