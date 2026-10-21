#!/bin/bash
# Veröffentlicht ein Release – ausschließlich vom aktuellen, sauberen main (keine Feature-Branches mehr).
# Aufruf: installer/release.sh <version> [--prerelease]
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"; VER="${1:?Version fehlt}"; cd "$REPO"
git fetch -q origin main
if [ -z "${TRENDO_RELEASE_DRY:-}" ]; then
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || { echo "Abbruch: nicht auf main"; exit 2; }
[ -z "$(git status --porcelain)" ] || { echo "Abbruch: Arbeitskopie nicht sauber"; exit 2; }
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || { echo "Abbruch: main ist nicht gleich origin/main"; exit 2; }
grep -q "<Version>$VER</Version>" src/Trendo.App/Trendo.App.csproj || { echo "Abbruch: Version $VER steht nicht in Trendo.App.csproj"; exit 2; }
fi
dotnet test -c Release --nologo -v q
scripts/vuln_scan.sh
installer/build-setup.sh "$VER"
# Begleiter für Raspberry Pi / Linux (W7) – gleiche Version, Prüfsummen in dieselbe SHA256SUMS.txt
installer/companion/build.sh "$VER"
cp installer/companion/out/trendo-companion-linux-*.tar.gz artifacts/setup/
cd artifacts/setup
grep -v "trendo-companion-" SHA256SUMS.txt > SHA256SUMS.tmp || true; sha256sum trendo-companion-linux-*.tar.gz >> SHA256SUMS.tmp; mv SHA256SUMS.tmp SHA256SUMS.txt
if [ -n "${TRENDO_RELEASE_DRY:-}" ]; then echo "Probelauf: kein gh release"; ls -la; cat SHA256SUMS.txt; exit 0; fi
gh release create "v$VER" TrendoOS-Setup.exe "TrendoOS-$VER-full.nupkg" releases.win.json RELEASES assets.win.json SHA256SUMS.txt \
  trendo-companion-linux-arm64.tar.gz trendo-companion-linux-x64.tar.gz \
  --target "$(git rev-parse HEAD)" ${2:-} --title "Trendo OS $VER (unsigniert)" --notes "Gebaut von main @ $(git rev-parse --short HEAD). **Nicht code-signiert.**"
