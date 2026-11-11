# Bericht T-0610: Etappe „Kreativ & KI“ – Teil 1

**Zweig:** `claude/T-0610-kreativ-ki` (Worktree `studios-aufraeumen`), Basis `main` 9dda6f3d3. Noch nicht auf `main`.

## Inhalt

Vier neue Studios, jedes mit Kernbaustein ohne Internet, Prompt-Adapter ohne Nebenwirkung, eigenem Programm (Vorlagen, Menüband, Exporte, Hilfe) und allen Einträgen (Katalog, Symbol, Profil, Team, Wissen, Vorschläge, Anforderung, Studio-Bild):

- **Texte zusammenfassen** (STUDIO-SUMMARY-01, KI & Automatik): Kernsätze nach Worthäufigkeit in Originalreihenfolge, Länge kurz/mittel/lang, Stichwörter, Lesezeit, Export Markdown/Text, „Mit Claude ausführlicher“ über den Zugang der Person.
- **Logos & Marken** (STUDIO-LOGO-01): fünf Formen, fünf Schriften, Vorschau auf hellem und dunklem Grund und in kleinen Größen, WCAG-Kontrastprüfung, Export SVG und Markenpaket.
- **Podcast aufnehmen** (STUDIO-PODCAST-01): Kapitel „mm:ss Titel“ mit Fehlerhinweisen und Player-Vorschau, Lautheit -16 LUFS mit FFmpeg (neue MP3 mit Kapiteln, Original bleibt), Shownotes, Kapiteldatei, RSS-Feed nur mit https. Veröffentlicht nichts.
- **Comics & Storyboards** (STUDIO-COMIC-01): vier Seitenaufteilungen, Sprechblasen, Bildunterschriften, eigene Bilder, Export SVG (Bilder eingebettet) und Storyboard-Liste.

Dazu im Render-Werkzeug `TRENDO_PROG_TPL=Schlüssel:Vorlage`, um beliebige Programme mit Vorlage als Bild zu prüfen.

## Geänderte Dateien

Neu: `src/Trendo.Core/Kreativ/KreativKits.cs`, `src/Trendo.Core/PromptAction/KreativPromptAdapters.cs`, `src/Trendo.App/MainWindow.KreativStudios.cs`, `tests/Trendo.Core.Tests/KreativKitsTests.cs`, `tests/Trendo.App.Tests/KreativStudiosUiTests.cs`, vier Studio-Bilder, vier Ordner unter `docs/programme/`.
Geändert: StudioCatalog, StudioIcons, PromptChips, W6 (Init), CapabilityCatalog, IntelligencePromptModel, StudioKnowledge, StudioProfiles, StudioTeams, `requirements.yaml` (vier Einträge nach Freigabe vom 09.11.2026), `tools/studio_bilder.py`, `tools/Trendo.Render/Program.cs`, Katalogmatrix und Programmkatalog (neu erzeugt; nur `katalog.json`, `KATALOG.md` und die neuen Ordner übernommen, die übrigen 1089 Dateien hätten nur Stand und Messzeiten geändert), Zählstände in vier Tests (Studios 59→63, Adapter 86→90, Teilweise 56→60, Übersicht 37→41).

## Testergebnis

Volltest `dotnet test Trendo.sln -c Release` und Paket-Tests, Exitcode 0:
Api 16 bestanden; Core 2223 bestanden, 5 übersprungen; App 1207 bestanden, 4 übersprungen; Paket-Tests 11 OK. Auszug in `reports/volltest-T-0610/`.

Erster Volltest: 1 Fehler (Programmkatalog nicht neu erzeugt), behoben. Zweiter Lauf durch Sitzungsneustart abgebrochen, dritter Lauf vollständig grün.

## Sichtprüfung

Alle vier Studios mit Vorlage als Bild gerendert (1600 × 1000). Die Logo-Vorschau war zu groß und lief über den Rand; jetzt auf 170 px Höhe begrenzt.

## Nicht geprüft

- Lautheit mit echtem FFmpeg und echter Aufnahme (nur Argumente und Kapiteldatei getestet).
- Feed in einem echten Podcast-Player.
- „Mit Claude ausführlicher“ und „Geschichte mit Claude“ mit echter Anmeldung.
- Unabhängige Prüfung durch den QA-Agenten steht noch aus.

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
