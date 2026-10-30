# Priorisierte Roadmap 98–113

Grundsatz: keine 16 Systeme gleichzeitig. Kleine Pakete auf vorhandenem Code, je Paket eigener Branch `claude/<task-id>-<kurz>`, höchstens 1–3 parallel auf getrennten Dateien, Volltest vor jedem Push, Release nach deiner Vorgabe.

## P0 – Fehler und Sicherheitslücken im heutigen Code (Must, sofort)
| Task | Inhalt | Dateien | Agent (Builder / QA) | Freigabe |
|---|---|---|---|---|
| T-0240 | **TV: Zugangsdaten in den sicheren Speicher**, Altdaten umziehen (Befund 1) | `MainWindow.Tv.cs`, `Tv/IpTv.cs`, `Secrets.cs` | 03-security / 19-qa | keine extra (Bugfix) |
| T-0241 | Erweiterungen: Installieren/Entfernen über `ApprovalGate` + Not-Stopp, atomarer Austausch, Download ohne Auto-Redirect, Offline beachten | `Extensions.cs`, `MainWindow.W5Ext*.cs`, `ExtensionCatalog.cs` | 03-security / 19-qa | Quellenliste (C10) |
| T-0242 | glTF-Achsen (Z→Y oben) + asymmetrischer Rundreisetest | `MeshIO.cs`, `Scene3DTests.cs` | 12-blender / 19-qa | – |
| T-0243 | Entpacken/Packen mit Konfliktregel statt stillem Überschreiben | `Archives.cs` | 15-device / 19-qa | – |
| T-0244 | Ehrliche Texte statt „läuft flüssig“; Tests, die ohne Programm still bestehen, als „übersprungen“ melden | `MainWindow.Spec04.cs`, `W4Studio3DTests.cs` u. a. | 19-qa | – |

## P1 – Beweisbarer Status (111, Must)
| T-0245 | `EvidenceState` neben `StatusRules` (TESTED nur mit Commit + Log-SHA, sonst STALE/INVALID), getrennte Zähler in `Progress.cs`, Anzeige in `MainWindow.StudiosView.cs` | 01-architecture / 19-qa | A1 (Register 98–113) |
| T-0246 | `tools/trendo_ops.py`: Job-Limit, Pfadliste je Job, verwaiste RUNNING → INTERRUPTED | 08-agent-workflow / 19-qa | – |

## P2 – Fundament für die Suiten (Should)
- Gemeinsames Speichern/Wiederherstellen und Autosave für Text, Tabelle, Präsentation (100), Formatversion + Migration des eigenen Formats.
- `WorkflowEngine.RunDueAsync` mit Lease (110), Ersatz-KI im Modus „nur lokal“ abschaltbar (C7).
- `Ledger` mit IDs und Storno statt Löschen, `decimal` für Preise (105, Schemaänderung → Freigabe).
- `TwinPlus` lückenfest, `Vision` nur mit Kalibrierung (109).

## P3 – Fachliche Bündel in Reihenfolge der Übergabe (Could, je ein Vertical Slice)
100 Office → 101 Media → 102 CAD → 103 Game/3D → 104 Web/Code → 105 Business → 106 Kommunikation → 107 System → 108 Wissen → 109 Robotik/IoT → 110 KI → 111 Cross-Studio → 112 Plugins → 113 XR. Je Bündel zuerst die 1–2 kleinen Pakete aus den Agentenberichten (`agentenberichte/`), z. B. 106 CAL-19 Frei/Belegt, 108 Quellen-/Behauptungsmodell, 113 `XrCapability` mit Simulator-Adapter.

## P4 – 98/99 vollständig und Altbestand
98 Zero-Config nur nach C1, Mediatheken, Quellenregister; 99 REA mit Lizenz-/Herkunftsnachweis; IMP-95/96 erst mit Originalunterlagen.

## Blockiert / nur mit Hardware oder Programmen
- 113: 14 Funktionen brauchen echte XR-Geräte/Runtime.
- 103/104: Blender-/Unreal-Läufe nur mit geprüfter Installation (am PC vorhanden: Blender 5.2, UE 5.8 – noch nicht in Tests eingebunden).
- 109: Realwelt-Aktoren nur mit eindeutiger Freigabe; Simulation ≠ Live.
