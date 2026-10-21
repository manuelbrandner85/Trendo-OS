# ADR-0008: Masterprompt MP-1.0 und Tech-Stack
**Status:** ACCEPTED (Fortschreibung von ADR-0005) · **Datum:** 2026-10-22

## Kontext
Der Masterprompt vom 2026-10-22 nennt Rust bzw. Qt nur **als Beispiele** für mögliche Technologien.

## Entscheidung
ADR-0005 (**.NET 10 + Avalonia**) bleibt gültig. Die Beispiele im Masterprompt sind keine Vorgabe; ein Wechsel würde ~200 PRs funktionierenden, PC-geprüften Code verwerfen. Rust/Qt können nur für einzelne Komponenten (z. B. Pi-Systemdienste) per eigenem ADR mit Manuels Zustimmung eingesetzt werden.

## Folgen
Keine Codeänderung. Linux x64 und ARM64 (Pi) werden weiter über .NET/Avalonia bedient.
