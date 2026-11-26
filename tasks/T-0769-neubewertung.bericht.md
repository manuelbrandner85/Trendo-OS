# Bericht T-0769: Neubewertung aller Studios

**Auftrag:** `tasks/T-0769-neubewertung.md` · **Branch:** `claude/T-0769-neubewertung` · **Basis:** main 542dc1ce7 (0.9.110)

## Geänderte Dateien
- `docs/audit/2026-11-26-neubewertung/BERICHT.md`
- `docs/audit/2026-11-26-neubewertung/restliste.md`
- `docs/audit/2026-11-26-neubewertung/fehlende-studios.md`
- `docs/audit/2026-11-26-neubewertung/bilder/` (13 PNG, 1600×1000, zusammen 2,2 MB)
- `tasks/T-0769-neubewertung.md`, `tasks/T-0769-neubewertung.bericht.md`

Kein Produktcode geändert.

## Kommandos
- `dotnet build Trendo.sln -c Release` → 0 Fehler (191 Warnungen); `dotnet build tools/Trendo.Render -c Release` → 0 Fehler
- Bilder: `tools/Trendo.Render/bin/Release/net10.0/Trendo.Render.exe <bild>.png 1600 1000 <seite>` mit eigenem `TRENDO_RENDER_ROOT`
- Gezielte Testklassen (alle mit echten Werkzeugen, wo vorhanden):

| Testklasse | Ergebnis | Dauer | Echte Werkzeuge |
|---|---|---|---|
| `BueroEchtlauf_Ui` | 6/6 bestanden | 101,8 s | LibreOffice, qpdf, Tesseract 5.4.0 |
| `BueroAusbau_Ui` | 12/12 bestanden | 21,1 s | – |
| `T0725_Echtlauf_Ui` | 4/4 bestanden | 44,0 s | LibreOffice, qpdf |
| `VectorStudios_Ui` | 5/5 bestanden | 17,1 s | FFmpeg 9.0.2, Chrome |
| `T0697ComicPodcast_Ui` | 8/8 bestanden | 22,5 s | FFmpeg; Mikrofon und Comic-KI ersetzt |
| `GameFree_Ui` | 5/5 bestanden | 37 s | Chrome, Godot 4.7.2 |
| `T0765_AppBaukasten_Ui` | 3/3 bestanden | 13,5 s | – |
| Core `T0755_Gefahren` + `T0763` | 27/27 bestanden | 29 s | Authenticode, Leistungsindikatoren |

Kein Lauf übersprungen, keiner fehlgeschlagen. Kein Volltest (macht die Hauptsitzung).

## Ergebnis
- **16.11.2026, 83 Studios:** 2 VOLLWERTIG, 78 TEILWEISE, 1 FEHLT, 1 NICHT GETESTET, 1 abgelehnt.
- **26.11.2026, 80 Studios:** 0 VOLLWERTIG, 79 TEILWEISE (davon 5 Grenzfälle), 0 FEHLT, 1 NICHT GETESTET (Konsolenspiele, BLOCKIERT).
- **Warum 0 statt 2:** Heute werden die 6 Kriterien streng angelegt. Die PDF-Werkstatt scheitert nur an K4, „Dateien teilen“ an K4 und K6.
- **Fortschritt:**
  - K1 (Ergebnis aus leerer Vorlage) ist bei 66 von 80 Studios belegt.
  - K5 (Klick bis Datei mit echtem Werkzeug) ist bei 62 von 80 belegt.
  - „Filme machen“ steigt von FEHLT auf TEILWEISE.
- **Engpässe:**
  - K4 (KI im Dokument): nur 5 ✓.
  - K2 (Fremdprogramm): nur 8 ✓.
  - K6 (Bedienung): nur 14 ✓.
- **Fehlende Studios:** 56 von 79 freigegebenen fehlen. Je Kategorie: Business 11, 3D 8, Technik 7, Wissen 10, Entwickeln 12, System 8.

## Befunde nebenbei (nicht behoben, nur gemeldet)
- **Belege:** In `reports/T-0698`, `T-0703`, `T-0707` und `T-0723` stimmen die Ergebnisdateien per SHA-256, einzelne Beschreibungs- und Rohlog-Dateien nicht (u. a. `claude-lauf.md`; `claude-lauf.json` stimmt).
- **Stille Tests:** Rund 30 Stellen kehren ohne Voraussetzung mit `return` zurück oder schlucken Netzfehler (Liste in `BERICHT.md` 3.7). Kein ✓ hängt allein daran, jedes hat einen Rohlog.
- **Katalog:** `docs/programme/katalog.json` führt 97 von 123 Programmen als „Test NOT_RUN“. SYS-04, SYS-07, CORE-04 und X-08 stehen auf `PC_VERIFIED`, obwohl ihr Kernweg ersetzt ist.
- **LibreOffice:** Die Berichte T-0692, T-0694 und T-0753 nennen LibreOffice „nicht installiert“. Es liegt aber im Trendo-Werkzeugordner und lief bei dieser Prüfung.
- **Gestaltung:** mehrere goldene Hauptaktionen in Tagesplaner, KI fragen und Qualität prüfen; der Knopf „Scanner …“ ist nur ein Hinweis.

## Was NICHT geprüft wurde
- **Nur aus Berichten und Rohlogs übernommen:**
  - Echtläufe aus T-0689 bis T-0767 außer den oben selbst gelaufenen
  - harte Abbruchtests (`ProgramCrashTests`, `T0714_HuelleE2Tests`, `T0711_LinkLocksTests`)
- **SHA-256 nur stichprobenhaft nachgerechnet:** T-0691, T-0698, T-0725, T-0735, T-0738, T-0742 und T-0743.
- **Kein echter Claude-Aufruf**, nichts installiert, kein winget.
- **Bilder:** nur 13 Seiten gerendert. Tastenkürzel wurden nirgends als echter Tastendruck geprüft.
- Einzelne Seiten sind nicht gelesen; die Liste steht in `BERICHT.md`, Abschnitt 6.

## Offene Punkte (Entscheidung des Nutzers)
- **Herauslösen oder als erledigt zählen:** Rechnungen & Angebote, Marktanalyse, 3D-Druck vorbereiten, Level-Editor, Webseiten messen.
- **Unklarer Name:** Was ist mit „Mods für eigene Spiele“ gemeint?
- **Kategorien:** Sollen Tagesplaner und Recherche mit Quellen nach KI & Automatik umziehen?
- **Claude-Anmeldung:** eingebettete claude.ai-Anmeldung (`ClaudeWeb.cs:14`) gegen das Verbot in `ClaudeCode.cs:46`. Weiter offen seit 16.11.

## Status
Bewertung fertig, Dokumente im Zweig. Kein Produktcode geändert, kein Merge, kein Release.

## Korrekturrunde nach Gegenprüfung (26.11.2026)
- **Bücher schreiben:** K4 von ◐ auf ✓ (`reports/T-0707`, echte lokale KI, `.tdoc` stimmt per SHA). Damit sind es 5 Grenzfälle und K4 hat 5 ✓.
- **Logos und Animation:** K4-Begründung korrigiert: Die Knöpfe laufen über `AssistFromProgram` ins Dokument. Es bleibt ◐, weil kein Test sie auslöst.
- **Stille Tests:** vollständig aufgelistet und gegen die Urteile geprüft; keine Herabstufung nötig.
- **T-0698:** `claude-lauf.json` stimmt, nur `claude-lauf.md` nicht; weitere SHA-Abweichungen in T-0703, T-0707 und T-0723 nachgetragen.
- **Werkzeuge:** Krita, Inkscape und 7-Zip im Trendo-Werkzeugordner nachgetragen (Künstlerstudio K2, Q3).
- **K4-Regel vereinheitlicht:** Eine KI-Aktion, die nur eine Projektdatei schreibt oder nur etwas anzeigt, zählt ◐. ✗ nur ohne KI-Aktion (jetzt nur noch SPEC-12, gesperrt).
- **Kleinere Korrekturen:**
  - Zahl der Grenzfälle in `fehlende-studios.md`
  - Ablegen-Aussage (echte DragEnter/DragOver/Drop-Ereignisse in Büro-Tests)
  - Kreativ & Medien 13 statt 12 erklärt
  - Schreibweise der Urteile vereinheitlicht
- **Zahlen per Skript neu gezählt:** K1 66/13/1/0, K2 8/61/7/4, K3 39/34/0/7, K4 5/74/1/0, K5 62/16/2/0, K6 14/52/14/0 (✓/◐/✗/–).
