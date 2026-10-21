#!/bin/bash
# Baut TrendoOS-Setup.exe (Velopack, Installation pro Benutzer ohne Admin/UAC) – auch auf Linux (Cross-Pack).
# Voraussetzung: .NET 10 SDK, `dotnet tool install -g vpk`. Ausgabe: artifacts/setup/
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"; VER="${1:-0.3.0}"; OUT="$REPO/artifacts/setup"; PUB="$REPO/artifacts/publish-win-x64"
rm -rf "$OUT" "$PUB"
dotnet publish "$REPO/src/Trendo.App/Trendo.App.csproj" -c Release -r win-x64 --self-contained true -p:Version="$VER" -o "$PUB"
vpk "[win]" pack -u TrendoOS -v "$VER" -p "$PUB" -e TrendoOS.exe --packTitle "Trendo OS" --packAuthors "Trendo OS Projekt" -o "$OUT" --yes
mv "$OUT/TrendoOS-win-Setup.exe" "$OUT/TrendoOS-Setup.exe"
(cd "$OUT" && sha256sum TrendoOS-Setup.exe TrendoOS-win-Portable.zip > SHA256SUMS.txt && cat SHA256SUMS.txt)
