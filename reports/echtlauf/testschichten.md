# Testschichten – was „getestet“ wirklich heißt

Erzeugt von `python tools/echtlauf/testschichten.py` aus dem Quelltext der Tests (keine Handarbeit, kein Testlauf). Was auf diesem PC wirklich mit dem Fremdwerkzeug lief, belegt die Echtlauf-Station unter `reports/echtlauf/<datum>/`.

## Schichten

| Schicht | Bedeutung | Tests |
|---|---|---|
| intern | prüft nur Trendos eigenen Code (Rechnen, Dateiformate, Oberfläche ohne Fremdprogramm) | 2716 |
| ersetzt | mindestens ein Ersatz-Haken (`*Override`) ist gesetzt – das eigentliche Werkzeug (Claude, FFmpeg-Lauf, Netz, Installation …) läuft **nicht**; kein Ende-zu-Ende-Nachweis | 419 |
| echt | braucht ein echtes Fremdwerkzeug (Attribut wie `NeedsFact`/`BlenderFact` oder Suche im Test); fehlt es, wird der Test sichtbar übersprungen | 110 |
| anforderung | läuft nur mit gesetzter Umgebungsvariable (z. B. `TRENDO_PC_LAUF`, `TRENDO_ACE_LIVE`), sonst übersprungen | 23 |
| fest-uebersprungen | `Skip = "…"` ohne Bedingung | 0 |
| **gesamt** | | **3268** |

Ersatz-Haken im Code: **239** (`public … *Override { get; set; }` in `src/`); in Tests gesetzt: 226; Zuweisungen in Tests (je Test gezählt): 820.

Grenzen der Auswertung: Ein Test zählt als „ersetzt“, wenn in seinem Abschnitt oder im Kopf der Testdatei ein Haken gesetzt wird (Hilfsmethoden zwischen zwei Tests werden dem vorherigen Test zugeschlagen). „echt“ heißt: der Test benutzt das Werkzeug, wenn es da ist – ob es auf einem bestimmten PC lief, steht nur im Lauf-Beleg. Die Zuordnung zu Programmen kommt aus `docs/programme/katalog.json` (Feld `Tests`).

## Je Programm

| Programm | Studio | Tests | intern | ersetzt | echt | Anforderung | fest übersprungen | häufigste Haken |
|---|---|---|---|---|---|---|---|---|
| Alle Studios (`studios`) | CORE-01 | 19 | 18 | 1 | 0 | 0 | 0 | `ClaudeRunOverride` ×1, `ClaudeStatusOverride` ×1 |
| Dateien (`dateien`) | CORE-01 | 10 | 10 | 0 | 0 | 0 | 0 | – |
| Desktop-Symbole (`desktop-symbole`) | CORE-01 | 10 | 10 | 0 | 0 | 0 | 0 | – |
| Ersteinrichtung (`einrichtung`) | CORE-01 | 13 | 11 | 2 | 0 | 0 | 0 | `PickOverride` ×1, `SwitchPersonOverride` ×1, `WindowsConfirmOverride` ×1 |
| Projekte (`projects`) | CORE-01 | 17 | 16 | 1 | 0 | 0 | 0 | `PickOverride` ×1 |
| Startbildschirm (`start`) | CORE-01 | 34 | 31 | 3 | 0 | 0 | 0 | `BrowsersOverride` ×1, `WebAppLaunchOverride` ×1, `ClaudeOpenOverride` ×1 |
| KI (`ki`) | CORE-02 | 8 | 7 | 1 | 0 | 0 | 0 | `KiBackendsOverride` ×1 |
| KI fragen (`ki-fragen`) | CORE-02 | 9 | 4 | 5 | 0 | 0 | 0 | `KiBackendsOverride` ×4, `ClaudeStatusOverride` ×1 |
| Mit Claude verbinden (`claude-verbinden`) | CORE-02 | 5 | 4 | 1 | 0 | 0 | 0 | `KiBackendsOverride` ×1 |
| Online-Dienste (`keys`) | CORE-02 | 7 | 4 | 3 | 0 | 0 | 0 | `KeyValidatorOverride` ×2, `KiBackendsOverride` ×1 |
| Verbindungen (`verbindungen`) | CORE-02 | 6 | 4 | 2 | 0 | 0 | 0 | `ClaudeHomeOverride` ×1, `ClaudeRunOverride` ×1, `ClaudeStatusOverride` ×1 |
| KI-Rollen und Teams (`ki-rollen`) | CORE-03 | 5 | 4 | 1 | 0 | 0 | 0 | `KiBackendsOverride` ×1 |
| Geräteverbund (PC, Pi, Android) (`verbund`) | CORE-04 | 14 | 14 | 0 | 0 | 0 | 0 | – |
| 3D (`3d`) | CORE-05 | 47 | 37 | 8 | 2 | 0 | 0 | `AssimpDllOverride` ×1, `AssimpRunOverride` ×1, `DiscoverOverride` ×1 |
| Trendo 3D-Druck (`3d-druck`) | CORE-05 | 30 | 24 | 4 | 2 | 0 | 0 | `DiscoverOverride` ×1, `SliceOverride` ×1, `ClaudeRunOverride` ×1 |
| Trendo CAD (`cad`) | CORE-05 | 31 | 25 | 4 | 2 | 0 | 0 | `DiscoverOverride` ×1, `SliceOverride` ×1, `ClaudeRunOverride` ×1 |
| Spiele bauen (`spiele-programm`) | CORE-06 | 98 | 60 | 34 | 4 | 0 | 0 | `GamePromptOverride` ×16, `EngineProbeOverride` ×8, `KiToolOverride` ×3 |
| Werkstatt (`spiele`) | CORE-06 | 35 | 23 | 8 | 4 | 0 | 0 | `ClaudeRunOverride` ×4, `ClaudeStatusOverride` ×4, `PickOverride` ×2 |
| Webseite bauen (`dev`) | CORE-07 | 57 | 13 | 42 | 2 | 0 | 0 | `ClaudeRunOverride` ×22, `ClaudeStatusOverride` ×22, `WebDesignKiOverride` ×11 |
| Audio bearbeiten (`ton`) | CORE-08 | 20 | 12 | 5 | 3 | 0 | 0 | `MicRecordOverride` ×2, `AudioOverride` ×1, `AuPluginRunOverride` ×1 |
| Bildbearbeitung (`bild`) | CORE-08 | 13 | 10 | 0 | 3 | 0 | 0 | – |
| Medien bearbeiten (`kreativ-medien`) | CORE-08 | 9 | 6 | 0 | 3 | 0 | 0 | – |
| Videoschnitt (`schnitt`) | CORE-08 | 38 | 16 | 19 | 3 | 0 | 0 | `FfmpegRunOverride` ×9, `ExportAsyncOverride` ×5, `EncodersOverride` ×2 |
| Kameras & Roboter steuern (`roboter`) | CORE-09 | 28 | 27 | 1 | 0 | 0 | 0 | `ObjectsRunOverride` ×1 |
| Wissen für die KI (`ki-wissen`) | CORE-10 | 9 | 5 | 4 | 0 | 0 | 0 | `KiBackendsOverride` ×2, `DocsRunOverride` ×1, `WeekSummaryOverride` ×1 |
| Freigaben (`approvals`) | CORE-11 | 9 | 8 | 1 | 0 | 0 | 0 | `ClaudeConsentOverride` ×1 |
| Schutz & Datenschutz (`schutz`) | CORE-11 | 8 | 6 | 2 | 0 | 0 | 0 | `AppBackendOverride` ×1, `ResticDirOverride` ×1, `ResticRunOverride` ×1 |
| Sicherung (`sicherung`) | CORE-11 | 9 | 9 | 0 | 0 | 0 | 0 | – |
| KI-Vorlagen (`ki-vorlagen`) | CORE-12 | 5 | 3 | 2 | 0 | 0 | 0 | `KiBackendsOverride` ×2 |
| Skills (`ki-skills`) | CORE-12 | 7 | 5 | 2 | 0 | 0 | 0 | `GitHubHandlerOverride` ×1, `KiBackendsOverride` ×1 |
| Grundriss & Wohnen (`architektur`) | SPEC-01 | 25 | 23 | 2 | 0 | 0 | 0 | `IfcRunOverride` ×2, `IfcViewScriptOverride` ×1, `PickOverride` ×1 |
| Schaltung & Simulation (`elektronik`) | SPEC-02 | 17 | 17 | 0 | 0 | 0 | 0 | – |
| Tragwerk & Mechanik (`technik`) | SPEC-03 | 20 | 17 | 3 | 0 | 0 | 0 | `ClaudeRunOverride` ×2, `ClaudeStatusOverride` ×2, `ClaudeConsentOverride` ×1 |
| VR & AR gestalten (`xr`) | SPEC-04 | 19 | 17 | 2 | 0 | 0 | 0 | `GltfRunOverride` ×1, `GltfTransformOverride` ×1, `GltfValidatorOverride` ×1 |
| Messdaten & Auswertung (`wissenschaft`) | SPEC-05 | 20 | 19 | 1 | 0 | 0 | 0 | `PickOverride` ×1 |
| Lernen mit Karteikarten (`lernen`) | SPEC-06 | 15 | 15 | 0 | 0 | 0 | 0 | – |
| Kleidung & Produkte (`mode-programm`) | SPEC-08 | 39 | 34 | 5 | 0 | 0 | 0 | `PickOverride` ×2, `GarmentRunOverride` ×2, `SewingDirOverride` ×1 |
| Aus Fotos (`scan3d`) | SPEC-09 | 30 | 25 | 5 | 0 | 0 | 0 | `ScanToolOverride` ×3, `Open3dRunOverride` ×1, `PickOverride` ×1 |
| Punktwolke (`punktwolke`) | SPEC-09 | 27 | 24 | 3 | 0 | 0 | 0 | `ScanToolOverride` ×2, `PickOverride` ×1 |
| Geräte überwachen (`zwilling`) | SPEC-10 | 17 | 17 | 0 | 0 | 0 | 0 | – |
| Geld (`finanzen`) | SPEC-11 | 12 | 12 | 0 | 0 | 0 | 0 | – |
| Sicherheits-Werkzeuge & Beweise (`forensik`) | SPEC-12 | 5 | 3 | 2 | 0 | 0 | 0 | `ShareAddressOverride` ×1, `YaraDirOverride` ×1, `YaraRunOverride` ×1 |
| Karten & Touren (`karten`) | SPEC-13 | 16 | 14 | 2 | 0 | 0 | 0 | `MapHttpOverride` ×1, `VectorScriptOverride` ×1 |
| CNC-Werkstatt (`cnc`) | SPEC-14 | 13 | 12 | 1 | 0 | 0 | 0 | `CncPhotoOverride` ×1 |
| Live streamen (`sendung`) | SPEC-15 | 33 | 26 | 7 | 0 | 0 | 0 | `AppPathOverride` ×3, `ObsDirOverride` ×3, `BcClockOverride` ×1 |
| Auto & Fahrten (`fahrzeug`) | SPEC-16 | 13 | 13 | 0 | 0 | 0 | 0 | – |
| KI-Agenten bauen (`agenten-bauen-programm`) | STUDIO-AGENTS-01 | 1 | 0 | 1 | 0 | 0 | 0 | `FolderPickOverride` ×1, `PickOverride` ×1 |
| Animation & Motion (`animation-programm`) | STUDIO-ANIM-01 | 1 | 0 | 1 | 0 | 0 | 0 | `AnimVideoOverride` ×1 |
| Künstlerstudio (`kuenstler-programm`) | STUDIO-ART-01 | 7 | 5 | 2 | 0 | 0 | 0 | `ImageAiOverride` ×1, `ArtVideoOverride` ×1 |
| Bücher schreiben (`buch-programm`) | STUDIO-BOOK-01 | 1 | 1 | 0 | 0 | 0 | 0 | – |
| Recherche mit Quellen (`quellen-programm`) | STUDIO-CITE-01 | 1 | 1 | 0 | 0 | 0 | 0 | – |
| Code mit KI (`code-ki-programm`) | STUDIO-CODEAI-01 | 1 | 0 | 1 | 0 | 0 | 0 | `CodeAiRunOverride` ×1, `FolderPickOverride` ×1 |
| Comics & Storyboards (`comic-programm`) | STUDIO-COMIC-01 | 1 | 1 | 0 | 0 | 0 | 0 | – |
| Konsolenspiele portieren (`konsole-programm`) | STUDIO-CONSOLE-01 | 10 | 2 | 8 | 0 | 0 | 0 | `ConsoleSendOverride` ×4, `LibStartOverride` ×4, `ConsoleGitOverride` ×3 |
| Tagesplaner (`tagesplaner-programm`) | STUDIO-DAYPLAN-01 | 1 | 0 | 1 | 0 | 0 | 0 | `DayNowOverride` ×1 |
| Filme machen (`film-programm`) | STUDIO-FILM-01 | 2 | 2 | 0 | 0 | 0 | 0 | – |
| Bilder mit KI (`bild-ki-programm`) | STUDIO-IMAGEAI-01 | 1 | 0 | 1 | 0 | 0 | 0 | `ImageAiOverride` ×1 |
| Briefe schreiben (`brief`) | STUDIO-LETTER-01 | 29 | 26 | 3 | 0 | 0 | 0 | `ClaudeRunOverride` ×1, `ClaudeStatusOverride` ×1, `PickOverride` ×1 |
| Logos & Marken (`logo-programm`) | STUDIO-LOGO-01 | 1 | 1 | 0 | 0 | 0 | 0 | – |
| KI-Modelle vergleichen (`modelle-vergleichen-programm`) | STUDIO-MODELS-01 | 1 | 0 | 1 | 0 | 0 | 0 | `KiBackendsOverride` ×1 |
| Trendo Musik (`musik-studio`) | STUDIO-MUSIK-01 | 63 | 12 | 51 | 0 | 0 | 0 | `AceClientOverride` ×19, `MusicLyricsOverride` ×12, `StemSplitOverride` ×9 |
| Verkaufen (`verkaufen`) | STUDIO-ONLINE-BUSINESS-01 | 7 | 7 | 0 | 0 | 0 | 0 | – |
| Fotos verwalten (`fotos-verwalten-programm`) | STUDIO-PHOTOS-01 | 1 | 0 | 1 | 0 | 0 | 0 | `FolderPickOverride` ×1 |
| Print on Demand (`pod-programm`) | STUDIO-POD-01 | 18 | 7 | 11 | 0 | 0 | 0 | `PodDelayOverride` ×9, `PodPngOverride` ×9, `PodWebOverride` ×9 |
| Podcast aufnehmen (`podcast-programm`) | STUDIO-PODCAST-01 | 2 | 2 | 0 | 0 | 0 | 0 | – |
| Texte zusammenfassen (`zusammenfassen-programm`) | STUDIO-SUMMARY-01 | 1 | 1 | 0 | 0 | 0 | 0 | – |
| Eigene KI trainieren (`ki-trainieren-programm`) | STUDIO-TRAIN-01 | 1 | 0 | 1 | 0 | 0 | 0 | `PickOverride` ×1 |
| TV & Live (`tv`) | STUDIO-TV-01 | 15 | 5 | 10 | 0 | 0 | 0 | `TvPlayOverride` ×7, `TvFetchOverride` ×6, `TvLegalOpenOverride` ×2 |
| Videos mit KI (`video-ki-programm`) | STUDIO-VIDEOAI-01 | 1 | 0 | 1 | 0 | 0 | 0 | `ImageAiOverride` ×1, `PickOverride` ×1, `VideoAiFfmpegOverride` ×1 |
| Bilder erkennen (`bilder-erkennen-programm`) | STUDIO-VISION-01 | 1 | 0 | 1 | 0 | 0 | 0 | `FolderPickOverride` ×1, `PickOverride` ×1, `VisionTesseractOverride` ×1 |
| Betriebsmodus (`modus`) | SYS-01 | 3 | 3 | 0 | 0 | 0 | 0 | – |
| Medien (`medien`) | SYS-02 | 29 | 21 | 7 | 1 | 0 | 0 | `FfmpegRunOverride` ×5, `PhotoFoldersOverride` ×1, `TrashOverride` ×1 |
| Werkzeuge: Notizen, Rechner, Wecker (`werkzeuge`) | SYS-02 | 24 | 23 | 0 | 1 | 0 | 0 | – |
| Übersicht (`alltag-start`) | SYS-02 | 24 | 23 | 0 | 1 | 0 | 0 | – |
| Downloads (`downloads`) | SYS-03 | 18 | 16 | 1 | 1 | 0 | 0 | `DownloadRunOverride` ×1 |
| Sicher prüfen (`netz-pruefen`) | SYS-03 | 17 | 16 | 0 | 1 | 0 | 0 | – |
| Programme verwalten (`appcenter`) | SYS-04 | 6 | 2 | 4 | 0 | 0 | 0 | `AppBackendOverride` ×4 |
| Abläufe (Automatisch erledigen) (`ablaeufe`) | SYS-05 | 13 | 10 | 3 | 0 | 0 | 0 | `ShareAddressOverride` ×1, `AutomationHttpOverride` ×1, `AutomationInstallOverride` ×1 |
| Ordner im Heimnetz teilen (`teilen`) | SYS-06 | 10 | 9 | 1 | 0 | 0 | 0 | `ShareAddressOverride` ×1 |
| Geräte & Laufwerke (`geraete`) | SYS-07 | 4 | 2 | 2 | 0 | 0 | 0 | `DeviceRunnerOverride` ×1, `PciFetchOverride` ×1, `DeviceSysRootOverride` ×1 |
| Recherche im Lesemodus (`recherche`) | SYS-08 | 5 | 2 | 3 | 0 | 0 | 0 | `ReaderFetchOverride` ×3 |
| Adressbuch (`kontakte`) | SYS-09 | 8 | 7 | 1 | 0 | 0 | 0 | `ImapStreamOverride` ×1 |
| Aufgaben (`aufgaben`) | SYS-09 | 9 | 8 | 1 | 0 | 0 | 0 | `ImapStreamOverride` ×1 |
| E-Mail & Kontakte (`post`) | SYS-09 | 23 | 20 | 1 | 2 | 0 | 0 | `ImapStreamOverride` ×1 |
| Kalender (`kalender`) | SYS-09 | 11 | 9 | 2 | 0 | 0 | 0 | `ClaudeRunOverride` ×1, `ClaudeStatusOverride` ×1, `SoundOverride` ×1 |
| Posteingang (`posteingang`) | SYS-09 | 9 | 6 | 3 | 0 | 0 | 0 | `ImapStreamOverride` ×3, `SendOverride` ×2 |
| Start (`doku-start`) | SYS-10 | 23 | 16 | 0 | 7 | 0 | 0 | – |
| Trendo PDF (`pdf`) | SYS-10 | 28 | 18 | 3 | 7 | 0 | 0 | `PickOverride` ×2, `TesseractOverride` ×1 |
| Trendo Präsentation (`praesentation`) | SYS-10 | 28 | 21 | 0 | 7 | 0 | 0 | – |
| Trendo Tabelle (`tabelle`) | SYS-10 | 39 | 28 | 4 | 7 | 0 | 0 | `ClaudeRunOverride` ×4, `ClaudeStatusOverride` ×4 |
| Trendo Text (`text`) | SYS-10 | 87 | 32 | 48 | 7 | 0 | 0 | `ClaudeStatusOverride` ×47, `ClaudeRunOverride` ×46, `DictateOverride` ×7 |
| Apps (`apps`) | SYS-11 | 13 | 12 | 0 | 1 | 0 | 0 | – |
| Hinzufügen (`hinzufuegen`) | SYS-11 | 12 | 11 | 0 | 1 | 0 | 0 | – |
| Pflegen (`prog-pflege`) | SYS-11 | 14 | 13 | 0 | 1 | 0 | 0 | – |
| Zusatzprogramme (`components`) | SYS-11 | 20 | 15 | 4 | 1 | 0 | 0 | `GitHubHandlerOverride` ×3, `OssSearchOverride` ×2, `GitHubFetchOverride` ×1 |
| Einstellungen (`settings`) | SYS-12 | 42 | 41 | 1 | 0 | 0 | 0 | `ClaudeRunOverride` ×1, `ClaudeStatusOverride` ×1, `SoundOverride` ×1 |
| Konto, Lizenz & Vollversion (`lizenz`) | SYS-12 | 16 | 16 | 0 | 0 | 0 | 0 | – |
| Personen (`personen`) | SYS-12 | 15 | 15 | 0 | 0 | 0 | 0 | – |
| Sehen (`bedien-sehen`) | SYS-12 | 18 | 18 | 0 | 0 | 0 | 0 | – |
| Trendo-Updates (`updates`) | SYS-12 | 15 | 15 | 0 | 0 | 0 | 0 | – |
| Fortschritt (`fortschritt`) | SYS-13 | 14 | 14 | 0 | 0 | 0 | 0 | – |
| Hardware & Leistung (`hardware`) | SYS-13 | 14 | 14 | 0 | 0 | 0 | 0 | – |
| Leistung (`qual-leistung`) | SYS-13 | 13 | 13 | 0 | 0 | 0 | 0 | – |
| System & Sicherheit (`system`) | SYS-13 | 59 | 14 | 45 | 0 | 0 | 0 | `ClaudeStatusOverride` ×45, `ClaudeRunOverride` ×44, `ReviewOverride` ×7 |
| Systemcheck (`systemcheck`) | SYS-13 | 14 | 13 | 1 | 0 | 0 | 0 | `InstallOverride` ×1 |
| Produktion (`produktion`) | X-01 | 6 | 4 | 1 | 1 | 0 | 0 | `ShareAddressOverride` ×1 |
| Selbstheilung (`heilung`) | X-02 | 3 | 3 | 0 | 0 | 0 | 0 | – |
| Baukasten (`baukasten`) | X-03 | 14 | 7 | 7 | 0 | 0 | 0 | `ClaudeStatusOverride` ×5, `ClaudeRunOverride` ×3, `ChatTrashOverride` ×1 |
| Erweiterungen (`erweiterungen`) | X-04 | 8 | 8 | 0 | 0 | 0 | 0 | – |
| KI-Bibliothek (`ki-bibliothek`) | X-05 | 6 | 6 | 0 | 0 | 0 | 0 | – |
| KI-Quellen (`ki-quellen`) | X-05 | 16 | 11 | 5 | 0 | 0 | 0 | `ClaudeStatusOverride` ×3, `KiDownloadOverride` ×2, `KiSourceReleasesOverride` ×2 |
| Kostenlose Internet-KIs (`ki-internet`) | X-05 | 6 | 5 | 1 | 0 | 0 | 0 | `KiBackendsOverride` ×1, `OpenRouterFileOverride` ×1, `OpenRouterModelsOverride` ×1 |
| Open Source (`oss`) | X-06 | 17 | 2 | 14 | 1 | 0 | 0 | `GitHubHandlerOverride` ×10, `OssSearchOverride` ×6, `SourceRunOverride` ×2 |
| Freie Bilder & Klänge (`assets`) | X-07 | 13 | 9 | 4 | 0 | 0 | 0 | `MediaFetchOverride` ×3, `AudioOverride` ×1, `IconFetchOverride` ×1 |
| Handy verbinden (`handy`) | X-08 | 18 | 11 | 7 | 0 | 0 | 0 | `AdbOverride` ×3, `PhoneAddressesOverride` ×2, `TailscaleInstalledOverride` ×2 |
| Speicher & Leistung (`ressourcen`) | X-09 | 4 | 2 | 2 | 0 | 0 | 0 | `TrashOverride` ×1, `TaskKillOverride` ×1, `TaskMeasureOverride` ×1 |
| Sauberer Nachbau (`nachbau`) | X-10 | 5 | 4 | 1 | 0 | 0 | 0 | `ShareAddressOverride` ×1 |
| Vorhaben (`vorhaben`) | X-11 | 7 | 7 | 0 | 0 | 0 | 0 | – |
| (ohne Programmzuordnung) (`(ohne Programmzuordnung)`) | – | 2049 | 1855 | 86 | 85 | 23 | 0 | `WebDesignEditorDirOverride` ×12, `WebDesignAuditOverride` ×11, `WebDesignKiOverride` ×11 |

## Programme ohne einen einzigen Test der Schicht „echt“

Alle Studios (`studios`), Dateien (`dateien`), Desktop-Symbole (`desktop-symbole`), Ersteinrichtung (`einrichtung`), Projekte (`projects`), Startbildschirm (`start`), KI (`ki`), KI fragen (`ki-fragen`), Mit Claude verbinden (`claude-verbinden`), Online-Dienste (`keys`), Verbindungen (`verbindungen`), KI-Rollen und Teams (`ki-rollen`), Geräteverbund (PC, Pi, Android) (`verbund`), Kameras & Roboter steuern (`roboter`), Wissen für die KI (`ki-wissen`), Freigaben (`approvals`), Schutz & Datenschutz (`schutz`), Sicherung (`sicherung`), KI-Vorlagen (`ki-vorlagen`), Skills (`ki-skills`), Grundriss & Wohnen (`architektur`), Schaltung & Simulation (`elektronik`), Tragwerk & Mechanik (`technik`), VR & AR gestalten (`xr`), Messdaten & Auswertung (`wissenschaft`), Lernen mit Karteikarten (`lernen`), Kleidung & Produkte (`mode-programm`), Aus Fotos (`scan3d`), Punktwolke (`punktwolke`), Geräte überwachen (`zwilling`), Geld (`finanzen`), Sicherheits-Werkzeuge & Beweise (`forensik`), Karten & Touren (`karten`), CNC-Werkstatt (`cnc`), Live streamen (`sendung`), Auto & Fahrten (`fahrzeug`), KI-Agenten bauen (`agenten-bauen-programm`), Animation & Motion (`animation-programm`), Künstlerstudio (`kuenstler-programm`), Bücher schreiben (`buch-programm`), Recherche mit Quellen (`quellen-programm`), Code mit KI (`code-ki-programm`), Comics & Storyboards (`comic-programm`), Konsolenspiele portieren (`konsole-programm`), Tagesplaner (`tagesplaner-programm`), Filme machen (`film-programm`), Bilder mit KI (`bild-ki-programm`), Briefe schreiben (`brief`), Logos & Marken (`logo-programm`), KI-Modelle vergleichen (`modelle-vergleichen-programm`), Trendo Musik (`musik-studio`), Verkaufen (`verkaufen`), Fotos verwalten (`fotos-verwalten-programm`), Print on Demand (`pod-programm`), Podcast aufnehmen (`podcast-programm`), Texte zusammenfassen (`zusammenfassen-programm`), Eigene KI trainieren (`ki-trainieren-programm`), TV & Live (`tv`), Videos mit KI (`video-ki-programm`), Bilder erkennen (`bilder-erkennen-programm`), Betriebsmodus (`modus`), Programme verwalten (`appcenter`), Abläufe (Automatisch erledigen) (`ablaeufe`), Ordner im Heimnetz teilen (`teilen`), Geräte & Laufwerke (`geraete`), Recherche im Lesemodus (`recherche`), Adressbuch (`kontakte`), Aufgaben (`aufgaben`), Kalender (`kalender`), Posteingang (`posteingang`), Einstellungen (`settings`), Konto, Lizenz & Vollversion (`lizenz`), Personen (`personen`), Sehen (`bedien-sehen`), Trendo-Updates (`updates`), Fortschritt (`fortschritt`), Hardware & Leistung (`hardware`), Leistung (`qual-leistung`), System & Sicherheit (`system`), Systemcheck (`systemcheck`), Selbstheilung (`heilung`), Baukasten (`baukasten`), Erweiterungen (`erweiterungen`), KI-Bibliothek (`ki-bibliothek`), KI-Quellen (`ki-quellen`), Kostenlose Internet-KIs (`ki-internet`), Freie Bilder & Klänge (`assets`), Handy verbinden (`handy`), Speicher & Leistung (`ressourcen`), Sauberer Nachbau (`nachbau`), Vorhaben (`vorhaben`)

## Ersatz-Haken nach Nutzung in Tests

| Haken | Ort | Tests, die ihn setzen |
|---|---|---|
| `ClaudeStatusOverride` | `src/Trendo.App/MainWindow.Claude.cs:45` | 62 |
| `ClaudeRunOverride` | `src/Trendo.App/MainWindow.Claude.cs:46` | 57 |
| `PickOverride` | `src/Trendo.App/MainWindow.Files.cs:19` | 29 |
| `AceClientOverride` | `src/Trendo.App/MainWindow.Musik.cs:16` | 24 |
| `WebDesignEditorDirOverride` | `src/Trendo.App/MainWindow.WebDesign.cs:14` | 22 |
| `WebDesignKiOverride` | `src/Trendo.App/MainWindow.WebDesignKi.cs:17` | 22 |
| `WebDesignKiStateOverride` | `src/Trendo.App/MainWindow.WebDesignKi.cs:23` | 20 |
| `WebDesignVisionOverride` | `src/Trendo.App/MainWindow.WebDesignKi.cs:19` | 20 |
| `WebDesignAuditOverride` | `src/Trendo.App/MainWindow.WebDesignKi.cs:21` | 19 |
| `AppPathOverride` | `src/Trendo.App/MainWindow.Programs.cs:13` | 16 |
| `GamePromptOverride` | `src/Trendo.App/MainWindow.ProgGamePrompt.cs:17` | 16 |
| `KiBackendsOverride` | `src/Trendo.App/MainWindow.W2.cs:15` | 16 |
| `PodDelayOverride` | `src/Trendo.App/MainWindow.PodPrintful.cs:31` | 14 |
| `PodWebOverride` | `src/Trendo.App/MainWindow.PodPrintful.cs:30` | 14 |
| `MusicLyricsOverride` | `src/Trendo.App/MainWindow.MusikStudio.cs:39` | 12 |
| `FfmpegRunOverride` | `src/Trendo.App/MainWindow.FilmDelivery.cs:12` | 11 |
| `GitHubHandlerOverride` | `src/Trendo.App/MainWindow.Programme.cs:268` | 11 |
| `StemSplitOverride` | `src/Trendo.App/MainWindow.MusikSpuren.cs:12` | 11 |
| `ClaudeConsentOverride` | `src/Trendo.App/MainWindow.ClaudeBridge.cs:24` | 10 |
| `EngineProbeOverride` | `src/Trendo.App/MainWindow.ProgGameEngines.cs:16` | 9 |
| `PodPngOverride` | `src/Trendo.App/MainWindow.PodPrintful.cs:32` | 9 |
| `TvPlayOverride` | `src/Trendo.App/MainWindow.Tv.cs:140` | 9 |
| `AudioOverride` | `src/Trendo.App/MainWindow.MusicPlayer.cs:19` | 7 |
| `DictateOverride` | `src/Trendo.App/MainWindow.ClaudeFallback.cs:22` | 7 |
| `OssSearchOverride` | `src/Trendo.App/MainWindow.W4Oss.cs:20` | 7 |
| `ReviewOverride` | `src/Trendo.App/MainWindow.ClaudeExtras.cs:323` | 7 |
| `SectionsOverride` | `src/Trendo.App/MainWindow.MusikVideo.cs:15` | 7 |
| `TvFetchOverride` | `src/Trendo.App/MainWindow.Tv.cs:142` | 7 |
| `ExportAsyncOverride` | `src/Trendo.App/MainWindow.VideoSchnitt.cs:542` | 6 |
| `MasterRunOverride` | `src/Trendo.App/MainWindow.MusikProfi.cs:11` | 6 |
| `MelodyOverride` | `src/Trendo.App/MainWindow.MusikMitsingen.cs:16` | 6 |
| `ProbeOverride` | `src/Trendo.App/MainWindow.ClaudeExtras.cs:387` | 6 |
| `SpeakOverride` | `src/Trendo.App/MainWindow.ClaudeExtras.cs:438` | 6 |
| `KieWebOverride` | `src/Trendo.App/MainWindow.Kie.cs:21` | 5 |
| `MicRecordOverride` | `src/Trendo.App/MainWindow.MusikStimme.cs:14` | 5 |
| `SongFactsFetchOverride` | `src/Trendo.App/MainWindow.MusikTools.cs:19` | 5 |
| `AceBaseReadyOverride` | `src/Trendo.App/MainWindow.MusikExtend.cs:14` | 4 |
| `AppBackendOverride` | `src/Trendo.App/MainWindow.AppCenter.cs:12` | 4 |
| `BibHttpOverride` | `src/Trendo.App/MainWindow.Library3D.cs:14` | 4 |
| `ConsoleSendOverride` | `src/Trendo.App/MainWindow.ConsolePort.cs:18` | 4 |
| `FolderPickOverride` | `src/Trendo.App/MainWindow.KreativStudios2.cs:406` | 4 |
| `InstallOverride` | `src/Trendo.App/MainWindow.Programs.cs:141` | 4 |
| `KiDownloadOverride` | `src/Trendo.App/MainWindow.KiLibrary.cs:23` | 4 |
| `KiStepOverride` | `src/Trendo.App/MainWindow.KiPy.cs:15` | 4 |
| `KiToolOverride` | `src/Trendo.App/MainWindow.ClaudeFallback.cs:20` | 4 |
| `KieDelayOverride` | `src/Trendo.App/MainWindow.Kie.cs:22` | 4 |
| `LibStartOverride` | `src/Trendo.App/MainWindow.GameLibrary.cs:21` | 4 |
| `AdbOverride` | `src/Trendo.App/MainWindow.W7.cs:15` | 3 |
| `ClaudeOpenOverride` | `src/Trendo.App/MainWindow.Claude.cs:20` | 3 |
| `ConsoleGitOverride` | `src/Trendo.App/MainWindow.ConsolePort.cs:20` | 3 |
| `DownloadsOverride` | `src/Trendo.App/MainWindow.Library3DPro.cs:18` | 3 |
| `ImageAiOverride` | `src/Trendo.App/MainWindow.KreativStudios2.cs:538` | 3 |
| `ImapStreamOverride` | `src/Trendo.App/MainWindow.Pim.cs:35` | 3 |
| `MediaFetchOverride` | `src/Trendo.App/MainWindow.MediaBrowser.cs:19` | 3 |
| `NetlifyHandlerOverride` | `src/Trendo.App/MainWindow.Connections.cs:19` | 3 |
| `NvidiaOverride` | `src/Trendo.App/MainWindow.KiPy.cs:17` | 3 |
| `ObsDirOverride` | `src/Trendo.App/MainWindow.Programs.cs:16` | 3 |
| `OwnVoiceRunOverride` | `src/Trendo.App/MainWindow.MusikEigeneStimme.cs:34` | 3 |
| `ProgramFindOverride` | `src/Trendo.App/MainWindow.StudioIntel.cs:20` | 3 |
| `ReaderFetchOverride` | `src/Trendo.App/MainWindow.Research.cs:14` | 3 |
| `ScanToolOverride` | `src/Trendo.App/MainWindow.Spec09Scan.cs:23` | 3 |
| `SongAnalyseOverride` | `src/Trendo.App/MainWindow.MusikTools.cs:17` | 3 |
| `SoundOverride` | `src/Trendo.App/MainWindow.ClaudeExtras.cs:103` | 3 |
| `ToolRunOverride` | `src/Trendo.App/MainWindow.Runde2Tools.cs:12` | 3 |
| `UnrealEngineOverride` | `src/Trendo.App/MainWindow.ProgGameUnreal.cs:18` | 3 |
| `UnrealRunOverride` | `src/Trendo.App/MainWindow.ProgGameUnreal.cs:16` | 3 |
| `VideoKiOverride` | `src/Trendo.App/MainWindow.MusikVideoKi.cs:19` | 3 |
| `WebDesignParseOverride` | `src/Trendo.App/MainWindow.WebDesignCode.cs:28` | 3 |
| `ClaudeFallbackOverride` | `src/Trendo.App/MainWindow.ClaudeFallback.cs:18` | 2 |
| `ClaudeHomeOverride` | `src/Trendo.App/MainWindow.Connections.cs:21` | 2 |
| `ConnRunOverride` | `src/Trendo.App/MainWindow.Connections.cs:15` | 2 |
| `CoverImageOverride` | `src/Trendo.App/MainWindow.MusikCover.cs:16` | 2 |
| `DrivesOverride` | `src/Trendo.App/MainWindow.SetupVollbild.cs:19` | 2 |
| `EditGenModelOverride` | `src/Trendo.App/MainWindow.VideoGenKi.cs:15` | 2 |
| `EncodersOverride` | `src/Trendo.App/MainWindow.VideoSchnitt.cs:545` | 2 |
| `ExportOverride` | `src/Trendo.App/MainWindow.VideoSchnitt.cs:134` | 2 |
| `FigmaHandlerOverride` | `src/Trendo.App/MainWindow.WebDesignCode.cs:30` | 2 |
| `GarmentRunOverride` | `src/Trendo.App/MainWindow.ProgModeGarment.cs:20` | 2 |
| `GhExeOverride` | `src/Trendo.App/MainWindow.Connections.cs:17` | 2 |
| `GitHubFetchOverride` | `src/Trendo.App/MainWindow.Programme.cs:19` | 2 |
| `GpuMemoryOverride` | `src/Trendo.App/MainWindow.MusikStudio.cs:132` | 2 |
| `IfcRunOverride` | `src/Trendo.App/MainWindow.Spec01Ifc.cs:12` | 2 |
| `KeyValidatorOverride` | `src/Trendo.App/MainWindow.axaml.cs:1074` | 2 |
| `KiSourceReleasesOverride` | `src/Trendo.App/MainWindow.KiSourceSetup.cs:15` | 2 |
| `KittlCliOverride` | `src/Trendo.App/MainWindow.PodDesigner.cs:427` | 2 |
| `KittlConsoleOverride` | `src/Trendo.App/MainWindow.PodDesigner.cs:429` | 2 |
| `OwnVoiceMixOverride` | `src/Trendo.App/MainWindow.MusikEigeneStimme.cs:35` | 2 |
| `PhoneAddressesOverride` | `src/Trendo.App/MainWindow.PhoneEasy.cs:26` | 2 |
| `PlayFramesOverride` | `src/Trendo.App/MainWindow.VideoSchnitt.cs:447` | 2 |
| `PreviewSourceOverride` | `src/Trendo.App/MainWindow.VideoPreview.cs:19` | 2 |
| `ProgramStartsOverride` | `src/Trendo.App/MainWindow.StudioIntel.cs:18` | 2 |
| `SendOverride` | `src/Trendo.App/MainWindow.Pim.cs:36` | 2 |
| `ShareAddressOverride` | `src/Trendo.App/MainWindow.Share.cs:15` | 2 |
| `SingersOverride` | `src/Trendo.App/MainWindow.MusikVerstehen.cs:15` | 2 |
| `SourceRunOverride` | `src/Trendo.App/MainWindow.SourceApps.cs:28` | 2 |
| `TailscaleInstalledOverride` | `src/Trendo.App/MainWindow.PhoneEasy.cs:30` | 2 |
| `ThemeOverride` | `src/Trendo.App/MainWindow.MusikVerstehen.cs:19` | 2 |
| `TrashOverride` | `src/Trendo.App/MainWindow.DiskMapPage.cs:21` | 2 |
| `TvLegalOpenOverride` | `src/Trendo.App/MainWindow.TvLegal.cs:24` | 2 |
| `VoiceAnalyseOverride` | `src/Trendo.App/MainWindow.MusikStimme.cs:16` | 2 |
| `VollpaketOnlineOverride` | `src/Trendo.App/MainWindow.VollpaketOnline.cs:12` | 2 |
| `WebIconFetchOverride` | `src/Trendo.App/MainWindow.DockLook.cs:18` | 2 |
| `WordsOverride` | `src/Trendo.App/MainWindow.MusikVerstehen.cs:17` | 2 |
| `AgentAskOverride` | `src/Trendo.App/MainWindow.AgentTab.cs:24` | 1 |
| `AnimVideoOverride` | `src/Trendo.App/MainWindow.KreativStudios2.cs:323` | 1 |
| `ArtVideoOverride` | `src/Trendo.App/MainWindow.Kuenstlerstudio.cs:32` | 1 |
| `AssimpDllOverride` | `src/Trendo.App/MainWindow.Studio3DAssimp.cs:13` | 1 |
| `AssimpRunOverride` | `src/Trendo.App/MainWindow.Studio3DAssimp.cs:12` | 1 |
| `AuPluginRunOverride` | `src/Trendo.App/MainWindow.AudioPluginHost.cs:17` | 1 |
| `AutomationHttpOverride` | `src/Trendo.App/MainWindow.N8n.cs:29` | 1 |
| `AutomationInstallOverride` | `src/Trendo.App/MainWindow.N8n.cs:27` | 1 |
| `AxeDirOverride` | `src/Trendo.App/MainWindow.WebAxe.cs:11` | 1 |
| `AxeRunOverride` | `src/Trendo.App/MainWindow.WebAxe.cs:12` | 1 |
| `BcClockOverride` | `src/Trendo.App/MainWindow.Spec15.cs:30` | 1 |
| `BluetoothOverride` | `src/Trendo.App/MainWindow.Phone.cs:20` | 1 |
| `BrowsersOverride` | `src/Trendo.App/MainWindow.WebApps.cs:18` | 1 |
| `ChatTrashOverride` | `src/Trendo.App/MainWindow.ClaudeChat.cs:50` | 1 |
| `CncPhotoOverride` | `src/Trendo.App/MainWindow.RealismViews.cs:21` | 1 |
| `CodeAiRunOverride` | `src/Trendo.App/MainWindow.KreativStudios4.cs:55` | 1 |
| `CodeRunOverride` | `src/Trendo.App/MainWindow.CodeEditor.cs:97` | 1 |
| `CoverTranslateOverride` | `src/Trendo.App/MainWindow.MusikCoverPlus.cs:15` | 1 |
| `CreditsFontOverride` | `src/Trendo.App/MainWindow.VideoCredits.cs:16` | 1 |
| `DayNowOverride` | `src/Trendo.App/MainWindow.KreativStudios4.cs:1107` | 1 |
| `DeviceRunnerOverride` | `src/Trendo.App/MainWindow.Devices.cs:11` | 1 |
| `DeviceSysRootOverride` | `src/Trendo.App/MainWindow.Devices.cs:12` | 1 |
| `DiscoverOverride` | `src/Trendo.App/MainWindow.Print3DPro.cs:21` | 1 |
| `DocsRunOverride` | `src/Trendo.App/MainWindow.WissenDocs.cs:11` | 1 |
| `DownloadRunOverride` | `src/Trendo.App/MainWindow.DownloadManager.cs:23` | 1 |
| `EditLipMouthOverride` | `src/Trendo.App/MainWindow.VideoLipSync.cs:18` | 1 |
| `EditLipWordsOverride` | `src/Trendo.App/MainWindow.VideoLipSync.cs:16` | 1 |
| `EngineExportOverride` | `src/Trendo.App/MainWindow.Studio3DEngines.cs:13` | 1 |
| `EngineRunOverride` | `src/Trendo.App/MainWindow.ProgGameEngines.cs:18` | 1 |
| `EraSoundOverride` | `src/Trendo.App/MainWindow.MusikCoverWerkstatt.cs:149` | 1 |
| `ExportRecycleOverride` | `src/Trendo.App/MainWindow.VideoExporte.cs:13` | 1 |
| `F3dExeOverride` | `src/Trendo.App/MainWindow.Studio3DF3d.cs:14` | 1 |
| `F3dRunOverride` | `src/Trendo.App/MainWindow.Studio3DF3d.cs:12` | 1 |
| `GameJudgeOverride` | `src/Trendo.App/MainWindow.ProgGameUgs.cs:15` | 1 |
| `GameRefFetchOverride` | `src/Trendo.App/MainWindow.ProgGameStage3.cs:18` | 1 |
| `GameServiceReadyOverride` | `src/Trendo.App/MainWindow.ProgGameStage3.cs:20` | 1 |
| `GitHubInstallRootsOverride` | `src/Trendo.App/MainWindow.Programme.cs:131` | 1 |
| `GitHubInstallerRunOverride` | `src/Trendo.App/MainWindow.Programme.cs:129` | 1 |
| `GltfRunOverride` | `src/Trendo.App/MainWindow.Spec04Gltf.cs:14` | 1 |
| `GltfTransformOverride` | `src/Trendo.App/MainWindow.Spec04Gltf.cs:16` | 1 |
| `GltfValidatorOverride` | `src/Trendo.App/MainWindow.Spec04Gltf.cs:15` | 1 |
| `IconFetchOverride` | `src/Trendo.App/MainWindow.MediaIcons.cs:16` | 1 |
| `IfcViewScriptOverride` | `src/Trendo.App/MainWindow.Spec01IfcView.cs:19` | 1 |
| `ImageWriteOverride` | `src/Trendo.App/MainWindow.SetupAbbild.cs:18` | 1 |
| `KiChatOverride` | `src/Trendo.App/MainWindow.axaml.cs:983` | 1 |
| `KiInstallerRunOverride` | `src/Trendo.App/MainWindow.KiSourceSetup.cs:17` | 1 |
| `KiRunOverride` | `src/Trendo.App/MainWindow.KiLibrary.cs:25` | 1 |
| `KiServerStartOverride` | `src/Trendo.App/MainWindow.KiSourceSetup.cs:19` | 1 |
| `KreaturRunOverride` | `src/Trendo.App/MainWindow.Kreatur.cs:21` | 1 |
| `LetterRecycleOverride` | `src/Trendo.App/MainWindow.W1Letter.cs:379` | 1 |
| `LibFetchOverride` | `src/Trendo.App/MainWindow.GameLibrary.cs:23` | 1 |
| `LibGodotOverride` | `src/Trendo.App/MainWindow.GameLibrary.cs:27` | 1 |
| `LiveServerStartOverride` | `src/Trendo.App/MainWindow.Runde2Tools.cs:77` | 1 |
| `MapHttpOverride` | `src/Trendo.App/MainWindow.Spec13.cs:204` | 1 |
| `MicStreamOverride` | `src/Trendo.App/MainWindow.MusikMitsingen.cs:14` | 1 |
| `MixamoPrepOverride` | `src/Trendo.App/MainWindow.ProgGameMixamo.cs:20` | 1 |
| `MixamoRiggerOverride` | `src/Trendo.App/MainWindow.ProgGameMixamo.cs:22` | 1 |
| `MsMashupRunOverride` | `src/Trendo.App/MainWindow.MusikMashup.cs:14` | 1 |
| `MsSpeechMixOverride` | `src/Trendo.App/MainWindow.MusikSounds.cs:20` | 1 |
| `MsSpeechTtsOverride` | `src/Trendo.App/MainWindow.MusikSounds.cs:18` | 1 |
| `MusicFoldersOverride` | `src/Trendo.App/MainWindow.MusicPlayer.cs:18` | 1 |
| `NatureFetchOverride` | `src/Trendo.App/MainWindow.ProgGameUnreal.cs:20` | 1 |
| `NotenRunOverride` | `src/Trendo.App/MainWindow.MusikStemsPlus.cs:15` | 1 |
| `ObjectsRunOverride` | `src/Trendo.App/MainWindow.Core09Objects.cs:11` | 1 |
| `Open3dRunOverride` | `src/Trendo.App/MainWindow.Spec09Open3d.cs:12` | 1 |
| `OpenRouterFileOverride` | `src/Trendo.App/MainWindow.OpenRouter.cs:24` | 1 |
| `OpenRouterModelsOverride` | `src/Trendo.App/MainWindow.OpenRouter.cs:20` | 1 |
| `OpenRouterValidateOverride` | `src/Trendo.App/MainWindow.OpenRouter.cs:22` | 1 |
| `OssReadmeOverride` | `src/Trendo.App/MainWindow.W4Oss.cs:22` | 1 |
| `PciFetchOverride` | `src/Trendo.App/MainWindow.DevicesPci.cs:10` | 1 |
| `PexelsFetchOverride` | `src/Trendo.App/MainWindow.MusikVideo.cs:17` | 1 |
| `PhoneFirewallOverride` | `src/Trendo.App/MainWindow.PhoneEasy.cs:28` | 1 |
| `PhotoFoldersOverride` | `src/Trendo.App/MainWindow.Fotoalbum.cs:19` | 1 |
| `PluginRootsOverride` | `src/Trendo.App/MainWindow.AudioQa.cs:87` | 1 |
| `PreviewAudioOverride` | `src/Trendo.App/MainWindow.VideoPreview.cs:21` | 1 |
| `PreviewMixOverride` | `src/Trendo.App/MainWindow.VideoPreview.cs:23` | 1 |
| `ProbeAnalyzeOverride` | `src/Trendo.App/MainWindow.LiveStudio.cs:25` | 1 |
| `Render3DOverride` | `src/Trendo.App/MainWindow.Studio3DPro.cs:29` | 1 |
| `ReplaceAudioOverride` | `src/Trendo.App/MainWindow.MusikDrehbuch.cs:30` | 1 |
| `ResticDirOverride` | `src/Trendo.App/MainWindow.SchutzRestic.cs:14` | 1 |
| `ResticRunOverride` | `src/Trendo.App/MainWindow.SchutzRestic.cs:15` | 1 |
| `ScrcpyOverride` | `src/Trendo.App/MainWindow.Phone.cs:16` | 1 |
| `SewingDirOverride` | `src/Trendo.App/MainWindow.ProgSewing.cs:12` | 1 |
| `SewingRunOverride` | `src/Trendo.App/MainWindow.ProgSewing.cs:13` | 1 |
| `SliceOverride` | `src/Trendo.App/MainWindow.Print3DPro.cs:23` | 1 |
| `SongExportOverride` | `src/Trendo.App/MainWindow.MusikStudio.cs:566` | 1 |
| `SongRecycleOverride` | `src/Trendo.App/MainWindow.MusikStudio.cs:40` | 1 |
| `StockDownloadOverride` | `src/Trendo.App/MainWindow.MusikVideo.cs:18` | 1 |
| `StudioLaunchOverride` | `src/Trendo.App/MainWindow.StudioMcp.cs:10` | 1 |
| `StudioMcpOverride` | `src/Trendo.App/MainWindow.ClaudeLos.cs:12` | 1 |
| `SubsOverride` | `src/Trendo.App/MainWindow.VideoExportSettings.cs:22` | 1 |
| `SwitchPersonOverride` | `src/Trendo.App/MainWindow.Lock.cs:21` | 1 |
| `SystemLightOverride` | `src/Trendo.App/MainWindow.Look.cs:53` | 1 |
| `TailscaleLoginOverride` | `src/Trendo.App/MainWindow.PhoneEasy.cs:32` | 1 |
| `TaskClockOverride` | `src/Trendo.App/MainWindow.TaskCards.cs:18` | 1 |
| `TaskKillOverride` | `src/Trendo.App/MainWindow.TaskManager.cs:23` | 1 |
| `TaskMeasureOverride` | `src/Trendo.App/MainWindow.TaskManager.cs:22` | 1 |
| `TesseractOverride` | `src/Trendo.App/MainWindow.PdfF3.cs:22` | 1 |
| `TodayOverride` | `src/Trendo.App/MainWindow.MusikTagesidee.cs:15` | 1 |
| `TvLegalSearchOverride` | `src/Trendo.App/MainWindow.TvLegal.cs:22` | 1 |
| `TvRecordOverride` | `src/Trendo.App/MainWindow.Tv.cs:144` | 1 |
| `TvVideoResolveOverride` | `src/Trendo.App/MainWindow.TvVideos.cs:25` | 1 |
| `TvVideoSearchOverride` | `src/Trendo.App/MainWindow.TvVideos.cs:23` | 1 |
| `UninstallPcOverride` | `src/Trendo.App/MainWindow.SetupAbbild.cs:26` | 1 |
| `UploadOverride` | `src/Trendo.App/MainWindow.ProgGameRelease.cs:18` | 1 |
| `VectorScriptOverride` | `src/Trendo.App/MainWindow.Spec13Vector.cs:18` | 1 |
| `VideoAiFfmpegOverride` | `src/Trendo.App/MainWindow.KreativStudios3.cs:93` | 1 |
| `VideoCheckOverride` | `src/Trendo.App/MainWindow.MusikVideoKi.cs:21` | 1 |
| `VisionCheckOverride` | `src/Trendo.App/MainWindow.MusikVideoKi.cs:24` | 1 |
| `VisionTesseractOverride` | `src/Trendo.App/MainWindow.KreativStudios4.cs:511` | 1 |
| `WebAppLaunchOverride` | `src/Trendo.App/MainWindow.WebApps.cs:16` | 1 |
| `WebDesignBenchBackendOverride` | `src/Trendo.App/MainWindow.WebDesignAssets.cs:17` | 1 |
| `WebDesignGlbConvertOverride` | `src/Trendo.App/MainWindow.WebDesign3D.cs:14` | 1 |
| `WebDesignHardwareOverride` | `src/Trendo.App/MainWindow.WebDesignAssets.cs:18` | 1 |
| `WebDesignTemplatesOverride` | `src/Trendo.App/MainWindow.WebDesignComponents.cs:15` | 1 |
| `WeekSummaryOverride` | `src/Trendo.App/MainWindow.WissenWoche.cs:15` | 1 |
| `WeekTodayOverride` | `src/Trendo.App/MainWindow.WissenWoche.cs:16` | 1 |
| `WfDueOverride` | `src/Trendo.App/MainWindow.Workflows.cs:20` | 1 |
| `WindowsConfirmOverride` | `src/Trendo.App/MainWindow.Lock.cs:19` | 1 |
| `XdgOverride` | `src/Trendo.Core/DesktopPlatform.cs:102` | 1 |
| `XrFixOverride` | `src/Trendo.App/MainWindow.Spec04Pro.cs:39` | 1 |
| `YaraDirOverride` | `src/Trendo.App/MainWindow.ForensikYara.cs:13` | 1 |
| `YaraRunOverride` | `src/Trendo.App/MainWindow.ForensikYara.cs:14` | 1 |
| `ExtFetchOverride` | `src/Trendo.App/MainWindow.W5ExtCat.cs:24` | 0 |
| `FilesOpenOverride` | `src/Trendo.App/MainWindow.Dateien.cs:94` | 0 |
| `FreeGpuOverride` | `src/Trendo.App/MainWindow.Gpu.cs:27` | 0 |
| `HealthOverride` | `src/Trendo.App/MainWindow.W7.cs:16` | 0 |
| `IfcViewDirOverride` | `src/Trendo.App/MainWindow.Spec01IfcView.cs:14` | 0 |
| `ImageReadyOverride` | `src/Trendo.App/MainWindow.SetupVollbild.cs:84` | 0 |
| `KartenDirOverride` | `src/Trendo.App/MainWindow.Spec13Vector.cs:13` | 0 |
| `LibExtractOverride` | `src/Trendo.App/MainWindow.GameLibrary.cs:25` | 0 |
| `MsCropRunOverride` | `src/Trendo.App/MainWindow.MusikRemix.cs:74` | 0 |
| `ScanFfmpegOverride` | `src/Trendo.App/MainWindow.Spec09Scan.cs:25` | 0 |
| `SourceWebStartOverride` | `src/Trendo.App/MainWindow.SourceApps.cs:30` | 0 |
| `TelegramInstallerOverride` | `src/Trendo.App/MainWindow.WebApps.cs:20` | 0 |
| `ToolExeOverride` | `src/Trendo.App/MainWindow.Runde2Tools.cs:13` | 0 |

## Tests der Schichten „echt“, „anforderung“ und „fest-uebersprungen“

| Schicht | Test | Datei | Bedingung |
|---|---|---|---|
| echt | API: nur an 127.0.0.1 gebunden (Verbindung über Nicht-Loopback-Adresse scheitert) | `tests/Trendo.Api.Tests/ApiTests.cs` | NeedsLanFact |
| echt | Abspielen ohne fremdes Fenster: mpv startet mit force-window=no und vo=null; echter mpv spielt eine WAV, ohne ein Fenster zu öffnen | `tests/Trendo.App.Tests/MpvWindowTests.cs` | Suche im Test |
| echt | SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da) | `tests/Trendo.App.Tests/Sys03UiTests.cs` | Suche im Test |
| echt | CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei | `tests/Trendo.App.Tests/W4MediaUiTests.cs` | Suche im Test |
| echt | W1, Z4, W6, A5 in echtem Chrome: Ansichten wirken nach unten, Spalten nach eigener Breite, dunkler Satz, Stapel im Rahmen – im Editor und im Export (übersprungen ohne Chrome oder Node) | `tests/Trendo.App.Tests/WebDesignStufe2UiTests.cs` | Suche im Test |
| echt | W5, Z1, W4, Z2 in echtem Chrome: Komponenten mit Überschreibung, Profi-Bausteine folgen den Gestaltungswerten, Tastatur und axe-core je Baustein, Bewegung mit „weniger Bewegung“ und Ersatzweg, Zeitleiste im Export (übersprungen ohne Chrome, Node oder axe-core in %TEMP%\\trendo-axe) | `tests/Trendo.App.Tests/WebDesignStufe2UiTests.cs` | Suche im Test |
| echt | Z2: Editor und Export (bewegung.js) berechnen Schlüsselbilder gleich wie C# (übersprungen ohne Node) | `tests/Trendo.App.Tests/WebDesignStufe2UiTests.cs` | Suche im Test |
| echt | Z4/W1: Editor (ansichten.js) und Export (C#) erzeugen dieselben Regeln (übersprungen ohne Node) | `tests/Trendo.App.Tests/WebDesignStufe2UiTests.cs` | Suche im Test |
| echt | K7 in echtem Chrome: lokal anmelden, Eintrag speichern, nach dem Neuladen noch da, „nur nach Anmeldung“ sperrt, Supabase mit öffentlichem Schlüssel (übersprungen ohne Chrome/Node) | `tests/Trendo.App.Tests/WebDesignStufe3QaUiTests.cs` | Suche im Test |
| echt | Sicherheit 3: Einlesen von HTML lädt nichts aus dem Netz, auch kein @import in CSS (übersprungen ohne Chrome/Edge) | `tests/Trendo.App.Tests/WebDesignStufe3QaUiTests.cs` | Suche im Test |
| echt | K3/K6 im Editor in echtem Chrome: Skizze mit der Maus zeichnen und als PNG übergeben; HTML-Block zeigt nur erlaubte Elemente (übersprungen ohne Chrome/Node) | `tests/Trendo.App.Tests/WebDesignStufe3Teil2UiTests.cs` | Suche im Test |
| echt | K6 in echtem Chrome mit GrapesJS: Seite → Code → Seite ist gleich; fremde Seite: Skript und Ereignisse weg, Text vollständig (übersprungen ohne Chrome/Edge) | `tests/Trendo.App.Tests/WebDesignStufe3Teil2UiTests.cs` | Suche im Test |
| echt | Z3 in echtem Chrome (Software-Grafik): Modell erscheint, ohne WebGPU läuft WebGL, ohne 3D erscheint das Standbild, Splat erscheint (übersprungen ohne Chrome/Node) | `tests/Trendo.App.Tests/WebDesignZ3Tests.cs` | Suche im Test |
| echt | Z3: Ausschnitt und Nahgrenze für Splats (ohne Browser): Objekt behält seine Größe, Aufnahme mit Landschaft wird auf das Motiv begrenzt, Motiv wird nie angeschnitten (übersprungen ohne Node) | `tests/Trendo.App.Tests/WebDesignZ3Tests.cs` | Suche im Test |
| echt | Webseite: echte Browser-Vorschau je Technik (übersprungen ohne Chrome) | `tests/Trendo.App.Tests/WebsitePreviewTests.cs` | Suche im Test |
| echt | assimp echt über TrendoOS --assimp-convert: eigener Prozess schreibt GLB | `tests/Trendo.Core.Tests/AssimpConvertTests.cs` | AssimpFact |
| echt | assimp echt: OBJ → GLB → OBJ (8 Ecken, 12 Dreiecke bleiben), kaputte Datei ergibt Exit 3 ohne Absturz | `tests/Trendo.Core.Tests/AssimpConvertTests.cs` | TRENDO_WERKZEUGE_BELEG |
| echt | assimp −4: im Prozess – ungültige Eingabe Exit 2, fehlende DLL 5, keine DLL-Datei 5 (BadImageFormat) | `tests/Trendo.Core.Tests/AssimpConvertTests.cs` | Suche im Test |
| echt | LUFS nach BS.1770 stimmt mit FFmpeg ebur128 überein (±0,5 LU) – Musiksignal und Sinus | `tests/Trendo.Core.Tests/AudioDspTests.cs` | Suche im Test |
| echt | axe echt: fehlerhafte Seite → Bild-Alt, Sprache, Titel, Link, Knopf, Beschriftung, Überschriftenfolge gefunden (Seiten-Skript lief nicht); saubere Seite ohne Fehler | `tests/Trendo.Core.Tests/AxeCheckTests.cs` | AxeFact |
| echt | axe −1: ohne Einrichtung, ohne HTML, bei Fehler oder kaputter Antwort klare Meldung statt Absturz | `tests/Trendo.Core.Tests/AxeCheckTests.cs` | Suche im Test |
| echt | DIN 5008 echt: Form A gemessen (Anschrift 44,7 mm, Info 32 mm, Betreff 80,46 mm); langer Text → zweite Seite mit „Seite 2 von 2“ | `tests/Trendo.Core.Tests/DinLetterTests.cs` | TypstFact |
| echt | DIN 5008 echt: typst setzt Form B als PDF; gemessen: Anschrift bei 25/62,7 mm, Info bei 125/50 mm, Betreff bei 98,46 mm (±0,5 mm) | `tests/Trendo.Core.Tests/DinLetterTests.cs` | TypstFact |
| echt | DIN 5008 −2: Einschleusen über # oder ] im Text ist geschützt | `tests/Trendo.Core.Tests/DinLetterTests.cs` | Suche im Test |
| echt | Cli: UTF-8-Ausgabe eines Python-Prozesses kommt mit Umlauten an (stdout und stderr) | `tests/Trendo.Core.Tests/DocMarkdownTests.cs` | Suche im Test |
| echt | Dokumente echt: Word, Excel (Tabelle), PowerPoint, HTML, PDF → Markdown mit Umlauten und Titel; kaputte Excel-Datei abgelehnt statt Müll | `tests/Trendo.Core.Tests/DocMarkdownTests.cs` | MdFact |
| echt | Kampf S1-05 Browser-Spiel (echt mit Node): Schwert besiegt Gegner vor der Figur; Boss braucht zwei Schläge; Geschoss fliegt und trifft; ohne Waffe nichts | `tests/Trendo.Core.Tests/GameCombatTests.cs` | Suche im Test |
| echt | Inventar S1-05 Browser-Spiel (echt mit Node): Apfel einsammeln, der Figur geben, +1 Leben; Inventar im Spielstand | `tests/Trendo.Core.Tests/GameInventoryTests.cs` | Suche im Test |
| echt | Spielmenü S1-14: Einstellungen – Lautstärke in 25er-Schritten, hoher Kontrast, große Schrift, ruhige Effekte, Karte; bleiben beim nächsten Start; Steuerungshilfe | `tests/Trendo.Core.Tests/GameMenusTests.cs` | Suche im Test |
| echt | Spielmenü S1-14: Esc pausiert (Spiel steht), Esc setzt fort; „Zum Menü“ führt zurück | `tests/Trendo.Core.Tests/GameMenusTests.cs` | Suche im Test |
| echt | Spielmenü S1-14: Start im Menü (Spielen, Einstellungen, Steuerung), das Spiel steht still, Tasten erreichen das Spiel nicht; Enter (oder Leertaste) auf „Spielen“ startet | `tests/Trendo.Core.Tests/GameMenusTests.cs` | Suche im Test |
| echt | Spielmenü S1-14: ausgeschaltet startet das Spiel sofort; Einstellung landet im Spiel; Zeichnen mit Menü, Karte und Kontrast läuft ohne Fehler | `tests/Trendo.Core.Tests/GameMenusTests.cs` | Suche im Test |
| echt | Spielmenü S1-14: mit Spielstand steht „Fortsetzen“ oben und setzt fort; „Spielen“ beginnt neu | `tests/Trendo.Core.Tests/GameMenusTests.cs` | Suche im Test |
| echt | CORE-06: Ton-Spur und Treffer-Animation – Geräusch je Ereignis und Musik landen im Spielordner, config.js nennt sie, game.js spielt sie (sonst Piepton); nach einem Treffer ist der Zustand „treffen“ | `tests/Trendo.Core.Tests/GameProTests.cs` | Suche im Test |
| echt | CORE-06: Gebautes Spiel mit 2 Leveln, Hintergrund und Lauf-Animation – config.js trägt alles, Bildstreifen liegt im Spielordner, game.js ist gültiges JavaScript (Node) | `tests/Trendo.Core.Tests/GameRulesTests.cs` | Suche im Test |
| echt | CORE-06: Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node) | `tests/Trendo.Core.Tests/GameRulesTests.cs` | Suche im Test |
| echt | Spielstand S1-15 Browser-Spiel (echt mit Node): Speichern am Speicherpunkt, neuer Start setzt fort, neuere Fassung wird ignoriert, Spiel ohne Spielstände speichert nicht | `tests/Trendo.Core.Tests/GameSavesTests.cs` | Suche im Test |
| echt | Fahrzeug S1-05 Browser-Spiel (echt mit Node): Wagen einsteigen und schneller, Treffer kostet nur den Wagen; Ufo fliegt mit Treibstoff; E steigt aus | `tests/Trendo.Core.Tests/GameVehicleTests.cs` | Suche im Test |
| echt | Gameplay S1-05 Browser-Spiel (echt mit Node): Doppelsprung per neuem Tastendruck in der Luft, nur mit Fähigkeit; Sprint mit Umschalt | `tests/Trendo.Core.Tests/GameplayTests.cs` | Suche im Test |
| echt | glTF +5: Einrichten per npm – feste Fassung, eigener Ordner, ohne Installationsskripte | `tests/Trendo.Core.Tests/GltfToolsTests.cs` | Suche im Test |
| echt | glTF echt: Khronos-Prüfer – Trendo-Regal fehlerfrei (9840 Dreiecke), abgeschnittene Datei mit GLB_UNEXPECTED_END_OF_CHUNK_DATA | `tests/Trendo.Core.Tests/GltfToolsTests.cs` | ValidatorFact |
| echt | glTF echt: Packen für Web & Brille – Regal deutlich kleiner, danach weiter fehlerfrei mit meshopt und WebP | `tests/Trendo.Core.Tests/GltfToolsTests.cs` | TransformFact |
| echt | Symbole echt: 276 Sätze mit Lizenzregel; in 4 echten Sätzen (21 000+ Symbole inkl. Aliasse) wird jedes Symbol sicher und als gültiges XML ausgegeben | `tests/Trendo.Core.Tests/IconSetsTests.cs` | IconFact |
| echt | ICS echt: jeder Termin im Fenster 08.11.2025–08.11.2028 stimmt mit der unabhängigen Python-Referenz überein (Zeit und Titel) | `tests/Trendo.Core.Tests/IcsForeignTests.cs` | ReferenzFact |
| echt | ICS fremd −2: Schutz vor Riesenreihen – minütliche Regel höchstens 5000 Termine | `tests/Trendo.Core.Tests/IcsForeignTests.cs` | Suche im Test |
| echt | IFC echt: Vorlage „2-Zimmer-Wohnung“ → IFC4 mit IfcOpenShell, wieder eingelesen, gleiche Anzahl Bauteile | `tests/Trendo.Core.Tests/IfcExchangeTests.cs` | TRENDO_WERKZEUGE_BELEG |
| echt | IFC −2: Dateiname nur aus sicheren Zeichen | `tests/Trendo.Core.Tests/IfcExchangeTests.cs` | Suche im Test |
| echt | Videos mit KI echt mit FFmpeg: drei Standbilder mit Einblendung ergeben ein MP4 mit der geplanten Länge, vorhandene Datei bleibt | `tests/Trendo.Core.Tests/KreativKits3Tests.cs` | FfmpegFact |
| echt | Claude-Regeln: Web nutzt lokale Bibliotheken (ohne CDN, ohne GSAP), 3D arbeitet sichtbar in Blender, Hunyuan3D wird in der EU nicht verwendet | `tests/Trendo.Core.Tests/KreaturUndRegelnTests.cs` | Suche im Test |
| echt | Kreatur-Baukasten: echter Blender baut einen Drachen mit Rig und schreibt .blend und .glb (übersprungen ohne Blender) | `tests/Trendo.Core.Tests/KreaturUndRegelnTests.cs` | BlenderFact |
| echt | Briefe F3: Rechtschreibung im Brieftext – Tippfehler mit Stelle und Vorschlag, Platzhalter [Datum] und richtige Wörter nicht angestrichen | `tests/Trendo.Core.Tests/LetterF3Tests.cs` | NeedsFact |
| echt | LETTER-012: Echte Tesseract-Texterkennung eines gerenderten Briefs (deu), unsichere Wörter markiert, Sprachpaket per Hash geprüft | `tests/Trendo.Core.Tests/LetterQaTests.cs` | NeedsFact |
| echt | LTR-QA-002/004 / LETTER-003/016: DOCX real mit LibreOffice zu PDF und ODT; A4, 1 Seite, Hash im Protokoll | `tests/Trendo.Core.Tests/LetterQaTests.cs` | NeedsFact |
| echt | Echt mit FFmpeg: Szene mit Logo und Bauchbinde, Mikrofon und Musik gemischt – Datei entsteht, Pegel-Datei hat Messwerte, Logo ist im Bild | `tests/Trendo.Core.Tests/LiveShowTests.cs` | Suche im Test |
| echt | M2 + M4: echte Testdateien richtig eingestuft; zwei Animationen mit und ohne Figur heißen in der GLB genau wie die Spielaktionen | `tests/Trendo.Core.Tests/MixamoTests.cs` | BlenderFact |
| echt | M3: Blender bereitet die eigene Figur für den Auto-Rigger vor (ohne Skelett, Füße auf dem Boden, FBX) | `tests/Trendo.Core.Tests/MixamoTests.cs` | BlenderFact |
| echt | M4: Blender gleicht Namen an, setzt Meter-Maßstab, Laufen auf der Stelle, überträgt auf die Figur und exportiert FBX und GLB | `tests/Trendo.Core.Tests/MixamoTests.cs` | BlenderFact |
| echt | M4: Unreal-Protokoll – Erfolg mit Größe; Faktor-100-Fehler und Abbruch werden gemeldet | `tests/Trendo.Core.Tests/MixamoTests.cs` | Suche im Test |
| echt | MLT +8: melt-Argumente – Datei, Ausgabe als eigene Argumente, H.264/AAC | `tests/Trendo.Core.Tests/MltProjectTests.cs` | Suche im Test |
| echt | MLT echt: melt rendert den Trendo-Schnitt – 5,5 s, 1280×720, Bild und Ton, rote Stelle bei 3,5 s | `tests/Trendo.Core.Tests/MltProjectTests.cs` | TRENDO_WERKZEUGE_BELEG |
| echt | Kurzclip: Start am stärksten Abschnitt; echter Export mit FFmpeg ergibt ein Hochkant-Video mit Ton | `tests/Trendo.Core.Tests/MusicProTests.cs` | Suche im Test |
| echt | Objekte echt: Modell mit gepinnter Prüfsumme; Demobild → Fahrrad und Hund sicher erkannt, Fahrzeug nur einmal gezählt, markiertes Bild geschrieben | `tests/Trendo.Core.Tests/ObjectDetectTests.cs` | YoloxFact |
| echt | IFC/Open3D +3: jede Python-Laufzeit hat Info, eigenes Skript und eindeutige Kennung | `tests/Trendo.Core.Tests/Open3dTests.cs` | Suche im Test |
| echt | Open3D echt: Glätten halbiert das Rauschen ohne Schrumpfen, Vereinfachen erreicht die Ziel-Dreieckszahl, Unsinn wird abgelehnt | `tests/Trendo.Core.Tests/Open3dTests.cs` | Open3dFact |
| echt | PCI echt: gepinnte Prüfsumme; 2524 Hersteller, 21 739 Geräte; bekannte Grafikkarte und Netzwerkkarte gefunden; Lesen unter 100 ms | `tests/Trendo.Core.Tests/PciIdsTests.cs` | PciFact |
| echt | S4-04 echt: MIDI-Datei auf Platte, von FFmpeg als MIDI erkannt (wenn vorhanden), SHA-256 gleich nach erneutem Speichern | `tests/Trendo.Core.Tests/PianoRollTests.cs` | TRENDO_MIDI_BELEG |
| echt | PMTiles +6: Hinweis nennt das neue Format | `tests/Trendo.Core.Tests/PmTilesTests.cs` | Suche im Test |
| echt | PMTiles echt: go-pmtiles wandelt MBTiles um (bis 87 381 Kacheln, mit Blattverzeichnissen) – jede Kachel gleich wie im Original | `tests/Trendo.Core.Tests/PmTilesTests.cs` | PmFact |
| echt | Vorschau V1 echt: 1080p-Stück liefert das zur Uhr passende Bild, verwirft zu alte, kaputte Datei meldet Fehler | `tests/Trendo.Core.Tests/PreviewStreamTests.cs` | FfmpegFact |
| echt | Vorschau V1: Auch auf einem voll ausgelasteten PC kommen aus einem 1080p-Stück alle Bilder in brauchbarem Tempo an | `tests/Trendo.Core.Tests/PreviewStreamTests.cs` | FfmpegFact |
| echt | Vorschau V1: fehlendes ffmpeg meldet Fehler statt abzustürzen | `tests/Trendo.Core.Tests/PreviewStreamTests.cs` | Suche im Test |
| echt | Python-Vorlage besteht den eigenen Test, Git bleibt ohne Remote | `tests/Trendo.Core.Tests/PromptActionTests.cs` | Suche im Test |
| echt | ffmpeg macht aus dem Bild eine JPEG-Datei | `tests/Trendo.Core.Tests/PromptActionTests.cs` | FfmpegFact |
| echt | restic echt: sichern, Stände, zurückholen (Byte für Byte gleich), Ablage ohne Klartext, zweite Sicherung nur mit der Änderung, falsches Passwort verständlich | `tests/Trendo.Core.Tests/ResticBackupTests.cs` | ResticFact |
| echt | S4-14 echt: MIDI mit Tempowechseln auf Platte, FFmpeg erkennt sie, Prüfsumme stabil | `tests/Trendo.Core.Tests/ScoreSyncTests.cs` | TRENDO_SCORE_BELEG |
| echt | Schnitt echt: T-Shirt (Teagan) und Hose (Titan) für Brust 92/Taille 76/Hüfte 100 → SVG in Millimetern, plausible Bogengröße, ergänzte Maße genannt | `tests/Trendo.Core.Tests/SewingTests.cs` | SewingFact |
| echt | Schnitt −1: unbekannter Schnitt, fehlender oder unplausibler Brustumfang, fehlende Einrichtung, Fehler und kaputte Antwort → klare Meldung | `tests/Trendo.Core.Tests/SewingTests.cs` | Suche im Test |
| echt | S4-16-P/N: zwei Spuren platziert → richtige Kanäle, Tiefton nur Tiefen, WAV mit Kanalmaske 5.1(side) und Kanalplan; Stereo wird nicht still als 5.1 geschrieben; Kopfhörer-Vorschau links lauter und rechts später | `tests/Trendo.Core.Tests/SpatialAudioTests.cs` | TRENDO_51_BELEG |
| echt | S4-13 echt: Mixdown als WAV 24 Bit – FFmpeg liest 7,5 s + Ausklang, Lautheit messbar, Prüfung ohne Rot (eigene Klänge) | `tests/Trendo.Core.Tests/StepSequencerTests.cs` | TRENDO_SEQ_BELEG |
| echt | CORE-05: Render-Verlauf – neue Bilder bekommen eindeutige Namen, neuestes zuerst; Blender-Skript (Bauen + Cycles-Render) ist gültiges Python | `tests/Trendo.Core.Tests/Studio3DProTests.cs` | Suche im Test |
| echt | SYS-02-P23 (T-0244 getrennt): Audio mit echtem ffmpeg umwandeln, abspielbar, Prüfsumme im Protokoll | `tests/Trendo.Core.Tests/Sys02Tests.cs` | NeedsFact |
| echt | SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P07: Text aus einem echten PDF | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt | `tests/Trendo.Core.Tests/Sys10Tests.cs` | NeedsFact |
| echt | SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert) | `tests/Trendo.Core.Tests/Sys10Tests.cs` | Suche im Test |
| echt | SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler | `tests/Trendo.Core.Tests/Sys11Tests.cs` | Suche im Test |
| echt | T-0223 T8: Rechtschreibung Deutsch – Fehler gefunden mit Vorschlägen, richtige Wörter, Zahlen, Adressen und eigene Wörter nicht angestrichen | `tests/Trendo.Core.Tests/TextDocTests.cs` | NeedsFact |
| echt | Tiled echt: pytiled-parser liest Trendos Karten – Größe, Tileset, Ebenen, Objekte mit Eigenschaften und Welt stimmen | `tests/Trendo.Core.Tests/TiledExportTests.cs` | PytiledFact |
| echt | Tiled −1: leeres oder riesiges Level wird verständlich abgelehnt; unbekannte Zeichen bleiben leer | `tests/Trendo.Core.Tests/TiledExportTests.cs` | Suche im Test |
| echt | Vektorkarte +4: Rasterleser lehnt Vektorkarte weiter ab (Karten-Studio schaltet dann auf MapLibre) | `tests/Trendo.Core.Tests/VectorMapTests.cs` | Suche im Test |
| echt | Vektorkarte echt: Protomaps-Ausschnitt – alle Kacheln lesbar, entpackt als MVT (Protobuf), Gebiet stimmt | `tests/Trendo.Core.Tests/VectorMapTests.cs` | VecFact |
| echt | V5 und V6 echt: Stück mit Bewegung, Deckkraft, Tonverlauf, Effekten und bewegter Einblendung wird von ffmpeg wirklich gerendert | `tests/Trendo.Core.Tests/VideoAnimTests.cs` | FfmpegFact |
| echt | Mit echtem FFmpeg: Überblenden + Titel im Hintergrund exportiert (Fortschritt steigt bis 100 %), Abbrechen beendet FFmpeg und löscht die halbe Datei | `tests/Trendo.Core.Tests/VideoEditPlusTests.cs` | Suche im Test |
| echt | Videoschnitt-Export mit echtem FFmpeg: Testbild mit Ton + Standbild + Musik → MP4 mit Bild und Ton in der erwarteten Länge | `tests/Trendo.Core.Tests/VideoEditTests.cs` | Suche im Test |
| echt | Echter Export mit FFmpeg (Software-Encoder): Bereich 1–3 s als Hochformat 1080 × 1920 mit Lautheit; nur Ton als M4A | `tests/Trendo.Core.Tests/VideoExportTests.cs` | Suche im Test |
| echt | CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt | `tests/Trendo.Core.Tests/VideoProTests.cs` | Suche im Test |
| echt | S3-02 echt mit FFmpeg: Proxy 540p gleich lang wie das Original, Vorschau nimmt den Proxy, Export immer das Original; fehlt das Original, wird nicht der Proxy ausgeliefert | `tests/Trendo.Core.Tests/VideoProxyTests.cs` | Suche im Test |
| echt | Echter Export mit eingebrannten Untertiteln (FFmpeg libass) | `tests/Trendo.Core.Tests/VideoSubtitleTests.cs` | Suche im Test |
| echt | CORE-07: Git-Stand anlegen, Änderung erkennen, speichern (echtes git) | `tests/Trendo.Core.Tests/W4DevTests.cs` | NeedsFact |
| echt | CORE-07: Python- und .NET-Vorlage bestehen ihren eigenen Test (echte Werkzeuge) | `tests/Trendo.Core.Tests/W4DevTests.cs` | Suche im Test |
| echt | CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check) | `tests/Trendo.Core.Tests/W4GameTests.cs` | Suche im Test |
| echt | CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben | `tests/Trendo.Core.Tests/W4MediaTests.cs` | NeedsFact |
| echt | X-06: Kopie holen (echtes git, lokales Quell-Repo) mit Herkunftsdatei; unsichere Adressen abgewiesen | `tests/Trendo.Core.Tests/W4OssTests.cs` | NeedsFact |
| echt | CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen | `tests/Trendo.Core.Tests/W4Studio3DTests.cs` | NeedsFact |
| echt | X-01: Video-Paket mit echtem FFmpeg und 3D-Schriftzug mit echtem Blender | `tests/Trendo.Core.Tests/W5FusionTests.cs` | Suche im Test |
| echt | YARA echt: Archiv mit gepinnter Prüfsumme; yr kompiliert alle 310 Regeln; saubere Dateien ohne Befund; Prüfmarke in Unterordner wird als Erpressersoftware gemeldet | `tests/Trendo.Core.Tests/YaraScanTests.cs` | YrFact |
| echt | YARA echt: Regel-Archiv hat die gepinnte Prüfsumme; alle 310 Regeln landen flach im Regelordner, jede mit Regelkopf, 309 mit Erkennungsart | `tests/Trendo.Core.Tests/YaraScanTests.cs` | RulesFact |
| anforderung | Z6 PC-Lauf: Prüflauf mit echten lokalen Modellen über Ollama, Protokoll unter reports/ (nur mit TRENDO_PC_LAUF=1) | `tests/Trendo.App.Tests/WebDesignStufe2UiTests.cs` | TRENDO_PC_LAUF, TRENDO_PC_MODELLE |
| anforderung | Z3 PC-Lauf: echtes Modell (Poly Haven, CC0) über Blender nach GLB, HDR-Licht, Grafikkarte, Bilder und gemessene Bildrate unter reports/ (nur mit TRENDO_PC_LAUF=1) | `tests/Trendo.App.Tests/WebDesignZ3Tests.cs` | TRENDO_PC_LAUF |
| anforderung | S4-20 echt gegen FFmpeg ebur128: Lautheit und True Peak von Trendo stimmen mit FFmpeg überein (±0,5) | `tests/Trendo.Core.Tests/AudioQaTests.cs` | TRENDO_AQA_BELEG |
| anforderung | S3-11-P echt mit FFmpeg: Scopes-Bild (Bild · Waveform · Vektorskop), neutrale LUT gilt als gleichwertig, falsche LUT (Farben vertauscht) nicht; Export mit LUT läuft | `tests/Trendo.Core.Tests/ColorScopesTests.cs` | TRENDO_LUT_BELEG |
| anforderung | M4 mit echten Analysen (optional): Ampel und Punkte werden ausgegeben | `tests/Trendo.Core.Tests/CoverStudioTests.cs` | TRENDO_COVER_ECHT |
| anforderung | S3-17-P echt mit FFmpeg: Abspann-Clip 6 s – am Anfang unten, am Ende oben (gemessen), gültiges MP4 | `tests/Trendo.Core.Tests/CreditsTests.cs` | TRENDO_CREDITS_BELEG |
| anforderung | Echt mit FFmpeg: ganze Auslieferung – Film, Stems gleich lang wie der Film, EDL, OTIO, Cue-Liste, Prüfbericht mit SHA-256 | `tests/Trendo.Core.Tests/FilmDeliveryTests.cs` | TRENDO_FILM_BELEG |
| anforderung | S3-13 echt: Wan 2.2 erzeugt einen Clip (1280 × 704); gleicher Zufallswert ergibt praktisch dieselben Bilder, anderer Zufallswert andere; Nachweis passt | `tests/Trendo.Core.Tests/GenVideoTests.cs` | TRENDO_GENVIDEO_BELEG, TRENDO_GENVIDEO_ECHT |
| anforderung | S3-05-P echt mit FFmpeg: rotes Hintergrundvideo, darüber Greenscreen-Aufnahme mit blauer Figur – Grün wird durchsichtig, Figur bleibt; mit Maske links verschwindet die Figur rechts | `tests/Trendo.Core.Tests/GreenscreenTests.cs` | TRENDO_KEY_BELEG |
| anforderung | S3-09 echt mit FFmpeg: Vergleichsvorschau (Mund mit Namen, Tonwelle, Ton) als MP4 in Länge der Aufnahme | `tests/Trendo.Core.Tests/LipSyncTests.cs` | TRENDO_LIP_BELEG |
| anforderung | S3-09 echt: Mund im KI-Video (MediaPipe je Bild) gegen den Gesang verglichen; derselbe Mund 200 ms verschoben wird als versetzt erkannt | `tests/Trendo.Core.Tests/LipSyncTests.cs` | TRENDO_LIP_MOUTH, TRENDO_LIP_MOUTH_WAV |
| anforderung | S3-09 echt: Sprachaufnahme → Whisper-Wortzeiten → Mundbilder; Prüfung OK; dieselbe Spur auf 200 ms später gesetzten Ton → FAIL; Vorschau | `tests/Trendo.Core.Tests/LipSyncTests.cs` | TRENDO_LIP_BELEG, TRENDO_LIP_MODEL, TRENDO_LIP_WAV, TRENDO_LIP_WHISPER |
| anforderung | S3-02 Multicam echt mit FFmpeg: zwei Kameradateien mit 2,25 s Versatz werden über den Ton gefunden; Kamera ohne gemeinsamen Ton wird abgewiesen | `tests/Trendo.Core.Tests/MulticamTests.cs` | TRENDO_MC_BELEG |
| anforderung | Suno V8 echt mit FFmpeg: Sprache nach 2 s über der Musik; Musik ist beim Sprechen deutlich leiser (Sidechain) und blendet aus | `tests/Trendo.Core.Tests/MusicSoundsTests.cs` | TRENDO_SPEECH_BELEG |
| anforderung | S4-06 echt: Host in der Abschottung – abstürzendes Plugin wird isoliert gemeldet (Absturzbericht, nichts übernommen); nicht freigegebenes Plugin startet gar nicht; freigegebenes VST3 verarbeitet Ton | `tests/Trendo.Core.Tests/PluginHostTests.cs` | TRENDO_PLUGINHOST_ENV, TRENDO_PLUGINHOST_SCRIPT, TRENDO_PLUGIN_BELEG, TRENDO_PLUGIN_CRASH, TRENDO_PLUGIN_GOOD, TRENDO_PLUGIN_GOOD_PARAMS |
| anforderung | S4-06 echt (nur lesen): Airwindows Consolidated VST3 und CLAP – x64, Einstiegspunkt gefunden, ladbar; Bericht mit Prüfsumme | `tests/Trendo.Core.Tests/PluginInspectTests.cs` | TRENDO_PLUGIN_BELEG, TRENDO_PLUGIN_ECHT |
| anforderung | S3-18-P echt mit FFmpeg: ProRes 422 HQ (.mov), EXR- und PNG-Sequenz mit WAV – Codec, Profil, Pixelformat, Bildzahl und Tonlänge per ffprobe geprüft | `tests/Trendo.Core.Tests/ProExportTests.cs` | TRENDO_PRO_BELEG |
| anforderung | S3-03 echt mit FFmpeg: Storyboard-Tafel je Einstellung in Plan-Länge (MP4, 4 s), Code sichtbar | `tests/Trendo.Core.Tests/ShotPlanTests.cs` | TRENDO_SHOT_BELEG |
| anforderung | T-0223 T1: Abgleich – von LibreOffice umgewandelte Dateien liest Trendo mit gleichem Inhalt (nur mit TRENDO_LO_ABGLEICH) | `tests/Trendo.Core.Tests/TextDocTests.cs` | TRENDO_LO_ABGLEICH |
| anforderung | T-0230: ODT speichern und lesen, auch nach LibreOffice Writer – Seite, Kopf-/Fußzeile, Links, Kommentare, Fuß-/Endnoten, Verzeichnis, Beschriftung, Alternativtext | `tests/Trendo.Core.Tests/TextProTests.cs` | TRENDO_WERKZEUGE |
| anforderung | S4-12-P echt mit FFmpeg/Rubber Band: Tempo 80 % und +2 Halbtöne sowie Intonation +30 Cent bleiben im Toleranzbereich (Länge ±1 %, Tonhöhe ±10 Cent, Phase stabil) | `tests/Trendo.Core.Tests/TimePitchTests.cs` | TRENDO_TP_BELEG |
| anforderung | S3-16 echt mit FFmpeg: WebVTT und SRT werden von FFmpeg gelesen und ins Video eingebettet (drei Untertitelspuren de/it/en) | `tests/Trendo.Core.Tests/TimedTextTests.cs` | TRENDO_SUBS_BELEG |
| anforderung | S3-12-P echt mit FFmpeg: verrauschter Clip entrauscht – gegen das saubere Original messbar besser (SSIM, PSNR); 2× hochskaliert ohne Artefakte, absichtlich kaputtes Hochskalieren rot | `tests/Trendo.Core.Tests/VideoEnhanceTests.cs` | TRENDO_ENH_BELEG |
