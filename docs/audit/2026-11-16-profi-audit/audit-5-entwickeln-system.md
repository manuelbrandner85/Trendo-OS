# Audit Gruppe 5 – „Entwickeln“ und „System & Sicherheit“

Stand: Arbeitskopie `studios-aufraeumen` (ba52927a8), nur gelesen. Nichts gebaut, keine Tests gestartet.
Belege als `pfad:zeile` relativ zum Repo. „nicht geprüft“ = von mir nicht im Code nachgesehen, also keine Aussage.

Vorbemerkungen, die für alle 20 Studios gelten:

- **Keines der Programme dieser Gruppe läuft in der Programm-Hülle.** `docs/programme/katalog.json` führt alle 39 Programme der beiden Bereiche mit `"Shell": false`. Autosave, Versionen und das Undo/Redo aus `ProgramDocument` gibt es hier also nicht; was an Rückgängig/Sichern vorhanden ist, ist je Seite einzeln gebaut.
- **Rechte in der Desktop-App sind faktisch immer „Besitzer“.** `Rbac`/`ApprovalGate` sind sauber (`src/Trendo.Core/Rbac.cs:14-30`, `src/Trendo.Core/ApprovalGate.cs:69-124`) und die lokale API setzt Rollen serverseitig per Token durch (`src/Trendo.Api/LocalApi.cs:33-51`, Selbsttest `src/Trendo.Api/ApiSelfTest.cs:36-43`). In der Oberfläche steht aber 688-mal `Principal.LocalOwner` (Zählung über `src/Trendo.App/*.cs`), ein anderer Principal entsteht dort nie. 28 Stellen rufen `Approvals.Approve(..., Principal.LocalOwner)` selbst auf, meist direkt nach `Request` im selben Klick.
- **KI-Aktionen („Los“) sind in dieser Gruppe überwiegend Anzeige oder Sperre.** Alle Adapter stehen auf `Partial` oder `Blocked` (`src/Trendo.Core/PromptAction/CapabilityCatalog.cs:83-102`, `ConnectedAdapters.cs:294-914`, `SystemPromptAdapter.cs:261-291`). Claude über die Brücke kann dagegen Seiten bedienen (`MainWindow.ClaudeBridge.cs:247-250`), sensible Seiten nur nach Zustimmung je Lauf, die Seite „Freigaben“ gar nicht.

## Übersicht

| Studio-Id | Name | Programme (Seitenschlüssel) | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| CORE-07 | Webseite bauen | `dev` (Website-Generator + Web-Design-Studio + „Programme aus Vorlage“), `code` (Code-Editor) | TEILWEISE | Software-Teil baut, testet nur per `dotnet build`, paketiert nichts; Code-Editor ohne Terminal/Debugger/Autosave | groß |
| X-03 | Apps zusammenklicken | `baukasten` | TEILWEISE | Es entsteht keine App, nur eine statische Einzelseite aus 7 Bausteinen | groß |
| X-04 | Trendo erweitern | `erweiterungen` | TEILWEISE | Erweiterungen sind nur Daten (Vorlagen, Linklisten, Texte), keine neuen Funktionen | groß |
| X-06 | Projekte von GitHub holen | `oss` | TEILWEISE | Echte Installation nur mit nachgebildetem Netz getestet; KI-Aktion klont nichts | mittel |
| X-10 | Programme nachbauen | `nachbau` | TEILWEISE | Verwaltungswerkzeug (Regeln, Tagebuch, Ähnlichkeitsprüfung), entwickelt selbst nichts | mittel |
| CORE-01 | Desktop einrichten | `start`, `studios`, `dateien`, `desktop-symbole`, `einrichtung`, `projects` | TEILWEISE | Dateien ohne Rückgängig für Dateiaktionen, kein smb/sftp; Status selbst IN_PROGRESS | mittel |
| CORE-04 | PC, Pi & Handy verbinden | `verbund` | TEILWEISE | Echte Pi-/Android-Hardware nie geprüft | groß |
| CORE-11 | Daten schützen | `schutz`, `sicherung`, `approvals` | TEILWEISE | ZIP-Sicherung unverschlüsselt und ohne Prüflesen; „Passwörter schützen“ ohne Passwortverwaltung | mittel |
| SPEC-12 | System auf Gefahren prüfen | `forensik` | TEILWEISE | Keine Systemprüfung, nur Prüfsumme, Beweisfälle, Einzeldatei-Regelprüfung | groß |
| SYS-01 | Startmodus wählen | `modus` | TEILWEISE | Linux/Pi-Edition nicht im Angebot; Seite ist Anzeige plus Stick-Schreiber | mittel |
| SYS-03 | Surfen & Herunterladen | `downloads`, `netz-pruefen` | TEILWEISE | Kein eigener Browser, „Surfen“ startet fremde Browser | mittel |
| SYS-04 | Programme installieren | `appcenter` | TEILWEISE | Echter winget-Lauf nicht getestet; Freigabe wird im selben Klick selbst erteilt | mittel |
| SYS-06 | Dateien teilen | `teilen` | VOLLWERTIG (im engen Zuschnitt) | Nur Lesen, unverschlüsseltes HTTP im Heimnetz | klein |
| SYS-07 | Geräte erkennen | `geraete` | TEILWEISE | Reine Liste, keine Aktion am Gerät | mittel |
| SYS-11 | Programme starten | `apps`, `hinzufuegen`, `prog-pflege`, `components` | TEILWEISE | Kindersperre nur an einem Knopf; Deinstallieren verweist auf Windows | mittel |
| SYS-12 | Schrift & Farben anpassen | `settings`, `bedien-sehen`, `personen`, `updates`, `lizenz` | TEILWEISE | Echter Update-Weg in Tests ersetzt; Narrator-Bedienung offen | mittel |
| SYS-13 | Qualität prüfen | `system`, `systemcheck`, `fortschritt`, `hardware`, `qual-leistung` | TEILWEISE | Entwickler-Prüfwerkzeuge, wenig Nutzen für Endnutzer | klein |
| X-02 | Einrichten & Reparieren | `heilung` | TEILWEISE | Repariert nur Trendos eigenen Datenordner | mittel |
| X-08 | Handy verbinden | `handy` | TEILWEISE | Kein Lauf mit echtem Handy belegt | groß |
| X-09 | Leistung anzeigen | `ressourcen` | TEILWEISE | Anzeige; beendet nur Trendo-eigene Programme (bewusst) | klein |

Zählung Gesamturteile: 1 × VOLLWERTIG (eng), 19 × TEILWEISE, 0 × FEHLT, 0 × BLOCKIERT.

---

## CORE-07 Webseite bauen (inkl. Web-Design-Studio, „Software-Studio“, Code-Editor)

Das Studio besteht aus vier verschieden reifen Teilen auf zwei Seiten:
(a) **Web-Design-Studio** (visueller Editor Puck + React Three Fiber im WebView), (b) **Website-Generator** aus Beschreibung, (c) **„Programme aus einer Vorlage“** (das frühere Software-Studio), (d) **Code-Editor** (`code`).

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | (a) echter visueller Editor im WebView (`MainWindow.WebDesign.cs:92-107`, Bündel `src/Trendo.App/Assets/webdesign/assets/editor.js`). Die Seite `dev` selbst ist aber eine lange gestapelte Karten-Seite (`MainWindow.W4Web.cs:87-102`, `MainWindow.W4Dev.cs:16-67`); (d) echter Editor mit Baum, Reitern, Ausgabe (`MainWindow.CodeEditor.cs:146-256`) |
| 2 | Werkzeuge | TEILWEISE | Web: Seiten, Tokens, Komponenten, Medien, CMS, Sprachen, Mods, Daten (`MainWindow.WebDesign.cs:80-90`). Code: Speichern, Ausführen, Testen, Projektsuche, Git-Stand speichern (`MainWindow.CodeEditor.cs:165-174`). Kein Terminal, kein Debugger, keine Diff-Ansicht, kein Branch/Push, kein Umbenennen/Löschen von Dateien im Baum (im gelesenen Code nicht vorhanden) |
| 3 | Neues Projekt | VOLLWERTIG | 4 Vorlagen legen echte Dateien an (`src/Trendo.Core/DevStudio.cs:11-60`), Website-Bau (`MainWindow.W4Web.cs:261-280`), neue Seite im Design-Studio (`MainWindow.WebDesignSite.cs:38`) |
| 4 | Import | TEILWEISE | HTML-Import und Figma-Import in den visuellen Editor (`MainWindow.WebDesignCode.cs:106`, `:129`), Ordner öffnen im Code-Editor (`MainWindow.CodeEditor.cs:157-164`). Beide Import-Wege in Tests ersetzt (`WebDesignParseOverride`, `FigmaHandlerOverride`, `tests/Trendo.App.Tests/WebDesignStufe3QaUiTests.cs:23,59`) |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG | Projektliste mit „Bearbeiten“ (`MainWindow.W4Dev.cs:45-57`), `seite.trendo.json` wird geladen (`src/Trendo.Core/Website/WebDesign.cs:134`) |
| 6 | Rückgängig | TEILWEISE | Design-Studio: Rückgängig, Stände, Varianten (`MainWindow.WebDesignKi.cs:311,364-398`). Code-Editor: nur das Undo des Textfelds je Datei, keine Projekt-Historie außer Git. Generator-Editor (`web-editor`) ist ein einfaches `TextBox` (`MainWindow.W4Web.cs:162`) |
| 7 | Vorschau | TEILWEISE | Design-Studio live im Editor. Generator: Standbild aus Headless-Chrome, Desktop 1280 und Handy 390 (`MainWindow.W4Web.cs:290-301`), kein Live-Neuladen (so auch `docs/claude/vorbilder/web-vscode-webflow.md:16`). Code-Editor: keine Vorschau |
| 8 | Speichern/Autosave | TEILWEISE | Design-Studio speichert mit Sicherung der Vorfassung (`MainWindow.WebDesign.cs:30-37`, Test „Web Design +2“ in `WebDesignTests.cs`). Code-Editor nur Strg+S, kein Autosave (`MainWindow.CodeEditor.cs:54-63,134`) |
| 9 | Wiederherstellung nach Absturz | FEHLT | Ungespeicherter Entwurf liegt nur im Speicher (`_wdDraft`, `MainWindow.WebDesign.cs:38-40`); Code-Editor hält Änderungen nur in `TextDocument` (`MainWindow.CodeEditor.cs:20`). Kein Wiederherstellen-Weg gefunden |
| 10 | Export | TEILWEISE | Web: fertige statische Website in `veroeffentlichen/`, ZIP-Paket (`src/Trendo.Core/Website/WebDesignPublish.cs:76-86`), optional Vite-Dateien. Software: **kein** Export, kein `dotnet publish`, kein Installer, kein Paket (`DevStudio.cs` vollständig gelesen, 96 Zeilen) |
| 11 | Rundreise | TEILWEISE | Export wird in echtem Chrome geprüft (Tests „in echtem Chrome“, z. B. `WebDesignStufe3UiTests.cs:123`, übersprungen ohne Chrome/Node). Export → wieder als Projekt öffnen: nicht geprüft |
| 12 | Fachfunktion real | TEILWEISE | Web: ja, echte Dateien, Qualitätsprüfung mit Reparatur (`WebsiteQuality.Check/Fix`), SEO-Prüfung, axe-core. Software: „Testen“ heißt bei .NET nur `dotnet build` (`DevStudio.cs:66`), bei der Web-Vorlage gibt es keinen Test und die Meldung lautet trotzdem „Test nicht bestanden“ (`DevStudio.cs:72`, `MainWindow.W4Dev.cs:56`) |
| 13 | KI/Claude | TEILWEISE | Design-Studio: KI plant Aktionen auf dem Seitenmodell, erst nach „Übernehmen“ (`MainWindow.WebDesignKi.cs:172-308`). Claude arbeitet im echten Projektordner, das Studio zeigt die Datei (`MainWindow.ClaudeChat.cs:761-778`). Prompt-Aktion „software.anlegen“ legt nur die Vorlage an; „software.veroeffentlichen“ ist gesperrt (`SoftwarePromptAdapter.cs:10,36,51`) |
| 14 | Tests | TEILWEISE | Echt: Vorlagen laufen mit echtem python/node/dotnet, wenn vorhanden (`tests/Trendo.Core.Tests/W4DevTests.cs:36-52`), echtes Git (`:20`), Editor-Bündel in echtem Chrome. Ersetzt: KI (`WebDesignKiOverride`), Netlify (`FakeNetlify`, `WebDesignStufe3Teil2UiTests.cs:146`), axe (`AxeRunOverride`). Der WebView in der App wird im Testmodus nie geladen (`MainWindow.WebDesign.cs:94`, Test `WebDesignUiTests.cs:23`) → Brücke WebView↔App mit echtem WebView2 NICHT GETESTET. „Ausführen“ startet im Testmodus nichts (`MainWindow.CodeEditor.cs:88`) |
| 15 | Bedienung | TEILWEISE | Einfach/Profi vorhanden (`MainWindow.W4Web.cs:88-96`). Code-Editor: Strg+S, Strg+Umschalt+S; kein Rechtsklick-Menü und kein Drag-and-drop im Baum (`MainWindow.CodeEditor.cs`, kein `ContextMenu`/`DragDrop`). Vollbild: nicht geprüft |

**Antwort auf die Leitfrage Software:** Das Studio *erstellt* lauffähige Kleinstprojekte (Konsole C#/.NET 10, Python, Node, statische Webseite), *startet* sie (`CodeProject.RunCommand`, `src/Trendo.Core/CodeProject.cs:89-96`) und *testet* Python/Node echt. Es *kompiliert* .NET nur als Test-Ersatz und *paketiert nichts*. Zielplattformen: nur das, was auf dem PC installiert ist; es gibt keine Auswahl. Real geprüft sind genau die drei „Hallo“-Vorlagen.

**Antwort auf die Leitfrage Web:** Echte Projektdateien ja; Designfunktionen ja (stärkster Teil der Gruppe); Codebearbeitung ja, aber an drei verschiedenen Stellen (TextBox im Generator, Code-Pane im Design-Studio, eigener Code-Editor); responsive Vorschau ja (Ansichten mit eigener Breite, echte Chrome-Tests); validierter Export ja (SEO-, Export-, Geheimnis-Prüfung vor dem Paket, `WebDesignPublish.cs:63-73`); Veröffentlichen zu Netlify gebaut mit Zustimmung je Dateiliste (`MainWindow.WebDesignCode.cs:331-351`), aber nur gegen eine Nachbildung getestet.

**Gesamturteil:** TEILWEISE – der Web-Design-Teil ist nahe an einem Fachprogramm, der Software-Teil ist ein Vorlagen-Anleger mit Texteditor.

**Was fehlt (priorisiert):**
1. Software: Bauen/Veröffentlichen als eigener Schritt (z. B. `dotnet publish`, Einzeldatei-EXE, ZIP), mit geprüftem Ergebnis.
2. „Ausführen“ und „Testen“ laufen synchron im Klick und blockieren die Oberfläche bis 120 s bzw. 180 s (`MainWindow.CodeEditor.cs:94`, `DevStudio.cs:71`, `Cli.cs:31`); keine Live-Ausgabe, kein Stopp, keine Eingabe.
3. Autosave und Wiederherstellen nach Absturz für Code-Editor und Design-Entwurf.
4. Ein Arbeitsplatz statt Kartenstapel: Editor, Vorschau und Code nebeneinander (auch im Vorbild-Abgleich offen, `web-vscode-webflow.md:15-16`).
5. Echter Lauf des WebView-Editors in der App und ein echter Netlify-Lauf als Beleg unter `reports/`.
6. Dateiverwaltung im Code-Editor (umbenennen, löschen, verschieben), Diff vor dem Commit.

**Abnahmekriterien:**
- Aus jeder Vorlage entsteht per Klick ein startbares Paket; ein Test startet das Paket in einem leeren Ordner und prüft die Ausgabe.
- Ein Programm mit Endlosschleife lässt sich stoppen, die Oberfläche bleibt währenddessen bedienbar.
- Nach hartem Beenden bietet der Code-Editor und das Design-Studio den letzten ungespeicherten Stand an.
- Eine exportierte Website besteht in echtem Chrome die Export-Prüfung ohne Befund und lässt sich wieder als Projekt öffnen.
- Ein echter Upload zu einem Test-Konto ist mit Rohlog unter `reports/` belegt.

**Aufwand:** groß. **Risiko:** mittel (Prozessverwaltung, WebView2-Abhängigkeit).
**Dateien:** `src/Trendo.Core/DevStudio.cs`, `src/Trendo.Core/CodeProject.cs`, `src/Trendo.Core/Website/*`, `src/Trendo.App/MainWindow.W4Dev.cs`, `MainWindow.W4Web.cs`, `MainWindow.CodeEditor.cs`, `MainWindow.WebDesign*.cs`, `src/Trendo.App/WebsitePreview.cs`, `tools/webdesign-editor/`.

---

## X-03 Apps zusammenklicken (`baukasten`, eingegliedert in CORE-07)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Bausteine ziehen, in der Vorschau anklicken und umsortieren (`MainWindow.W4Builder.cs:11-13,129,162`) |
| 2 | Werkzeuge | TEILWEISE | 7 Bausteinarten: Überschrift, Text, Bild, Knopf, Liste, Kontakt, Trennlinie (`src/Trendo.Core/PageBuilder.cs:16-17`) |
| 3 | Neues Projekt | TEILWEISE | Es gibt genau eine Seite `seite.json` je Projekt (`MainWindow.W4Builder.cs:18,32-44`) |
| 4 | Import | TEILWEISE | Vorlage aus Erweiterung (Test „X-04 (UI): … im Baukasten öffnen“); Datei-Import: nicht geprüft |
| 5 | Bearbeiten | VOLLWERTIG | Laden aus Projekt oder Arbeitsordner (`:32-44`) |
| 6 | Rückgängig | VOLLWERTIG | Stapel, Knopf und Strg+Z (`:22,74,177,231`); kein Wiederholen |
| 7 | Vorschau | VOLLWERTIG | Handy/Bildschirm, „Ausprobieren im Browser“ (`:181`) |
| 8 | Speichern | TEILWEISE | Speichern ins Projekt (`:46-59`); Autosave: nicht geprüft |
| 9 | Absturz | FEHLT | kein Weg gefunden |
| 10 | Export | TEILWEISE | `index.html` + Bilder, ZIP (`:96-101`, `PageBuilder.cs:60-71`) |
| 11 | Rundreise | NICHT GETESTET | kein Test, der die exportierte ZIP wieder öffnet |
| 12 | Fachfunktion | TEILWEISE | Erzeugt echtes, sauber maskiertes HTML (`PageBuilder.cs:32-55`). Eine **App** entsteht nicht: keine Logik, keine Daten, kein Formular mit Auswertung, keine zweite Seite (so auch `docs/claude/vorbilder/baukasten-carrd-webflow.md:15-16`) |
| 13 | KI | TEILWEISE | „seite.schreiben“ schreibt HTML und `seite.json`; 3D, Scroll, Video, Veröffentlichen fest `false` (`SystemPromptAdapter.cs:46-64`) |
| 14 | Tests | TEILWEISE | UI-Test mit Ziehen, Eigenschaften, Rückgängig, ZIP (Testname „App-Designer: …“); kein Browser-Lauf |
| 15 | Bedienung | TEILWEISE | Ziehen, Strg+Z; Rechtsklick/Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – ein ordentlicher Einzelseiten-Baukasten; das Versprechen „Apps“ im Namen wird nicht erfüllt.
**Was fehlt:** 1. Mehrere Seiten und Navigation. 2. Daten und Logik (Liste speichern, Formular auswerten). 3. Direktes Bearbeiten des Textes auf der Seite (`baukasten-carrd-webflow.md:14`, gegen Code prüfen). 4. Wiederholen, Autosave. 5. Abgrenzung oder Zusammenführung mit dem Web-Design-Studio, das alles davon schon kann.
**Abnahmekriterien:** Eine Einkaufslisten-App aus der Beispielliste speichert Einträge und zeigt sie nach Neustart; zwei Seiten sind verlinkt; die exportierte ZIP läuft im Browser ohne Netz.
**Aufwand:** groß (oder klein, wenn auf das Web-Design-Studio umgestellt und ehrlich umbenannt). **Risiko:** gering.
**Dateien:** `src/Trendo.Core/PageBuilder.cs`, `src/Trendo.App/MainWindow.W4Builder.cs`.

---

## X-04 Trendo erweitern (`erweiterungen`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Liste mit Installieren/Ein/Aus/Entfernen (`MainWindow.W5Ext.cs`, 169 Zeilen; Aufbau nicht zeilenweise gelesen) |
| 2 | Werkzeuge | VOLLWERTIG | Prüfen des Manifests, Markenschutz, Prüfsumme, atomarer Austausch (`src/Trendo.Core/Extensions.cs:23,52,74,113-114`) |
| 3–5 | Neu/Import/Bearbeiten | TEILWEISE | Installieren aus Ordner oder ZIP, Beispiel anlegen (`Extensions.cs:105,191-194`); kein Editor zum Schreiben einer Erweiterung |
| 6 | Rückgängig | TEILWEISE | Entfernen mit Freigabe (`Extensions.cs:179`) |
| 7–9 | Vorschau/Speichern/Absturz | nicht geprüft | – |
| 10–11 | Export/Rundreise | FEHLT | Kein Export einer eigenen Erweiterung gefunden |
| 12 | Fachfunktion | TEILWEISE | Installation real, mit ApprovalGate und Not-Stopp (`Extensions.cs:88-90`, Test „T-0241 (UI): Not-Stopp verhindert das Installieren“). Erweiterungen sind **nur Daten**: Arten `seite`, `vorhaben`, `links`, `text` (`Extensions.cs:8,15-17`) – „Neue Funktionen für Trendo hinzufügen“ (`StudioCatalog.cs:92`) stimmt so nicht |
| 13 | KI | TEILWEISE | „erweiterung.entwerfen“ nur Entwurf; Installation gesperrt (`SystemPromptAdapter.cs:227,291`) |
| 14 | Tests | TEILWEISE | 7 Tests, Katalog lokal mit Prüfsummen; GitHub-Katalog nachgebildet (Testnamen „X-04 Katalog: …“) |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil:** TEILWEISE – sicher gebaut, aber ein Vorlagen-Paketdienst, kein Erweiterungssystem.
**Was fehlt:** 1. Entscheidung: echte Funktions-Erweiterungen (dann mit Sandbox) oder ehrlicher Name „Vorlagen & Pakete“. 2. Erweiterung in Trendo selbst erstellen und als ZIP ausgeben. 3. Aktualisieren installierter Erweiterungen.
**Abnahmekriterien:** Eine in Trendo erstellte Erweiterung lässt sich exportieren, auf einem leeren Datenordner installieren und wirkt dort sichtbar; eine veränderte ZIP wird nach der Freigabe abgewiesen.
**Aufwand:** groß (bei echten Funktionen), sonst klein. **Risiko:** hoch bei fremdem Code.
**Dateien:** `src/Trendo.Core/Extensions.cs`, `ExtensionCatalog.cs`, `src/Trendo.App/MainWindow.W5Ext.cs`, `MainWindow.W5ExtCat.cs`.

---

## X-06 Projekte von GitHub holen (`oss`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Suche, Trefferliste, Karten je Bereich (Katalog-Tools „Suchen“, „Prüfen & speichern“) |
| 2 | Werkzeuge | VOLLWERTIG | Lizenz einordnen und erklären, Kopie mit Herkunftsdatei, fertiges Windows-Programm aus dem Release mit Prüfsumme, sicheres Entpacken, Entfernen (`src/Trendo.Core/OpenSource.cs:12-51`, `GitHubApps.cs:86,127,171,218`) |
| 3–5 | Neu/Import/Bearbeiten | TEILWEISE | Holen ja; das geholte Projekt wird nicht im Code-Editor weitergeführt (Verknüpfung nicht geprüft) |
| 6 | Rückgängig | VOLLWERTIG | Entfernen löscht genau den eigenen Ordner (`GitHubApps.cs:217-218`) |
| 12 | Fachfunktion | TEILWEISE | Real, aber: Installer, die Trendo nicht auspacken kann, werden „still für den aktuellen Benutzer ausgeführt“ (`GitHubAppsInstallers.cs:6-8,38`) – fremder Code läuft nach einer Zustimmung |
| 13 | KI | TEILWEISE | „github.erklaeren“ nur Lizenztext; „projekt.klonen“ gesperrt (`SystemPromptAdapter.cs:162,287`); der Katalog sagt „Es wird nichts geklont“ (`CapabilityCatalog.cs:85`), die Oberfläche klont |
| 14 | Tests | TEILWEISE | 4 Tests mit Kennung, 2 davon „echt/übersprungen“; UI-Installation mit Nachbildung („GitHub (UI): Treffer mit Beschreibung …“) |
| 7–11, 15 | – | nicht geprüft | – |

**Gesamturteil:** TEILWEISE – nützlicher Holdienst mit Lizenzhilfe.
**Was fehlt:** 1. Beleg eines echten Laufs (Suche → Installieren → Starten → Entfernen) unter `reports/`. 2. Vor stillem Installer-Lauf ein deutlicher eigener Hinweis mit Signaturprüfung. 3. Übergabe eines Quellcode-Projekts an den Code-Editor mit Bauanleitung.
**Abnahmekriterien:** Ein freies Projekt wird geholt, im Code-Editor geöffnet und gebaut; ein Programm mit falscher Prüfsumme wird abgewiesen; Entfernen hinterlässt keinen Ordner.
**Aufwand:** mittel. **Risiko:** mittel (fremde Programme).
**Dateien:** `src/Trendo.Core/OpenSource.cs`, `GitHubApps.cs`, `GitHubAppsInstallers.cs`, `src/Trendo.App/MainWindow.W4Oss.cs`, `MainWindow.GitHubPlus.cs`.

---

## X-10 Programme nachbauen (`nachbau`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Formular: Projekt anlegen, Tagebuch, Prüfung (`MainWindow.W5CleanRoom.cs`, 103 Zeilen; Katalog-Tools nur „＋ Anlegen“) |
| 2 | Werkzeuge | TEILWEISE | Rollen, Regeln, Tagebuch, Ähnlichkeitsprüfung über normalisierte Tokens (`src/Trendo.Core/CleanRoom.cs:13-25,52-73`) |
| 10 | Export | TEILWEISE | Bericht als `.md` (`CleanRoom.cs:90`) |
| 12 | Fachfunktion | TEILWEISE | Ähnlichkeitsprüfung rechnet real. „Programme … neu entwickeln“ (`StudioCatalog.cs:93`) leistet die Seite nicht – sie begleitet nur |
| 13 | KI | TEILWEISE | „nachbau.beschreiben“ schreibt Regeln als `.md` (`SystemPromptAdapter.cs:101-117`) |
| 14 | Tests | TEILWEISE | 4 Tests (u. a. „X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung …“) |
| 3–9, 11, 15 | – | nicht geprüft | – |

**Gesamturteil:** TEILWEISE – Begleitwerkzeug mit Formular, kein Entwicklungsprogramm.
**Was fehlt:** 1. Anforderungsliste als bearbeitbares Dokument mit Abgleich „erfüllt/offen“. 2. Verbindung zum Code-Editor und zu Tests je Anforderung. 3. Vergleichsansicht der Treffer nebeneinander.
**Abnahmekriterien:** Jede Anforderung hat einen Status und einen verknüpften Test; die Prüfung zeigt ähnliche Stellen nebeneinander; der Bericht nennt Herkunft und Datum jedes Eintrags.
**Aufwand:** mittel. **Risiko:** gering.
**Dateien:** `src/Trendo.Core/CleanRoom.cs`, `src/Trendo.App/MainWindow.W5CleanRoom.cs`.

---

## CORE-01 Desktop einrichten (`start`, `studios`, `dateien`, `desktop-symbole`, `einrichtung`, `projects`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Dateien: Symbolansicht, Registerkarten, Ziehen zwischen Ordnern (`MainWindow.Dateien.cs:233-244,548`); Desktop mit Symbolen und Fenstern |
| 2 | Werkzeuge | TEILWEISE | Neu, Ausschneiden, Kopieren, Einfügen, Umbenennen, Papierkorb (Katalog-Tools `dateien`). Offen laut Abgleich: Rückgängig für Dateiaktionen, Packen im Kontextmenü, smb/sftp (`docs/claude/vorbilder/dateien-explorer-finder-nautilus.md:22-24`; gegen Code nicht einzeln geprüft) |
| 3–5 | Projekte | TEILWEISE | Projekt anlegen und öffnen (`projects`); Einzelheiten nicht geprüft |
| 6 | Rückgängig | FEHLT (Dateien) | siehe Abgleich `:23` |
| 8–9 | Speichern/Absturz | nicht geprüft | Fensterlage wird gemerkt (Testname „Fenster: Einstellung übernehmen … alle kommen mit Lage und Zustand zurück“) |
| 12 | Fachfunktion | VOLLWERTIG | Echte Dateioperationen, Papierkorb statt Löschen |
| 13 | KI | TEILWEISE | „desktop.zeigen“ nur Anzeige, „desktop.aendern“ gesperrt (`ConnectedAdapters.cs:294`, `SystemPromptAdapter.cs:261`) |
| 14 | Tests | TEILWEISE | 9 Tests mit Kennung plus Fenster-, Tasten-, Registerkarten-Tests (Headless) |
| 15 | Bedienung | VOLLWERTIG | Rechtsklick-Menüs (`MainWindow.Dateien.cs:462,552`), Drag-and-drop, Tasten je Stil (Testname „Tasten: Finder Zeilenschalter = umbenennen …“) |

**Gesamturteil:** TEILWEISE – der Dateimanager ist nahe am Vorbild, das Studio selbst steht im eigenen Register auf IN_PROGRESS (`requirements.yaml:143-148`).
**Was fehlt:** 1. Strg+Z für Verschieben/Löschen. 2. Netzlaufwerke. 3. Umzugs-Assistent vom alten PC (`desktop-windows11-gnome-shell.md:27`).
**Abnahmekriterien:** Verschieben und Löschen lassen sich mit Strg+Z zurücknehmen; ein Netzordner erscheint unter „Andere Orte“.
**Aufwand:** mittel. **Risiko:** mittel (Datenverlust bei Dateiaktionen).
**Dateien:** `src/Trendo.App/MainWindow.Dateien.cs`, `MainWindow.Files.cs`, `MainWindow.DesktopIcons.cs`, `MainWindow.StudiosView.cs`, `MainWindow.Setup.cs`.

---

## CORE-04 PC, Pi & Handy verbinden (`verbund`) und X-08 Handy verbinden (`handy`)

Beide teilen Code (`src/Trendo.Core/W7/*`, `MainWindow.Phone*.cs`, `MainWindow.W7.cs`), X-08 ist in CORE-04 eingegliedert (`StudioCatalog.cs:150`).

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Geführte Schritte und Gerätelisten (Katalog-Tools `handy`, `verbund`) |
| 2 | Werkzeuge | TEILWEISE | Koppeln per Einmal-Code, QR, WLAN-adb, Bluetooth, Datei empfangen, Handy bedient Trendo (Testnamen „Handy: …“, „X-08: …“) |
| 12 | Fachfunktion | TEILWEISE / NICHT GETESTET | Begleit-Server echt mit Token-Hash, Rate-Limit, nur Heimnetz (Testnamen „CORE-04 Begleiter: echter HTTP-Server …“). **Echte Pi- und Android-Hardware fehlt** (`requirements.yaml:172`); der Studiosatz sagt selbst „Gegenstellen in Arbeit“ (`StudioCatalog.cs:108`). Kein Beleg eines Handy-Laufs unter `reports/` gefunden (Suche nach adb/scrcpy/Begleiter traf nur Testprotokolle) |
| 13 | KI | TEILWEISE | „handy.nennen“ ruft nur `adb devices -l`; Verbinden und Verbund gesperrt (`CapabilityCatalog.cs:100-101`, `SystemPromptAdapter.cs:281,283`) |
| 14 | Tests | TEILWEISE | CORE-04 14, X-08 13, „Handy“ 23 Testnamen; adb/Bluetooth über nachgebildeten Prozess-Läufer |
| 6–11 | – | nicht anwendbar/nicht geprüft | – |
| 15 | Bedienung | nicht geprüft | – |

Serverseitige Rechte hier vorhanden: Token je Gerät, Entkoppeln mit zeitgleichem Hash-Vergleich, Bedienen nur mit Erlaubnis am PC (Testnamen oben).

**Gesamturteil:** TEILWEISE – PC-Seite sorgfältig, das eigentliche Versprechen (Pi und Handy wirklich verbunden) ist unbelegt.
**Was fehlt:** 1. Lauf mit echtem Android-Gerät (Kabel und WLAN) mit Rohlog. 2. Lauf mit echtem Raspberry Pi. 3. Eigenes „Datei senden“ zum Handy (`geraete-phonelink-kdeconnect.md:21`). 4. Aufträge auf das stärkste Gerät verteilen (`:26`).
**Abnahmekriterien:** Ein echtes Handy wird per QR gekoppelt, eine Datei geht in beide Richtungen, Entfernen sperrt es sofort; dasselbe mit einem Pi; je ein Protokoll mit SHA-256 unter `reports/`.
**Aufwand:** groß. **Risiko:** hoch (Hardware-Vielfalt, Netz).
**Dateien:** `src/Trendo.Core/W7/Companion.cs`, `Fusion.cs`, `PhoneAccess.cs`, `WirelessAdb.cs`, `BluetoothLink.cs`, `src/Trendo.App/MainWindow.Phone.cs`, `MainWindow.PhoneEasy.cs`, `MainWindow.PhoneRemote.cs`, `MainWindow.W7.cs`, `MainWindow.W5Fusion.cs`.

---

## CORE-11 Daten schützen (`schutz`, `sicherung`, `approvals`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Zeitleiste der Sicherungen mit Vergleich, Vorschau, Einzeldatei (`MainWindow.BackupBrowser.cs:52,84,95-101`) |
| 2 | Werkzeuge | VOLLWERTIG | Sichern, Vorschau der Änderung, Wiederherstellen ganz und einzeln, restic-Sicherung auf anderes Laufwerk, Ordner auf Geheimnisse prüfen (`src/Trendo.Core/Backup.cs:16,38,55,89,133`, `Sys/ResticBackup.cs`, `W3/Security.cs:44-62`) |
| 6 | Rückgängig | VOLLWERTIG | Vor jedem Wiederherstellen wird der heutige Stand gesichert (`Backup.cs:58`), bei Einzeldatei die alte Fassung aufgehoben (`:141-146`) |
| 8 | Zuverlässigkeit | TEILWEISE | `Create` hat keine Fehlerbehandlung je Datei und räumt nicht auf (`Backup.cs:21-29`): bricht das Lesen einer Datei ab, bleibt eine unvollständige `trendo-sicherung-*.zip` liegen, die `List()` als neueste Sicherung führt (`:11-12`). Kein Prüflesen nach dem Schreiben |
| 9 | Wiederherstellung | VOLLWERTIG (geprüft) | Rundreise in Tests: „W0-05: Sicherung + Wiederherstellung …“ (`Tests.cs`), „CORE-11: Sicherung – … Wiederherstellen holt den alten Stand zurück“ und „eine kaputte ZIP lässt die Daten unverändert“ (`SystemPagesTests.cs`), „T-0214: Zwei Wiederherstellungen in derselben Sekunde + schreibgeschützte Datei“ (`BackupRestoreTests.cs`). restic echt, Byte für Byte (`ResticBackupTests.cs:96-98`, übersprungen ohne restic im Prüfordner) |
| 10–11 | Export/Rundreise | TEILWEISE | ZIP unverschlüsselt im Datenordner `backups`; nur restic verschlüsselt und extern. Wiederherstellen löscht nichts, was nach der Sicherung dazukam (`Backup.cs:61-71`) – es ist kein Zurücksetzen auf den Stand |
| 12 | Fachfunktion | TEILWEISE | Gesichert wird **nur Trendos Datenordner** (`Backup.cs:22`), nicht Dokumente des Nutzers. „Passwörter schützen“ (`StudioCatalog.cs:97`): eine Passwortverwaltung habe ich in diesem Studio nicht gefunden; der Katalog nennt nur `ISecretStore` (`CapabilityCatalog.cs:90`) |
| 13 | KI | TEILWEISE | „geheimnis.zeigen“ nennt nur den Speicher; Ändern gesperrt (`ConnectedAdapters.cs:323`, `SystemPromptAdapter.cs:263`). Für Claude sind `schutz`, `sicherung` sensibel, `approvals` gesperrt (`MainWindow.ClaudeBridge.cs:247-249`) |
| 14 | Tests | VOLLWERTIG | siehe 9; UI: `BackupUiTests`, `BackupTimelineUiTests`, `SchutzResticUiTests` |
| 15 | Bedienung | nicht geprüft | – |

Hinweis Doku≠Code: `docs/claude/vorbilder/schutz-windows-sicherheit-timemachine.md:20` nennt die Einzeldatei-Rückholung noch als „nur ganze Wiederherstellung“; im Code ist sie da (`Backup.cs:133`).

**Gesamturteil:** TEILWEISE – die Sicherung ist der am besten real geprüfte Teil der Gruppe, hat aber einen Zuverlässigkeitsfehler und einen engen Umfang.
**Was fehlt:** 1. `Backup.Create` robust machen: je Datei abfangen, in eine Zwischendatei schreiben, nach Erfolg umbenennen, Prüflesen. 2. Eigene Ordner des Nutzers in die Sicherung aufnehmen können. 3. Verschlüsselung auch ohne restic oder restic als Standard. 4. Echtes „Zurück auf diesen Stand“ (auch Entfernen neuerer Dateien, nach Rückfrage). 5. Passwort-Teil klären.
**Abnahmekriterien:** Eine während der Sicherung gesperrte Datei führt zu einer vollständigen Sicherung mit Hinweis, nie zu einer halben ZIP in der Liste; nach „Zurück auf Stand X“ ist der Ordner byte-gleich zur Sicherung; die Sicherung auf einem zweiten Laufwerk ist ohne Passwort nicht lesbar.
**Aufwand:** mittel. **Risiko:** hoch (Datenverlust).
**Dateien:** `src/Trendo.Core/Backup.cs`, `src/Trendo.Core/Sys/ResticBackup.cs`, `src/Trendo.Core/W3/Security.cs`, `src/Trendo.Core/ApprovalGate.cs`, `src/Trendo.App/MainWindow.BackupBrowser.cs`, `MainWindow.SchutzRestic.cs`, `MainWindow.Setup.cs:530`.

---

## SPEC-12 System auf Gefahren prüfen (`forensik`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Formular: Datei wählen, Prüfen, Fall anlegen (`MainWindow.Forensik.cs:33,55`, 77 Zeilen) |
| 2 | Werkzeuge | TEILWEISE | SHA-256-Vergleich, Beweisfall mit Kette, Bericht, 120-Stunden-Löschung (`src/Trendo.Core/W3/Forensics.cs:22,31,56-64,73,81,92`), Regelprüfung mit yara-x nach Einrichtung (`MainWindow.ForensikYara.cs:16-50`) |
| 12 | Fachfunktion | TEILWEISE | Das Gebaute arbeitet real (Kopie wird gegen das Original geprüft, `Forensics.cs:64`). **Das System wird aber nicht geprüft**: keine Prüfung von Autostart, Prozessen, Diensten, Netzverbindungen. Der Satz „Prüft das System auf Gefahren“ (`StudioCatalog.cs:98`) verspricht mehr |
| 13 | KI | BLOCKIERT | „forensik.aendern“ gesperrt, Katalog „Forensik unverändert lassen“ (`SystemPromptAdapter.cs:265`, `CapabilityCatalog.cs:91`) – bewusst (Verbesserung 34 abgelehnt), nicht als Mangel zu werten |
| 14 | Tests | TEILWEISE | 3 Tests mit Kennung („Beweis wird unverändert … gesichert“, „Höchstens 120 h …“); yara-Lauf über `Override` (`MainWindow.ForensikYara.cs:13`) → echter yara-x-Lauf NICHT GETESTET, soweit ich sehe |
| 3–11, 15 | – | nicht geprüft | – |

**Gesamturteil:** TEILWEISE – Beweissicherung gut, Gefahrenprüfung des Systems fehlt.
**Was fehlt:** 1. Systemprüfung (Autostart, laufende Programme mit Signatur, offene Verbindungen) mit verständlicher Ampel. 2. Ordner- statt Einzeldatei-Prüfung mit Regeln. 3. Echter yara-x-Lauf als Beleg. 4. Anbindung an Defender-Ergebnisse (ein Defender-Knopf existiert bei SYS-03, `MainWindow.Sys03.cs:66`).
**Abnahmekriterien:** Ein Testprogramm im Autostart wird gemeldet; eine EICAR-Testdatei in einem Ordner wird gefunden; ein Fall ist nach 120 h vollständig gelöscht (besteht schon als Test).
**Aufwand:** groß. **Risiko:** mittel (Fehlalarme; 120-Stunden-Regel darf nicht berührt werden).
**Dateien:** `src/Trendo.Core/W3/Forensics.cs`, `src/Trendo.Core/Sys/YaraScan.cs`, `src/Trendo.App/MainWindow.Forensik.cs`, `MainWindow.ForensikYara.cs`.

---

## SYS-01 Startmodus wählen (`modus`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE | Seite `modus` (`MainWindow.W7.cs:62`) mit „Stick oder SSD schreiben …“ (Katalog-Tools) |
| 12 | Fachfunktion | TEILWEISE | Stick-Schreiber mit Schutz vor falschem Laufwerk, geprüft auf virtueller Festplatte (Testnamen „Stick-Schreiber: … schreiben (virtuelle Festplatte), Boot-Tasten“, „Stick-Schreiber schützt: …“). Start von einem echten Stick auf echter Hardware: kein Beleg gesucht/gefunden – nicht geprüft |
| 13 | KI | TEILWEISE | „modus.zeigen“ nur Anzeige (`CapabilityCatalog.cs:89`) |
| 14 | Tests | TEILWEISE | 2 Tests mit Kennung, 1 „echt/übersprungen“ |
| übrige | – | nicht geprüft | Inhalt der Seite nicht zeilenweise gelesen |

**Gesamturteil:** TEILWEISE – Status selbst IN_PROGRESS (`requirements.yaml:240-242`).
**Was fehlt:** 1. Beleg „Stick geschrieben und davon gestartet“. 2. Klare Aussage in der Seite, dass Linux/Pi derzeit nicht im Angebot sind (Entscheidung Desktop Edition), statt eines Modus-Versprechens.
**Abnahmekriterien:** Ein geschriebener Stick startet auf einem Testgerät bis zum Trendo-Desktop; das Windows-Laufwerk lässt sich in keinem Weg auswählen.
**Aufwand:** mittel. **Risiko:** hoch (Datenträger löschen).
**Dateien:** `src/Trendo.App/MainWindow.W7.cs`, `MainWindow.SetupAbbild.cs`.

---

## SYS-03 Surfen & Herunterladen (`downloads`, `netz-pruefen`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Download-Liste mit Fortschritt; drei Reiter Werkzeuge (Katalog-Tabs `netz-pruefen`) |
| 2 | Werkzeuge | VOLLWERTIG | Prüfsumme, Link-Warnung, Kurzlink auflösen, Quarantäne, Defender, Lesemodus, Offline-Kopie, PDF, Lesezeichen Import/Export, Downloads sortieren mit Rückgängig (`MainWindow.Sys03.cs:34-170`) |
| 6 | Rückgängig | TEILWEISE | Sortieren rückgängig (`:98`), Quarantäne zurück (`:61`) |
| 12 | Fachfunktion | TEILWEISE | Downloads echt mit Anhalten/Fortsetzen über `.part` (`src/Trendo.Core/Sys/DownloadQueue.cs:7-9,92`). **Surfen:** kein eigener Browser – es gibt „Im Standardbrowser öffnen“, Chrome, Tor starten (Katalog-Tools; in `MainWindow.Sys03.cs` kein `NativeWebView`) |
| 13 | KI | TEILWEISE | „datei.laden“ mit Seiteneffekt nach Freigabe, höchstens 2 MB (`ConnectedAdapters.cs:351`, `CapabilityCatalog.cs:92`) |
| 14 | Tests | TEILWEISE | 17 Tests mit Kennung; Fortsetzen gegen lokalen Testserver („SYS-03-P07 …“); Download-Manager „ohne Netz (nachgebildet)“ |
| übrige | – | nicht geprüft | – |

**Gesamturteil:** TEILWEISE – guter Download- und Prüfhelfer; „Surfen“ im Namen ist ein Startknopf für fremde Browser.
**Was fehlt:** 1. Entweder eigener Browser (WebView ist im Projekt vorhanden) oder Name ohne „Surfen“. 2. Echter Download aus dem Netz als Beleg. 3. Ein Arbeitsplatz statt dreier Reiter mit Einzelwerkzeugen.
**Abnahmekriterien:** Eine Adresse lässt sich in Trendo öffnen und lesen; ein abgebrochener 100-MB-Download setzt nach Neustart fort und hat die richtige Prüfsumme.
**Aufwand:** mittel. **Risiko:** mittel (Browser = Angriffsfläche).
**Dateien:** `src/Trendo.Core/Sys/Sys03.cs`, `Sys/DownloadQueue.cs`, `src/Trendo.App/MainWindow.Sys03.cs`, `MainWindow.DownloadManager.cs`.

---

## SYS-04 Programme installieren (`appcenter`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Suche, Karten beliebter Programme, Listen mit Knöpfen (`MainWindow.AppCenter.cs:91-115`) |
| 2 | Werkzeuge | VOLLWERTIG | Suchen, Installieren, Aktualisieren, Entfernen über winget bzw. flatpak (`src/Trendo.Core/W3/Packages.cs:54-87`) |
| 12 | Fachfunktion | TEILWEISE | Befehle real, als Benutzer, mit ID-Prüfung (`Packages.cs:51,60`). Erfolg = Exitcode 0 (`:66`), keine Nachprüfung, ob das Programm danach da ist |
| – | Rechte | TEILWEISE | Der Text verspricht „Vor jeder Änderung fragt Trendo dich“ (`MainWindow.AppCenter.cs:93`). Tatsächlich: `Plan` → sofort `Approve(LocalOwner)` → `Execute` im selben Klick, ohne Dialog (`:30-32`). Auch „Entfernen“ hat keine Rückfrage. Kein `_settings.Child`-Test auf dieser Seite – die Kindersperre steht nur am Knopf in „Hinzufügen“ (`MainWindow.AddHub.cs:200`) |
| 13 | KI | TEILWEISE | „programm.installieren“ nur Katalogprogramme nach Lizenz und Freigabe (`CapabilityCatalog.cs:93`, `ConnectedAdapters.cs:432`) |
| 14 | Tests | NICHT GETESTET (echt) | UI-Test über `AppBackendOverride` (`MainWindow.AppCenter.cs:12`, Test „SYS-04 UI: Suchen, installieren, …“ in `W3UiTests.cs`); Core prüft nur das Lesen der Ausgaben und die Freigabe (`W3Tests.cs`). Ein echter winget-Lauf dieser Seite ist nicht belegt |
| übrige | – | nicht geprüft | – |

**Installation/Deinstallation/Update von Trendo selbst** (gehört fachlich hierher und zu SYS-12): Update über Velopack von den GitHub-Releases, Sicherung der laufenden Version, Rückkehr (`src/Trendo.App/TrendoUpdater.cs:24-88`), Startwächter nach drei Fehlstarts (`src/Trendo.Core/Updates.cs:28-50`). In Tests ist der Updater ersetzt (`TrendoUpdater.cs:5-6`). Realer Beleg vorhanden, aber alt: `reports/c4-setup/c4-setup-test-2026-10-25.txt` (Deinstallieren, alt installieren, Update 0.9.6→0.9.7, Rückfall, Daten byte-gleich – „ALLE BESTANDEN“). Für den heutigen Stand (0.9.103) habe ich keinen solchen Lauf gefunden. Rückkehr nutzt `FromNupkgNoChecksum` (`TrendoUpdater.cs:86`), also ohne Prüfsumme des gesicherten Pakets. Codesignatur gilt laut Projektregel als nicht bestanden.

**Gesamturteil:** TEILWEISE – funktional vollständig gedacht, aber der echte Weg ist ungeprüft und die „Freigabe“ ist eine Formsache.
**Was fehlt:** 1. Echte Rückfrage mit Vorschau vor Installieren und besonders vor Entfernen. 2. Kindersperre und Rollenprüfung im Core (`AppCenter.Plan` setzt fest `LocalOwner`, `Packages.cs:96`). 3. Echter winget-Lauf (installieren, erkennen, entfernen) mit Rohlog. 4. Nachprüfung nach dem Lauf. 5. Aktueller Setup-/Update-/Rückfall-Lauf als Beleg.
**Abnahmekriterien:** Im Kinderprofil ist Installieren und Entfernen auch über die API-Schicht verweigert; „Entfernen“ fragt mit Programmnamen nach; ein echtes kleines Programm wird installiert, in der Liste erkannt und wieder entfernt.
**Aufwand:** mittel. **Risiko:** hoch (Änderungen am System des Nutzers).
**Dateien:** `src/Trendo.Core/W3/Packages.cs`, `src/Trendo.App/MainWindow.AppCenter.cs`, `MainWindow.AddHub.cs`, `src/Trendo.App/TrendoUpdater.cs`, `src/Trendo.Core/Updates.cs`, `MainWindow.Updates.cs`, `UninstallPrompt.cs`.

---

## SYS-06 Dateien teilen (`teilen`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | VOLLWERTIG (eng) | Ordner wählen, Dauer, Starten/Beenden, Link; zusätzlich Handys suchen (LocalSend) (`MainWindow.Share.cs:102,112`, `MainWindow.ShareLocalSend.cs`) |
| 12 | Fachfunktion | VOLLWERTIG | Eigener Nur-Lese-Server, nur private Adresse, nur GET, Token im Pfad, Ablaufzeit (`src/Trendo.App/ShareServer.cs:9,16,20`, `src/Trendo.Core/W3/Sharing.cs:17-30,62`) |
| – | Rechte | VOLLWERTIG | Echter Bestätigungsdialog zwischen `Request` und `Approve`, Ablehnen und Schließen lehnen ab (`MainWindow.Share.cs:46-58`, Tests „SYS-06 Freigabe-Dialog: …“) |
| 13 | KI | TEILWEISE | „datei.teilen“ nur Ordner im Projekt, nach Freigabe, höchstens 24 h (`CapabilityCatalog.cs:94`) |
| 14 | Tests | VOLLWERTIG | 8 Tests, 1 „echt“ |
| Lücke | – | TEILWEISE | Unverschlüsseltes HTTP, kein Hochladen zurück (bewusst, `netz-teilen-downloads-taskmanager.md:14`) |

**Gesamturteil:** VOLLWERTIG im engen Zuschnitt – tut genau, was der Satz verspricht, und ist das Muster für eine richtige Freigabe.
**Was fehlt:** 1. Verschlüsselung (oder deutlicher Hinweis). 2. Empfangen vom anderen Gerät.
**Abnahmekriterien:** Ein zweites Gerät im WLAN lädt eine Datei; nach Ablauf antwortet der Link nicht mehr; von außerhalb des Heimnetzes ist nichts erreichbar.
**Aufwand:** klein. **Risiko:** mittel (Netz).
**Dateien:** `src/Trendo.App/ShareServer.cs`, `MainWindow.Share.cs`, `MainWindow.ShareLocalSend.cs`, `src/Trendo.Core/W3/Sharing.cs`, `W3/LocalSend.cs`.

---

## SYS-07 Geräte erkennen (`geraete`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE | Liste nach Gruppen, „Geräte suchen“, Namen unbekannter PCI-Geräte nachschlagen (`MainWindow.Devices.cs:28-49`, `MainWindow.DevicesPci.cs`) |
| 12 | Fachfunktion | TEILWEISE | Liest echte Gerätedaten (PnP-CSV unter Windows, `/sys` unter Linux), erkennt neu/entfernt (`src/Trendo.Core/W3/Devices.cs:22,45,80,118`). Keine Aktion am Gerät (kein Auswerfen, kein Drucker-Test) |
| 13 | KI | TEILWEISE | „geraete.nennen“ nur Liste (`ConnectedAdapters.cs:649`) |
| 14 | Tests | TEILWEISE | 3 Tests mit Kennung; Läufer nachgebildet (`MainWindow.Devices.cs:11`) |
| übrige | – | nicht anwendbar/nicht geprüft | – |

**Gesamturteil:** TEILWEISE – eine ehrliche Anzeige, kein Geräte-Manager.
**Was fehlt:** 1. Aktionen: sicher auswerfen, Testseite drucken, Kamera-Vorschau. 2. Treiber-/Problemhinweise mit nächstem Schritt.
**Abnahmekriterien:** Ein eingesteckter Stick erscheint binnen Sekunden und lässt sich auswerfen; ein Gerät mit Problem zeigt einen verständlichen nächsten Schritt.
**Aufwand:** mittel. **Risiko:** gering.
**Dateien:** `src/Trendo.Core/W3/Devices.cs`, `W3/PciIds.cs`, `src/Trendo.App/MainWindow.Devices.cs`, `MainWindow.DevicesPci.cs`.

---

## SYS-11 Programme starten (`apps`, `hinzufuegen`, `prog-pflege`, `components`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Hinzufügen-Zentrale mit Reitern und Suche; Pflege-Seite mit vier Reitern voller Einzelwerkzeuge (Katalog-Tabs) |
| 2 | Werkzeuge | VOLLWERTIG | Portable-ZIP nur mit Prüfsumme, Offline-Installer mit `.sha256`, Verknüpfungen, Autostart ein/aus mit Bestätigung, Voraussetzungen prüfen, Zurücksetzen mit Sicherung, Programmliste als winget-Import (`src/Trendo.Core/Sys/Sys11.cs:16,53,64,87,149`, `MainWindow.Sys11.cs:43-154`) |
| 6 | Rückgängig | TEILWEISE | Zurücksetzen legt Sicherung an (Test „SYS-11-P18 …“); Autostart wird verschoben, nicht gelöscht (`Sys11.cs:53`) |
| 11 | Rundreise | VOLLWERTIG | „SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise)“ (`Sys11Tests.cs`) |
| 12 | Fachfunktion | TEILWEISE | Deinstallieren fremder Programme: Knopf öffnet nur die Windows-Einstellungen (`MainWindow.Sys11.cs:67`) |
| – | Rechte | TEILWEISE | Kindersperre nur als deaktivierter Knopf in der Hinzufügen-Zentrale (`MainWindow.AddHub.cs:200-201`); die übrigen Installationswege dieser Seiten prüfen `_settings.Child` nicht (Vorkommen nur in `AddHub.cs` und `Lock.cs`) |
| 13 | KI | TEILWEISE | „programm.starten“ nur feste Liste und Datei im Projekt, nach Freigabe (`CapabilityCatalog.cs:96`) |
| 14 | Tests | TEILWEISE | 12 Tests mit Kennung, keiner „echt“ im Namen; echter winget-Weg für mpv in `NoVollpaketTests.cs` (Art des Laufs nicht geprüft) |

**Gesamturteil:** TEILWEISE – viele richtige Einzelteile, verteilt auf vier Seiten; Rechte nur an der Oberfläche.
**Was fehlt:** 1. Kindersperre und Rollen im Core statt am Knopf. 2. Eine Programm-Seite je Programm (Starten, Aktualisieren, Entfernen an einem Ort). 3. Zusammenführung mit SYS-04, das dasselbe über winget tut.
**Abnahmekriterien:** Im Kinderprofil schlägt jede Installation fehl, egal über welche Seite; ein portables Programm wird mit falscher Prüfsumme abgewiesen (besteht schon); Entfernen eines von Trendo installierten Programms geht ohne Windows-Einstellungen.
**Aufwand:** mittel. **Risiko:** mittel.
**Dateien:** `src/Trendo.Core/Sys/Sys11.cs`, `src/Trendo.Core/Dependencies.cs`, `ProgramInstaller.cs`, `src/Trendo.App/MainWindow.Sys11.cs`, `MainWindow.AddHub.cs`, `MainWindow.Programs.cs`.

---

## SYS-12 Schrift & Farben anpassen (`settings`, `bedien-sehen`, `personen`, `updates`, `lizenz`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Einstellungen-App mit Bereichen links und Suche (Testname „Einstellungen-App wie Windows 11: …“) |
| 2 | Werkzeuge | VOLLWERTIG | Design, Bedienstil, Schriftgröße, Kontrastprüfung, Vorlesen, Einstellungen sichern/einlesen, Bericht über alle Seiten (`MainWindow.Sys12.cs:98-147`, `src/Trendo.Core/Sys/Sys12.cs:22-89`) |
| 8 | Speichern | VOLLWERTIG | Einstellungen bleiben gespeichert (Testnamen „… Einstellung bleibt gespeichert“) |
| 10–11 | Export/Rundreise | TEILWEISE | Einstellungen speichern/einlesen (`:121-122`); Rundreise-Test nicht geprüft |
| 12 | Fachfunktion | TEILWEISE | Wirkt real auf die Oberfläche. Offen laut Register: Narrator-Bedienung (`requirements.yaml:341`), Status IN_PROGRESS (`:337`) |
| – | Personen/Rechte | TEILWEISE | Personen mit PIN, Kinderprofil; die Rolle wirkt nur an wenigen Oberflächenstellen (siehe Vorbemerkung). PIN nur als Prüfwert, Sperre nach 5 Fehlversuchen (Testname „Anmeldung: PIN prüfen …“) |
| – | Updates | NICHT GETESTET (heutiger Stand) | siehe SYS-04: Updater in Tests ersetzt (`UpdatesUiTests.cs`), realer Beleg nur vom 25.10. |
| 13 | KI | TEILWEISE | „darstellung.zeigen“ nur Anzeige, Ändern gesperrt (`SystemPromptAdapter.cs:275`) |
| 14 | Tests | TEILWEISE | 14 Tests mit Kennung, Headless |

**Gesamturteil:** TEILWEISE – als Einstellungen brauchbar und breit; Rechte und Update-Weg sind die offenen Stellen.
**Was fehlt:** 1. Rolle der angemeldeten Person als Principal durchreichen. 2. Aktueller echter Update- und Rückkehr-Lauf. 3. Bedienung mit Bildschirmleser geprüft.
**Abnahmekriterien:** Ein Kinderprofil kann keine Freigabe erteilen (Test auf Core-Ebene); Update und Rückkehr sind für die aktuelle Version mit Protokoll belegt; alle Einstellungen sind per Tastatur erreichbar.
**Aufwand:** mittel. **Risiko:** mittel.
**Dateien:** `src/Trendo.App/MainWindow.Sys12.cs`, `MainWindow.Einstellungen.cs`, `MainWindow.Lock.cs`, `MainWindow.Updates.cs`, `MainWindow.Lizenz.cs`, `src/Trendo.Core/Sys/Sys12.cs`, `src/Trendo.Core/Profile.cs`.

---

## SYS-13 Qualität prüfen (`system`, `systemcheck`, `fortschritt`, `hardware`, `qual-leistung`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE | „Jetzt messen“, „Kette prüfen“, Rückfall-Sicherung, vorige Version finden (`MainWindow.Sys13.cs:73-93`); Hardware-Seite mit Profil und Eignung je Studio (`MainWindow.Hardware.cs`) |
| 12 | Fachfunktion | TEILWEISE | Rechnet real: Median-Zeiten, Telemetrie-Suche im Quelltext, Lizenzliste der Pakete, Hash-Kette des Protokolls (`src/Trendo.Core/Sys/Sys13.cs:14-110`, `src/Trendo.Core/AuditLog.cs`). Mehrere Funktionen prüfen das Entwicklungsprojekt (Quellcode, `project.assets.json`) und sind für Endnutzer ohne Quelltext leer – nicht im Lauf geprüft |
| 13 | KI | TEILWEISE | „pruefung.starten“ schreibt ein Protokoll (`ConnectedAdapters.cs:803`) |
| 14 | Tests | TEILWEISE | 13 Tests mit Kennung, Headless |
| übrige | – | nicht anwendbar/nicht geprüft | – |

**Gesamturteil:** TEILWEISE – ehrliche Prüfseiten; „prüft, ob alles zusammen funktioniert“ ist in der Auslieferung eher ein Bericht als eine Prüfung.
**Was fehlt:** 1. Ein Knopf „Alles prüfen“ für Endnutzer mit Ampel und nächstem Schritt. 2. Trennung Entwickler-Prüfung / Nutzer-Prüfung.
**Abnahmekriterien:** In der installierten Version liefert jede Karte ein Ergebnis oder ist ausgeblendet; eine veränderte Protokollzeile wird mit Stelle gemeldet (besteht als Test).
**Aufwand:** klein. **Risiko:** gering.
**Dateien:** `src/Trendo.Core/Sys/Sys13.cs`, `src/Trendo.App/MainWindow.Sys13.cs`, `MainWindow.Hardware.cs`, `MainWindow.Qualitaet.cs`.

---

## X-02 Einrichten & Reparieren (`heilung`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE | Ein Knopf „Jetzt prüfen und reparieren“ und Ergebnisliste (`MainWindow.Heal.cs:27,36`, 41 Zeilen) |
| 12 | Fachfunktion | TEILWEISE | Sechs echte Prüfungen: Ordner, kaputte JSON-Dateien in Quarantäne, verwaiste Programmpfade, alte Zwischendateien, Speicherplatz, Protokoll (`src/Trendo.Core/W3/SelfHealing.cs`). Alles betrifft nur Trendos Datenordner; fehlende Zusatzprogramme, kaputte Installation oder defekte Projekte repariert es nicht |
| 6 | Rückgängig | TEILWEISE | Kaputte Dateien werden beiseitegelegt, nicht gelöscht; alte Zwischendateien werden gelöscht (`SelfHealing.cs`, Abschnitte 2 und 4) |
| 13 | KI | BLOCKIERT | „reparatur.zeigen“ nur Prüfung, Reparatur gesperrt, „Verbesserung 35 bleibt offen“ (`CapabilityCatalog.cs:99`, `SystemPromptAdapter.cs:279`) – offene Entscheidung, kein Mangel |
| 14 | Tests | TEILWEISE | 3 Tests mit Kennung |

**Gesamturteil:** TEILWEISE – solide Selbstprüfung des Datenordners, „richtet automatisch ein und repariert Fehler“ ist weiter gefasst als das Gebaute.
**Was fehlt:** 1. Prüfung und Nachinstallation fehlender Zusatzprogramme an dieser Stelle. 2. Reparatur der Installation (Dateien gegen Prüfsummen). 3. Rückholen aus der Quarantäne per Knopf.
**Abnahmekriterien:** Eine gelöschte Programmdatei der Installation wird erkannt und ersetzt; ein fehlendes Zusatzprogramm wird mit einem Klick nachgeholt; eine beiseitegelegte Datei lässt sich zurückholen.
**Aufwand:** mittel. **Risiko:** mittel.
**Dateien:** `src/Trendo.Core/W3/SelfHealing.cs`, `src/Trendo.App/MainWindow.Heal.cs`, `MainWindow.Setup.cs`.

---

## X-09 Leistung anzeigen (`ressourcen`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | VOLLWERTIG | Reiter Leistung mit Kurven, Prozesse sortier- und durchsuchbar, Speicher (Katalog-Tools; `MainWindow.TaskManager.cs`, 174 Zeilen, nicht zeilenweise gelesen) |
| 12 | Fachfunktion | TEILWEISE | Echte Messung aus zwei Stichproben (`src/Trendo.Core/Sys/TaskManager.cs:13,50`). Beenden nur für Programme aus Trendos eigenen Ordnern – bewusst (`:5-7,25`). Kein Autostart-Einfluss – bewusst (`netz-teilen-downloads-taskmanager.md:19`) |
| 13 | KI | TEILWEISE | „leistung.lesen“ ein Schnappschuss (`SystemPromptAdapter.cs:119-125`) |
| 14 | Tests | TEILWEISE | Task-Manager-Tests Core und UI (Testnamen „Task-Manager: …“); Status IN_PROGRESS im Katalog |

Hinweis Doku≠Code: `geraete-phonelink-kdeconnect.md:13` führt „Was belegt den Platz?“ als fehlend; es gibt `src/Trendo.Core/Sys/DiskMap.cs` und `MainWindow.DiskMapPage.cs` (Inhalt nicht geprüft).

**Gesamturteil:** TEILWEISE – als Anzeige rund, als Task-Manager absichtlich beschnitten.
**Was fehlt:** 1. GPU- und Netzlast je Programm (nicht geprüft, ob vorhanden). 2. Entscheidung, ob fremde Programme nach Rückfrage beendet werden dürfen.
**Abnahmekriterien:** Die Werte weichen bei einer Lastprobe höchstens wenige Prozentpunkte vom Windows-Task-Manager ab; ein hängendes Trendo-Werkzeug lässt sich nach Rückfrage beenden.
**Aufwand:** klein. **Risiko:** gering.
**Dateien:** `src/Trendo.Core/Sys/TaskManager.cs`, `src/Trendo.Core/Utilities.cs`, `src/Trendo.App/MainWindow.TaskManager.cs`, `MainWindow.DiskMapPage.cs`.

---

## Auffälligkeiten der Gruppe (Fassaden, tote Knöpfe, Doku≠Code, Fehler)

**Rechte und Freigaben**
1. **Freigabe im selben Klick selbst erteilt.** `MainWindow.AppCenter.cs:30-32` (Installieren, Aktualisieren, **Entfernen** ohne Dialog), ebenso das Muster an 28 Stellen (`grep Approvals.Approve(` in `src/Trendo.App`). Richtig gemacht ist es bei SYS-06 (`MainWindow.Share.cs:46-58`) und beim Website-Paket in zwei Schritten (`MainWindow.W4Web.cs:316-336`). Der Satz „Vor jeder Änderung fragt Trendo dich“ (`MainWindow.AppCenter.cs:93`) stimmt für das App-Center so nicht.
2. **Rollen wirken in der App nicht.** Kein Principal außer `LocalOwner` in `src/Trendo.App` (688 Vorkommen); `AppCenter.Plan` setzt ihn sogar im Core fest (`src/Trendo.Core/W3/Packages.cs:96`). Das Kinderprofil sperrt genau einen Knopf (`MainWindow.AddHub.cs:200`). Serverseitig durchgesetzt sind Rollen nur in der lokalen API (`src/Trendo.Api/LocalApi.cs:33-51`) und beim Begleiter/Handy-Zugang (Token).
3. **Netlify-Upload läuft nicht über das ApprovalGate**, sondern über einen Zustand in der Seite (`_wdConsent`, `MainWindow.WebDesignCode.cs:328-338`). Die Zustimmung ist fachlich gut gelöst (Fingerabdruck der Dateiliste), steht aber nicht in „Freigaben“ und unterliegt nicht dem Not-Stopp – der Not-Stopp-Bezug ist von mir nur aus dem gelesenen Code geschlossen (kein `Approvals`-Aufruf in diesem Weg).

**Fehler und Risiken im Code**
4. **Unvollständige Sicherung kann als neueste gelten.** `src/Trendo.Core/Backup.cs:21-29`: keine Fehlerbehandlung je Datei, kein Aufräumen; die halbe ZIP trägt den gültigen Namen und erscheint in `List()` (`:11-12`). Kein Test dafür gefunden.
5. **Oberfläche blockiert bei „Ausführen“/„Testen“.** Synchroner Prozesslauf im Klick (`MainWindow.CodeEditor.cs:84-98`, `MainWindow.W4Dev.cs:56`), Zeitgrenze 120 s/180 s, danach wird das Programm beendet (`src/Trendo.Core/Cli.cs:31`). Programme mit Fenster oder Server lassen sich so nicht sinnvoll starten.
6. **Irreführende Meldung:** Für die Web-Vorlage gibt es keinen Test (`DevStudio.cs:63-72`), die Liste meldet trotzdem „Test nicht bestanden“ (`MainWindow.W4Dev.cs:56`).
7. **Projektliste startet bei jedem Zeichnen Git-Prozesse:** bis zu 20 Projekte × mehrere `git`-Aufrufe synchron (`MainWindow.W4Dev.cs:48-50`, `DevStudio.cs:81-88`).
8. **Headless-Chrome ohne Sandbox und mit offenem Fernsteuer-Zugang** für Vorschau und Prüfung: `--no-sandbox`, `--remote-allow-origins=*`, `--remote-debugging-port` (`src/Trendo.App/WebsitePreview.cs:164-166`). Geladen werden dabei auch importierte und KI-erzeugte Seiten.
9. **PowerShell-Skripte laufen mit `-ExecutionPolicy Bypass`** aus dem Code-Editor (`src/Trendo.Core/CodeProject.cs:93`).
10. **Rückkehr zur alten Version ohne Prüfsumme** des gesicherten Pakets (`src/Trendo.App/TrendoUpdater.cs:86`).

**Doku ≠ Code**
11. `CapabilityCatalog.cs:83` „Veröffentlichen und Push sind nicht angeschlossen“ – die Oberfläche lädt zu Netlify hoch (`MainWindow.WebDesignCode.cs:331`).
12. `CapabilityCatalog.cs:85` „Es wird nichts geklont“ – die Oberfläche holt Kopien und installiert Programme (`OpenSource.cs:51`, `GitHubApps.cs:127`).
13. `CapabilityCatalog.cs:99` „Es wird nichts repariert“ – der Knopf repariert (`SelfHealing.cs`, `repair = true`). In allen drei Fällen beschreibt der Katalog nur den KI-Weg; für Leser wirkt es wie eine Aussage über das Studio.
14. Vorbild-Abgleiche veraltet: Einzeldatei aus Sicherung (`schutz-…md:20`, gebaut in `Backup.cs:133`); Baukasten „Bausteine ziehen“ (`baukasten-…md:14`, gebaut in `MainWindow.W4Builder.cs:129,162`); „Was belegt den Platz?“ (`geraete-…md:13`, es gibt `DiskMap.cs`).
15. Katalog-Status „IMPLEMENTED / Test PC_VERIFIED“ für fast alle Programme der Gruppe, obwohl die Wege nach außen (winget, Netlify, GitHub-Installation, yara-x, Updater, Handy) in den Tests ersetzt sind. `PC_VERIFIED` verweist teils auf sehr alte Belege (`requirements.yaml:170,198`: `reports/pc-0.9.1`, `pc-0.9.2`).

**Name verspricht mehr als das Programm**
16. X-03 „Apps zusammenklicken“ → statische Einzelseite. X-04 „Neue Funktionen hinzufügen“ → Datenpakete. X-10 „Programme … neu entwickeln“ → Tagebuch und Ähnlichkeitsprüfung. SPEC-12 „Prüft das System auf Gefahren“ → Prüfsumme und Beweisfälle. SYS-03 „Im Internet surfen“ → startet fremde Browser. CORE-11 „Schützt … Passwörter“ → keine Passwortverwaltung gefunden. CORE-07 Software-Teil „Programme … bauen“ → Vorlagen-Anleger.

**Aufbau**
17. Dreifach vorhandene Code-Bearbeitung in CORE-07 (TextBox `web-editor` in `MainWindow.W4Web.cs:162`, Code-Pane des Design-Studios `MainWindow.WebDesignCode.cs:187`, Code-Editor `MainWindow.CodeEditor.cs`) und doppelte Website-Erstellung (Generator und Design-Studio) plus Baukasten als dritter Weg.
18. Doppelte Programmverwaltung: SYS-04 (winget) und SYS-11 (Hinzufügen, Pflegen, Zusatzprogramme, GitHub) lösen dieselbe Aufgabe auf fünf Seiten.

**Nicht geprüft (ausdrücklich):** Inhalt der Seiten `modus`, `lizenz`, `einrichtung`, `projects`, `fortschritt`, `systemcheck`; `MainWindow.Phone*.cs` zeilenweise; `DiskMap`; Vollbild und Rechtsklick außerhalb von Dateien und Code-Editor; ob die mit `TRENDO_PC_LAUF=1` markierten echten Läufe je unter `reports/` abgelegt wurden.
