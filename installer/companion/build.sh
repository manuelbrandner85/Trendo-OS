#!/bin/sh
# Baut den Trendo-Begleiter für Raspberry Pi (linux-arm64) und Linux-PC (linux-x64) als Einzeldatei + install.sh.
set -e
VER="${1:?Version fehlt}"
cd "$(dirname "$0")/../.."
OUT=installer/companion/out; rm -rf "$OUT"; mkdir -p "$OUT"
for r in linux-arm64 linux-x64; do
  dotnet publish src/Trendo.Companion -c Release -r $r --self-contained -p:PublishSingleFile=true -p:DebugType=none -p:Version="$VER" -o "$OUT/$r"
  "$OUT/$r/trendo-companion" --version 2>/dev/null | grep -qx "$VER" || [ "$r" != linux-x64 ] || { echo "Versionsprüfung fehlgeschlagen"; exit 3; }
  cp installer/companion/install.sh installer/companion/trendo-companion.service "$OUT/$r/"
  tar -C "$OUT/$r" -czf "$OUT/trendo-companion-$r.tar.gz" .
done
(cd "$OUT" && sha256sum *.tar.gz > SHA256SUMS.txt && cat SHA256SUMS.txt)
