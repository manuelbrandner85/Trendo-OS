# Bericht T-0711: B6 „Programm-Hülle für alle“ – Etappe 1b

**Branch:** `claude/T-0711-huelle-etappe-1b` (vom Integrationszweig `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0711`.
**Grundlage:** T-0708 (`ProgramLink`, Briefe), Umzugsliste `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E1b).

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| 1 Sperre für ProgramLink-Sicherungen | offener Punkt aus T-0708: zwei Fenster konnten dieselbe Brief-Sicherung anbieten (`ProgramLink.Pending` kannte keine Sperre) | `ProgramLink.Locks` (die `ProgramLocks` der Hülle): eigene Notsicherung beim Schreiben gesperrt, `Pending` blendet Sicherungen eines anderen *laufenden* Fensters aus, `Restore` lehnt sie ab, `Saved`/`Reset`/`Discard` geben frei; Sperre eines abgestürzten Prozesses gilt als verwaist. In der App für Briefe und alle neuen Anschlüsse gesetzt (`ProgLocks`) | Core `T0711_LinkLocksTests.Second_window_does_not_offer_held_backup`, `.Close_releases_backup`; UI: jeder Programm-Test öffnet ein zweites Fenster mit demselben Datenordner und prüft, dass es die Karte **nicht** zeigt |
| 2 Allgemeiner Anschluss | Briefe einzeln verdrahtet | `MainWindow.LinkSlots.cs`: `LinkSlot` (Stand lesen/übernehmen, „wert zu sichern“, Titel), Formular-Entwurf je bearbeitetem Element (`LinkForm`), Knöpfe Rückgängig · Wiederholen, Wiederherstellen-Karte, Takt (`SlotsAutosave` aus `LinksAutosave`). Kein neues Speicher-/Rückgängig-System – alles läuft über `ProgramLink`/`ProgramDocument` | alle UI-Tests unten |
| 2 E-Mail „Schreiben“ | Formular nur im Speicher | Entwurf (An/Betreff/Text) angeschlossen; Kopfzeile und Knöpfe der Mail unverändert (T-0709), nur die Zeile Rückgängig · Wiederholen über den Feldern; nach „Im Mailprogramm öffnen“ (.eml jetzt atomar) bzw. Übergabe ans Schreibfenster ist die Sicherung weg | `T0711_HuelleEtappe1b_Ui.Mail_draft` (inkl. echter .eml-Datei, Sicherung weg) |
| 2 Editor der Übersicht | Text nur bis „Speichern“ | alle Reiter (Pfad, Text, Kodierung, BOM, Zeilenende, geändert) ein Stand; Rückgängig ganzer Schritte, Strg+Z im Feld bleibt zeichenweise; Sicherung weg, wenn alle Reiter gespeichert | `Editor_tabs` (Speichern schreibt die echte Textdatei) |
| 2 PDF „Seiten ordnen“ | eigener Stapel `_pdfOrgUndo` ohne Grenze, ohne Wiederholen, ohne Sicherung | Stapel ersetzt durch die Hülle (100 Schritte, **jetzt mit Wiederholen**), Notsicherung, Karte öffnet die PDF wieder im Ordnen-Modus; „Als Kopie speichern“ atomar | `Pdf_organize` (echte PDF, Speichern ergibt 2 Seiten), bestehend `PdfOrganize_Ui` grün |
| 2 Trendo Musik | Eingaben und Notenrolle nur im Speicher, Notenrolle mit eigenem Stapel | zwei Anschlüsse: Eingaben zum Lied (gilt als gespeichert, sobald daraus ein Lied entstand) und Notenrolle (eigener Stapel ersetzt, jetzt mit Wiederholen; MIDI speichern atomar) | `Music_inputs`, `Music_roll` (echte .mid), bestehend `MusikNoten_Ui` u. a. grün |
| 2 Spiele-Werkstatt | Einstellungen/Raster nur bis „Speichern & Vorschau“ | Entwurf je Spielordner, jede Einstellung/jeder Rasterklick ein Schritt; `spiel.json`/`config.js` atomar | `Game_workshop`; Core `Old_files_stay_readable` (alte spiel.json) |
| 2 Roboter-Werkstatt | Bahn nur im Speicher | Ziel + Wegpunkte angeschlossen; Knöpfe in der vorhandenen Zeile (keine zusätzliche Zeile, Arbeitsfläche bleibt gleich hoch); CSV atomar | `Robot_path`, `CORE09_Ui.Arm_plan` grün |
| 2 Recherche | Notiz nur bis „Bericht speichern“, ging beim Neuzeichnen verloren | Notiz angeschlossen, bleibt beim Neuzeichnen stehen; notizen.txt atomar | `Research_note` |
| 2 Wissen / Vorlagen / Skills | Editoren nur bis „Speichern“ | Entwurf je Seite/Vorlage/Skill („neu“ für Neues); Karte öffnet den Editor; wissen.json, Papierkorb, vorlagen.json (auch Skills) atomar; Vorlagen-Editor bekommt „Abbrechen“ (verwirft bewusst) | `Wiki_editor`, `Prompt_editor`, `Skill_editor`; Core `Old_files_stay_readable` (alte wissen.json, vorlagen.json) |
| 2 Sauberer Nachbau | Beschreibung nur bis „Beschreibung speichern“ | Entwurf je Projekt; BESCHREIBUNG.md atomar | `Cleanroom_spec` |
| 3 Absturz | – | CrashProbe-Modus `link-editor` (Stand des Editors, mit Sperre) | Core `Kill_while_autosaving_editor_link`: 12 harte Abbrüche, 10–11 davon mitten im Schreiben, nie eine halbe Sicherung, höchstens der laufende Schritt verloren, liegengebliebene Sperre gilt als verwaist |

Alle elf Programme: nichts entfällt; vorhandene Knöpfe behalten Namen und Wirkung (`pdf-org-undo`, `ms-roll-undo` heißen weiter so, Funktions-Baseline grün). Kein Programm unverhältnismäßig groß – nicht abgegrenzt. Bewusst nicht angeschlossen: PDF-Formularwerte (gehören zum Formular-Ausfüllen, E2), DAW/Spuren der Musik (ungeprüft, wie in der Liste).

## Rot-Grün
Neue Tests nutzen `ProgramLink.Locks`, `LinkSlots`, `MailLink`, `EditorLink` … und die Knöpfe `*-undo/-redo/-recovery/-restore-0` – auf dem alten Stand nicht übersetzbar bzw. nicht vorhanden (rot). Zwischenstände waren rot und sind behoben: Undo war direkt nach dem Öffnen an (Musik, Spiele – Neuzeichnen löste „getippt“ aus, jetzt nur bei echtem Unterschied), Roboter-Zeile machte `CORE09_Ui.Arm_plan` rot (jetzt in vorhandener Zeile), Funktions-Baseline vermisste `pdf-org-undo`/`ms-roll-undo` (Namen wieder wörtlich im Quelltext).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- App, ein Lauf mit 124 angegebenen Klassen (alle Klassen, die die berührten Seiten/Knöpfe verwenden, dazu UebersichtEinfach_Ui, ProgramHeadDesign_Ui, DesignPaket3_Ui, FreigabenKernUi, T0708_HuelleEtappe1_Ui, EveryButton_*, ProgramShellUiTests, OfficeRecoveryUiTests, LetterOffice_Ui): **481 Tests, 3 rot, 1 übersprungen** (`reports/T-0711/app-tests.log`). Danach Nachlauf mit Endstand (13 Klassen inkl. T0711, CORE09, PdfOrganize, MusikNoten und den geforderten): **58 Tests, 1 rot, 1 übersprungen** (`app-tests-nachlauf.log`). Die verbliebenen Roten:
  - `CORE09_Ui.Image_check` (Bild prüfen) – auch rot mit der unveränderten `MainWindow.Core09.cs` (Gegenprobe gefahren); nicht aus diesem Paket, nicht behoben.
  - `BueroEchtlauf_Ui.Deck_real` – LibreOffice-Skript scheitert beim Anlegen der Fremddatei (Präsentation, nicht berührt).
  - `CORE09_Ui.Arm_plan` war im ersten Lauf rot (meine zusätzliche Zeile), im Nachlauf grün.
- Core gezielt (T0711, T0708, ProgramCrash, ProgramDocument, ProgramLock, GameStudio, Knowledge, Prompt, Skill, Research, CleanRoom, Mail, PianoRoll, AtomicFile): **196 Tests, 194 grün** (`core-tests.log`). Rot: `SYS09_Mail.Draft` – prüft nur `ToEml`/`ParseEml` im Speicher (Zeilenende `\n` statt `\r\n`), von diesem Paket nicht berührt; `T0708_ProgramLinkTests.Kill_while_autosaving_link` – Zeitbedingung „mindestens 8 von 16 Abbrüchen mitten im Schreiben“ im parallelen Lauf 7/16, einzeln zweimal grün (10/16, 9/16); Modus `link` unverändert.
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests OK** (`baseline.log`).
- Bilder (1600×1000, angesehen): `post.png`, `editor.png`, `roboter.png`, `recherche.png` – Rückgängig · Wiederholen nicht golden, eine goldene Hauptaktion bleibt. Die Karte selbst ist nur im UI-Test geprüft (der Render-Lauf leert seinen Datenordner).
- Prüfsummen `reports/T-0711/SHA256SUMS.txt`. Kein Volltest.

## Abnahmekriterien (für die Programme dieser Etappe)
1. Aus leerer Vorlage ein echtes Ergebnis: **NICHT GETESTET** als eigenes Ziel (nebenbei: echte .eml, .txt, .pdf, .mid, BESCHREIBUNG.md, Wissens-Seite, Vorlage).
2. Fremddatei importieren/exportieren im Fremdprogramm: **NICHT GETESTET** (nicht Gegenstand).
3. Prozessabbruch verliert höchstens 30 s: **VOLLWERTIG** für den Sicherungsweg aller elf (Takt 20 s + Tipp-Pause 1 s; echter Abbruchtest `link-editor` mit Sperre; je Programm UI-Test „Fenster ohne Speichern geschlossen → Karte → Zurückholen“). Ein harter Abbruch des App-Prozesses selbst ist nicht gefahren.
4. Claude/KI-Änderung rückgängig: **TEILWEISE** – KI-Änderungen, die über die Seite laufen (Mail „Freundlicher formulieren“, Liedtext mit KI), sind beim Neuzeichnen ein Schritt; nicht mit echter KI geprüft.
5. Klick bis zur echten Datei: **TEILWEISE** – Mail (.eml), Editor (.txt), PDF (Kopie), Musik (.mid), Wissen/Vorlage/Nachbau über die Knöpfe; ohne Fremdprogramm.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Knöpfe in allen Stufen sichtbar; keine neuen Kürzel (Strg+Z im Feld bleibt Feld-Rückgängig).

## Was NICHT geprüft wurde
- Kein Volltest; kein harter Abbruch der App selbst; kein echter KI-Lauf.
- Passwortgeschützte PDF beim Zurückholen (öffnet ohne Passwort – dann gesperrt, Seitenfolge bleibt im Speicher).
- Karte der Notenrolle bei kleinem Fenster (1440×900: Klick auf „Wiederherstellen“ traf im Test eine Überlagerung; bei 1600×1100 grün). Lage prüfen.

## Offene Punkte
- E2 als nächste Etappe (Reihenfolge unverändert): `OfficeAutosave` (Text, Tabelle, Präsentation, CAD, 3D) auf `ProgramLink` umziehen, PDF-Formularwerte, Briefköpfe/Verkaufen/Vorhaben/Adressbuch atomar → E3 → E4 → E5 (siehe `huelle-umzug.md` Abschnitt 4).
- Vorbestehend rot: `CORE09_Ui.Image_check`, `SYS09_Mail.Draft`, `BueroEchtlauf_Ui.Deck_real`; `T0708…Kill_while_autosaving_link` zeitempfindlich bei Last.

## Berührte Dateien
- neu: `src/Trendo.App/MainWindow.LinkSlots.cs`, `tests/Trendo.Core.Tests/T0711_LinkLocksTests.cs`, `tests/Trendo.App.Tests/T0711HuelleEtappe1bUiTests.cs`, `tasks/T-0711-*`, `reports/T-0711/*`
- geändert Core: `Programs/ProgramLink.cs` (Locks), `GameStudio.cs`, `W2/Knowledge.cs`, `W2/KnowledgeWiki.cs`, `W2/Prompts.cs`, `W3/Research.cs`, `W3/Mail.cs` (atomar)
- geändert App: `MainWindow.ProgramLinks.cs` (Briefe mit Sperre, Takt ruft `SlotsAutosave`), `MainWindow.Mail.cs` (nur Entwurf), `MainWindow.Helfer.cs`, `MainWindow.Pdf.cs`, `MainWindow.PdfOrganize.cs`, `MainWindow.MusikStudio.cs`, `MainWindow.MusikNoten.cs`, `MainWindow.MusikTexte.cs`, `MainWindow.W4Game.cs`, `MainWindow.Core09.cs`, `MainWindow.Research.cs`, `MainWindow.Wissen.cs`, `MainWindow.Prompts.cs`, `MainWindow.Skills.cs`, `MainWindow.W5CleanRoom.cs`
- geändert sonst: `tests/Trendo.CrashProbe/Program.cs` (Modus `link-editor`), `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`
- **nicht** angefasst: `MainWindow.ProgramShell.cs`, ClaudeBridge/PromptAction (B5), Kopfzeile/Knöpfe der Mail (T-0709).

**Status:** Etappe 1b umgesetzt und gezielt geprüft; nicht im Volltest.
