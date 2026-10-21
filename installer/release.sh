#!/bin/bash
# Veröffentlicht ein Release – ausschließlich vom aktuellen, sauberen main (keine Feature-Branches mehr).
# Aufruf: installer/release.sh <version> [--prerelease]
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"; VER="${1:?Version fehlt}"; cd "$REPO"
git fetch -q origin main
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || { echo "Abbruch: nicht auf main"; exit 2; }
[ -z "$(git status --porcelain)" ] || { echo "Abbruch: Arbeitskopie nicht sauber"; exit 2; }
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || { echo "Abbruch: main ist nicht gleich origin/main"; exit 2; }
grep -q "<Version>$VER</Version>" src/Trendo.App/Trendo.App.csproj || { echo "Abbruch: Version $VER steht nicht in Trendo.App.csproj"; exit 2; }
dotnet test -c Release --nologo -v q
installer/build-setup.sh "$VER"
cd artifacts/setup
gh release create "v$VER" TrendoOS-Setup.exe "TrendoOS-$VER-full.nupkg" releases.win.json RELEASES assets.win.json SHA256SUMS.txt \
  --target "$(git rev-parse HEAD)" ${2:-} --title "Trendo OS $VER (unsigniert)" --notes "Gebaut von main @ $(git rev-parse --short HEAD). **Nicht code-signiert.**"
