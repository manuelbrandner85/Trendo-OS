# ADR-0001: Ein Kern, drei Editionen mit OS-Adaptern
**Status:** ACCEPTED (aus Spezifikation) · **Datum:** 2026-10-21 · **Quelle:** SYSTEMBIBEL §1–3, ARCHITEKTUR.md

## Kontext
Trendo OS soll als Windows-App, bootbares Linux (externe SSD/USB) und Pi4+Windows-Verbund laufen. Ein eigener OS-Kernel ist nicht geplant.

## Entscheidung
Gemeinsamer modularer Core (Projekte, Memory, Studio-Registry, Event-Bus, Berechtigungen/Approvals, Secrets, Software-Manifest, QA-Evidenz, UI-Designsystem) mit versionierter lokaler API. Plattformspezifisches nur in Adaptern: Windows-Adapter, Linux-Adapter, Pi-Bridge. Studios als Plug-ins mit Programmadaptern (Blender, Unreal, LibreOffice, Browser …).

## Konsequenzen
- Keine Annahme, dass Windows-EXE unter Linux oder x64 auf Pi-ARM laufen.
- Windows-Edition ersetzt weder Shell, Taskleiste, Registry noch Treiber.
- Linux-Edition basiert auf bestehender Distribution (Auswahl offen, OFFENE_ENTSCHEIDUNGEN Nr. 3).
