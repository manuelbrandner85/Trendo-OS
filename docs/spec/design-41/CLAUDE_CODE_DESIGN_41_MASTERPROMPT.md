# TRENDO OS – ULTIMATE HYPERREALISTIC PREMIUM EXPERIENCE GODMODE

## Verbindlicher Claude-Code-Entwicklungsauftrag – Verbesserung 41 (genehmigt)

**Auftragskennung:** `UI-041-PREMIUM-EXPERIENCE`  
**Produkt:** Trendo OS  
**Repository:** https://github.com/manuelbrandner85/Trendo-OS  
**Verantwortung:** Claude Code = Planung, Review, echte lokale Änderungen, Builds, Tests und Bildnachweise  
**Umfang:** NUR die bereits ausdrücklich genehmigte Designverbesserung 41 (alle 20 Funktionen). Kein Neuaufbau oder implizites Aktivieren anderer Erweiterungen.  
**Projektstand:** Vor Ausführung anhand des *aktuellen* Repository-HEAD erneut prüfen; alle unten genannten Beobachtungen sind Momentaufnahmen.

> **Starte mit einer Bestandsprüfung und entwickle anschließend einen echten ausführbaren Design-Vertical-Slice.** Die Bilder sind Designvorgaben und **KEINE** Screenshots eines echten Trendo-Builds. Erzeuge keine bloße visuelle Attrappe. Stelle dem Auftraggeber Vergleichsbilder des tatsächlich laufenden Avalonia-Builds vor. **Warte vor der Übertragung auf sämtliche Studios auf die gesonderte visuelle Freigabe des Auftraggebers.**

---

## 1. Eingelieferte Referenzbilder – verbindlicher Dateiplan

Die Dateien liegen relativ zu **dieser Markdown-Datei** im Unterordner `referenzen/`.

| Priorität | Datei | Zu implementierender Bereich |
|---|---|---|
| 1 | `referenzen/01_DESKTOP_PREMIUM.png` | Premium-Hauptdesktop: dunkler Naturhintergrund, reale Leisten, klarer Fokus, Premium-Dock |
| 1 | `referenzen/02_STARTMENUE.png` | Intelligentes Startmenü: Suche, Favoriten, zuletzt verwendet, Navigation |
| 1 | `referenzen/03_STUDIOKARTEN.png` | Hochwertige Studioübersicht: lesbare Titel, differenzierte Karten, klare Aktionen |
| 1 | `referenzen/04_BRIEFSTUDIO.png` | **Erstes** umzubauendes reales Beispielstudio, mit korrekter Formular- und Dokumentstruktur |
| 1 | `referenzen/05_TRENDO_KI.png` | Trendo-KI-Bedienung, reale Zustände, gut lesbarer Chat, dezente KI-Visualisierung |
| 2 | `referenzen/06_FENSTER.png` | Fenstertitel, Schatten, Ebenen, Drag/Resize, aktive/inaktive Fenster |
| 2 | `referenzen/07_EINSTELLUNGEN.png` | Themes, Kontrast, Skalierung, bevorzugte Farben und Wallpaper |
| 2 | `referenzen/08_SYSTEMMETRIKEN.png` | Echte, gemessene CPU/RAM/GPU/SSD-Anzeigen statt Demo-Werte |
| 2 | `referenzen/09_WALLPAPER_THEMES.png` | Dynamische Wallpaper als **optionale** visuelle Ergänzung |
| 2 | `referenzen/10_DATEIMANAGER.png` | Stil und Lesbarkeit echter Datei-/Programmfenster |
| Überblick | `referenzen/00_DESIGN_ATLAS_A.png` | Zusammenhängende Übersicht des gewünschten Premium-Looks |
| Überblick | `referenzen/00_DESIGN_ATLAS_B.png` | Zweite Übersicht inklusive Desktop, Studio, KI und Einstellungen |

### Bildinterpretationsregeln

- Die Bilder sind KI-generierte **Soll-Referenzen** und enthalten illustrative Texte, Uhrzeiten, Auslastungen, Wetterangaben, App-Logos und gegebenenfalls ungenaue Beschriftungen. **Übernimm keine fiktiven Werte, Statusmeldungen oder personenbezogenen Inhalte.**
- Alle UI-Texte in der realen Anwendung müssen auf verständlichem Deutsch erscheinen. Verwende zentrale, konsistente Bezeichnungen aus dem bestehenden Studio-Katalog.
- Referenzbilder sind *visuelle Ziele*, keine technisch bindenden Pixelmaße. Passe Layouts für unterschiedliche Auflösungen responsiv an.
- Eventuelle stilisierte „T“-Zeichen sind **unverbindliche Logoentwürfe**, keine Markenfreigabe. **Kein endgültiges Logo ohne Zustimmung.**
- Abgebildete Anwendungen wie Blender oder Unreal sind nur dann als bereit/startfähig anzuzeigen, wenn sie tatsächlich verfügbar und entsprechend freigegeben sind.
- Verwende Bilder nicht als Hintergründe, die eine Benutzeroberfläche nur vortäuschen. Programmiere jede Schaltfläche, jedes Fenster und jeden relevanten Zustand **echt**.
- Die Referenzen sind zusätzliche Bestandteile der freigegebenen Designrichtung. Bei Konflikten mit Sicherheit, Benutzbarkeit oder technischen Vorgaben gilt die vorhandene funktionierende Systemarchitektur.

---

## 2. Bestandsprüfung des wirklichen GitHub-Projekts

Arbeite **nicht** auf einem erfundenen oder neuen Trendo-Projekt. Untersuche zuerst:

- `CLAUDE.md`, `README.md`, `Trendo.sln`, `requirements.yaml`.
- `src/Trendo.App/Trendo.App.csproj` – beim letzten bekannten Stand `.NET 10`, `Avalonia 12.1.4`, `FluentTheme`, Inter-Font.
- `src/Trendo.App/App.axaml`, `src/Trendo.App/MainWindow.axaml.cs`.
- `src/Trendo.App/MainWindow.Look.cs`, `MainWindow.Windows.cs`, `MainWindow.StartMenu.cs`, `MainWindow.DesktopMenu.cs`.
- `MainWindow.W1Letter.cs`, `MainWindow.W1Office.cs`, zugehörige weitere Briefstudio-Teile.
- `StudioCatalog.cs`, `StudioIcons.cs`, `MdiIcons.cs`, gegebenenfalls weitere UI-Adapter.
- `docs/design/`, `reports/p1/ui-design/`, `reports/pc-0.9.5/`, `docs/build-log/` und die aktuellen `tests/`.

**Wichtiger bekannter technischer Befund:** Der bisherige Hauptdesktop setzt eine Designfläche von etwa 1536 × 800 und feste Seitenleisten (`192,*,322`) ein und verkleinert via `LayoutTransformControl`/`ScaleTransform` die komplette Oberfläche auf kleinere Fenster. Dies kann Schrift und Interaktionen unvorteilhaft verkleinern. Ersetze dies durch **echte adaptive Layoutregeln**, nicht durch einen weiteren globalen Zoom-Trick.

**Wichtiger bekannter Funktionsbefund:** Die Klickbarkeit der 54 Studio-Karten und die Fensterverwaltung wurden bereits repariert und teilweise über Mausklicktests geprüft. **Diese Funktionalität darf nicht regressieren.** Prüfe aber den neuesten tatsächlichen HEAD, bevor du dich auf diese Zahlen verlässt.

**Wichtiger bekannter Fensterbefund:** Es existieren MDI-artige Trendo-Arbeitsfenster mit Drag, Resize, Min/Max, Taskleiste und teilweise Snap. Sie müssen nach dem Redesign weiter funktionieren.

### Verbindlicher Preflight

1. Repository und aktuelle Berechtigungen prüfen.
2. Aktuellen Branch, HEAD-SHA, Dirty State und laufende fremde Arbeiten feststellen.
3. Aktuelle native Windows-Anwendung mit bestehenden Befehlen bauen und testen.
4. Tatsächliche Screenshots vor Änderungen aus dem laufenden Build erzeugen.
5. Aktuell offene UI-Probleme und zu schützende Funktionalität dokumentieren.
6. Nur mit eigenem isoliertem Branch beginnen. Keine ungefragte Änderung anderer Bereiche.

---

## 3. Branch- und Repo-Vorschriften

Richte dich nach `CLAUDE.md` und dem neueren tatsächlichen Projektstand.

Zielbranch: `claude/UI-041-premium-design` (falls Namenskollision: eindeutiger, konfliktfreier Name). Keine direkten Commits auf `main`; kein Force-Push; kein Rebase fremder gepushter Branches.

Auf dem Windows-Entwicklungs-PC PowerShell gemäß bestehenden Projektregeln nutzen. Kein `winget`, `msiexec`, `choco`, Registry- oder systemweiter Eingriff ohne gesonderte Erlaubnis. Keine Änderung von Sicherheits-, Recovery-, Forensik-, Zahlungs-, Kunden- oder Accountdaten. Keine neuen verpflichtenden APIs. Keine öffentlichen Releases.

Vor dem Merge: möglichst kleine, überprüfbare Commits, vollständige verfügbare Tests, tatsächlich erzeugte Screenshots und eine Begutachtungsentscheidung des Auftraggebers.

---

## 4. Zielbild – „Trendo Obsidian Premium“

Designphilosophie: **ruhige, realistisch wirkende Premium-Materialien**, eigenständige Identität, höchstmögliche Klarheit. Schöne Tiefe statt unlesbarem Neon. Die Oberfläche muss nach mehreren Arbeitsstunden weiterhin angenehm und effizient sein.

### Vorgeschlagene Tokens (Startwerte, vor endgültiger Festlegung visuell prüfen)

| Rolle | Farbwert/Prinzip |
|---|---|
| Desktop-Untergrund | `#050B17` bis `#0D1B30` |
| Window-Surface | `#112138` mit kontrollierter Deckkraft |
| Elevated-Surface | `#172A44` |
| Glas-Rahmen | `#335578` dezent |
| Haupttext | `#F0F6FF` |
| Sekundärtext | `#A7BCD4` |
| Akzent | `#51C5FF` oder bestehende nutzerwählbare Farbe |
| Fokus | deutlich unterscheidbarer Ring mit geprüfter Kontrastwirkung |
| Erfolgs-/Fehlerstatus | nur mit echten Zuständen, Farbe nie alleinige Information |
| Radius | konsistent, z. B. 10 / 14 / 18 px je Ebene |
| Schatten | weiche mehrstufige Tiefe, keine permanent großen Blur-Kosten |
| Animation | 120–240 ms für Microinteractions; keine künstlich verlängerten Operationen |

Diese Palette ist nicht zu hardcoden. Baue **zentral änderbare Avalonia-Ressourcen und Control-Styles**. Bestehende Hell-, Dunkel- und Hochkontrastthemen sowie Akzentwahl weiterverwenden. Überprüfe, ob Farben und Opazitäten in den aktuellen Theme-Funktionen besser dynamisch gebunden werden können statt als statische Brushes neu erzeugt zu werden.

### Typografie

- Inter oder eine andere **rechtmäßig integrierte** gut lesbare Systemschrift.
- Sachliche Hierarchie: groß genug für Orientierung, zurückhaltend bei Metadaten.
- Systemtexte, Status und primäre Buttons auch bei 1366 × 768 gut lesbar.
- Mindestens sinnvoll große fokussierbare Klickflächen.
- Screenreader-Texte und Tastaturfokus vollständig erhalten.

---

## 5. Zwanzig verbindliche Komponenten von Verbesserung 41

Implementiere **alle 20 freigegebenen Bereiche**, zunächst im eingeschränkten **Design-Vertical-Slice** und nach separater visueller Freigabe in geeigneten gemeinsamen UI-Komponenten für alle Studios:

1. **HyperRealistic Material Design Engine:** echte Avalonia-Controls mit mehrschichtigen Oberflächen, glaubwürdiger Tiefe, reflektierenden Akzenten, kontrollierter Transparenz und performantem Fallback.
2. **Cinematic Trendo Boot Experience:** eleganter nicht blockierender Start, sanfte Überblendung, kein künstlicher Warte-Timer, reduzierte Bewegung möglich.
3. **Signature Trendo Brand Identity:** klar unterscheidbares Trendo-Markensystem; Logo **nur als Vorschlag** und nicht ohne visuelle Freigabe endgültig ersetzen.
4. **Adaptive High-End Wallpaper Engine:** hochwertige geeignete Wallpaper, optionale sehr dezente Bewegung; statisch/offline/Raspberry-Pi-Fallback.
5. **Real Spatial Window Depth:** aktive Fenster, Schattierung, Z-Ebenen, Titelleisten, Resize und Focus-States plausibel und funktional.
6. **Ultra-Smooth Interaction Motion:** Animationen in realen Fenstern/Menüs/Buttons; `ReduceMotion` korrekt berücksichtigen.
7. **Intelligent Responsive Desktop Layout:** echte adaptive Seitenleisten, skalierbare Studio-Karten, ohne proportional geschrumpfte Gesamtoberfläche.
8. **Premium Studio Card System:** visuell hochwertige Karten mit realen Statuswerten und echten Klick-/Tastaturaktionen.
9. **Ultra-Professional Studio Workspaces:** fachlich unterschiedliche Oberflächen, mit dem Briefstudio als erstem komplett getesteten Referenzstudio.
10. **Trendo AI Living Interface:** hochwertige KI-Panels; nur echte Zustände `nicht verbunden/prüfen/bereit/arbeitet/fehler`.
11. **Next-Generation Start Menu & Command Center:** Suche, Favoriten, letzte Projekte, alle Studios, Tastatur/Win/Esc, klare Hierarchie.
12. **Luxury Dock & Taskbar Experience:** verlässliche Taskleiste, aktive Fenster, Fokus, Min/Max, optionales Autohide ohne Overlayfehler.
13. **Beautiful Real-Time System Center:** CPU/RAM/GPU/SSD nur mit echten Messdaten, bei fehlender Quelle ehrlich `Nicht verfügbar`.
14. **Premium Typography & Iconography:** einheitliche Icons, Größen, Kontraste, Abstände und Fonts.
15. **Intelligent Contextual Workspace:** relevante Werkzeuge pro Aufgabenbereich, ohne globale Navigation zu verwirren.
16. **Micro-Interaction & Feedback System:** Hover, Fokus, Laden, Fertig, Fehler, Speichern; keine täuschenden Fortschrittsanzeigen.
17. **Beautiful First-Run Welcome Experience:** einfacher echter Einrichtungsdialog, kein Show-Setup und keine zwingenden Konten.
18. **Adaptive Visual Performance Engine:** High-End-PC-, Standard-PC- und Pi-Low-Power-Profil; ruhiger Fallback ohne intensive Filter.
19. **Accessibility Without Visual Compromise:** kontrastreich, vergrößerbar, keyboard-only, screenreader-fähig, reduzierbare Bewegung.
20. **Pixel-Perfect Design & Real Hardware QA:** vor/nach-Screenshots, echte UI-Interaktion, Größen-/DPI-/Performance-/Accessibilitytests.

---

## 6. Umsetzung in klaren Phasen

### UI41-0 – Baseline sichern

- Build-/Test-Ausgangszustand dokumentieren; Screenshots der real laufenden Windows-App sichern.
- Selbst wenn ein vorhandener Screenshot eine Designidee zeigt: immer von echter Buildausgabe unterscheiden.
- Visuelle Abweichungen gegen Referenzen nach Funktion/Klarheit/Typografie/Material/Responsivität kategorisieren.
- Prüfen, welche Ressourcen und Icons bereits existieren, um unnötige Assets zu vermeiden.

### UI41-1 – Design Tokens und Avalonia-Komponenten

- Zentrale ResourceDictionary-/Style-Architektur in bestehende Avalonia-App einführen, wenn technisch passend.
- Keine zweite globale Theme Engine schaffen.
- Wiederverwendbare Oberflächen: `PremiumSurface`, `PremiumStudioCard`, `PremiumTitleBar`, `PremiumStatusTile`, `PremiumActionButton`, `PremiumPanel` (konkrete Namen an vorhandene Konventionen anpassen).
- Alle neuen Komponenten erhalten Fokus-, Tastatur-, Disabled-, Hover-, Pressed-, Fehler- und High-Contrast-Zustände.
- Tests der Theme-Umschaltung und Benutzerpräferenzen.

### UI41-2 – Desktop, Seitenleisten und Dock

- Fixe Kombination `192,*,322` durch geeignete responsive Spalten-/Panelsteuerung ablösen.
- Auf schmalen Viewports: optional linke Navigation komprimieren und rechtes System-/KI-Panel ausklappen; Hauptinhalt behält nutzbare Mindestbreite.
- DPI- und Betriebssystemskalierung beachten; kein genereller `ScaleTransform` als Ersatz für adaptives Layout.
- Große, klare Desktop-Studio-Karten und hochwertige Widget-Hierarchie.
- Dock/Taskbar darf keinen Input blockieren und keine Karten verdecken.
- Fensterverwaltung bewahren.

### UI41-3 – Startmenü und KI-Bereich

- Bestehendes Startmenü aus `MainWindow.StartMenu.cs` visuell modernisieren, Such- und Keybinding-Funktion unverändert erhalten.
- Bestehende lokale KI-Anbindung und ihre Zustände bewahren und visuell verbessern.
- KI-Status nur aus tatsächlichen Verbindungs-/Ausführungsdaten abbilden: `Nicht verbunden`, `Verbindung wird geprüft`, `Bereit`, `Antwort läuft`, `Fehler`, `Offline`.
- Unverfügbare Modelle und Werkzeuge dürfen nicht als online/bereit erscheinen. Keine künstlich erzeugten Unterhaltungen als echte Nutzerdaten.
- Tastatur, Suchfeld, KI-Eingabe und bestehende Berechtigungen gegen Regression testen.

### UI41-4 – Briefstudio als echtes Referenzstudio

- Vorhandene Views und Brief-/Dokumentenlogik untersuchen.
- Oberfläche modular gestalten: Navigation links; Formular-/Editorbereich; echte Dokumentvorschau rechts, bei schmalen Fenstern als wechselbare Ansicht.
- Keine der fiktiven Adressen, Namen oder Musterwerte aus den Bildern als echte Kundendaten einfügen.
- Ein reales Testdokument erstellen, speichern, erneut öffnen und zulässige Formate exportieren.
- Bestehende Briefköpfe, Aktenbezüge, Versandfreigaben und Dokumentmodelle bewahren.
- Struktur vor/nach prüfen und tatsächliche Funktionstests dokumentieren.

### UI41-5 – Materialtiefe, Animation, Performance

- Schatten, subtile Beleuchtung und kontrollierte Glas-/Frosting-Materialien hinzufügen, wo Plattform und Leistung es erlauben.
- Bei schwacher GPU auf einfache Materialdarstellung zurückfallen, ohne Bedienelemente zu verstecken.
- Optionen für reduziertes Motion und maximalen Kontrast strikt respektieren.
- CPU, RAM, Renderinglast, Startzeit und Interaktionslatenz vorher/nachher vergleichen.
- Keine dauerhafte Reopen-/Neuinstanzierung für banale Themeänderungen, falls dies vermeidbar und sicher refaktorierbar ist.

### UI41-6 – Review-Build und Freigabestopp

**Hier bewusst STOPPEN, bevor alle 54 Studiooberflächen umgebaut werden.**

Liefern:
1. Echten startbaren Windows-Entwicklungsbuild.
2. Baseline-Vorher-/Nachher-Screenshots unter identischen Bedingungen.
3. Premium-Desktop, Startmenü, Studioübersicht, KI und echtes Briefstudio.
4. Responsivität mindestens 1366 × 768 und 1920 × 1080; soweit möglich DPI 125/150 %.
5. Kontrast-/Tastatur-/Klicktests einschließlich aller aktuell katalogisierten Studio-Karten.
6. Reales Testprotokoll und Performancevergleich.
7. Visuell prüfbare Reviewdateien mit Zuordnung zu den Referenzbildern.
8. **Explizite Freigabefrage:** „Soll dieser geprüfte Designstandard auf alle übrigen Studios übertragen werden? JA oder NEIN?“

**Ohne dieses neue JA** kein globaler Studio-Rollout und keine endgültige Logofestlegung.

### UI41-7 – Übertragung auf alle Studios (nur nach separatem JA)

- Gemeinsame Controls pro Studio inkrementell verwenden.
- Fachlayouts passend zum echten Workflow belassen: Game Studio ≠ Briefstudio ≠ Business Studio ≠ Dateimanager.
- Jedes Studio mit Screenshot und realem Funktions-/Interaktionstest.
- Keine Neuimplementierung bestehender Backendlogik nur wegen des visuellen Upgrades.

---

## 7. Verbindlicher QA- und Abnahmeplan

### Funktionale Regression

- `dotnet build Trendo.sln -c Release`
- `dotnet test Trendo.sln -c Release`, sofern aktuelle Lösung dies unterstützt; ansonsten alle aktuellen Core-/App-/API-Tests separat gemäß `AGENTS.md`.
- Studio-Karten: tatsächliche Klicks und Tastaturaktivierung; kein Dead-Button-Regression.
- Fenster: Drag, Resize an Kanten, Snap, Focus, Min/Max/Close, Taskleistenaktivierung.
- Startmenü: Win/Esc, Suche, Tastatur, Pins/Favoriten und aktive Suche.
- KI-Panel: getrennte Tests im Modus `Nicht verbunden` und mit echter lokaler KI, falls technisch eingerichtet.
- Briefstudio: Schreiben mit Testdaten, echtes Speichern, nach Möglichkeit DOCX-/PDF-Export und erneutes Öffnen.
- Themes: Dunkel, Hell, hoher Kontrast, User-Wallpaper, Akzentwechsel, gespeicherte Einstellungen.
- Keine Umgehung bestehender Berechtigungs- und Sicherheitslogik.

### Viewport-Matrix

- 1024 × 768, sofern aktuelle Mindestfenstergröße erlaubt.
- 1280 × 720.
- 1366 × 768.
- 1440 × 900.
- 1920 × 1080.
- 2560 × 1440.
- 3840 × 2160.
- 100/125/150/200 % DPI, soweit Testhardware verfügbar.

### Barrierefreiheit

- Vollständige Tastaturnavigation.
- Sichtbarer, kontrastreicher Tastaturfokus.
- Screenreader-Namen und sinnvolle Bedienreihenfolge.
- Gute Farbkontraste, kein Status ausschließlich durch Farbe.
- Bediengrößen und Schriften bei kleineren Displays weiterhin gut nutzbar.
- `ReduceMotion` deaktiviert unnötige Animationen.

### Performance

- Vorher-/Nachher-Kaltstart und Warmstart auf gleicher Hardware vergleichen.
- CPU und RAM im Leerlauf messen.
- Renderleistung bei 54 Studio-Karten und mehreren geöffneten Fenstern messen.
- GPU-Speicher-/Blur-Effekte auf unterschiedlichen Geräten testen, soweit verfügbar.
- Kein fingiertes „4K 120 FPS“; nur tatsächliche Messungen berichten.

### Zielplattformen

- Windows-Vertical-Slice zuerst physisch prüfen.
- Styles/Designsystem portierbar für Linux-PC und Raspberry Pi halten.
- Linux-/ARM64-Hardwaretests nur `TESTED` markieren, wenn sie wirklich durchgeführt wurden; sonst `NOT_RUN` oder `BLOCKED`.

---
## 8. Keine falschen UI-Zustände oder ungewollte Seiteneffekte

- KI-Referenzbilder sind Illustrationen, keine Screenshots des aktuellen oder neuen Builds.
- Keine fiktiven CPU-Auslastungen, aktiven Agenten, KI-Antworten, Wetterwerte oder Dateiinhalte.
- Kein unfreigegebenes Logo als finale Trendo-Marke festlegen.
- Blender/Unreal nur dann als startbereit kennzeichnen, wenn die Anwendung wirklich installiert beziehungsweise erreichbar ist.
- Keine rein dekorativen Buttons ohne echte Aktion; nicht fertig implementierte Studios ehrlich markieren.
- Keine Änderung existierender Datenschutz-/Berechtigungslogik oder versteckte Netzwerkübertragung.
- Keine sensiblen Daten, Zugangsdaten oder personenbezogenen Systeminformationen in das öffentliche GitHub-Repository einchecken.
- Keine Demo-Animation, die den Start oder den Test künstlich verzögert.

---

## 9. Verbindliche Produkt- und Entscheidungsgrenzen

Design 41 ist mit **allen 20 Punkten genehmigt**. Es ändert nicht die genehmigten Grundlagen von Trendo OS:

- **Windows-Edition** als native Anwendung auf Windows, kein ungefragter Ersatz von Windows-Shell oder Taskleiste.
- **Linux-PC-Edition** als eigenständig bootendes System auf kompatiblem USB-/SSD-Medium.
- **Raspberry-Pi-4-ARM64-Edition** als eigenständig bootfähiges ressourcenschonendes System.
- Weiterhin **One-Setup**, normale Windows-/Linux-Nutzerfreiheiten und Drittanbieterkompatibilität.
- **Claude Code** als Projektkoordinator und **Claude Code** als autorisierter lokaler Entwickler; keine erfundenen Tools/APIs.
- **Blender und Unreal Engine 5** bleiben externe eigenständige Anwendungen auf geeignetem PC.
- Lokale Offline-KI darf für normale Endnutzer kein verpflichtendes bezahltes Cloud-KI-Abo benötigen.
- Alle menschlichen Freigaben vor kritischen autonomen Aktionen erhalten.
- Temporäre forensische Beweiskopien samt abgeleiteter Caches/Indizes unterliegen der bestehenden **maximalen 120-Stunden-Frist**.
- Verbesserungen **23, 25, 34** bleiben ausdrücklich **NEIN**. Die umfassende Language-&-Localization-2.0-Erweiterung bleibt abgelehnt.
- Verbesserung **35** ist noch **OFFEN** und wird nicht als Extra-Erweiterung eingeführt.

Die **visuelle finale Logoabnahme** und **Freigabe für den Rollout auf alle Studios** sind eigenständige zukünftige Entscheidungen.

---

## 10. Reale Ergebnisartefakte im Projekt-Repository

Erstelle bei tatsächlicher lokaler Claude-Code-Ausführung:

1. Einen eigenen UI41-Branch, mit kleinen nachvollziehbaren Commits.
2. Einen Vorher-/Nachher-Bericht mit aktuellem Commit-SHA und Datum.
3. Echte Avalonia-Styles, Controls und responsive Layouts (kein Screenshot-Trick).
4. Screenshots des tatsächlich gestarteten Windows-Builds für Desktop, Startmenü, Studioübersicht, Briefstudio, KI, kleine und große Bildschirmauflösung.
5. Einen Bildvergleich gegen Referenz `01` bis `05`, mit dokumentierten Abweichungen und Ursachen.
6. Rohlogs der `dotnet` Builds und aller Testprojekte; Fehl- und Warnungszahlen angeben.
7. Messergebnisse für Startzeit, Speicherverbrauch und UI-Reaktion mit dokumentierter Hardware.
8. Liste verbleibender Defekte, technischer Einschränkungen und nicht gelaufener Tests.
9. Einen Bericht `tasks/UI-041-PREMIUM-EXPERIENCE.bericht.md` beziehungsweise passenden projekttypischen Bericht.
10. Eine separate **JA/NEIN**-Frage zur visuellen Abnahme und zur globalen Übertragung.

Keine Pushes bei roten Pflicht-Tests; bei fehlenden Berechtigungen keine voreilige Behauptung, dass gepusht oder gemergt wurde.

---

## 11. Ausführungsauftrag – JETZT

**Claude Code:** Lies diese Markdown-Datei einschließlich aller im ZIP enthaltenen Referenzbilder. Stelle sicher, dass Claude Code den **wirklichen aktuellen Stand** von `manuelbrandner85/Trendo-OS` benutzt, die geltenden Repo-Regeln beachtet und seine Änderungen auf `UI-041` beschränkt. Überwache echte Tests und Nachweise.

**Claude Code:** Starte mit UI41-0. Implementiere danach UI41-1 bis UI41-6 als **reale, ausführbare Änderungen** am bestehenden Avalonia-Projekt. Behalte funktionierende Studio- und Fensterlogik. Liefere Screenshots und Testergebnisse des tatsächlich gestarteten Programms. Stelle ungetestete Elemente ehrlich dar.

**HOLD POINT:** **STOPPE vor UI41-7** und präsentiere dem Auftraggeber die echten Vorher-/Nachher-Ergebnisse. Frage ausdrücklich:

> „Ist das neue Premium-Desktopdesign einschließlich des Briefstudios freigegeben, und soll ich es auf alle anderen Studios übertragen? JA oder NEIN?“

**Ziel:** Trendo OS soll sich visuell wie ein eigenständiges, hochwertiges Desktopprodukt anfühlen — ohne den vorhandenen Code, die echte Benutzbarkeit, die 3 Editionen oder die Nutzerfreiheit zu gefährden.
