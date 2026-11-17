# Bericht T-0709 Design-Reste

**Auftrag:** `tasks/T-0709-design-reste.md` – offene Punkte des Bild-Audits „Anordnung und Design“ (V-01…V-22, freigegeben 16.11.2026) nach T-0684, T-0686, T-0700 und der Rest aus T-0705.
**Branch:** `claude/T-0709-design-reste` vom Integrationszweig `claude/T-0685-b1-integration` (Stand 64c15b0a8). Eigene Kopie in `%TEMP%`, nur dieser Zweig gepusht, kein Merge, kein Release.
**Status:** umgesetzt, gezielt getestet, im Bild nachgeprüft (Einfach und Profi); Volltest steht aus (Hauptsitzung).

Tests: neue Klasse `tests/Trendo.App.Tests/DesignPaket4UiTests.cs` (`DesignPaket4_Ui`, 8 Tests, echtes Avalonia-Headless-Fenster, Klicks über `Press()`; ersetzt ist nichts – kein Dateidialog nötig, die PDF-Exporte schreiben echte Dateien in den Testordner).

## Je Punkt: was war, was ist jetzt, Beleg

### 1. V-14 in Tabelle, Präsentation und Mail
- **War:** Tabelle und Präsentation: Speichern nur im goldenen „Datei“-Menü, Rückgängig/Wiederholen als „Zurück/Vor“ in der Gruppe „Bearbeiten“ nur auf dem Reiter „Start“. Mail: „Als Entwurf speichern“ unten im Schreibfenster, „Als Datei sichern“ in der zweiten Aktionszeile der gelesenen Nachricht (Bild `vorher/tabelle-*.png`, `vorher/praes-*.png`).
- **Jetzt:** gemeinsamer Baustein `DocActions` + neuer `DocBtn` (`MainWindow.DocActions.cs`). Tabelle und Präsentation: links oben vor „Datei“ die Gruppe **Speichern · ↶ · ↷ · Als PDF** auf jedem Reiter des Menübands (in „Einfach“ mit Wort: „Rückgängig“, „Wiederholen“). „Speichern“ ist die eine goldene Fläche, „Datei“ ist ein normales Menü mit allen Formaten (Speichern unter, Handzettel, Bilder, Video …). Die Gruppe „Bearbeiten“ der Präsentation heißt jetzt „Vorführen“ (nur noch der Knopf dazu). Mail: im Schreibfenster steht „Als Entwurf speichern“ (und „KI-Text zurücknehmen“, falls da) links oben in der Leiste vor „Neue E-Mail“; bei einer gelesenen Nachricht „Als Datei sichern“ (.eml) am selben Platz. „Senden …“ bleibt im Schreibfenster mit Rückfrage. Namen, Klicks und Hinweise unverändert.
- **Nicht hier:** CAD, 3D, Code-Editor, Briefe (T-0708 parallel).
- **Beleg:** `Doc_actions_sheet_and_deck` (Reihenfolge, links oben, sichtbar in Einfach und Profi, genau ein goldener Knopf, Rückgängig auch auf Reiter „Daten“, Klick nimmt Zelleingabe zurück und bringt sie wieder), `Doc_actions_export_writes_pdf` (Klick „Als PDF“ in Tabelle und Präsentation → zwei echte Dateien mit `%PDF`-Kopf), `Doc_actions_mail` (Gruppe an Stelle 0 der Leiste, Klick legt den Entwurf an). Bilder `nachher/tabelle-{einfach,profi}.png`, `praes-*.png`, `mail-schreiben-*.png`.

### 2. Verkaufen: eine Reiterzeile
- **War:** zwei Zeilen – Studio-Reiter „Verkaufen · Geschäftsidee“ und darunter „Kasse · Online & Artikel · Auswertung“ (`vorher/verkaufen-*.png`).
- **Jetzt:** eine Zeile „Kasse · Online & Artikel · Auswertung · Geschäftsidee“ (die übrigen Seiten des Studios hängen mit kleinem Abstand an, Namen `business-tab-*` wie bisher). Die Studio-Reiterzeile entfällt nur auf der Seite „Verkaufen“; „Geschäftsidee“ selbst hat weiter eine Zeile. Kein Reiter fehlt.
- **Beleg:** `Sell_has_one_tab_row` (Einfach und Profi: genau eine sichtbare Reiterzeile mit den vier Einträgen, „Geschäftsidee“ öffnet die Seite, „Auswertung“ bleibt auf Verkaufen). Bild `nachher/verkaufen-*.png`.

### 3. V-07/V-12 in gesperrten Programmen (Podcast, Comic) und Suche nach sichtbaren Pfadfeldern
- **Suche:** neuer Test `No_visible_path_field_in_simple` geht in „Einfach“ über **alle Seiten** und **jedes Programm mit erster Vorlage** und meldet jedes sichtbare Pfadfeld. Ergebnis: **keins** – der V-07-Baustein aus T-0700 greift überall (auch Podcast und Comic). Der Test war schon auf dem Integrationszweig grün und hält den Stand jetzt fest (Wächter, kein Rot-Grün-Nachweis). `PathPickAuditTests` grün.
- **Comic-Sprechblasen:** bereits erledigt in T-0697 (Knöpfe „Sprechblase, Gedankenblase, Ausruf, Textkasten“, erster Schritt „Erste Sprechblase setzen“, Bild `vorher/comic-einfach.png`) – nichts zu tun.
- **Podcast-Kapitel (V-12):** war ein Textfeld im Format „00:00 Titel“ je Zeile, dazu „Ziel −16 LUFS, Spitze −1,5 dBTP, MP3 128 kbit/s“ (`vorher/podcast-einfach.png`). Jetzt in „Einfach“ eine Liste: je Kapitel Feld für die Zeit und Feld für den Namen, „Entfernen“, darunter „+ Kapitel hinzufügen“ (an der Marke, sonst eine Minute nach dem letzten). Gespeichert wird weiter das bekannte Textformat, Shownotes/Feed/MP3-Kapitel lesen es unverändert; fehlerhafte Zeilen gehen nicht verloren. Das Textfeld bleibt im Baum und ist ab „Erweitert“ sichtbar. V-11: der LUFS-Satz heißt in „Einfach“ „Zum Schluss gleicht Trendo die Lautstärke aus und speichert eine MP3 mit Kapiteln.“
- **Beleg:** `Podcast_chapters_as_list` (Liste statt Textfeld, Name ändern landet im Dokument, hinzufügen → „32:05 Neues Kapitel“, entfernen, rückgängig über die Hülle, Profi zeigt das Textfeld), `KreativStudios_Ui.Podcast` unverändert grün. Bilder `nachher/podcast-{einfach,profi}.png`.

### 4. V-22 und weitere offene V-Punkte aus dem Abgleich
- **Webseite bauen (V-03/V-22):** sichtbar drei goldene Knöpfe („Seite bauen“, „Editor öffnen“, „Neue Seite daraus“), in den Abschnitten darunter weitere („Seite anlegen“, „Gestaltung übernehmen“, „Ins Projekt holen“, „Verbinden“ …) → eine goldene Hauptaktion: „Seite bauen“. „Editor öffnen“ normal, alle übrigen Hauptknöpfe ruhig hervorgehoben (goldener Rand), Klick und Name unverändert (`MainWindow.W4Dev.cs`, `WebDesign.cs`, `WebDesignKi.cs`). Beleg `Website_one_gold` (Einfach und Profi: genau `web-build` golden). Bild `nachher/dev-*.png`.
- **E-Mails & Termine (V-21):** Kalender-Monatsansicht mit sichtbaren Tageslinien, Wochentag links über der Tageszahl (vorher unsichtbare 0,5-px-Linien, Kopf mittig über links stehenden Zahlen; `v22/kalender-einfach.png` → `nachher/kalender-*.png`). Adressbuch vor dem ersten Kontakt nur „+ Neuer Kontakt“ und Einlesen; Sichern, Tabelle, Nummern, Etikettengröße, Etiketten und Geburtstage erscheinen mit dem ersten Kontakt (im Baum bleiben sie, keine Funktion fehlt); der obere „+ Neuer Kontakt“ ist dann ruhig, golden bleibt der Knopf im Leerzustand (`v22/kontakte-einfach.png` → `nachher/kontakte-*.png`). Belege: `AddressBook_Ui`, `CalendarGrid_Ui`, `O1O6_PimUi`, `IcsForeignUi` grün; im Bild geprüft.
- **V-15 Abläufe:** Umschalter hieß „Einfach: Trendo-Abläufe / Profi: n8n und Node-RED“ (zweite Bedeutung von Einfach/Profi) → „Trendo-Abläufe / n8n und Node-RED“. Beleg `N8nWorkspace_Ui`, `AblaufEditor_Ui` grün, Bild `nachher/ablaeufe-einfach.png`.

### 5. Büro-Studios: wichtigste Exporte in „Einfach“ sichtbar (Rest aus T-0705)
- **War:** `ProgramSpec.SimpleExports` war gesetzt (Bewerbung 2, Karten 2 …), aber `SimpleRibbon` schob in „Einfach“ jeden Export hinter „Mehr“.
- **Jetzt:** `MainWindow.SimpleMode.cs` – `OfficeDirectExports` (Programm → Anzahl): Bewerbung „Lebenslauf (PDF)“ + „Lebenslauf (Word)“, Präsentation gestalten „PowerPoint“ + „PDF“, Karten „Druck-PDF mit Beschnitt“, PDF-Werkstatt „Ergebnis als PDF“, Scannen „PDF (alle Seiten)“. Ruhig, nicht golden (Gold-Regel bleibt). Die Hülle (`ProgramShell.cs`) ist nicht angefasst.
- **Bewusst nicht:** Verträge und Formulare – dort stehen die Ergebnisse schon als Befehle direkt im Menüband („Erinnerungen (Kalender)“, „Kündigung schreiben“ bzw. „Ausgefüllt speichern“). Ein zusätzlicher Export-Knopf hätte im Test das Menüband umbrechen lassen (V-01: Arbeitsfläche bei 313/315 px statt ≤ 300 px). Zwei Exporte nur dort, wo beide in eine Zeile passen (Karten mit zweien: 315 px → einer).
- **Beleg:** `Office_exports_visible_in_simple` (alle fünf: Export direkt sichtbar, nicht golden, höchstens ein goldener Knopf), `ProgramHeadDesign_Ui.Workspace_starts_in_upper_third` grün, `BueroAusbau_Ui` grün. Bilder `nachher/bewerbung-einfach.png`, `nachher/karten-prog-einfach.png`, `nachher/formulare-einfach.png`, `nachher/vertraege-einfach.png`.

## Tabelle V-01 … V-22

| Nr. | Stand | Wo / Grund |
|---|---|---|
| V-01 Schmaler Kopf | erledigt in T-0686 | – (hier nur gesichert: zweiter Export nur, wo er nicht umbricht) |
| V-02 Erster Schritt im Leerzustand | erledigt in T-0686, T-0702 | – |
| V-03 Ein goldener Knopf | erledigt in T-0686/T-0700; **jetzt erledigt** für Tabelle, Präsentation (vorher „Datei“ golden, jetzt „Speichern“), Webseite bauen (3 → 1), Adressbuch leer (2 → 1) | – |
| V-04 Werkzeugleisten | erledigt in T-0686 | – |
| V-05 Falsche Wörter | erledigt in T-0684 | – |
| V-06 „Mehr anzeigen“ | erledigt in T-0684 | bewusst offen: auf `doku-start` bleibt das Suchfeld sichtbar, während „Zuletzt benutzt“ eingeklappt ist (klein, eigener Seitenumbau) |
| V-07 Dateiauswahl mit einem Knopf | erledigt in T-0700; **jetzt** über alle Seiten und Programme geprüft und als Test festgehalten | – |
| V-08 Speicherstand | erledigt in T-0686 | – |
| V-09 „Jetzt einrichten“ | erledigt in T-0700 | – |
| V-10 Fenstertitel | erledigt in T-0684 | bewusst offen: 18 Seiten mit zwei Namen (Leiste vs. Studio) – Namensentscheidung des Nutzers, kein Design-Detail |
| V-11 Alltagswörter | erledigt in T-0700; **jetzt** Podcast (LUFS/dBTP/kbit/s) | – |
| V-12 Knöpfe statt Schreibzeichen | erledigt in T-0700 (Bücher, Wissen, KI-Vorlagen), Comic in T-0697; **jetzt** Podcast-Kapitel | bewusst offen: Präsentation „Folien aus Stichpunkten“ (Einrückung mit „  - “) – gehört zum Umbau der Präsentations-Startseite |
| V-13 Eine KI-Eingabe | erledigt in T-0684 | bewusst offen: „Webseite bauen“ hat zwei Beschreibungsfelder (siehe V-22) |
| V-14 Speichern/Rückgängig/Export am selben Platz | erledigt in T-0700 (Text, Geld, Briefe); **jetzt erledigt** in Tabelle, Präsentation, Mail | bewusst offen hier: CAD, 3D, Code-Editor (T-0708 parallel) |
| V-15 Eine Reiterzeile | erledigt in T-0700 (TV, Verkaufen-Ideen); **jetzt erledigt** Verkaufen (2 → 1 Zeile), Abläufe-Umschalter ohne „Einfach/Profi“ | – |
| V-16 Leere Flächen | erledigt in T-0684 | – |
| V-17 Eine Flächenfarbe | erledigt in T-0700 | – |
| V-18 Entwicklerseiten nur Profi | erledigt in T-0684 | bewusst offen: interne Kennungen im Geräteverbund und Seitenschlüssel in der Skill-Liste (KI-Zentrale, nahe am gesperrten KI-Weg) |
| V-19 Wörter unter den Dock-Symbolen | erledigt in T-0684 | – |
| V-20 Trendo Text | erledigt in T-0692 | – |
| V-21 E-Mails & Termine | Reiter in T-0689; **jetzt erledigt**: Kalender mit Linien, Adressbuch vor dem ersten Kontakt entrümpelt | – |
| V-22 13 schwache Programme | teilweise: Videoschnitt T-0695, Logos T-0696, Comics/Podcast T-0697 (+ jetzt Kapitel), Text T-0692, TV T-0700, Geld T-0694/T-0700, Webseite bauen **jetzt** (eine Hauptaktion) | bewusst offen: **Webseite bauen** zwei Beschreibungsfelder → vier Schritt-Karten (eigener Seitenumbau); **3D-Modelle, 3D-Druck** (Drucker-Einrichtung immer sichtbar) – 3D gesperrt, parallel T-0707; **Geld** kein Eintragen-Feld auf der Übersicht, Kennzahlen doppelt (Übersicht + Monat); **Online-Dienste/Verbindungen** Wände aus Schlüsselfeldern (KI-Zentrale, nahe am gesperrten KI-Weg); **Dateien, Bild, Audio** nicht neu angesehen |

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler; `tools/Trendo.Render` gebaut.
- App gezielt, Lauf A (32 Klassen: Pflicht + berührte Programme): **197 Tests, 1 rot** – `ProgramHeadDesign_Ui.Workspace_starts_in_upper_third` (durch den zweiten Büro-Export, siehe Punkt 5) → behoben.
- App gezielt, Lauf B (26 Klassen nach Kalender/Adressbuch/Webseite/Abläufe): **150 Tests, 2 rot** – wieder `Workspace_starts_in_upper_third` (Formulare 315, Verträge 313 px mit nur einem Export) und `Website_one_gold` (der gewählte Reiter „Webseite bauen“ zählte als goldene Aktion) → Formulare/Verträge ohne Zusatz-Export (siehe Punkt 5), Test zählt gewählte Reiter nicht.
- App gezielt, Lauf C (`DesignPaket4_Ui`, `ProgramHeadDesign_Ui`, `BueroAusbau_Ui`, `BueroStudios_Ui`, `SimpleMode_Ui`): **36 Tests, 1 rot** – `Website_one_gold`: weiter unten auf der Seite waren noch goldene Abschnitts-Knöpfe → in `W4Dev.cs` ruhig gestellt.
- **Endstand:** `DesignPaket4_Ui` **8/8 grün**; Lauf D (16 Klassen: `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket1_Ui`, `DesignPaket3_Ui`, `W0b_Look`, alle Webdesign-UI-Klassen, `W4_Core07_Ui`, `StudiosKreativ3D_Ui`, `T0688_SoftwareStudio_Ui`): **132 Tests, 0 Fehler**. Zusammen mit Lauf A/B/C ist jede berührte Klasse auf dem Endstand grün oder nur durch die danach behobenen, oben genannten Tests rot gewesen; `ProgramHeadDesign_Ui` (in D) und `BueroAusbau_Ui`/`BueroStudios_Ui`/`SimpleMode_Ui` (in C) liefen nach der letzten Änderung an `SimpleMode.cs` grün. `PathPickAuditTests` und die Mail-/Kassen-/Podcast-Klassen liefen in A/B grün (ihre Dateien sind danach nicht mehr geändert worden).
- Auf dem Integrationszweig zuvor rot gemeldete Tests (T-0700: `W0b_Look.No_jargon`, `UebersichtEinfach_Ui.*`) sind auf diesem Stand grün.
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests, OK**.
- Datenschutz-Grep (ohne Groß-/Kleinschreibung) über alle geänderten Dateien: leer.
- Kein Volltest (macht die Hauptsitzung).

## Bilder (vorher/nachher)
Scratch-Ordner der Sitzung `t0709\vorher`, `t0709\nachher`, `t0709\v22`, 1600 × 1000, je eigener `TRENDO_RENDER_ROOT`. Angesehen: Tabelle (Einfach vorher/nachher, Profi), Präsentation (Einfach vorher, Profi nachher), Mail (Posteingang vorher, Schreibfenster nachher Einfach), Verkaufen (vorher/nachher Einfach), Podcast (Einfach vorher/nachher, Profi nachher), Comic (Einfach), Bewerbung und Karten (Einfach nachher), Kalender und Adressbuch (vorher/nachher Einfach), Webseite bauen (vorher/nachher Einfach), Abläufe (nachher), dazu Text, 3D-Druck, Geld, Online-Dienste für den Abgleich. Neues Render-Schalterchen `TRENDO_MAIL_COMPOSE=1` zeigt das Schreibfenster.

## Was NICHT geprüft wurde
- Kein Volltest, keine Api-Tests, keine Core-Tests (Core nicht geändert).
- Heller Stil, Kontrast, große Schrift, andere Fenstergrößen.
- Präsentation und Tabelle „Speichern“ über den echten Systemdialog (nur der PDF-Weg schreibt ohne Dialog; Speichern läuft über die vorhandene, getestete Funktion).
- Mail mit echtem Konto (ohne Konto geprüft; die Gruppe hängt nicht am Konto).
- Stufe „Erweitert“ nur in Tests.

## Offene Punkte
1. V-22 Rest: Webseite bauen in vier Schritt-Karten mit einer Beschreibung; Geld-Übersicht mit Eintragen-Zeile und einmal Kennzahlen; Online-Dienste als Zeilen „Name – Zustand – Verbinden“; 3D-Druck dreischrittig (nach T-0707).
2. V-14 in CAD, 3D, Code-Editor: T-0708.
3. V-10 Doppelnamen (Namensentscheidung), V-18 Kennungen im Geräteverbund/Skill-Liste, V-12 Präsentations-Stichpunkte, V-06 Suchfeld auf `doku-start`.
4. Zeilenenden: Git meldet „LF will be replaced by CRLF“ – keine inhaltliche Änderung.

## Berührte Dateien
src/Trendo.App: MainWindow.DocActions.cs, MainWindow.Tabelle.cs, MainWindow.Praesentation.cs, MainWindow.MailProgramView.cs, MainWindow.Kasse.cs, MainWindow.W5Sell.cs, MainWindow.SimpleMode.cs, MainWindow.PodcastStudio.cs, MainWindow.WebDesign.cs, MainWindow.WebDesignKi.cs, MainWindow.W4Dev.cs, MainWindow.Pim.cs, MainWindow.AddressBook.cs, MainWindow.Workflows.cs. tools/Trendo.Render/Program.cs. tests: neu DesignPaket4UiTests.cs. tasks: T-0709-design-reste.md, T-0709-design-reste.bericht.md.

## Abnahmekriterien (Paket ist reine Gestaltung)
1. Ergebnis aus leerer Vorlage: NICHT GETESTET (kein Fachumbau). 2. Fremddatei-Import/Export im Fremdprogramm: NICHT GETESTET (nur eigene PDF-Exporte, Kopf geprüft, nicht in einem Fremdprogramm geöffnet). 3. Absturz ≤ 30 s: NICHT GETESTET (unverändert, Hülle unberührt). 4. KI-Fachaufgabe: NICHT GETESTET. 5. Test vom Klick bis zur echten Datei: TEILWEISE (Klick „Als PDF“ in Tabelle/Präsentation → echte PDF-Datei; Klick „Als Entwurf speichern“ → Entwurf). 6. Einfach/Profi: VOLLWERTIG für die geänderten Regeln (beide Stufen getestet und im Bild); Tastenkürzel/Rechtsklick/Ziehen: unverändert.

```
ENTWICKLUNGSBERICHT
- Projekt: Trendo OS, T-0709 Design-Reste
- Verwendete Sprachen: C# (.NET 10, Avalonia 12)
- Rust eingesetzt: NEIN
- Rust-Komponenten: keine
- Grund der Sprachwahl: Oberflächen-Änderungen in der bestehenden C#-App; kein Leistungsengpass
- Durchgeführte Tests: siehe „Kommandos und Ergebnisse“
- Gemessene Verbesserungen: keine Leistungsmessung (nicht Ziel)
- Offene Probleme: siehe „Offene Punkte“
```
