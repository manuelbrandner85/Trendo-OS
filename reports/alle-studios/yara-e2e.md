# Auf Schadsoftware-Merkmale prüfen mit yara-x – 08.11.2026

Alle Studios, Runde 8, SPEC-12 „System auf Gefahren prüfen“.

## Bausteine
- **yara-x 1.22.0** (VirusTotal, BSD-3-Clause), das Rust-Einzelprogramm `yr`.
  - Fassung vom 20.10.2026. Die neuere 1.23.0 war erst drei Tage alt.
  - Archiv `yara-x-v1.22.0-x86_64-pc-windows-msvc.zip`, SHA-256 `eac36a58…1f2e`. Die Prüfsumme stammt aus dem Feld „digest“ des GitHub-Release.
  - Ein eigener Download scheiterte am 08.11. zweimal an einem 504-Fehler von GitHub. Die 92 Byte große Fehlerseite fiel sofort durch die Prüfsumme auf. Trendo prüft die Prüfsumme beim Einrichten ohnehin.
- **YARA-Regeln von ReversingLabs** (MIT), fester Stand `e0a0be54` (03.11.2025).
  - Archiv über codeload, SHA-256 `8572f9ef…efd0`, ermittelt beim echten Download.
  - **310 Regeln** in 10 Gruppen: Erpressersoftware, Trojaner, Hintertüren, Nachlader, Datendiebe, Rootkits, Viren, Exploits, möglicherweise unerwünschte Programme, verdächtige Zertifikate.
- **Rust:** Rust läuft hier nur als fertiges, fremdes Programm (`yr`). Es gibt keinen eigenen Rust-Code; die Anbindung ist C#, wie bei allen anderen Werkzeugen.

## Was die Nutzerin oder der Nutzer sieht
„System auf Gefahren prüfen“ → Werkzeug-Karte **„Auf Schadsoftware-Merkmale prüfen“**: Datei oder Ordner wählen, zum Beispiel „Downloads“, dann **Prüfen**.
- **Befunde** stehen auf Deutsch, die schwersten zuerst, z. B. „rechnung.exe → Erpressersoftware WannaCry“.
- **Grundsatz:** **Nichts wird gelöscht oder verschoben.** Quarantäne bleibt eine bewusste Entscheidung unter „Sicher prüfen“.
- **Ohne Treffer** steht dort ausdrücklich: „kein Freibrief – nur bekannte Muster“.

## Umsetzung
- **Befehl:** `yr scan -o ndjson -m -g --recursive --disable-console-logs -z 209715200 -a 30 <Regelordner> <Ziel>`.
  - Diese Optionen und das NDJSON-Format (`{"path", "rules":[{"identifier","meta","tags"}]}`) sind **aus dem Quellcode von yr 1.22.0 gelesen** (`cli/src/commands/scan.rs`, `help.rs`), nicht geraten.
  - `--recursive` braucht `=`, wenn eine Tiefe angegeben wird. Trendo gibt keine Tiefe an.
- **Regeln:** Sie werden **flach** in den Regelordner übernommen, nur `*.yara` direkt unter `yara/<gruppe>/` und mit sicheren Dateinamen. Pfade aus dem Archiv werden nie verwendet, damit nichts aus dem Ordner ausbrechen kann.
- **Einrichten:** Aus dem Programm-Archiv wird nur `yr.exe` übernommen.
- **Auswertung:** Die Meta-Felder der Regeln `tc_detection_type`, `tc_detection_name` und `tc_detection_factor` (Schwere 1–5) ergeben den deutschen Befund. Fehlen sie, gilt `category`.

## Tests
- **`YaraScanTests`** (6 Tests, grün):
  - NDJSON lesen
  - Zusammenfassung
  - Argumente
  - flaches Entpacken mit Schutz vor Pfadausbruch
  - Download nur mit Prüfsumme; nur `yr.exe` wird übernommen
  - verständliche Fehlermeldungen
- **`YaraRulesRealTests`** (1 Echtdaten-Test, grün), mit dem **echten Regel-Archiv**:
  - Die Prüfsumme stimmt.
  - Alle 310 Regeln werden übernommen.
  - 309 haben eine Erkennungsart; eine Zertifikats-Regel hat nur `category`, was den Rückfall im Parser bestätigt.
- **`ForensikYara_Ui`** (1 Oberflächentest, grün):
  - Ohne Einrichtung erscheint der Einrichten-Knopf.
  - Ein Treffer erscheint als Warnung mit deutscher Art; die geprüfte Datei bleibt unverändert.
  - Ohne Treffer kommt der Hinweis „kein Freibrief“; ein fehlender Ordner wird verständlich gemeldet.

## Nicht geprüft (NOT RUN)
- **Echter Lauf von `yr`** auf Testdateien. Das wäre ein heruntergeladenes Programm und braucht die Freigabe des Nutzers. Dabei würde auch das Programm-Archiv selbst noch einmal gegen seine Prüfsumme geprüft.
- Laufzeit auf großen Ordnern.
