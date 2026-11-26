# Fehlende Studios aus der Freigabe vom 09.11.2026 (T-0769)

Am 09.11.2026 hat der Nutzer 79 neue Studios freigegeben, in zwei Runden zu 48 und 31 Studios.

**Ziel:**
- 9 Kategorien mit je 12 bis 14 Studios.
- 12 Studios je Kategorie; Business & Geld, Wissen & Lernen, Entwickeln sowie System & Sicherheit bekommen 14 („Alle behalten“).

**Abgleich:**
- mit `src/Trendo.App/StudioCatalog.cs` (Bereich, `FoldedInto`, `Removed`)
- mit `docs/programme/katalog.json`

**Stand:** main 0.9.110.

**Zählweise „Ist“:** Gezählt werden die Studios, die in der Übersicht stehen (`StudioCatalog.InOverview`). Eingegliederte Studios zählen beim aufnehmenden Studio, entfernte gar nicht. Am 09.11. waren das 37 bis 38 vorhandene Studios. Heute sind es 61.

## 1. Zahlen je Kategorie

| Kategorie | Ist heute | davon aus der Freigabe gebaut | freigegeben, fehlt | Ist nach Bau | Soll |
|---|---|---|---|---|---|
| Arbeiten & Büro | 13 | 8 | 0 | 13 | 12 (1 zu viel, siehe Hinweis) |
| Business & Geld | 3 | 0 | **11** | 14 | 14 |
| Kreativ & Medien | 13 | 6 | 0 | 13 | 12 (1 zu viel) |
| KI & Automatik | 10 | 8 | 0 | 10 | 12 (2 zu wenig, siehe Hinweis) |
| 3D & Spiele | 4 | 0 | **8** | 12 | 12 |
| Technik & Fachgebiete | 5 | 0 | **7** | 12 | 12 |
| Wissen & Lernen | 5 | 1 | **10** | 15 | 14 (1 zu viel, siehe Hinweis) |
| Entwickeln | 2 | 0 | **12** | 14 | 14 |
| System & Sicherheit | 6 | 0 | **8** | 14 | 14 |
| **Summe** | **61** | **23** | **56** | **117** | 108–126 (gemerkt: 116) |

**Hinweis zur Verteilung.**

- **Freigabe:** In der zweiten Runde standen „Code mit KI, Bilder erkennen, Recherche mit Quellen, Tagesplaner“ als eine Gruppe, gemeint war vermutlich KI & Automatik. Damit hätte KI & Automatik 12 Studios, Arbeiten & Büro 12 und Wissen & Lernen 14.
- **Code:** Der Code führt den Tagesplaner unter Arbeiten & Büro und „Recherche mit Quellen“ unter Wissen & Lernen.
- **Folge:** Daher stehen heute bei KI & Automatik 2 Studios zu wenig und bei Arbeiten & Büro und Wissen & Lernen je 1 zu viel. Es fehlt kein freigegebenes KI-Studio.
- **Was der Nutzer entscheidet:** ob die beiden Studios umziehen oder ob KI & Automatik zwei neue Vorschläge (Ja/Nein) bekommt.
- **Ehrlich:** Vier KI-Studios sind in „KI fragen“ bzw. „Neues Projekt“ eingegliedert: KI-Team, Kostenlose KI, KI-Anweisungen, Großprojekte. Zählte man sie mit, hätte KI & Automatik 14.
- **Kreativ & Medien 13 statt 12:** 7 vorhandene Studios (Videos & Fotos bearbeiten, Live streamen, Trendo Musik, TV & Live, Filme machen, Künstlerstudio, Kleidung & Produkte) plus 6 freigegebene ergeben 13. Die Freigabe ging von 12 aus. Vermutlich wurde ein vorhandenes Studio am 09.11. nicht mitgezählt; die Ursache ist nicht eindeutig. Es fehlt nichts, ein Studio zu viel ist innerhalb von 12 bis 14 zulässig.
- **Gesamtzahl:** Die gemerkte Zahl 116 und die hier gezählte 117 weichen um 1 ab. Vermutlich liegt das an der Zählung der vorhandenen Studios am 09.11. (37 gemerkt, 38 aus heutiger Sicht).

## 2. Gebaut (23 von 79)

| Kategorie | Gebaute Studios aus der Freigabe |
|---|---|
| Arbeiten & Büro (8) | Bewerbung & Lebenslauf, PDF-Werkstatt, Präsentationen gestalten, Formulare ausfüllen, Dokumente scannen, Karten & Einladungen, Verträge-Ordner, Tagesplaner |
| Kreativ & Medien (6) | Podcast aufnehmen, Logos & Marken, Fotos verwalten, Comics & Storyboards, Bücher schreiben, Animation & Motion |
| KI & Automatik (8) | Bilder mit KI, Texte zusammenfassen, Eigene KI trainieren, KI-Agenten bauen, Videos mit KI, KI-Modelle vergleichen, Code mit KI, Bilder erkennen |
| Wissen & Lernen (1) | Recherche mit Quellen |

Alle 23 sind nach der Neubewertung TEILWEISE, fünf davon Grenzfälle (PDF-Werkstatt, Dokumente scannen, Tagesplaner, Logos & Marken, Bücher schreiben). Details stehen in `BERICHT.md`.

## 3. Fehlend (56), mit Teilabdeckung im heutigen Code und Bau-Reihenfolge

**Spalte „Heute schon“:**
- Sie zeigt, ob ein bestehendes Programm Teile davon kann.
- „keine“ heißt: Es gibt nur Text für die KI, zum Beispiel Startideen in `src/Trendo.Core/W2/StudioIdeas.cs` oder Beispielanweisungen in `StudioProfiles.cs`. Das zählt nicht.

**Bau-Reihenfolge je Kategorie:**
- Zuerst kommt, was im Alltag oft gebraucht wird und schon einen echten Kern im Code hat. Das ergibt wenig Aufwand und keine Attrappe.
- Danach folgen die Studios mit ganz neuem Kern.
- Jedes Studio braucht einen echten Fachkern und die Pflichten aus der Freigabe: StudioCatalog, Symbole, CapabilityCatalog, Profil, Team, Bild, requirements.yaml. Dazu kommen die Abnahmekriterien 1–6.

### Business & Geld – Ist 3, Soll 14, fehlen 11

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | Rechnungen & Angebote | weitgehend | Programm `finanzen` (SPEC-11): Rechnung, Angebot, XRechnung, Storno; `src/Trendo.Core/Business/Rechnungen.cs:9` | **Nicht eindeutig:** war als `STUDIO-INVOICE-01` geplant und ist in „Geld & Rechnungen“ aufgegangen (`Rechnungen.cs:10`). Entscheidung nötig: eigenes Studio (Rechnungs-Arbeitsplatz aus `finanzen` lösen) oder als erledigt zählen |
| 2 | Mahnungen | teilweise | Status „überfällig“ (`Business/Rechnungen.cs:217-231`); Brief „Zahlungserinnerung“ (`Letters.cs:140`) | baut direkt auf 1 auf: Mahnstufen, Fristen, Mahn-PDF |
| 3 | Belege & Spesen | teilweise | Scanner sortiert Bons nach „Belege“ (`Office/DocScan.cs:306-310`); DATEV-Export mit Beleg (`Business/GeldAuswertung.cs:200-244`) | Spesen- und Reisekostenabrechnung fehlen |
| 4 | Preise kalkulieren | teilweise | POD-Preis (`Pod/PodStudio.cs:71-82`), Staffelpreise in `mode-programm`, Marge (`BusinessStudio.cs:63`) | Kalkulations-Tabelle mit Zuschlägen als eigenes Programm |
| 5 | Marktanalyse | weitgehend (Grundfunktionen) | Reiter „Markt“ `biz-markt` (`MainWindow.W5Market.cs:17-69`, `BusinessMarket.cs`) | **Nicht eindeutig:** steckt im Business-Bereich von „Online verkaufen“; herauslösen oder als erledigt zählen |
| 6 | Verkaufsseiten | teilweise | fünfseitige Webseite (`MainWindow.W5Launch.cs:41`, `BusinessLaunch.cs`); Web-Design in `dev` | Vorlage „Verkaufsseite“ im Web-Design-Studio als Kern |
| 7 | Flyer & Visitenkarten | teilweise | „Plakat A4“ in `bild` (`MainWindow.Bildbearbeitung.cs:97`); Karten in `karten-programm` | Visitenkarten-Layout mit Beschnitt fehlt; Vektor-Editor aus Logos nutzbar |
| 8 | Social Media planen | teilweise | Bildvorlagen 1080×1080/1920 (`MainWindow.Bildbearbeitung.cs:90-97`), Videoformat 4:5 (`Media/VideoExport.cs:25`); Kennung `STUDIO-SOCIAL-01` nur in `Kie/Kie.cs:245` | Redaktionsplan/Kalender fehlt ganz; nichts veröffentlichen ohne Freigabe |
| 9 | Produktfotos für Shops | teilweise | „Hintergrund entfernen“ nur über kie.ai (`Kie/Kie.cs:191`); Printful-Mockups (`Pod/Printful.cs:14`) | lokaler Freisteller nötig, sonst kein Kern auf dem PC |
| 10 | Newsletter gestalten | keine | – | neuer Kern: E-Mail-HTML-Editor; Versand nur nach Freigabe über „E-Mails & Termine“ |
| 11 | Kundenmeinungen | keine | nur Support-Liste „Kundenanfragen“ (`MainWindow.W5Ops.cs:81`) | neuer Kern; Nutzen gering |

### 3D & Spiele – Ist 4, Soll 12, fehlen 8

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | 3D-Druck vorbereiten | weitgehend | Programm `3d-druck` in CORE-05: Druckplatte, Stützen, Zeit, Filament, Bambu (`Cad/Slicer.cs`, `Cad/PrintPrep.cs`) | **Nicht eindeutig:** Programm existiert. Als eigenes Studio herauslösen, eigenen G-Code-Kern und Absturzschutz ergänzen (heute G-Code nur über Bambu Studio) |
| 2 | Level-Editor | weitgehend | `spiele-programm` (CORE-06): Level im Raster, Ebenen, mehrere Level | **Nicht eindeutig:** steckt im Spiele-Studio. Herauslösen oder als erledigt zählen |
| 3 | Pixel-Spiele | teilweise | Vorlagen „Retro (2D-Pixel)“ (`Programs/GameDesign.cs:14,88-91`) | Pixel-Art-Editor mit Paletten/Animationsstreifen als Kern |
| 4 | Materialien & Texturen | teilweise | PBR + ambientCG im 3D-Studio (`Studio3DPro.cs:6-24`) | Material-/Textur-Editor (Kacheln, Normal-Map) als Kern |
| 5 | Figuren & Animation | teilweise | Mixamo (`Programs/Mixamo.cs:7`), Posenpuppe (`Kreativ/Kuenstler2.cs:7`), 2D-Animation | Riggen/3D-Animieren über Blender als echter Kern |
| 6 | Spiel-Sound & Musik | teilweise | Retro-Soundeffekte (`Media/Sfxr.cs:3`), Ton je Ereignis im Spiele-Studio, `musik-studio` | Bausteine zusammenführen |
| 7 | Architektur-Rundgang | teilweise | `architektur`: 2D/3D-Ansicht, OBJ-Export (`Arch/Plan3D.cs:6`) | Begehen fehlt; Weg über Godot/Unreal aus dem Grundriss |
| 8 | Brettspiele entwerfen | keine | `Programs/GameReferences.cs:46` schließt Brettspiele aus | neuer Kern (Spielplan, Karten, Regeln, Druck-PDF) |

### Technik & Fachgebiete – Ist 5, Soll 12, fehlen 7

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | Heizung & Energie | teilweise | Heizlast in `architektur` (`W6/Architecture2.cs:38`), Wärmedurchgang in `technik` | Kern aus vorhandenen Rechnungen |
| 2 | Solaranlage berechnen | teilweise (schwach) | nur Überwachung (`W6/TwinChart.cs:106-122`) | Auslegung und Ertragsrechnung neu, offline mit Standortdaten |
| 3 | Werkstatt & Holzbau | teilweise | `cnc`, `technik` (Balken), Holzdichten (`Cad/CadMeasure.cs:11`) | Zuschnitt- und Schnittliste als Kern |
| 4 | Mikrocontroller | teilweise | `elektronik` (Schaltplan, SPICE, Gerber) | Programmieren und Flashen von Arduino/ESP fehlt (Werkzeug nach Freigabe) |
| 5 | Heimautomation | teilweise | `zwilling` MQTT (`W6/TwinMqtt.cs:8-9`, Shelly/Tasmota/Home Assistant) | heute nur lesend. Schalten nur mit eindeutiger Freigabe (Realwelt-Aktor) |
| 6 | Garten planen | keine | – | neuer Kern (Beet-Plan, Pflanzkalender); kann auf dem Grundriss-Editor aufsetzen |
| 7 | Netzwerk planen | keine | nur Netzwerkstatus in `netz-pruefen` | neuer Kern (Plan, IP-Liste, Verkabelung) |

### Wissen & Lernen – Ist 5, Soll 14, fehlen 10

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | Quiz erstellen | teilweise | `lernen` (SPEC-06): „Quiz mit Auswahl“ (`MainWindow.Spec06.cs:13,23`) | freier Quiz-Editor auf vorhandenem Kern |
| 2 | Referate & Hausarbeiten | teilweise | `quellen-programm`, `text`, `praesentation` | Gliederung → Text → Literatur → Folien als eine Kette |
| 3 | Mathe-Helfer | teilweise | Rechner inkl. Funktionsgraph (`MainWindow.Helfer.cs:28,268`, `GraphView.cs:7`) | Lösungswege fehlen (CAS-Kern nötig) |
| 4 | Nachhilfe mit KI | keine | nur allgemein „KI fragen“ | Lernpfad mit lokaler KI, Fortschritt je Fach |
| 5 | Naturwissenschaft | teilweise | `wissenschaft` (Statistik, Regression, Fehlerfortpflanzung) | Periodensystem, Formeln, Einheiten |
| 6 | Zeitleisten | keine | nur Video-Zeitleiste (anderes Fach) | neuer Kern |
| 7 | Kurse erstellen | keine | nur fertige Malkurse (`MainWindow.Kuenstlerstudio.cs:743`) | neuer Kern (Lektionen, Quiz aus 1) |
| 8 | Programmieren lernen | keine | `code-ki-programm` erklärt Dateien | neuer Kern (Aufgaben mit echten Tests) |
| 9 | Rezepte & Ernährung | keine | „Rezept“ im Code = Studio-Ablauf (`Fusion.cs`) | neuer Kern |
| 10 | Stammbaum | keine | kein GEDCOM | neuer Kern mit GEDCOM-Import/Export |

### Entwickeln – Ist 2, Soll 14, fehlen 12

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | Webseiten messen | weitgehend (eigene Seiten) | `Website/WebsiteQuality.cs:21`, axe-core (`Website/AxeCheck.cs:5`) in `dev` | **Nicht eindeutig:** heute im Web-Design-Studio. Fremde Seiten messen wäre der neue Teil |
| 2 | Automatische Tests | teilweise | echtes `dotnet test` (`DevBuild.cs:12-14`), „Tests ausführen“ in Code mit KI | Tests erzeugen und Abdeckung zeigen |
| 3 | Installer bauen | teilweise | „Paket bauen“ = ZIP mit Prüfsumme (`DevBuild.cs:377-383`) | echter Setup-Installer (z. B. Inno, Velopack) fehlt |
| 4 | Desktop-Programme | teilweise | Vorlage C#/Avalonia im Software-Studio (`DevStudio.cs:15`) | eigener Arbeitsplatz mit Fenster-Designer |
| 5 | Lizenzen prüfen | teilweise | GitHub-Lizenzen (`OpenSource.cs:5`), Asset-Lizenzen | Abhängigkeiten eigener Projekte prüfen (NuGet, npm, pip) |
| 6 | Text- & Datenwerkzeuge | teilweise | Regex, Kodierung im Editor (`MainWindow.Helfer.cs:27,201`), Prüfsumme | JSON/CSV/Base64-Werkzeugkasten |
| 7 | Dokumentation schreiben | keine (spezifisch) | nur allgemein `text` | Markdown-Doku mit Vorschau und Export als Seite |
| 8 | Chatbots bauen | teilweise | `agenten-bauen-programm`, `ki-rollen` | lokaler Chatbot mit eigenem Wissen; kein öffentliches Veröffentlichen ohne Freigabe |
| 9 | Browser-Erweiterungen | keine | – | neuer Kern (Manifest V3, Test in Chrome) |
| 10 | Server & Container | keine | Docker nur als optionale Abhängigkeit | neuer Kern; braucht Docker/Podman (Freigabe) |
| 11 | Android-Apps | teilweise (schwach) | Port-Ziel Android (`ConsolePort/ConsolePort.cs:30`), Handy-Vorschau im Baukasten | Gradle und Android-SDK nötig (groß, Werkzeug nach Freigabe) |
| 12 | Mods für eigene Spiele | keine | – | **Name nicht eindeutig:** `WebDesignMods.cs` betrifft Webseiten, „Trendo erweitern“ erweitert Trendo selbst. Gemeint ist vermutlich: Mod-Paket für ein mit Trendo gebautes Spiel. Vor dem Bau klären |

### System & Sicherheit – Ist 6, Soll 14, fehlen 8

| Reihenfolge | Studio | Heute schon | Wo | Bemerkung |
|---|---|---|---|---|
| 1 | Speicher aufräumen | teilweise | Speicherplatz-Karte mit Duplikaten, nur Papierkorb nach Ja (`Sys/DiskMap.cs:3`, `MainWindow.DiskMapPage.cs:10-11`) | Temp und Caches ergänzen, nur eigene Bereiche, alles mit Rückgängig |
| 2 | Autostart aufräumen | teilweise | `forensik` liest Autostart/Run/Aufgaben (`Sys/Gefahren.cs:16-20,270-280`) | Ändern fehlt; nur Benutzer-Autostart, keine Registry-HKLM, mit Rückgängig |
| 3 | Datenschutz-Check | teilweise | Passwortfunde in `schutz`, DSGVO-Prüfung der Shop-Seite (`BusinessLaunch.cs:55-56`) | Windows-Datenschutz nur lesen und erklären (keine Systemänderung) |
| 4 | Systemprotokolle | keine | nur Trendo-eigene Protokolle | Ereignisanzeige lesend auswerten |
| 5 | Akku & Strom | teilweise (schwach) | Akku nur unter Linux (`W3/Devices.cs:24,134`), Handy-Akku | Windows-Akkubericht lesend |
| 6 | Bluetooth-Geräte | teilweise | `W7/BluetoothLink.cs:21` (aufs Handy ausgerichtet) | allgemeine Geräteliste mit Koppeln nach Freigabe |
| 7 | Dateien synchronisieren | teilweise (schwach) | LocalSend, Sicherung | Ordnerabgleich mit Konfliktanzeige als Kern |
| 8 | Fernzugriff | teilweise | Tailscale für das Handy (`W7/PhoneAccess.cs:9,112`), scrcpy | Fernzugriff auf einen PC braucht Sicherheitsprüfung (Security-Agent) |

## 4. Vorschlag für die Etappen

Empfehlung: Vor neuen Studios zuerst die Querschnitts-Schritte Q1–Q5 aus `restliste.md` bauen. Sonst wächst die Zahl der TEILWEISE-Studios weiter, wie schon im Audit vom 16.11. empfohlen.

Danach eine Etappe je Kategorie, Reihenfolge nach Lücke zum Soll und vorhandenem Kern:

1. **Business & Geld:** fehlen 11; Rechnungen, Mahnungen und Belege haben schon einen Kern.
2. **System & Sicherheit:** fehlen 8, alle lesend bzw. mit Rückgängig machbar; Speicher und Autostart haben einen Kern.
3. **Wissen & Lernen:** fehlen 10; Quiz, Referate und Mathe haben einen Kern.
4. **3D & Spiele:** fehlen 8; 3D-Druck, Level und Pixel haben einen Kern, brauchen aber Engines.
5. **Technik & Fachgebiete:** fehlen 7.
6. **Entwickeln:** fehlen 12, davon brauchen mehrere neue Werkzeuge (Android-SDK, Docker) mit Freigabe.

**Vorab zu entscheiden (Ja/Nein):**

- **Als erledigt zählen oder herauslösen:** Rechnungen & Angebote, Marktanalyse, 3D-Druck vorbereiten, Level-Editor und Webseiten messen gibt es weitgehend schon in einem bestehenden Studio. Sollen sie als eigene Studios herausgelöst werden (eigene Karte, eigener Ausbau)? Oder zählen sie als erledigt, dann sinkt die Zahl der fehlenden Studios auf 51.
- **Unklarer Name:** Was ist mit „Mods für eigene Spiele“ gemeint?
- **Umzug in KI & Automatik:** Sollen Tagesplaner und Recherche mit Quellen nach KI & Automatik umziehen, damit die Kategoriegrößen stimmen?

## 5. Entscheidung des Nutzers vom 26.11.2026 (Nachtrag T-0777)

Der Nutzer hat am 26.11.2026 die Fragen aus Abschnitt 4 so entschieden:

- **Umzug: Ja.** Tagesplaner (`STUDIO-DAYPLAN-01`) und Recherche mit Quellen (`STUDIO-CITE-01`) stehen jetzt unter KI & Automatik (`src/Trendo.App/StudioCatalog.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, umgesetzt in T-0777). Keine Funktion geht verloren: Seiten, Programme, Symbole, Vorlagen und KI-Aufträge bleiben gleich, nur der Bereich ändert sich.
- **Als erledigt zählen: Ja.** Rechnungen & Angebote, Marktanalyse, 3D-Druck vorbereiten, Level-Editor und Webseiten messen zählen als erledigt. Sie bleiben Teil ihres bestehenden Studios (Geld & Rechnungen, Online verkaufen, 3D-Modelle erstellen, Spiele bauen, Webseite bauen) und werden nicht als eigene Studios herausgelöst.
- **Damit fehlen noch 51** der 79 freigegebenen Studios (vorher 56).
- **Erste Studio-Etappe danach: Wissen & Lernen.** Das weicht von der Reihenfolge in Abschnitt 4 ab (dort Business & Geld zuerst); es gilt die Entscheidung des Nutzers.
- **Offen bleibt:** „Mods für eigene Spiele“ (Name unklar).

**Zahlen je Kategorie nach der Entscheidung:**

| Kategorie | Ist heute | freigegeben, fehlt | Ist nach Bau | Soll |
|---|---|---|---|---|
| Arbeiten & Büro | 12 | 0 | 12 | 12 |
| Business & Geld | 3 | **9** | 12 | 14 |
| Kreativ & Medien | 13 | 0 | 13 | 12 (1 zu viel, siehe Hinweis in Abschnitt 1) |
| KI & Automatik | 12 | 0 | 12 | 12 |
| 3D & Spiele | 4 | **6** | 10 | 12 |
| Technik & Fachgebiete | 5 | **7** | 12 | 12 |
| Wissen & Lernen | 4 | **10** | 14 | 14 |
| Entwickeln | 2 | **11** | 13 | 14 |
| System & Sicherheit | 6 | **8** | 14 | 14 |
| **Summe** | **61** | **51** | **112** | 108–126 |

- „Ist heute“ ist im Bild der Übersicht nach dem Umzug nachgezählt (Trendo.Render, Seite `studios`, Stand T-0777).
- Ehrlich: Bei Business & Geld, 3D & Spiele und Entwickeln stecken die fünf als erledigt gezählten Studios in einem bestehenden Studio. Diese Kategorien bleiben deshalb nach dem Bau unter dem Soll, solange dort nichts herausgelöst oder neu vorgeschlagen wird.
