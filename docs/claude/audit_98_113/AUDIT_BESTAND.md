# Read-only-Bestandsaudit zur Übergabe 98–113

Stand: 30.10.2026 · erstellt vom Master Director (Hauptsession) mit sieben Audit-Agenten (Rollen 02-requirements-decisions, 03-security-permissions, 19-qa-verification). Im Repo wurde für dieses Audit nichts geändert, nichts gebaut und kein Test gestartet. Diese Dateien sind noch **nicht committet**.

> **Spezifikationen eingelesen: 98–113 (16/16).** Prüfsummen laut `MANIFEST_98_113.json`: 16 von 16 stimmen.
> **Repo-Audit:** `main` @ `7e1b8634378a424a2b69cc9e4d70c0dbe5bfb322` (30.10.2026, „Build-Log: Releases 0.9.14 und 0.9.15“), App-Version 0.9.15.
> **Fehlende historisch originale Einzeldokumente:** Funktionslisten zu **IMP-95** (Architecture & BIM) und **IMP-96** (Lighting & Acoustics) – weder im Repo noch im Paket.
> **Implementierung:** Für keine der 811 Funktionen aus 98–113 ist der Spec-Vertrag vollständig erfüllt. Nichts davon ist PC_VERIFIED.

## 1. Repository und Plattform

| Punkt | Befund (gemessen) |
|---|---|
| Branch / HEAD | `main` = `origin/main` = `7e1b863`; Arbeitszweig `claude/T-02xx` gleich |
| Projekte | Trendo.Core, Trendo.App (Avalonia), Trendo.Api, Trendo.Companion – alle `net10.0` |
| Werkzeuge | .NET SDK 10.0.403, Python 3.13.15, Git, ffmpeg (winget), Ollama; Blender 5.2 und UE 5.8 installiert, aber nicht im PATH; mpv fehlt auf diesem PC |
| Umfang | 55 Studios in `StudioCatalog.cs`, 68 PromptAction-Adapter, rund 1 047 `[Fact]/[AvaloniaFact]`-Tests |
| Letzter Volltest | 30.10.2026 auf `c062bb3` (Code-Stand von HEAD; danach nur ein Build-Log-Commit): Api 15/15, Core 845 bestanden/1 übersprungen, App 432 bestanden/3 übersprungen, Paket-Tests `tests/claude_handoff` 8/8 OK |
| requirements.yaml | 101 IMPLEMENTED, 83 PC_VERIFIED, 25 TESTED, 125 APPROVED_SPEC, 35 IN_PROGRESS, 180 NOT_STARTED (Statuszeilen gezählt) |
| decisions.yaml | endet bei **IMP-97**; IMP-86 REJECTED, IMP-97 UNDECIDED, IMP-95/96 APPROVED „Funktionsliste fehlt im Repo“ |
| 20 Agenten | `.claude/agents/01…20` vorhanden; 12 Skills unter `.claude/skills/` |
| MCP | `.mcp.json` nur `trendo-readonly` (lokaler Read-only-Adapter) – keine getestete Blender-/Unreal-MCP-Anbindung |

Einschränkungen: Einige Tests brechen still ab, wenn ein Programm fehlt (z. B. `W4Studio3DTests.Blender_roundtrip` mit `if (bl is null) return;`, LibreOffice-/Tesseract-/Chrome-abhängige Tests). Ein grüner Lauf beweist dort nichts über Blender, LibreOffice usw.

## 2. Stand je Verbesserung (aus `FUNKTIONEN_98_113_MATRIX.csv`)

| Verb. | Thema | Funktionen | Code + Test (Teilvertrag) | Teilweise | Nur spezifiziert | Nur mit Hardware |
|---|---|---:|---:|---:|---:|---:|
| 98 | TV & IPTV / Zero-Config | 61 | 9 | 46 | 6 | – |
| 99 | REA-Rekonstruktion | 50 | 2 | 35 | 13 | – |
| 100 | Office | 50 | 8 | 38 | 4 | – |
| 101 | Media, Design, Broadcast | 50 | 1 | 15 | 34 | – |
| 102 | CAD, Engineering, Fertigung | 50 | 1 | 28 | 21 | – |
| 103 | Game, 3D, Interactive | 50 | – | 23 | 27 | – |
| 104 | Web, App, Code | 50 | – | 31 | 19 | – |
| 105 | Business, Finance, Shop | 50 | – | 26 | 24 | – |
| 106 | Kommunikation, Zusammenarbeit | 50 | – | 27 | 23 | – |
| 107 | System, Sicherheit, Geräte | 50 | – | 33 | 17 | – |
| 108 | Wissen, Forschung, Lernen | 50 | – | 24 | 26 | – |
| 109 | Robotik, IoT, GIS, Zwilling | 50 | – | 26 | 24 | – |
| 110 | KI-Agenten, autonome Produktion | 50 | – | 27 | 23 | – |
| 111 | Cross-Studio, Plattform | 50 | – | 34 | 16 | – |
| 112 | App-Ökosystem, Plugin-SDK | 50 | – | 25 | 25 | – |
| 113 | VR, AR, Spatial | 50 | – | 14 | 22 | 14 |
| **Summe** | | **811** | **21** | **452** | **324** | **14** |

„Code + Test“ heißt: Für einen Teil der Funktion gibt es Code und einen automatisierten Test, der im Volltest grün lief – **nicht** IMPLEMENTED/TESTED nach Spec und **nicht** PC_VERIFIED. Bei 98 hat die Quelle für die Zero-Config-Anforderungen keine IDs; die Agenten haben TV-01…TV-11 vergeben.

## 3. Wichtige Befunde im heutigen Code (vom Master Director nachgeprüft, wo markiert ✔)

1. ✔ **Xtream-Zugangsdaten im Klartext** – `MainWindow.Tv.cs` `TvAddSource` speichert die get.php-Adresse mit Benutzer und Passwort als `TvSource.Url` in `daten/tv/tv.json` (bestätigt: die lokale Datei enthält ein `password=`). `Secrets.cs`/`ISecretStore` existiert und wird dafür nicht genutzt. Risiko **hoch**.
2. ✔ **glTF-Export ohne Achsenwechsel** – `MeshIO.WriteGlb` rechnet nur cm→m; der Editor arbeitet mit Z oben (`Mesh3D.cs` Kamera `Cross(forward, (0,0,1))`), glTF verlangt Y oben. Modelle dürften in Blender/Unreal gekippt liegen (Import am PC nicht geprüft). Der Rundreisetest nutzt einen symmetrischen Würfel und kann das nicht finden.
3. **Erweiterungen ohne Freigabe** – Installieren/Entfernen/Katalog-Download in `MainWindow.W5Ext*.cs` ohne `ApprovalGate`/Not-Stopp, beliebiger GitHub-Account, kein Offline-Schutz, nicht atomarer Austausch. Positiv: Es wird **kein** fremder Code geladen (kein `Assembly.Load*`/`AssemblyLoadContext` in `src/`).
4. **Entpacken überschreibt still** – `Archives.Unpack` überschreibt vorhandene Dateien, `Archives.Pack` löscht ein vorhandenes Ziel (V107-09 verlangt Konfliktdialog).
5. **Status ohne Beweis** – `StudioRegistry.StatusRules.Tested` akzeptiert PASS/TESTED_BOX ohne Commit, Log und SHA (Kern von V111).
6. **Office ohne Autosave/Migration** – Text, Tabelle, Präsentation sichern nicht automatisch; das gemeinsame Speichern/Wiederherstellen nutzen nur `ProgGame`/`ProgMode`; DOCX/XLSX/PPTX ohne ZIP-Bomb-/XML-Schutz-Tests.
7. **Buchhaltung** – `Ledger.RemoveAt` löscht hart statt zu stornieren, Buchungen ohne ID/Mandant, `Product` mit `double` statt `decimal`, Steuersätze fest codiert.
8. **Mess- und Kalibrierlücken** – `TwinPlus.EnergyKwh/HoursAbove` rechnen über Datenlücken; `Vision.PixelToTable` liefert mm ohne gültige Kalibrierung.
9. **Workflows** – `WorkflowEngine.RunDueAsync` ohne Lease (Doppelstart möglich); Jobs in `tools/trendo_ops.py` ohne Limit/Abbruch/PID.
10. **Ehrlichkeit der Oberfläche** – `MainWindow.Spec04.cs` meldet „läuft flüssig“, obwohl nur ein Budget geprüft und nichts gemessen wurde.
11. **KI-Umschreiben** im Text-Studio ersetzt sofort (mit Rückgängig); 108-49 verlangt Diff-Vorschlag.

## 4. Korrekturen an Agentenaussagen
- Zwei Agenten hielten offen, ob die Python-Tests `tests/claude_handoff` im Volltest liefen. Sie liefen: „Ran 8 tests … OK“ im Volltest vom 30.10.2026.
- Der Volltest lief auf `c062bb3`; HEAD `7e1b863` ändert nur `docs/build-log/…`. Der getestete Code entspricht HEAD.

## 5. Quellen
- Funktionsmatrix: `FUNKTIONEN_98_113_MATRIX.csv` (811 Zeilen, `;`, UTF-8)
- Agentenberichte je Bereich: `agentenberichte/*_zusammenfassung.md`
- Übergabepaket (unverändert, entpackt): `%TEMP%\trendo-uebergabe\TRENDO_OS_GESAMTUEBERGABE_98_BIS_113\`
