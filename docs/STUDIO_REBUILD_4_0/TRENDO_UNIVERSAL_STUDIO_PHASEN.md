# Universal Studio Intelligence: Stand je Phase

Auftrag vom 09.11.2026. Hier steht nur Belegtes.

## Phase A: Audit (fertig)

Read-only, keine Änderung am Programmcode. Ergebnis: [Funktionsmatrix](TRENDO_STUDIO_FUNKTIONSMATRIX.md), erzeugt mit `scripts/studio_matrix.py`.

| Stufe | Studios |
|---|---|
| nur spezifiziert | 1 (SPEC-07, abgelehnt und bleibt abgelehnt) |
| teilweise | 14 |
| funktionsfähig ohne Testbeleg | 0 |
| getestet | 43 |
| abgenommen | 0 |

### Wichtigste Befunde

- Vorhandene Systeme sind `StudioCatalog` (Namen), `StudioRegistry` (Status aus `requirements.yaml`), `CapabilityCatalog` (Fachprofile), `ApprovalGate`, `Rbac`, `AuditLog`, `WorkflowEngine`, `AgentCenter`, `ProjectStore`, `IntelligenceCore`. Der Auftrag braucht keine zweite Registry, sondern Erweiterungen dieser Teile.
- 58 Studios, 98 Programme. Nur 12 Studios haben ein Dokumentprogramm mit Befehlen und Dokumentdaten. Bei den anderen erreicht Claude nur, was auf der Seite sichtbar ist.
- Die Funktionskataloge fehlen bei 27 oder 28 Studios (je nach Zählung in den 4.0-Dokumenten). Das ist die größte Lücke für die Funktions-Baseline.
- Die Testzahlen je Studio sind grob, viele Studios teilen dieselben Tests. Der Punkt „mindestens 20 eigene Testfälle“ ist für die meisten Studios nicht belegt.
- Die Belege in `requirements.yaml` stammen teils aus Version 0.9.2.
- Nutzertests mit Anfängern gab es bisher nicht. Sie lassen sich nicht automatisieren, nur vorbereiten (Klickzahl und Zeit bis zur ersten Aktion lassen sich messen).

## Claude-Zugang (Teil von Phase C)

Claude bedient Trendo während der Arbeit selbst über 15 Werkzeuge (`W2/TrendoMcp.cs`, `MainWindow.ClaudeBridge.cs`, `Trendo.Api/ClaudeBridgeHost.cs`). Der Zugang lauscht nur auf 127.0.0.1, gilt für einen Lauf und braucht ein Zufalls-Kennzeichen. Anmeldedaten von Claude berührt er nicht.

Echte Läufe mit Claude Code 2.1.316 am 09.11.2026 gegen ein laufendes Trendo-Fenster:

| Lauf | Ergebnis |
|---|---|
| Auftrag „kleines fertiges Jump-and-Run, Thema Weltraum, drei Level“, Stufe Sicher | Ohne Klick in 2 Minuten 47: Spiel „Sternenspringer“ mit drei Leveln, spielbar gebaut, gespeichert, Claude meldete offene Punkte selbst (Level 1 ohne eigenen Namen, kein eigener Ton). |
| Kurzauftrag, Stufe Voll mit Bildschirmfoto | Titel gesetzt, Fenster dreimal angesehen, gespeichert. Blender, Unreal und Godot waren verbunden. |

Dabei gefundene und behobene Fehler: Das Bildschirmfoto stürzte beim Verkleinern ab. Ein reiner Dateiname bei „Speichern unter“ landete im Arbeitsverzeichnis des Prozesses.

Nicht geprüft: der Lauf durch den echten Chat-Bereich der App (die Läufe gingen direkt an die Werkzeuge), eine echte Abo-Pause, ein Lauf mit Unreal oder Blender in einem Auftrag.

## Phase B bis F

Noch nicht abgeschlossen.
