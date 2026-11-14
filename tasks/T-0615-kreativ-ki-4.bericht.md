# Bericht T-0615: Etappe „Kreativ & KI“ – Teil 4

**Zweig:** `claude/T-0615-kreativ-ki-4` (eigener Worktree), Basis `origin/main` cce5c6261. Nicht auf `main`, kein Merge, kein Release.

## Inhalt

- **Code mit KI** (STUDIO-CODEAI-01, `code-ki-programm`): Projektordner wählen, Dateibaum (Projektbaum des Code-Editors, verknüpfte Ordner bleiben draußen), Datei im AvaloniaEdit-Editor ansehen und speichern (überschreibt eine inzwischen geänderte Datei erst nach Rückfrage, ungespeicherte Änderungen gehen nie still verloren), „Im Code-Editor öffnen“. Claude erklärt, ändert (vorher automatisch Schnappschuss) oder schreibt Tests – über `ClaudeFromStudioBar` mit Datei-Ausschnitt. Projektart (.NET, npm, Python, Cargo, Go) mit passendem Testbefehl; ausgeführt erst nach dem zweiten Klick, im Projektordner, mit Zeitlimit; Ausgabe in der Arbeitsfläche. Schnappschuss mit SHA-256, Liste „seit Schnappschuss geändert“, eigener LCS-Zeilen-Diff, Export als `.patch` (git-tauglich, `/dev/null` bei neu/gelöscht) und Bericht.
- **Bilder erkennen** (STUDIO-VISION-01, `bilder-erkennen-programm`): lokal, ohne Internet. Format aus den ersten Bytes, Größe, EXIF (Aufnahmedatum, Kamera, Belichtung, Blende, ISO, Brennweite; Parser prüft jede Länge), Hauptfarben (k-means++ mit festem Startwert), Helligkeit/Belichtung, Schärfe (Laplace-Varianz, Grenze einstellbar). Ordner prüfen listet unscharfe, zu dunkle und zu helle Fotos; unlesbare werden gezählt. Text im Bild mit Tesseract, wenn eingerichtet – sonst klarer Hinweis mit Weg zu den Zusatzprogrammen. „Mit Claude beschreiben“ hängt nur das geprüfte Bild über den Anhang-Weg der Claude-Leiste an. Bilder über 100 Megapixel werden vor dem Dekodieren abgelehnt. Export Markdown und CSV (Semikolon, BOM, Formel-Schutz).
- **Recherche mit Quellen** (STUDIO-CITE-01, `quellen-programm`): Quellenverwaltung für Buch, Artikel, Webseite mit Zitaten je Quelle (Seite, Notiz). Zitierstile APA 7, Harvard, DIN-ISO-690-nah, Kurzbelege im Text, Literaturverzeichnis deutsch sortiert, fehlende Pflichtfelder je Stil (⚠ an der Quelle), Doppelte über DOI, normalisierte Adresse und Titel. Export Markdown, BibTeX, Text; BibTeX-Import robust (@string, #, LaTeX-Akzente, kaputte Einträge werden gemeldet und übersprungen). Übernahme gelesener Seiten und Markierungen aus „Sicher recherchieren“ (SYS-08, `ResearchDesk`), „Im Recherche-Studio lesen“. „Mit Claude prüfen“: Belegt die Quelle die Aussage?
- **Tagesplaner** (STUDIO-DAYPLAN-01, `tagesplaner-programm`): Aufgaben mit Dauer, Wichtigkeit, Frist (auch als Text „Angebot schreiben, 90 min, hoch, bis 20.11.“), feste Termine, Termine des Tages und offene Aufgaben aus dem eigenen Kalender (SYS-09). Tag planen: Zeitblöcke zwischen Arbeitsbeginn und -ende um Termine herum, nach Frist und Wichtigkeit, Lücken füllen, Pause nach einstellbarer Arbeitszeit; was nicht passt → „Morgen“. Abhaken, „Unerledigtes auf morgen“. Export als Kalenderdatei (RFC 5545: CRLF, Faltung auf 75 Byte ohne zerschnittene Zeichen, Escape) und Text.
- Adapter für die KI-Zeile (je Studio ohne Nebenwirkung): Projekt untersuchen, Bildfakten, Quelle zitieren, Tag planen.
- Pflicht-Stellen: StudioCatalog, StudioIcons, CapabilityCatalog, StudioProfiles (mit Lieferformat), StudioTeams, StudioKnowledge, Prompt-Chips, `requirements.yaml`, Adapterliste, Programmkatalog (`docs/programme`), Fähigkeitsmatrix. Einträge stehen jeweils am Ende der Kategorie/Liste (Teil 3 läuft parallel).
- `Cli.Run`: wartet nach Prozessende höchstens 10 s auf die Ausgabe, damit das Zeitlimit auch greift, wenn ein Kindprozess die Ausgabe offen hält.

## Unabhängige Prüfung (Subagent, nur lesend) – behoben

- hoch: Speichern konnte eine inzwischen (z. B. von Claude) geänderte Datei still überschreiben → Prüfsummen-Abgleich, zweiter Klick nötig.
- mittel: Neuladen/Ordnerwechsel verwarf Getipptes → erst nach zweitem Klick.
- mittel: Zeitlimit griff nicht bei offen gehaltener Ausgabe → `Cli.Run` siehe oben.
- mittel: Schnappschuss las riesige Dateien und die Schnappschussdatei bei jedem Neuzeichnen → Dateien über 64 MB über Größe/Zeit, Schnappschuss zwischengespeichert.
- mittel: Bild mit riesigen Maßen konnte den Speicher sprengen → Größenprüfung vor dem Dekodieren.
- mittel: Doppelte-Suche quadratisch mit teurer Normalisierung → Schlüssel einmal je Quelle, Gruppen.
- mittel: doppelte Aufgaben-Kennungen in fremder Datei ließen den Tagesplaner abstürzen → bereinigt.
- mittel/niedrig: verknüpfte Ordner führten aus dem Projekt hinaus; fremdes Dokument konnte beliebige Datei an Claude hängen; Import brach bei 5000 Einträgen still ab; `.patch` für neue/gelöschte Dateien nicht anwendbar → alle behoben.
- Offen (niedrig): böswillige BibTeX-Datei mit vielen offenen Klammern kann den Import länger aufhalten (höchstens 5 MB).

## Testergebnis (gezielt, kein Volltest)

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release`: 2467 bestanden, 5 übersprungen, 0 Fehler (enthält 16 neue Tests `KreativKits4Tests`, QA-015, PromptAction, Katalogmatrix).
- App-Testklassen, je eigener Prozess: KreativStudios4_Ui 5/5, PromptBarTests 6/6, StudioFolds_Ui 3/3, ProgramCatalog_Ui 1/1 (nach Neubau), StudioBilder 1/1, StudioIconTests 4/4, StudioHeadingTests 2/2, StudiosAlle_Ui 93/93, StartMenu_StudioFenster 5/5, DesignHonesty 5/5, QA004_UiApproval 3/3, QA019_OfflineUi 1/1, Modern_Ui 13/13, ClaudeEverywhere_Ui 2/2, StudioDepsUi 3/3, KreativStudios2_Ui 5/5, KreativStudios_Ui 5/5, Sys13_Qualitaet_Ui 4/4, StudiosTechnikWissen_Ui 44/44, StudiosBueroGeld_Ui 36/36, W1_Search_Access 2/2, ComponentsUi 1/1, EveryButton_Alltag 1/1, EveryButton_AllPages 0/1 (1 übersprungen, wie vorgesehen).
- StudiosKreativ3D_Ui: 15 von 30 rot, wenn die Klasse allein läuft – erster Fehler `Knowledge_complete(CORE-08)`: `MainWindow.Programs` ist statisch und erst nach einem Fensterstart gefüllt; danach Folgefehler „different thread“. Betrifft nur CORE-08 und hängt von der Testreihenfolge ab, nicht von T-0615 (im Volltest der Hauptsitzung prüfen).
- Mehrere App-Klassen in einem Prozess ergeben viele „different thread“-Folgefehler; deshalb je Klasse einzeln gelaufen.

## Nicht geprüft

- Echter Testlauf mit dotnet/npm/python/cargo/go über die Oberfläche (im Test ersetzt).
- Texterkennung mit echtem Tesseract; Claude-Antworten mit echtem Zugang.
- Kalenderdatei in Outlook/Thunderbird (geprüft mit Ical.Net und dem eigenen Kalender-Import).
- BibTeX in Zotero/biber.

## Hinweis zum Zusammenführen mit Teil 3 (T-0614)

Gleiche Zeilen ändern: `MainWindow.W6.cs` (Init-Aufruf), Zähl-Tests (Tests.cs QA-015, PromptBarTests, PromptActionTests, StudioFoldsUiTests: hier jeweils +4), Programmkatalog und Matrix (neu erzeugen nach dem Zusammenführen).

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
