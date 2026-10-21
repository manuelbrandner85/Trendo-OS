#!/bin/bash
# SYS-13-P23: Bricht ab, wenn ein NuGet-Paket (auch indirekt) eine bekannte Sicherheitslücke hat.
set -euo pipefail
cd "$(dirname "$0")/.."
out=$(dotnet list Trendo.sln package --vulnerable --include-transitive 2>&1)
echo "$out"
if echo "$out" | grep -Eq '^\s+>\s+\S+.*\b(Low|Moderate|High|Critical)\b'; then echo "Abbruch: bekannte Sicherheitslücken in Paketen"; exit 3; fi
echo "Keine bekannten Lücken."
