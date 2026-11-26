# Restliste nach der Neubewertung (T-0769, Stand 0.9.110)

Grundlage sind `BERICHT.md` und die Kriterien K1–K6.

**Sortierung:**
- zuerst Querschnitts-Schritte, die viele Studios auf einmal heben;
- danach die Studios nach Nutzen (hoch → gering) und innerhalb gleichen Nutzens nach Aufwand (klein → sehr groß).

„BLOCKIERT“ heißt: Es fehlt ein Werkzeug, ein Konto oder ein Gerät. Das kann nur die Person selbst beschaffen oder freigeben.

## A. Querschnitt (zuerst – hebt viele Studios)

| Rang | Maßnahme | Hebt | Konkreter nächster Schritt | Aufwand |
|---|---|---|---|---|
| Q1 | KI-Adapter schreiben ins offene Dokument statt in TXT-Dateien | K4 in Büro- und Kreativ-Studios (≈ 20) | Für PDF-Werkstatt und Scannen die vorhandene `AssistChain` an die Fachaktion hängen; Logos hat den Weg schon (`AssistFromProgram`) und braucht nur den Echtlauf. Vorbild sind Tagesplaner, Bücher und Messwerte (echt mit lokaler KI, `reports/T-0707`). Beispiel PDF: „Seiten 1–3 heraus“ ergibt eine neue PDF, ein Rückgängig-Schritt. Je ein Echtlauf mit lokaler KI und Rohlog. | mittel |
| Q2 | Ein echter Claude-Abo-Lauf in den Kreativ- und Technik-Programmen über `ClaudeProgramDoc` | K4 überall dort, wo heute nur `ClaudeRunOverride` steht | Testschicht „echt“ mit `TRENDO_ECHT_CLAUDE`: Logo umfärben, Brief kürzen, Wand im Grundriss verschieben. Danach Strg+Z, Rohlog und SHA-256 unter `reports/`. | mittel |
| Q3 | Fremdprogramm-Nachläufe mit Werkzeugen, die schon da sind | K2 bei SYS-10, SPEC-11, Online verkaufen, CORE-05, SPEC-01, Künstlerstudio, Logos/Animation, Daten schützen, Trendo erweitern | LibreOffice aus dem Trendo-Werkzeugordner für Text/Geld-XLSX/Shop-CSV nutzen, FreeCAD 1.1 für CAD-STEP und DXF-Export, Krita für PSD/ORA, Inkscape für SVG, 7-Zip für Sicherungs- und Export-ZIPs. Laut T-0692/0694/0753 „nicht installiert“, tatsächlich aber vorhanden. **Erledigt mit T-0776** (`reports/T-0776/`): alle neun Nachläufe echt; dabei STEP- und PSD-Fehler behoben; offen: DXF-Texte in FreeCAD, SMIL-Bewegung in Inkscape, Umlaute im ZIP-Vermerk unter 7-Zip. | klein |
| Q4 | Stille Tests ehrlich machen | Glaubwürdigkeit aller Echtläufe | Die stillen Tests aus `BERICHT.md` Abschnitt 3.7 (u. a. `AssistChainUiTests.cs:208`, `W4AssetsUiTests.cs:37`, `CodeAiKiUiTests.cs:171`, `AgentEchtlaufUiTests.cs:59/79`, `KiTeamEchtlaufUiTests.cs:24`, `ChatAttachT0734UiTests.cs:265`, `KiChecksumLiveUiTests.cs:20`, `ModelsBatchUiTests.cs:234`, `T0742AblaufAusSatzUiTests.cs:298`) melden ohne Voraussetzung „übersprungen“ statt still zu bestehen. | klein |
| Q5 | Katalog aus Belegen erzeugen | Doku = Code | `docs/programme/katalog.json` neu erzeugen: Test-Status aus den Rohlogs, `PC_VERIFIED` für SYS-04, SYS-07, CORE-04 und X-08 zurücknehmen. SHA-Listen in `reports/T-0698`, `T-0703`, `T-0707` und `T-0723` neu erzeugen; dort passen einzelne Beschreibungsdateien nicht (z. B. `claude-lauf.md`). | klein |
| Q6 | Rechtsklick und Ablegen als gemeinsamer Baustein der Hülle | K6 in ≈ 25 Programmen | Kontextmenü „Ausschneiden/Kopieren/Einfügen/Löschen/Eigenschaften“ und Datei-Ablegen in `MainWindow.ProgramShell.cs` einmal bauen. Test mit echtem `DragEventArgs`. | mittel |
| Q7 | Restliche Programme in die Programm-Hülle | K3 bei Geld, Vorhaben, Karteikarten, Wissen, Druckplatte, „Aus Fotos“, Code-Editor, Erweiterungs-Werkstatt | Etappenweise umziehen wie T-0714–T-0717, je mit einem Kill-Test. | groß |

## B. Studios

| Rang | Id | Studio | Urteil jetzt | Wichtigste Lücke | Konkreter nächster Schritt | Nutzen | Aufwand |
|---|---|---|---|---|---|---|---|
| 1 | STUDIO-PDFTOOLS-01 | PDF-Werkstatt | TEILWEISE (Grenzfall) | KI schreibt nur `pdf-auftrag.txt` | KI-Aktion „Datei rein → neue PDF raus“ mit Rückgängig (Q1) | hoch | klein |
| 2 | STUDIO-CODEAI-01 | Code mit KI | TEILWEISE | Claude-Abo-Weg nie echt; kein Rechtsklick/Ziehen im Dateibaum | Claude-Echtlauf mit Rohlog; Kontextmenü im Baum | hoch | klein |
| 3 | SYS-04 | Programme installieren | TEILWEISE | kein einziger echter winget-Lauf | Die Person installiert und entfernt ein kleines Programm mit „Lauf aufzeichnen“, das Protokoll kommt nach `reports/` (BLOCKIERT bis zur Freigabe) | hoch | klein |
| 4 | STUDIO-DAYPLAN-01 | Tagesplaner | TEILWEISE (Grenzfall) | keine fremde .ics einlesbar | „.ics öffnen …“ + Plan direkt in den Trendo-Kalender; Gegenprobe mit Thunderbird oder Outlook, falls vorhanden | mittel | klein |
| 5 | STUDIO-LOGO-01 | Logos & Marken | TEILWEISE (Grenzfall) | KI-Weg ins Dokument gebaut (`AssistFromProgram`, Vorschau + Rückgängig), aber `logo-claude` in keinem Test | Echtlauf „Ideen mit Claude“ mit lokaler KI oder Claude: Änderung im offenen Logo, Strg+Z, Rohlog (Q2) | mittel | klein |
| 6 | STUDIO-BOOK-01 | Bücher schreiben | TEILWEISE (Grenzfall) | nur K6: kein Rechtsklick-Menü, keine eigenen Kürzel (dazu: nur Markdown-Feld, kein Satz) | Rechtsklick an Kapiteln und Kürzel (Q6) – dann vollwertig; danach Seitenvorschau mit Satzspiegel | mittel | klein |
| 7 | CORE-02 | KI fragen | TEILWEISE | Claude-Abo mit Anhang ungeprüft; zwei Chat-Seiten | Claude-Echtlauf mit Anhang; Seite „ki“ zusammenführen; Entscheidung zur eingebetteten Anmeldung (`ClaudeWeb.cs:14` gegen `ClaudeCode.cs:46`) | hoch | mittel |
| 8 | SYS-10 | Texte & Tabellen | TEILWEISE | KI ändert offenes Dokument nicht; MS-Office-Dateien ungeprüft | Text/Tabelle/Präsentation an die Kette anschließen; echte Word/Excel-Dateien als Testdaten | hoch | groß |
| 9 | STUDIO-FORMS-01 | Formulare ausfüllen | TEILWEISE | keine echten Amtsformulare; KI füllt nicht aus | 3 freie Amtsformulare als Testdaten; KI füllt aus „Meine Angaben“, mit Rückgängig | hoch | mittel |
| 10 | STUDIO-LETTER-01 | Briefe schreiben | TEILWEISE | KI ohne Echtlauf; kein Rechtsklick | Echtlauf lokale KI oder Claude im Brief; Rechtsklick über Q6 | hoch | mittel |
| 11 | SPEC-11 | Geld & Rechnungen | TEILWEISE | XRechnung nie extern validiert; keine echten Bankauszüge; nicht in der Hülle | KoSIT-Validator oder Mustang-Prüfung + echte CAMT/MT940 (anonymisiert); Umzug in die Hülle | hoch | mittel |
| 12 | SYS-05 | Aufgaben automatisieren | TEILWEISE | kein Austauschformat; kein Rechtsklick | Ablauf als Datei exportieren/importieren; Rechtsklick | hoch | mittel |
| 13 | STUDIO-SUMMARY-01 | Texte zusammenfassen | TEILWEISE | kein Sprachmodell im Kern | Zusammenfassung über `Intel.AskAsync` ins Dokument, Echtlauf mit Ollama | hoch | mittel |
| 14 | STUDIO-IMAGEAI-01 | Bilder mit KI | TEILWEISE | kein Bild-zu-Bild; Claude-Aktion schreibt nur Text | Bild-zu-Bild mit sd-cli, „Abbrechen“; Claude-Aktion malt wirklich | hoch | mittel |
| 15 | STUDIO-AGENTS-01 | KI-Agenten bauen | TEILWEISE | Agent-Ergebnisse nicht rückgängig; Beleg-SHA defekt | Rückgängig für Agent-Dateien; Beleg neu erzeugen | hoch | mittel |
| 16 | STUDIO-MUSIK-01 | Trendo Musik | TEILWEISE | Lied nicht rückgängig im Dokument; Mastern/Cover nicht echt belegt | Echtlauf Upload → Trennen → Mastern mit Rohlog | hoch | mittel |
| 17 | CORE-08 | Videos & Fotos bearbeiten | TEILWEISE | Bild: PSD-Export und Ablegen fehlen; Ton: Aufnahme nach Absturz verloren | Aufnahme-WAV nach Absturz wiederherstellen; Ablegen auf Leinwand/Welle | hoch | mittel |
| 18 | STUDIO-FILM-01 | Filme machen | TEILWEISE | erzeugt selbst keine Szenen; nur Tafeln ohne eigenes Material | Storyboard-Bilder mit echter Bild-KI in die Einstellungen; FDX-Import | hoch | groß |
| 19 | CORE-07 | Webseite bauen | TEILWEISE | Veröffentlichen nur gegen 127.0.0.1; Claude im App-Weg ersetzt | Upload auf Test-WebDAV der Person mit Rohlog (BLOCKIERT ohne Zugang); Kill-Test Code-Editor | hoch | mittel |
| 20 | SPEC-01 | Wohnung planen | TEILWEISE | DXF-Export nie in Fremd-CAD | DXF-Export in FreeCAD 1.1 öffnen (vorhanden); Rechtsklick-Menü | hoch | mittel |
| 21 | CORE-01 | Desktop einrichten | TEILWEISE | KI ohne Wirkung; Netzorte fehlen | Ablegen aus dem Explorer echt testen; KI-Aktion „Ordner aufräumen“ mit Rückgängig | hoch | mittel |
| 22 | CORE-11 | Daten schützen | TEILWEISE | nur Trendo-Datenordner; Verschlüsselung nur mit restic | Eigene Ordner aufnehmen; verschlüsselte Sicherung als Standard | hoch | mittel |
| 23 | SPEC-12 | System auf Gefahren prüfen | TEILWEISE | Prozesse/Dienste/Netz fehlen; yara nie echt | Prozesse und Dienste ergänzen; yara-x-Lauf (BLOCKIERT, nicht am PC) | hoch | mittel |
| 24 | SYS-11 | Programme starten | TEILWEISE | doppelt mit SYS-04; nur Knöpfe | mit SYS-04 zusammenführen; Kürzel und Rechtsklick | hoch | mittel |
| 25 | SYS-12 | Schrift & Farben anpassen | TEILWEISE | Setup.exe-Rückfall und Bildschirmleser ungeprüft | Setup-Rundlauf in einer VM; Narrator-Durchgang | hoch | mittel |
| 26 | X-02 | Einrichten & Reparieren | TEILWEISE | Nachholen von Zusatzprogrammen nie echt | Ein kleines Werkzeug echt nachholen, Rohlog | hoch | mittel |
| 27 | X-08 | Handy verbinden | TEILWEISE | kein echtes Android-Gerät | Lauf durch die Person mit „Lauf aufzeichnen“ (Gerät nötig) | hoch | klein |
| 28 | CORE-05 | 3D-Modelle erstellen | TEILWEISE | 3D-Druck ohne G-Code und Absturzschutz; CAD-STEP ohne Fremdbeleg | STEP in FreeCAD öffnen (Volumenvergleich); Druckplatte an `ProgramLink` | hoch | groß |
| 29 | CORE-06 | Spiele bauen | TEILWEISE | KI-Weg nur ersetzt | Echtlauf „Spiel aus einem Satz“ mit Claude; Mal-Fläche in Einfach direkt | hoch | groß |
| 30 | SYS-09 | E-Mails & Termine | TEILWEISE | nie gegen echtes Postfach; kein CalDAV/CardDAV | Echtlauf mit Testkonto der Person nach Anleitung T-0689 (BLOCKIERT ohne Konto) | hoch | groß |
| 31 | STUDIO-DOCSCAN-01 | Dokumente scannen | TEILWEISE (Grenzfall) | KI erzeugt keinen Scan; Knopf „Scanner …“ ist nur ein Hinweis | KI-Aktion Fotos → PDF (Q1); Knopf ehrlich umbenennen oder WIA anbinden | mittel | mittel |
| 32 | SPEC-14 | Fräsen & Fertigen | TEILWEISE | G-Code nie im Fremd-Viewer | `.nc` in CAMotics oder NC-Viewer laden, Rohlog | mittel | klein |
| 33 | SPEC-13 | Routen & Karten | TEILWEISE | keine echte Fremd-GPX | echte GPX der Person einlesen, Export in GPXSee/QGIS | mittel | klein |
| 34 | SPEC-05 | Messwerte auswerten | TEILWEISE | kein XLSX/PDF-Bericht | XLSX/PDF-Export, in LibreOffice geöffnet | mittel | klein |
| 35 | STUDIO-CITE-01 | Recherche mit Quellen | TEILWEISE | kein DOCX-Literaturverzeichnis; Zotero/Pandoc fehlen | DOCX-Verzeichnis, Prüfung in LibreOffice | mittel | klein |
| 36 | STUDIO-ART-01 | Künstlerstudio | TEILWEISE | PSD nie im Fremdprogramm | PSD und ORA im vorhandenen Krita (Trendo-Werkzeugordner) öffnen, Rohlog | mittel | klein |
| 37 | STUDIO-PHOTOS-01 | Fotos verwalten | TEILWEISE | keine echten Kamerafotos/EXIF im Test | Echtlauf mit Kamera-JPGs, Abbruch beim Kopieren | mittel | klein |
| 38 | STUDIO-TV-01 | TV & Live | TEILWEISE | Wiedergabe im App-Fenster nie echt | mpv mit `--wid` im echten Fenster prüfen | mittel | klein |
| 39 | X-07 | Freie Bilder & Klänge finden | TEILWEISE | kein echter Klick-Download-Test | Live-Test auf „übersprungen“ (Q4), Klick → Download echt | mittel | klein |
| 40 | SYS-03 | Downloads & Links | TEILWEISE | kein echter Netz-Download | Großer Download mit Neustart-Fortsetzen, Rohlog | mittel | klein |
| 41 | SYS-06 | Dateien teilen | TEILWEISE | K4/K6; kein zweites Gerät | Lauf mit zweitem Gerät, Rohlog; Kürzel/Rechtsklick | mittel | klein |
| 42 | SYS-07 | Geräte erkennen | TEILWEISE | echter Stick-Auswurf fehlt | Test-Stick der Person auswerfen, Protokoll | mittel | klein |
| 43 | SYS-13 | Qualität prüfen | TEILWEISE | KI-Weg ungetestet; zwei goldene Knöpfe | KI „Alles prüfen“ mit Test; eine goldene Aktion | mittel | klein |
| 44 | X-09 | Leistung anzeigen | TEILWEISE | GPU-Werte unter Last nie abgeglichen | GPU-Lastprobe gegen Task-Manager | mittel | klein |
| 45 | STUDIO-CONTRACTS-01 | Verträge-Ordner | TEILWEISE | Erinnerung nur als Datei | Termin in den Trendo-Kalender; Vertragsdaten per Texterkennung vorschlagen | mittel | mittel |
| 46 | STUDIO-DECKDESIGN-01 | Präsentationen gestalten | TEILWEISE | Generator statt Folien-Editor | Text direkt auf der Folie ändern, Gliederung zieht nach | mittel | mittel |
| 47 | STUDIO-CV-01 | Bewerbung & Lebenslauf | TEILWEISE | kein Import vorhandener Lebensläufe | DOCX/PDF-Lebenslauf einlesen; Rechtsklick | mittel | mittel |
| 48 | X-11 | Neues Projekt starten | TEILWEISE | KI legt Projekt am Programm vorbei an | KI legt Vorhaben mit Schritten an; CSV-Import | mittel | mittel |
| 49 | SYS-02 | Kleine Helfer & Musik | TEILWEISE | Video nur im fremden Fenster | eingebettete Wiedergabe; Ablegen/Rechtsklick im Player | mittel | mittel |
| 50 | STUDIO-POD-01 | Print on Demand | TEILWEISE | nie gegen echte Dienste | Lauf mit Printful-Testshop/Stripe-Testschlüssel der Person (BLOCKIERT ohne Konto) | mittel | mittel |
| 51 | SPEC-15 | Live streamen | TEILWEISE | keine Kürzel, kein Rechtsklick; rtmps nie | Kürzel Start/Stopp/Szene; private rtmps-Probe | mittel | mittel |
| 52 | STUDIO-PODCAST-01 | Podcast aufnehmen | TEILWEISE | echte Mikrofonaufnahme nie | Lauf mit Mikrofon durch die Person; Pegelanzeige | mittel | mittel |
| 53 | STUDIO-COMIC-01 | Comics & Storyboards | TEILWEISE | kein CBZ/PDF-Import | CBZ-Import + Gegenprobe in Comic-Reader | mittel | mittel |
| 54 | STUDIO-ANIM-01 | Animation & Motion | TEILWEISE | Export nicht unabhängig gelesen | animiertes SVG in Chrome abspielen, Bildvergleich | mittel | mittel |
| 55 | STUDIO-VISION-01 | Bilder erkennen | TEILWEISE | Claude-Beschreibung nur per Skript | Claude-Beschreibung über die App; Ordner ablegen | mittel | mittel |
| 56 | STUDIO-MODELS-01 | KI-Modelle vergleichen | TEILWEISE | keine KI-Bewertung | KI bewertet Antworten ins Dokument; Claude-Durchgang | mittel | mittel |
| 57 | STUDIO-TRAIN-01 | Eigene KI trainieren | TEILWEISE | Modell nirgends sonst nutzbar | Modell in Abläufen/Mail nutzbar machen | mittel | mittel |
| 58 | CORE-12 | KI-Anweisungen schreiben | TEILWEISE | Skill-Wirkung in Claude Code unbelegt | Echtlauf: Skill-Datei → Claude Code nutzt sie | mittel | mittel |
| 59 | X-05 | Kostenlose KI finden | TEILWEISE | eigene Modelldatei nicht einbindbar | GGUF mit Prüfsumme einbinden | mittel | mittel |
| 60 | SPEC-02 | Schaltungen berechnen | TEILWEISE | Fremdwerkzeug-Beleg fehlt | KiCad nach Freigabe, Gerber in GerbView (BLOCKIERT) | mittel | mittel |
| 61 | SPEC-03 | Statik nachrechnen | TEILWEISE | kein Austauschformat, keine Fremd-Gegenprobe | Gegenprobe gegen Frame3DD o. ä.; Austauschformat | mittel | mittel |
| 62 | SPEC-10 | Geräte überwachen | TEILWEISE | keine KI, kein echtes Gerät | Echtlauf mit Tasmota/Shelly (Gerät nötig); KI-Regel mit Rückgängig | mittel | mittel |
| 63 | SPEC-09 | Gegenstände einscannen | TEILWEISE | keine echten Fotos | Lauf mit Handyfotos und Maßvergleich; „Aus Fotos“ in die Hülle | mittel | mittel |
| 64 | SPEC-06 | Lernen mit Karteikarten | TEILWEISE | keine KI; nicht in der Hülle | KI-Karten aus Text mit Rückgängig; Hülle | mittel | mittel |
| 65 | CORE-10 | Wissen merken | TEILWEISE | Textfeld statt Markdown-Editor; KI ohne Dokumentwirkung | Markdown-Editor; Export in Obsidian öffnen | mittel | mittel |
| 66 | SYS-08 | Sicher recherchieren | TEILWEISE | kein Weg vom Treffer zur Quelle | Rechtsklick → „In Recherche mit Quellen“; Klick-Echtlauf | mittel | mittel |
| 67 | X-03 | Apps zusammenklicken | TEILWEISE | kein Fremd-HTML-Import; KI nur Parser | KI-Weg mit echtem Modell; Fremd-HTML importieren | mittel | mittel |
| 68 | X-06 | Projekte von GitHub holen | TEILWEISE | Installer-Lauf nie echt | Installer-Lauf in VM oder Testbenutzer mit Rohlog | mittel | mittel |
| 69 | X-04 | Trendo erweitern | TEILWEISE | Bedienung und Absturzschutz der Werkstatt | Werkstatt in die Hülle, Kürzel/Rechtsklick | mittel | mittel |
| 70 | CORE-03 | KI-Team einsetzen | TEILWEISE | Ergebnis nur ein Dateiordner | Team-Ergebnis als Hüllen-Dokument mit Rückgängig; Claude-Teamlauf | mittel | groß |
| 71 | X-01 | Großprojekte umsetzen | TEILWEISE | keine KI-Planung, kein Plan-Import | KI legt Karten aus einem Ziel an; echter Claude-Lauf | mittel | groß |
| 72 | STUDIO-VIDEOAI-01 | Videos mit KI | TEILWEISE | Szenenplan ohne KI; Wan sehr langsam | KI-Szenenplan ins Dokument; „Abbrechen“ bei Wan | mittel | groß |
| 73 | STUDIO-ONLINE-BUSINESS-01 | Online verkaufen | TEILWEISE | keine KI; Kassieren nicht unteilbar | Kassieren als Transaktion; KI-Aktion „Artikel anlegen“ | mittel | groß |
| 74 | STUDIO-CARDS-01 | Karten & Einladungen | TEILWEISE | keine freie Gestaltung auf der Karte | Text/Bild mit der Maus verschieben, Lage im PDF messen | mittel | groß |
| 75 | SPEC-08 | Kleidung & Produkte entwerfen | TEILWEISE | kein freies Neuzeichnen, kein Fremdimport | DXF-AAMA-Import; Teil von Null zeichnen | mittel | groß |
| 76 | X-10 | Programme nachbauen | TEILWEISE | Bedienung ungetestet | Kürzel/Ziehen testen; KI schreibt Anforderungen ins Dokument | gering | klein |
| 77 | SPEC-04 | 3D-Modelle ansehen | TEILWEISE | entfernt, nur Prüfer | keiner, solange entfernt | gering | klein |
| 78 | STUDIO-CONSOLE-01 | Konsolenspiele portieren | NICHT GETESTET | kein einziger echter Portierlauf | PS2Recomp und CMake nach Freigabe einrichten, `cubes_demo` portieren (BLOCKIERT) | gering | groß |
| 79 | SYS-01 | Startmodus wählen | TEILWEISE | Stick-Start nie belegt, Linux/Pi nicht im Angebot | Studio ehrlich zurückstufen (Desktop Edition) oder echten Stick-Start belegen | gering | groß |
| 80 | CORE-04 | PC, Pi & Handy verbinden | TEILWEISE | Name verspricht Pi, Gegenstelle fehlt | auf „Handy“ eingrenzen oder Pi-Lauf belegen | gering | groß |

## C. Die 10 wichtigsten Punkte

1. **Q1 KI ins Dokument:** Damit werden die drei K4-Grenzfälle vollwertig: PDF-Werkstatt, Dokumente scannen und Logos & Marken. Bücher schreiben wird mit Q6 vollwertig.
2. **Q2 Ein echter Claude-Lauf je Programmart:** Heute ist das überall ersetzt, außer bei Aufgaben automatisieren und KI-Agenten.
3. **Q3 Fremdprogramm-Nachläufe** mit dem vorhandenen LibreOffice und FreeCAD.
4. **Q4 und Q5 Stille Tests und Katalog ehrlich machen,** die SHA-Listen der Belege neu erzeugen.
5. **Tagesplaner:** .ics-Import, dann vollwertig. **Bücher schreiben:** Rechtsklick und Kürzel, dann vollwertig.
6. **Q6 Rechtsklick und Ablegen als Hüllen-Baustein.**
7. **Texte & Tabellen und Briefe:** KI im offenen Dokument, echte Word-/Excel-Dateien.
8. **Geld & Rechnungen:** XRechnung extern validieren, echte Bankauszüge, Umzug in die Hülle.
9. **Programme installieren:** ein echter winget-Lauf durch die Person.
10. **Q7 Restprogramme in die Hülle,** zuerst Geld, Vorhaben, Karteikarten, Wissen und die 3D-Druck-Platte.
