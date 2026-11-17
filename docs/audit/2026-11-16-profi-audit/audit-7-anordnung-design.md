# Trendo OS – Audit 7: Anordnung und Design aus Nutzersicht

Stand: Arbeitskopie `studios-aufraeumen` (0.9.103), 16.11.2026. Nur Analyse, am Repo wurde nichts geändert.

## Grundlage und Methode

- 188 Bilder mit `tools/Trendo.Render` erzeugt (1600 × 1000, dunkler Trendo-Stil), alle Läufe mit Exitcode 0.
  Ordner: Arbeitsordner der Prüfung (nicht im Repo)
- Dateinamen: `<Seitenschlüssel>_start.png` (Seite ohne Dokument), `<Schlüssel>_einfach.png` (Vorlage geöffnet, Stufe Einfach), `<Schlüssel>_profi.png` (Stufe Professionell), `desktop.png`, `3d_formen.png`.
- Umfang: Desktop, Studio-Übersicht, Einstellungen, alle 122 Seiten des Programmkatalogs (ohne die drei entfernten Studios), 39 Hüllen-Programme mit Vorlage in „Einfach“, 25 davon zusätzlich in „Professionell“.
- Angesehen (verkleinert auf 960–1100 px Breite, Texte lesbar): rund 155 der 188 Bilder. Nicht einzeln angesehen sind vor allem die gleich gebauten Startseiten der Hüllen-Programme, siehe „Nicht geprüft“.
- Die Zahl „Bedienelemente“ ist aus dem Bild gezählt: alles Anklickbare oder Ausfüllbare unterhalb der Fensterleiste, das ohne Scrollen sichtbar ist, einschließlich KI-Leiste. Genauigkeit etwa ±10 %. Eine maschinelle Zählung gibt es im Render-Werkzeug nicht.
- Urteil „Verständlichkeit“: Erkennt jemand ohne Fachwissen in fünf Sekunden, was das Programm tut und was der erste Schritt ist?

## Kurzfassung

| Urteil | Seiten |
|---|---|
| gut | 41 |
| mittel | 54 |
| schwach | 27 |

Die Programme selbst sind meist besser als ihr Rahmen. Was fast überall stört, kommt aus gemeinsamen Bausteinen: dem Kopfbereich der Programm-Hülle, der Wort-an-Knopf-Automatik, dem automatischen Einklappen und der Dateiauswahl. Diese vier Stellen zu ändern wirkt in 60 bis 100 Ansichten gleichzeitig.

Gute Muster, die schon da sind und als Vorbild taugen:

- Startseite der Hüllen-Programme mit „Neu“, „Was möchtest du machen?“ und „Zuletzt“ (`bewerbung-programm_start.png`).
- Kompakter Kopf (`animation-programm_einfach.png`, `pod-programm_einfach.png`): Arbeitsfläche beginnt bei 29 % der Fensterhöhe.
- Werkzeugpalette links mit Symbol und Wort in den Technik-Programmen (`architektur_einfach.png`, `elektronik_einfach.png`).
- Nummerierte Schritte (`fotos-verwalten-programm_einfach.png`, `film-programm_einfach.png`, `scan3d_start.png`).
- „Als Nächstes“ im Spiele-Studio (`spiele-programm_einfach.png`).

## 1. Systemweite Befunde

### S-01 Der Kopfbereich nimmt die halbe Höhe

In 21 der 39 Hüllen-Programme liegen sechs Streifen übereinander: KI-Leiste mit Beispielen, Titelkarte, Zeile „Mehr Werkzeuge / Dokumentname / Speicherstand“, Hinweis „Vorlage … geöffnet.“ mit Knopf „Kopieren“, Dokument-Reiter, Band „START“. Die Arbeitsfläche beginnt bei etwa 480 von 1000 Pixeln. Bei Karten & Einladungen ist die Karte deshalb unten abgeschnitten.
Die anderen 18 Hüllen-Programme haben schon einen kompakten Kopf (Beginn bei etwa 290 Pixeln).

- Bilder: `bewerbung-programm_einfach.png`, `karten-programm_einfach.png`, `podcast-programm_einfach.png`; Gegenbeispiel `animation-programm_einfach.png`.
- Umbau: überall der kompakte Kopf. Eine Zeile mit Programmname, Dokumentname und Speicherstand, darunter direkt das Band. Der Hinweis „Vorlage geöffnet“ erscheint vier Sekunden in der Statuszeile. Der Erklärsatz des Programms steht nur auf der Startseite und hinter „?“. Danach beginnt die Arbeitsfläche in allen Programmen im oberen Drittel.
- Aufwand: mittel.
- Dateien: `src/Trendo.App/MainWindow.ProgramShell.cs` (Kopf, Hinweis, Band), die Definitionen der 21 betroffenen Programme (`MainWindow.Bewerbung.cs`, `Formulare.cs`, `Karten.cs`, `KreativStudios*.cs`, `PdfWerkstatt.cs`, `DocScan.cs`, `Vertraege.cs`, `ProgGame.cs`). 18 Definitionen tragen `ScrollWorkspace = true`; ob genau dieser Schalter den hohen Kopf auslöst, ist nicht nachgeprüft.

### S-02 Keine eindeutige Hauptaktion: mehrere goldene Knöpfe, dazu Blau

Im Band sind fast immer zwei bis vier Knöpfe golden, „Speichern“ ist es in jedem Programm, obwohl automatisch gespeichert wird. Die eigentliche Hauptaktion steht oft zweimal da: golden im Band und blau in der Arbeitsfläche („Ausführen“, „Gestalten“, „Tag planen“, „Bilder malen“, „Frage stellen“, „Bild öffnen“). Blau ist außerdem die Farbe aktiver Reiter in mehreren Programmen. Das widerspricht der Entscheidung „Gold statt Blau“.

- Bilder: `ton_einfach.png` (vier goldene Knöpfe), `pdf-werkstatt-programm_einfach.png`, `tagesplaner-programm_einfach.png`, `bild-ki-programm_einfach.png`, `quellen-programm_einfach.png`.
- Umbau: je Ansicht genau ein goldener Knopf, und zwar der nächste sinnvolle Schritt. „Speichern“ wird neutral. Die Hauptaktion steht nur einmal, in der Arbeitsfläche. Blau entfällt als Knopf- und Reiterfarbe, aktive Reiter bekommen Gold.
- Aufwand: mittel.
- Dateien: `MainWindow.ProgramShell.cs` (`Primary` im Band), `App.axaml` (Klasse `accent`, 45 Verwendungen), die Programme mit eigener blauer Schaltfläche (`KreativStudios*.cs`, `PdfWerkstatt.cs`, `PodStudio.cs`, `ConsolePort.cs`).

### S-03 Die Wort-an-Knopf-Automatik beschädigt Leisten

Die Automatik aus T-0663 (V3) hängt in „Einfach“ das Wort an Symbol-Knöpfe. Vier Folgen sind im Bild zu sehen:

1. Abgeschnittene Werkzeugleiste rechts: „Zusamme“, „Seiten or“, „Komprimi“, „Verschiel“, „Zuschnei“, „Unterschrift“. Die Leiste ist fest 40 bzw. 88 Pixel breit.
2. Zusammengeklebte Leisten ohne Abstand: Dateien („Zurück→ Vor↑ Übergeordneter Ordner“), Videoschnitt und Musik (Abspiel-Leiste überlappt).
3. Falsches Wort: Der Pfeil für „Weiter“ heißt „Abspielen“ im Kalender, bei Geld & Rechnungen und in Formulare ausfüllen.
4. Wortwiederholung in Listen: 30-mal „✕ Beispiel entfernen“, „✕ Aufgabe entfernen“; im 3D-Studio verdrängen „Ausblenden“ und „Entfernen“ den Namen des Teils („Wü“, „Zyli“).

- Bilder: `pdf-werkstatt-programm_einfach.png`, `bild_einfach.png`, `dateien_start.png`, `schnitt_einfach.png`, `kalender_start.png`, `ki-trainieren-programm_einfach.png`, `3d_formen.png`.
- Umbau: Werkzeugleiste in „Einfach“ wie in den Technik-Programmen als Palette links mit Symbol über Wort und fester Mindestbreite. Leisten bekommen Abstand und brechen um. Der Pfeil bekommt je nach Stelle das richtige Wort („Weiter“, „Nächster Monat“). In Listenzeilen bleibt das Symbol „✕“ allein, das Wort steht im Hinweis.
- Aufwand: mittel.
- Dateien: `src/Trendo.App/SimpleWords.cs`, `GlyphIcons.cs` (Zeile 20), `MainWindow.ProgramShell.cs` (Zeilen 695–710), `MainWindow.Dateien.cs`, `MainWindow.VideoSchnitt.cs`, `MainWindow.Calendar*.cs`.

### S-04 Der Leerzustand zeigt auf etwas, das in „Einfach“ versteckt ist

Videoschnitt und Audio sagen „Rechts Videos, Fotos oder Musik hinzufügen“. Rechts ist in „Einfach“ nichts, weil die Eigenschaften-Spalte eingeklappt ist (E2). In der Bildbearbeitung gibt es keinen sichtbaren Weg, ein Foto zu öffnen. In Logos und Comics ist nicht zu sehen, wo man Namen oder Sprechblasentext eingibt.

- Bilder: `schnitt_einfach.png`, `ton_einfach.png`, `bild_einfach.png`, `logo-programm_einfach.png`, `comic-programm_einfach.png`.
- Umbau: Jede leere Arbeitsfläche zeigt in der Mitte einen Satz und einen goldenen Knopf für den ersten Schritt („Video hinzufügen“, „Foto öffnen“, „Namen eingeben“). Was für den ersten Schritt nötig ist, wird in „Einfach“ nie eingeklappt.
- Aufwand: mittel.
- Dateien: `MainWindow.ProgramShell.cs` (`SimpleCollapsed`, Zeilen 364–376 und 730), `MainWindow.VideoSchnitt.cs`, `MainWindow.AudioEditor.cs`, `MainWindow.Bildbearbeitung.cs`, `MainWindow.KreativStudios.cs`.

### S-05 „Mehr anzeigen (… weitere Funktionen)“ versteckt den Hauptinhalt

Das automatische Einklappen aus V2/V8 trifft auch den eigentlichen Inhalt. Texte & Tabellen zeigt die Überschrift „Vorlagen“ und darunter nichts. Die KI-Bibliothek zeigt nach der Wahl „Chat & Schreiben (7)“ kein einziges Modell. Bei Downloads sind es Dateien, keine „Funktionen“. In der Ersteinrichtung ist der Begrüßungssatz mit „…“ gekürzt.

- Bilder: `doku-start_start.png`, `ki-bibliothek_start.png`, `components_start.png`, `downloads_start.png`, `einrichtung_start.png`.
- Umbau: Der erste Block unter einer Überschrift bleibt immer offen. Der Knopf nennt, was folgt („Alle 19 Vorlagen zeigen“, „Alle 7 Modelle zeigen“). Begrüßung und Leerzustände werden nie gekürzt.
- Aufwand: klein.
- Dateien: `src/Trendo.App/MainWindow.SimpleMode.cs` (Zeile 181 und `SimplifyView`), `MainWindow.Uebersicht.cs`.

### S-06 Gleiche Dinge stehen nicht am gleichen Ort

Es gibt drei Bauarten. In der Hülle stehen Speichern und Rückgängig links im Band. Briefe hat Speichern, Exportieren und Senden oben rechts. Trendo Text, CAD, 3D und Musik haben eigene Leisten in jeweils anderer Form. Seiten wie Medien, Werkzeuge, Projekte oder Freigaben haben gar keine Titelkarte. Im 3D-Studio steht die KI-Leiste unter dem Titel statt darüber.

- Bilder: `brief_start.png`, `text_start.png`, `cad_start.png`, `3d_start.png`, `musik-studio_start.png`, `medien_start.png`.
- Umbau: ein fester Platz für fünf Dinge in jedem Programm. Links oben Speicherstand und Rückgängig, rechts oben „Fertig / Exportieren“, „KI ▾“ und „?“. Programme außerhalb der Hülle übernehmen nur diese Kopfzeile, ihr Fachaufbau bleibt.
- Aufwand: groß (deckt sich mit Paket B6 des Bestandsaudits).
- Dateien: `MainWindow.ProgramShell.cs`, `MainWindow.ProgramFrame.cs`, `MainWindow.Brief*.cs`, `MainWindow.Text.cs`, `MainWindow.Cad.cs`, `MainWindow.Studio3D*.cs`, `MainWindow.Musik*.cs`.

### S-07 Dateiauswahl braucht drei Bedienelemente und ist abgeschnitten

Auf mindestens 18 Seiten steht ein Feld „Klicken, um eine Datei zu wählen – …“ (Text meist abgeschnitten), daneben „Datei wählen …“ und ein dritter Knopf („Einlesen“, „Öffnen“, „Übernehmen“). Bei Medien sind es vier Elemente für einen Ordner.
Fehler: Im Verträge-Ordner ist die KI-Eingabe selbst zum Ordnerfeld geworden, mit „Ordner wählen …“ in der KI-Leiste.

- Bilder: `kontakte_start.png`, `medien_start.png`, `ki-wissen_start.png`, `prog-pflege_start.png`, `vertraege-programm_einfach.png`.
- Umbau: ein Knopf „Datei wählen …“, der nach der Auswahl sofort einliest und die Datei als kleines Kärtchen zeigt. Das Textfeld entfällt in „Einfach“. Die KI-Eingabe wird von der Erkennung ausgenommen.
- Aufwand: klein bis mittel.
- Dateien: `src/Trendo.App/MainWindow.Files.cs` (Zeilen 66–78), `src/Trendo.Core/Shell/PathFields.cs`.

### S-08 Ein Fenster, drei Namen

Fenstertitel, Titelkarte und Eintrag in der linken Leiste nennen oft Verschiedenes:

| Seite | Fenstertitel / Titelkarte | Inhalt |
|---|---|---|
| Audio bearbeiten, Bildbearbeitung, Videoschnitt | „Videos & Fotos bearbeiten“ | das jeweilige Programm |
| Konto, Lizenz & Vollversion | „Schrift & Farben anpassen“ | Lizenz |
| Hardware & Leistung, Leistung messen | „Qualität prüfen“ | Systemdaten |
| Verbindungen, Mit Claude verbinden | „KI fragen“ | GitHub, Netlify, Anmeldung |
| KI-Bibliothek, KI-Quellen | „Kostenlose KI finden“ | zwei verschiedene Seiten |
| Medien | „Medien“, Studio heißt „Kleine Helfer & Musik“ | Fotos |

- Bilder: `ton_einfach.png`, `lizenz_start.png`, `hardware_start.png`, `verbindungen_start.png`, `ki-quellen_start.png`.
- Umbau: Der Fenstertitel ist immer der Name der sichtbaren Seite. Die Titelkarte des Studios erscheint nur auf dessen Startseite.
- Aufwand: klein.
- Dateien: `MainWindow.StartMenu.cs` (Zeile 104), `MainWindow.ProgramFrame.cs`, `StudioCatalog.cs`, `MainWindow.StudioFolds.cs`.

### S-09 Mehrere KI-Eingaben auf einer Seite, dazu die Zeile „Gehört hierher:“

Die KI-Leiste steht über jeder Seite. Auf KI-Seiten kommen eigene Eingabefelder dazu: „KI fragen“ hat zwei, „Trendo KI (lokal)“ zwei, „Webseite bauen“ drei. Wer tippt wo? Die Zeile „Gehört hierher:“ mit bis zu sieben kleinen Knöpfen über der KI-Leiste erklärt sich nicht. Hinter ihr scheint oben rechts eine Uhrzeit durch.

- Bilder: `ki-fragen_start.png`, `ki_start.png`, `dev_start.png`, `settings_start.png`, `ton_einfach.png`.
- Umbau: Auf Seiten mit eigenem KI-Gespräch entfällt die obere KI-Leiste, die Seite selbst ist die Eingabe. „Gehört hierher:“ heißt „Verwandte Programme“ und wandert in das Menü „Mehr“.
- Aufwand: klein.
- Dateien: `MainWindow.ClaudePrograms.cs`, `MainWindow.PromptChips.cs`, `MainWindow.StudioFolds.cs` (Zeile 26), `MainWindow.ChatWindow.cs`.

### S-10 Fachwörter und Zeichen in der Stufe „Einfach“

Trotz E10 und V7 stehen in „Einfach“ sichtbar:

- Dateiformate und Kürzel: „ICS einlesen“, „(.vcf)“, „vCard“, „Gästeliste laden (CSV)“, „#RRGGBB“, „EXIF“, „GPX“, „ATS-Fassung“.
- Technik: „M3U-Adresse (auch get.php von Xtream)“, „−16 LUFS, −1,5 dBTP“, „720p30“, „COLMAP / Meshroom“, „Slicen“, „gcode.3mf“, „Live über MQTT“, „OAuth Device Flow“, „MCP-Server“, „Plugins“, „winget“, „restic“, „SHA-256“, „Tailscale“, „Get-PnpDevice“, „GPIO“.
- Schreibzeichen als Bedienung: „# Titel“ und „**fett**“ im Buch, „[[Seitentitel]]“ in Wissen, „{{Thema}}“ in KI-Vorlagen, Gliederung mit „–“ und „|“ in Präsentationen, Kapitel als „00:00 Titel“ im Podcast, G-Code-Liste in der CNC-Werkstatt.
- Tastenkürzel im Knopftext: „Teilen (S)“, „Löschen (Entf)“, „Abspielen (Leertaste)“.

- Bilder: `tv_start.png`, `podcast-programm_einfach.png`, `kalender_start.png`, `buch-programm_einfach.png`, `verbindungen_start.png`, `modus_start.png`.
- Umbau: Wortliste für „Einfach“ erweitern, Fachwort in den Hinweis. Schreibzeichen durch Knöpfe ersetzen („Überschrift“, „Fett“, „Kapitel hinzufügen“). Tastenkürzel nur im Hinweis.
- Aufwand: mittel.
- Dateien: `SimpleWords.cs`, `MainWindow.Tv*.cs`, `MainWindow.KreativStudios.cs` (Podcast), `MainWindow.KreativStudios2.cs` (Buch), `MainWindow.Calendar*.cs`, `MainWindow.Kontakte*.cs`, `MainWindow.VideoSchnitt.cs`.

### S-11 Wichtige Meldungen stehen in der kleinen Statuszeile

Unten steht eine blaue Zeile in Schreibmaschinenschrift. Dort landen auch Dinge, die den Erfolg entscheiden: „Bild-KI nicht eingerichtet“ (der Knopf „Bilder malen“ steht trotzdem bereit), „⚠ 1 Stab/Stäbe reichen nicht“. Der Speicherstand steht zweimal da: oben rechts „nicht gespeichert“ (in 21 Programmen am Rand abgeschnitten) und unten „Noch nicht gespeichert · 00:51“. Was „00:51“ bedeutet, sagt nichts.

- Bilder: `bild-ki-programm_einfach.png`, `technik_einfach.png`, `bewerbung-programm_einfach.png`, `comic-programm_einfach.png`.
- Umbau: Was den nächsten Schritt verhindert, steht als Hinweis mit Knopf direkt an der Hauptaktion („Bild-KI fehlt – jetzt einrichten“). Der Speicherstand steht einmal, oben links: „Wird automatisch gespeichert“ oder „Gespeichert“. Die Statuszeile zeigt nur Nebensachen, in normaler Schrift.
- Aufwand: klein bis mittel.
- Dateien: `MainWindow.ProgramShell.cs` (Zeile 362 und Statuszeile), `MainWindow.KreativStudios2.cs` (Bilder mit KI), `MainWindow.Spec03.cs`.

### S-12 Leere Flächen, leere Kästen, doppelte Leerzustände

Viele Seiten nutzen nur das obere Drittel, darunter ist nichts. Spalten enden nach wenigen Zeilen (Adressbuch). Es gibt Kästen ohne Inhalt (Großprojekte rechts, Projekte rechts). Bei Aufgaben steht ein großer Leerzustand über drei leeren Spalten, die dasselbe sagen.

- Bilder: `kontakte_start.png`, `produktion_start.png`, `projects_start.png`, `aufgaben_start.png`, `doku-start_start.png`.
- Umbau: Spalten füllen die Fensterhöhe. Leere Kästen werden nicht gezeichnet. Ein Leerzustand je Seite, in der Mitte der Hauptfläche, mit einem Knopf.
- Aufwand: klein.
- Dateien: `MainWindow.Kontakte*.cs`, `MainWindow.Produktion*.cs`, `MainWindow.Projects*.cs`, `MainWindow.Aufgaben*.cs`, gemeinsamer Baustein für Leerzustände in `MainWindow.ProgramFrame.cs`.

### S-13 Uneinheitliche Flächen, Farben und Symbole

Der Seitenhintergrund ist teils neutral grau, teils dunkelblau (PDF, Präsentation, CAD, Neues Projekt). Geld & Rechnungen und die Kasse haben kräftig grüne, blaue und orange Kacheln. Trendo Tabelle hat bunte Bildsymbole, alles andere einfarbige. In Reiterleisten trägt jeder Reiter dasselbe Symbol (Kleine Helfer: zehnmal die Aktentasche). In Briefe ist der Text der gewählten Kachel golden auf Gold und nicht lesbar.

- Bilder: `finanzen_start.png`, `verkaufen_start.png`, `tabelle_start.png`, `alltag-start_start.png`, `praesentation_start.png`, `brief_start.png`.
- Umbau: eine Flächenfarbe für alle Seiten. Kennzahl-Kacheln dunkel mit farbiger Zahl oder Randlinie. Reiter ohne Symbol oder mit eigenem. Gewählte Kacheln mit dunklem Text auf Gold.
- Aufwand: klein bis mittel.
- Dateien: `App.axaml`, `MainWindow.Geld*.cs`, `MainWindow.Kasse*.cs`, `MainWindow.Tabelle*.cs`, `MainWindow.Brief*.cs`, `StudioIcons.cs`.

### S-14 Zu viele Reiter-Ebenen

Die Projektregel lautet höchstens zwei Ebenen. Im Bild: Online verkaufen hat drei Reiterzeilen übereinander. Die KI-Zentrale hat eine linke Leiste mit 13 Einträgen, darin Seiten mit eigenen Reitern, darin bei Abläufe noch „Einfach: Trendo-Abläufe / Profi: n8n“ – eine zweite Bedeutung von „Einfach / Profi“ neben der Bedienstufe. TV & Live zeigt neun Reiter, bevor ein Sender existiert. Kleine Helfer hat zehn Reiter und darunter dieselben zehn Kacheln.

- Bilder: `verkaufen_start.png`, `ablaeufe_start.png`, `tv_start.png`, `alltag-start_start.png`.
- Umbau: Eine Reiterzeile je Seite. Reiter erscheinen erst, wenn sie Inhalt haben (TV: zuerst nur „Senderliste hinzufügen“). Übersichtsseiten zeigen Kacheln statt Reiter. „Einfach / Profi“ bleibt allein der Bedienstufe vorbehalten, bei Abläufe heißt es „Trendo-Abläufe / n8n“.
- Aufwand: mittel.
- Dateien: `MainWindow.Verkaufen*.cs`, `MainWindow.Tv*.cs`, `MainWindow.Alltag*.cs`, `MainWindow.Workflows*.cs`, `MainWindow.KiZentrale*.cs`.

### S-15 Entwickler- und Systemtexte auf Nutzerseiten

In den Einstellungen stehen Seiten, die für die Entwicklung gedacht sind: „Fortschritt“ mit „W0 · Setup“ und `requirements.yaml`, „System & Sicherheit“ mit rohen Protokollzeilen, „Systemcheck“ mit sechs roten Zeilen „Fehlt nicht gefunden“ und vollen Ordnerpfaden, interne Kennungen wie „office-lesen, briefe, office“ im Geräteverbund und Seitenschlüssel in der Skill-Liste. Auf dem Desktop hat die Leiste unten elf Bildsymbole ohne Wort.

- Bilder: `fortschritt_start.png`, `system_start.png`, `systemcheck_start.png`, `verbund_start.png`, `ki-skills_start.png`, `desktop.png`.
- Umbau: „Fortschritt“ und das Rohprotokoll nur in „Professionell“. Systemcheck in „Einfach“ als ein Satz mit einem Knopf („6 Zusatzprogramme fehlen – jetzt einrichten“), Einzelheiten aufklappbar. Pfade gekürzt. Die Leiste unten bekommt Wörter unter den Symbolen.
- Aufwand: klein.
- Dateien: `MainWindow.Fortschritt*.cs`, `MainWindow.System*.cs`, `MainWindow.Systemcheck*.cs`, `MainWindow.Verbund*.cs`, `MainWindow.Dock*.cs`.

## 2. Wirken E1–E12 und V1–V10 im Bild?

| Nr. | Im Bild | Anmerkung |
|---|---|---|
| E1 ein KI-Knopf | ja | „KI ▾“ überall, kein doppeltes „Claude“ im Band |
| E2 Einklappen in „Einfach“ | ja, mit Nebenwirkung | versteckt in Videoschnitt, Audio und Bild den ersten Schritt (S-04); zwei Knöpfe „Mehr“ und „Mehr Werkzeuge“ nebeneinander |
| E3 automatisch speichern | nicht erkennbar | Anzeige lautet „nicht gespeichert“ und „Noch nicht gespeichert“, „Speichern“ ist überall golden (S-02, S-11) |
| E4 Dateiauswahl | ja, mit Nebenwirkung | Feld plus zwei Knöpfe, Text abgeschnitten, Fehler in der KI-Leiste (S-07) |
| E5 Weitermachen | nicht prüfbar | im leeren Testprofil gibt es kein letztes Dokument |
| E6 „Was möchtest du machen?“ | ja | auf allen angesehenen Startseiten |
| E7 Fehler mit „Was tun?“ | nicht geprüft | kein Fehler ausgelöst |
| E8 Einführung | nicht geprüft | erscheint im Render-Lauf nicht |
| E9 ein Stufenschalter | ja | `bedien-sehen_start.png` |
| E10 Alltagswörter | teilweise | linke Leiste gut, in den Programmen viele Fachwörter (S-10) |
| E11 ein Suchfeld | ja | Studio-Übersicht |
| E12 Desktop entrümpelt | ja | Desktop jetzt fast leer; die Studio-Übersicht zeigt rechts weiter vier Info-Kästen |
| V1 Webseite bauen | teilweise | geführter Satz da, Seite bleibt lang, drei KI-Eingaben |
| V2 lange Listen | ja, mit Nebenwirkung | versteckt Hauptinhalt (S-05) |
| V3 Wort am Knopf | ja, mit Nebenwirkung | vier Schäden (S-03) |
| V4 ein KI-Weg | ja | |
| V5 Texte & Tabellen | teilweise | drei Reiter, aber weiter rund 30 Knöpfe in drei Zeilen |
| V6 Musik | ja | „Nur beschreiben / Mit eigenem Songtext“ |
| V7 Alltagswörter an Knöpfen | teilweise | „ICS“, „CSV“, „.vcf“ stehen weiter in „Einfach“ |
| V8 weniger Text | ja, mit Nebenwirkung | kürzt Begrüßung und Einleitungen |
| V9 Studio-Übersicht | ja | Bereichs-Kacheln; Unterzeilen abgeschnitten |
| V10 Grenze als Test | nicht prüfbar | KI-Trainieren zeigt sichtbar über 50 Elemente |

## 3. Die 15 Programme mit dem größten Bedarf

1. **Trendo Text** (`text_start.png`): rund 42 Elemente, drei Leistenzeilen, „Fett“ steht getrennt von „Kursiv“ und „Unterstrichen“, die Seite zeigt nur einen leeren Rahmen.
   Vorschlag: eine Zeile mit Schrift, Größe, Fett/Kursiv/Unterstrichen, Liste, Ausrichtung; alles andere unter „Mehr“. Auf der leeren Seite steht „Hier schreiben …“ mit blinkender Marke.
2. **Videoschnitt** (`schnitt_einfach.png`): Leerzustand zeigt nach rechts ins Leere, Abspiel-Leiste überlappt, Tastenkürzel in Knöpfen.
   Vorschlag: großer Knopf „Videos hinzufügen“ in der Vorschau, Medienliste in „Einfach“ sichtbar, Abspiel-Leiste mit drei Knöpfen (Zurück, Abspielen, Vor).
3. **Bildbearbeitung** (`bild_einfach.png`): kein sichtbares „Foto öffnen“, Werkzeugleiste abgeschnitten.
   Vorschlag: Leerzustand mit „Foto öffnen“ und „Leere Fläche“, Palette links mit acht Werkzeugen samt Wort, Rest unter „Mehr Werkzeuge“.
4. **Audio bearbeiten** (`ton_einfach.png`): falscher Fenstertitel, vier goldene Knöpfe, „5.1“, „Abmischen“, „Marke an den“ (abgeschnitten).
   Vorschlag: zwei große Knöpfe „Aufnehmen“ und „Tondatei öffnen“, danach Wellenform mit Abspielen; Effekte als Liste „Verbessern“.
5. **3D-Modelle erstellen** (`3d_start.png`, `3d_formen.png`): rund 48 Elemente, KI-Leiste an anderer Stelle, „Anfasser“, „Abwicklung“, Teilenamen abgeschnitten.
   Vorschlag: links Formen als Palette, rechts nur Eigenschaften des gewählten Teils, Arbeitsschritte als eine Zeile oben; Drucken und Modellsuche unter „Mehr“.
6. **3D-Druck** (`3d-druck_start.png`): Drucker-Einrichtung mit Adresse, Seriennummer und Zugangscode steht dauerhaft links, zwei Knöpfe abgeschnitten.
   Vorschlag: drei Schritte „Modell wählen – Drucker wählen – Drucken“; die Einrichtung öffnet sich nur, wenn kein Drucker bekannt ist.
7. **Geld & Rechnungen** (`finanzen_start.png`): kein sichtbarer Knopf zum Eintragen, Kennzahlen doppelt, bunte Kacheln.
   Vorschlag: oben eine Eingabezeile „Betrag – Wofür – Einnahme/Ausgabe – Eintragen“, darunter einmal die drei Kennzahlen, Diagramm und Budgets darunter.
8. **E-Mails & Termine** (`kontakte_start.png`, `post_start.png`, `posteingang_start.png`, `kalender_start.png`): Reiter „E-Mail & Kontakte“ meint „E-Mail schreiben“, Adressbuch zeigt Etikettengröße und Sicherungsknöpfe vor dem ersten Kontakt, Kalender ohne Linien.
   Vorschlag: Reiter „Posteingang · Schreiben · Kalender · Adressbuch · Aufgaben“; im Adressbuch nur „+ Neuer Kontakt“ und Suche, Einlesen/Sichern/Etiketten unter „Mehr“; Kalender mit Tageslinien und „Nächster Monat“.
9. **TV & Live** (`tv_start.png`): Fachwörter im Begrüßungssatz, neun Reiter ohne Inhalt, zwei Titelzeilen.
   Vorschlag: Start mit drei Kacheln „Mediatheken ansehen“, „Eigene Senderliste hinzufügen“, „Video-Plattformen“; Reiter erscheinen mit dem ersten Sender.
10. **Logos & Marken** (`logo-programm_einfach.png`): Name und Leitspruch lassen sich in „Einfach“ nicht sichtbar ändern.
    Vorschlag: links schmale Spalte „Name, Leitspruch, Form, Farbe“, rechts die Vorschau; Klick auf den Text im Logo öffnet das Feld.
11. **Comics & Storyboards** (`comic-programm_einfach.png`): kein sichtbarer Weg zu Text oder Bild, Seite klein in großer Fläche.
    Vorschlag: Seite füllt die Höhe; Klick in ein Bild zeigt daneben „Sprechblase“, „Erzähltext“, „Bild wählen“.
12. **Podcast aufnehmen** (`podcast-programm_einfach.png`): Lautheitswerte, Kapitel als Textformat, drei Startknöpfe.
    Vorschlag: großer runder Aufnahmeknopf mit Pegel, darunter „Kapitel hinzufügen“ als Liste; „Lautheit angleichen“ heißt „Lautstärke ausgleichen“.
13. **Webseite bauen** (`dev_start.png`): drei KI-Eingaben, lange Seite, Stilwahl ohne Rahmen, „Vite“.
    Vorschlag: vier nummerierte Schritte als Karten, immer nur der aktuelle Schritt offen; eine einzige Beschreibung.
14. **KI-Zentrale: Online-Dienste, Verbindungen, KI-Bibliothek, KI-Quellen** (`keys_start.png`, `verbindungen_start.png`, `ki-bibliothek_start.png`, `ki-quellen_start.png`): Wände aus Schlüsselfeldern, „MCP-Server“, leere Modell-Liste, sechs Häkchen zu Claude am Seitenanfang.
    Vorschlag: je Dienst eine Zeile „Name – Zustand – Verbinden“, das Schlüsselfeld erscheint erst nach „Verbinden“; Erweiterungen von Claude Code nur in „Professionell“.
15. **Dateien** (`dateien_start.png`): Leisten kleben zusammen, „Im Dateimanager des“ abgeschnitten, unten scheint die Desktop-Leiste durch.
    Vorschlag: Leiste mit Abständen, ausgegraute Knöpfe ohne Wort, deckender Hintergrund.

Knapp dahinter: Live streamen (roter Knopf „Stopp“ als größter Knopf, obwohl nichts läuft), Trendo Musik (überlappende Abspiel-Leiste, Takt/Tempo/Tonart), Großprojekte, Print on Demand (Anbieter-Schlüssel neben der Zeichenfläche).

## 4. Übersicht je Seite

Spalten: Seite (Schlüssel) · Verständlichkeit · sichtbare Bedienelemente in „Einfach“ · größtes Problem · Bild.

### Arbeiten & Büro

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Bewerbung & Lebenslauf (bewerbung-programm) | mittel | 36 | Kopf nimmt die halbe Höhe; „ATS-Fassung“ | bewerbung-programm_einfach.png |
| Briefe schreiben (brief) | mittel | 30 | eigener Aufbau, Speichern rechts; gewählte Kachel unlesbar | brief_start.png |
| Dokumente scannen (scannen-programm) | gut | 22 | Leiste rechts abgeschnitten („Whiteboard“) | scannen-programm_einfach.png |
| Adressbuch (kontakte) | schwach | 24 | Etiketten, Sichern, vCard vor dem ersten Kontakt; Spalten enden früh | kontakte_start.png |
| Aufgaben (aufgaben) | gut | 17 | doppelter Leerzustand | aufgaben_start.png |
| E-Mail & Kontakte (post) | mittel | 27 | Reitername meint „Schreiben“; Kontaktformular auf der Mail-Seite | post_start.png |
| Kalender (kalender) | mittel | 27 | „Abspielen“ statt „Weiter“; keine Tageslinien; „ICS einlesen“ | kalender_start.png |
| Posteingang (posteingang) | mittel | 26 | Konto, Ordner, Regeln alles gleichzeitig | posteingang_start.png |
| Formulare ausfüllen (formulare-programm) | gut | 27 | Leiste rechts abgeschnitten; „Abspielen“ beim Blättern | formulare-programm_einfach.png |
| Karten & Einladungen (karten-programm) | mittel | 21 | Karte unten abgeschnitten; Schrift ändern nicht sichtbar | karten-programm_einfach.png |
| Medien (medien) | mittel | 16 | vier Elemente für einen Ordner; kein Titelkopf | medien_start.png |
| Kleine Helfer, Übersicht (alltag-start) | gut | 27 | zehn Reiter und dieselben zehn Kacheln | alltag-start_start.png |
| Werkzeuge (werkzeuge) | gut | 30 | untere Hälfte leer | werkzeuge_start.png |
| Neues Projekt (vorhaben) | gut | 17 | linke Spalte fast leer | vorhaben_start.png |
| PDF-Werkstatt (pdf-werkstatt-programm) | mittel | 28 | zwölf Werkzeuge rechts abgeschnitten; Hauptaktion doppelt | pdf-werkstatt-programm_einfach.png |
| Präsentationen gestalten (praesentation-gestalten-programm) | mittel | 25 | Gliederung mit Sonderzeichen als Bedienung | praesentation-gestalten-programm_einfach.png |
| Tagesplaner (tagesplaner-programm) | gut | 30 | lange „✕ … entfernen“-Knöpfe; „Tag planen“ doppelt | tagesplaner-programm_einfach.png |
| Texte & Tabellen, Start (doku-start) | mittel | 16 | „Vorlagen“ leer, Inhalt hinter „Mehr anzeigen“ | doku-start_start.png |
| Trendo PDF (pdf) | gut | 14 | „PDF öffnen“ zweimal | pdf_start.png |
| Trendo Präsentation (praesentation) | gut | 14 | Hintergrund blau statt grau | praesentation_start.png |
| Trendo Tabelle (tabelle) | gut | 12 | bunte Symbole weichen ab | tabelle_start.png |
| Trendo Text (text) | schwach | 42 | drei Leistenzeilen; leere Seite ohne Schreibhinweis | text_start.png |
| Verträge-Ordner (vertraege-programm) | gut | 22 | KI-Eingabe ist zum Ordnerfeld geworden (Fehler) | vertraege-programm_einfach.png |

### Business & Geld

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Geld (finanzen) | schwach | 20 | kein sichtbares Eintragen; Kennzahlen doppelt; bunte Kacheln | finanzen_start.png |
| Verkaufen (verkaufen) | mittel | 19 | drei Reiterzeilen | verkaufen_start.png |
| Print on Demand (pod-programm) | mittel | 33 | Anbieter-Schlüssel neben der Zeichenfläche | pod-programm_einfach.png |

### Kreativ & Medien

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Animation & Motion (animation-programm) | mittel | 17 | Punkte auf der Zeitleiste unerklärt | animation-programm_einfach.png |
| Bücher schreiben (buch-programm) | mittel | 18 | „#“ und „**“ im Text sichtbar | buch-programm_einfach.png |
| Comics & Storyboards (comic-programm) | schwach | 16 | Text und Bild eingeben nicht sichtbar | comic-programm_einfach.png |
| Filme machen (film-programm) | gut | 19 | – (Name verspricht mehr, siehe Bestandsaudit) | film-programm_einfach.png |
| Fotos verwalten (fotos-verwalten-programm) | gut | 19 | Sortierart im Band statt bei Schritt 2 | fotos-verwalten-programm_einfach.png |
| Freie Bilder & Klänge (assets) | gut | 28 | fünf Filter vor der ersten Suche | assets_start.png |
| Kleidung & Produkte (mode-programm) | mittel | 38 | sehr viel auf einmal; „#RRGGBB“ | mode-programm_einfach.png |
| Künstlerstudio (kuenstler-programm) | gut | 39 | – | kuenstler-programm_einfach.png |
| Live streamen (sendung) | schwach | 24 | roter „Stopp“ als größter Knopf; „On-Air-Uhr“, „720p30“ | sendung_start.png |
| Logos & Marken (logo-programm) | schwach | 16 | Name und Leitspruch nicht änderbar sichtbar | logo-programm_einfach.png |
| Podcast aufnehmen (podcast-programm) | schwach | 17 | Lautheitswerte; Kapitel als Textformat | podcast-programm_einfach.png |
| Trendo Musik (musik-studio) | mittel | 38 | Abspiel-Leiste überlappt; Takt, Tempo, Tonart | musik-studio_start.png |
| TV & Live (tv) | schwach | 20 | „M3U“, „Xtream“; neun leere Reiter | tv_start.png |
| Audio bearbeiten (ton) | schwach | 22 | Leerzustand zeigt ins Leere; vier goldene Knöpfe | ton_einfach.png |
| Bildbearbeitung (bild) | schwach | 28 | kein „Foto öffnen“; Leiste abgeschnitten | bild_einfach.png |
| Medien bearbeiten (kreativ-medien) | mittel | 19 | Dateiauswahl mit drei Elementen | kreativ-medien_start.png |
| Videoschnitt (schnitt) | schwach | 26 | Leerzustand zeigt ins Leere; Abspiel-Leiste überlappt | schnitt_einfach.png |

### 3D, Spiele & VR

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| 3D (3d) | schwach | 48 | meiste Elemente; KI-Leiste an anderer Stelle | 3d_start.png |
| Trendo 3D-Druck (3d-druck) | schwach | 14 | Drucker-Einrichtung dauerhaft links; Knöpfe abgeschnitten | 3d-druck_start.png |
| Trendo CAD (cad) | mittel | 37 | eigene dritte Leistenform | cad_start.png |
| Aus Fotos (scan3d) | mittel | 17 | „COLMAP / Meshroom“, „4K (4096 px)“ | scan3d_start.png |
| Punktwolke (punktwolke) | gut | 16 | „Störpunkte“ | punktwolke_einfach.png |
| Konsolenspiele portieren (konsole-programm) | gut | 24 | Name passt nicht zu „Was möchtest du spielen?“ | konsole-programm_einfach.png |
| Spiele bauen (spiele-programm) | gut | 19 | einzelnes „Ja“ ohne Knopfform | spiele-programm_einfach.png |
| Werkstatt (spiele) | gut | 15 | – | spiele_start.png |

### Technik & Fachgebiete

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| CNC-Werkstatt (cnc) | mittel | 17 | G-Code-Liste füllt ein Viertel | cnc_einfach.png |
| Geräte überwachen (zwilling) | mittel | 20 | Aktionen und Zeitraum in einer Zeile; „MQTT“ | zwilling_start.png |
| Karten & Touren (karten) | gut | 18 | – | karten_einfach.png |
| Schaltung & Simulation (elektronik) | gut | 27 | „Widerst.“, „Kondens“ abgekürzt | elektronik_einfach.png |
| Tragwerk & Mechanik (technik) | gut | 18 | Warnung nur in der Statuszeile | technik_einfach.png |
| Grundriss & Wohnen (architektur) | gut | 26 | – | architektur_einfach.png |

### Wissen & Lernen

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Lernen mit Karteikarten (lernen) | gut | 15 | – | lernen_start.png |
| Messdaten & Auswertung (wissenschaft) | gut | 17 | – | wissenschaft_einfach.png |
| Recherche mit Quellen (quellen-programm) | mittel | 30 | Art der Quelle doppelt (Band und Formular) | quellen-programm_einfach.png |
| Recherche im Lesemodus (recherche) | mittel | 20 | „.onion“, „Tor“ im Einleitungssatz | recherche_start.png |
| Wissen für die KI (ki-wissen) | mittel | 19 | linke Spalte mit sieben Knöpfen; „[[…]]“ | ki-wissen_start.png |

### KI & Automatik

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Abläufe (ablaeufe) | mittel | 27 | zweite Bedeutung von „Einfach / Profi“ | ablaeufe_start.png |
| Bilder erkennen (bilder-erkennen-programm) | mittel | 21 | „Bild öffnen“ doppelt; Farbcodes | bilder-erkennen-programm_einfach.png |
| Bilder mit KI (bild-ki-programm) | mittel | 18 | „nicht eingerichtet“ nur in der Statuszeile | bild-ki-programm_einfach.png |
| Code mit KI (code-ki-programm) | mittel | 21 | Kopf nimmt die halbe Höhe | code-ki-programm_einfach.png |
| Eigene KI trainieren (ki-trainieren-programm) | mittel | 55 | 30-mal „✕ Beispiel entfernen“ | ki-trainieren-programm_einfach.png |
| Produktion (produktion) | schwach | 24 | kleines Formular, leerer Kasten | produktion_start.png |
| KI, lokal (ki) | schwach | 26 | zwei Eingaben; „nicht verbunden“ ohne klaren Knopf | ki_start.png |
| KI fragen (ki-fragen) | mittel | 30 | zwei Eingaben auf einer Seite | ki-fragen_start.png |
| Mit Claude verbinden (claude-verbinden) | mittel | 22 | Fenster heißt „KI fragen“ | claude-verbinden_start.png |
| Online-Dienste (keys) | schwach | 34 | Wand aus Schlüsselfeldern | keys_start.png |
| Verbindungen (verbindungen) | schwach | 30 | „Plugins“, „Marktplätze“, „MCP-Server“ | verbindungen_start.png |
| KI-Agenten bauen (agenten-bauen-programm) | mittel | 28 | Formular plus Rohdatei | agenten-bauen-programm_einfach.png |
| KI-Vorlagen (ki-vorlagen) | mittel | 26 | „{{…}}“ in den Karten | ki-vorlagen_start.png |
| Skills (ki-skills) | mittel | 30 | interne Seitenschlüssel in der Liste | ki-skills_start.png |
| KI-Modelle vergleichen (modelle-vergleichen-programm) | gut | 19 | „Frage stellen“ doppelt | modelle-vergleichen-programm_einfach.png |
| KI-Rollen und Teams (ki-rollen) | mittel | 34 | drei Wege zu starten | ki-rollen_start.png |
| KI-Bibliothek (ki-bibliothek) | schwach | 30 | Modell-Liste hinter „Mehr anzeigen“ | ki-bibliothek_start.png |
| KI-Quellen (ki-quellen) | schwach | 36 | sechs Häkchen zu Claude am Anfang | ki-quellen_start.png |
| Kostenlose Internet-KIs (ki-internet) | mittel | 30 | vier Blöcke, Einrichtung verlangt Schlüssel | ki-internet_start.png |
| Texte zusammenfassen (zusammenfassen-programm) | gut | 17 | Kopf nimmt die halbe Höhe | zusammenfassen-programm_einfach.png |
| Videos mit KI (video-ki-programm) | gut | 18 | „Szenen planen“ doppelt | video-ki-programm_einfach.png |

### Entwickeln

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Baukasten (baukasten) | gut | 24 | zwei goldene Knöpfe | baukasten_start.png |
| Sauberer Nachbau (nachbau) | mittel | 11 | Fläche zu zwei Dritteln leer | nachbau_start.png |
| Open Source (oss) | gut | 21 | – | oss_start.png |
| Erweiterungen (erweiterungen) | mittel | 13 | Dateiauswahl mit vier Elementen | erweiterungen_start.png |
| Webseite bauen (dev) | schwach | 30 | drei KI-Eingaben; lange Seite | dev_start.png |

### System & Sicherheit

| Seite | Urteil | Elemente | Größtes Problem | Bild |
|---|---|---|---|---|
| Desktop / Startbildschirm (start) | mittel | 29 | elf Symbole ohne Wort; sehr leer | desktop.png |
| Alle Studios (studios) | mittel | 30 | Unterzeilen abgeschnitten; vier Info-Kästen rechts | studios_start.png |
| Dateien (dateien) | schwach | 26 | Leisten kleben zusammen | dateien_start.png |
| Desktop-Symbole (desktop-symbole) | mittel | 12 | kein Rand, kein Titelkopf | desktop-symbole_start.png |
| Ersteinrichtung (einrichtung) | gut | 16 | Begrüßungssatz gekürzt | einrichtung_start.png |
| Projekte (projects) | mittel | 9 | rechter Kasten leer | projects_start.png |
| Einstellungen (settings) | gut | 26 | Zeile „Gehört hierher:“ mit sieben Knöpfen | settings_start.png |
| Konto, Lizenz & Vollversion (lizenz) | mittel | 19 | Titelkarte sagt „Schrift & Farben anpassen“ | lizenz_start.png |
| Personen (personen) | gut | 18 | – | personen_start.png |
| Sehen (bedien-sehen) | gut | 30 | – | bedien-sehen_start.png |
| Trendo-Updates (updates) | gut | 20 | Einleitung gekürzt | updates_start.png |
| Betriebsmodus (modus) | schwach | 16 | „winget“, „Get-PnpDevice“, „GPIO“ | modus_start.png |
| Downloads (downloads) | mittel | 30 | drei Themen auf einer Seite | downloads_start.png |
| Sicher prüfen (netz-pruefen) | mittel | 28 | vier Formulare untereinander | netz-pruefen_start.png |
| Sicherheits-Werkzeuge & Beweise (forensik) | schwach | 22 | Portnummern, „yara-x“ | forensik_start.png |
| Ordner im Heimnetz teilen (teilen) | gut | 24 | Netzadresse in der Auswahl | teilen_start.png |
| Freigaben (approvals) | gut | 14 | leerer Zustand ohne Erklärung | approvals_start.png |
| Schutz & Datenschutz (schutz) | mittel | 22 | „restic“, voller Pfad | schutz_start.png |
| Sicherung (sicherung) | mittel | 21 | voller Pfad im Einleitungssatz | sicherung_start.png |
| Selbstheilung (heilung) | gut | 15 | interne Ordnernamen | heilung_start.png |
| Geräte & Laufwerke (geraete) | gut | 15 | – | geraete_start.png |
| Handy verbinden (handy) | mittel | 28 | Profi-Teil unter dem einfachen Weg sichtbar | handy_start.png |
| Speicher & Leistung (ressourcen) | gut | 18 | – | ressourcen_start.png |
| Geräteverbund (verbund) | schwach | 20 | Befehlszeilen und interne Kennungen | verbund_start.png |
| Programme verwalten (appcenter) | gut | 27 | „winget“ im Einleitungssatz | appcenter_start.png |
| Apps (apps) | mittel | 23 | acht Zeilen „fehlt“ in Orange | apps_start.png |
| Hinzufügen (hinzufuegen) | mittel | 34 | sieben Filter, lange Liste | hinzufuegen_start.png |
| Pflegen (prog-pflege) | schwach | 26 | Prüfsummen-Felder, Dateiauswahl dreifach | prog-pflege_start.png |
| Zusatzprogramme (components) | mittel | 19 | zwei ähnliche goldene Knöpfe; Inhalt eingeklappt | components_start.png |
| Fortschritt (fortschritt) | schwach | 23 | Entwicklerseite | fortschritt_start.png |
| Hardware & Leistung (hardware) | mittel | 19 | Titelkarte sagt „Qualität prüfen“ | hardware_start.png |
| Leistung (qual-leistung) | mittel | 18 | „Kette prüfen“ | qual-leistung_start.png |
| System & Sicherheit (system) | schwach | 19 | rohe Protokollzeilen | system_start.png |
| Systemcheck (systemcheck) | mittel | 20 | sechs rote Zeilen, volle Pfade | systemcheck_start.png |

## 5. Vorschlagsliste für den Nutzer (Ja/Nein)

Die Nummern V-01 bis V-22 sind neu und nicht dieselben wie V1–V10 aus T-0663. Keine Funktion entfällt: Alles bleibt über „Mehr“, „Mehr Werkzeuge“ oder die Stufen „Erweitert“ und „Professionell“ erreichbar. Dunkel-Gold-Look, Deutsch und das Aussehen nach dem Profi-Vorbild bleiben. Sortiert nach Wirkung.

| Nr. | Vorschlag | Nutzen in einem Satz | Aufwand | Befund |
|---|---|---|---|---|
| V-01 | Schmaler Kopf in allen Programmen: eine Zeile mit Name und Speicherstand, darunter sofort die Werkzeuge. | Die Arbeitsfläche wird in 21 Programmen fast doppelt so hoch. | mittel | S-01 |
| V-02 | Leere Arbeitsfläche zeigt immer einen großen Knopf für den ersten Schritt („Foto öffnen“, „Video hinzufügen“). | Du siehst sofort, wie du anfängst, ohne zu suchen. | mittel | S-04 |
| V-03 | Genau ein goldener Knopf je Ansicht, kein Blau mehr, jede Hauptaktion nur einmal. | Der nächste Schritt ist auf einen Blick zu erkennen. | mittel | S-02 |
| V-04 | Werkzeugleisten reparieren: nichts abgeschnitten, Abstand zwischen Knöpfen, Werkzeuge links mit Symbol und Wort. | Jedes Werkzeug ist lesbar und trifft sich leichter. | mittel | S-03 |
| V-05 | Falsche Wörter berichtigen: „Weiter“ und „Nächster Monat“ statt „Abspielen“, kein „Kopieren“ unter Hinweisen. | Knöpfe sagen, was sie wirklich tun. | klein | S-03 |
| V-06 | „Mehr anzeigen“ versteckt nie den Hauptinhalt und nennt, was kommt („Alle 19 Vorlagen zeigen“). | Seiten wirken nicht mehr leer, obwohl etwas da ist. | klein | S-05 |
| V-07 | Dateiauswahl mit einem Knopf statt Feld plus zwei Knöpfen. | Eine Datei zu wählen ist ein Klick statt drei. | klein–mittel | S-07 |
| V-08 | Speicherstand einmal und verständlich: „Wird automatisch gespeichert“ / „Gespeichert“. | Du musst dich nicht fragen, ob deine Arbeit sicher ist. | klein | S-11 |
| V-09 | Was den nächsten Schritt verhindert, steht direkt am Knopf mit „Jetzt einrichten“. | Du klickst nicht ins Leere, wenn etwas fehlt. | klein–mittel | S-11 |
| V-10 | Fenstertitel ist immer der Name der Seite, die du siehst. | Du weißt jederzeit, wo du bist. | klein | S-08 |
| V-11 | Fachwörter in „Einfach“ durch Alltagswörter ersetzen, Fachwort im Hinweis (rund 40 Stellen). | Du verstehst jeden Knopf ohne Vorwissen. | mittel | S-10 |
| V-12 | Schreibzeichen durch Knöpfe ersetzen (Überschrift, Fett, Kapitel, Sprechblase). | Du musst keine Zeichen wie „#“ oder „**“ lernen. | mittel | S-10 |
| V-13 | Auf KI-Seiten nur eine Eingabe; „Gehört hierher:“ wird „Verwandte Programme“ im Menü „Mehr“. | Es ist klar, wo du deine Frage eintippst. | klein | S-09 |
| V-14 | Speichern, Rückgängig, Exportieren, KI und Hilfe in jedem Programm am selben Platz. | Was du in einem Programm gelernt hast, gilt in allen. | groß | S-06 |
| V-15 | Höchstens eine Reiterzeile je Seite; Reiter erscheinen erst, wenn sie Inhalt haben. | Weniger Auswahl auf einmal, weniger Verirren. | mittel | S-14 |
| V-16 | Spalten füllen das Fenster, leere Kästen verschwinden, ein Leerzustand je Seite. | Seiten wirken fertig und ruhig statt halb leer. | klein | S-12 |
| V-17 | Eine Flächenfarbe überall, Kennzahlen dunkel mit Gold statt bunter Kacheln, lesbare gewählte Kacheln. | Alles sieht nach einem System aus. | klein–mittel | S-13 |
| V-18 | Entwicklerseiten und Rohprotokolle nur in „Professionell“; Systemcheck als ein Satz mit einem Knopf. | Die Einstellungen erschrecken nicht mehr mit roten Listen. | klein | S-15 |
| V-19 | Wörter unter den Symbolen der Leiste am unteren Rand des Desktops. | Du erkennst jedes Programm, bevor du klickst. | klein | S-15 |
| V-20 | Trendo Text: eine Werkzeugzeile und „Hier schreiben …“ auf der leeren Seite. | Schreiben beginnt ohne Suchen. | mittel | Programm 1 |
| V-21 | E-Mails & Termine: Reiter neu benennen, Adressbuch entrümpeln, Kalender mit Linien. | Post, Kalender und Adressen sind auf Anhieb zu finden. | mittel | Programm 8 |
| V-22 | Videoschnitt, Bild, Audio, 3D, 3D-Druck, Geld, TV, Logos, Comics, Podcast, Webseite bauen, KI-Dienste, Dateien einzeln nach Abschnitt 3 umbauen. | Die 13 schwächsten Programme werden so klar wie die besten. | groß (je Programm klein bis mittel) | Abschnitt 3 |

Empfohlene Reihenfolge: V-05, V-06, V-08, V-10 (klein, sofort sichtbar), dann V-01 bis V-04 (größte Wirkung), dann der Rest.

## 6. Nicht geprüft

- Die entfernten Studios VR & AR (`xr`), Auto & Fahrten (`fahrzeug`), Kameras & Roboter (`roboter`): nicht gerendert.
- Rund 33 Bilder sind erzeugt, aber nicht einzeln angesehen: vor allem die Startseiten der Hüllen-Programme (gleicher Aufbau wie die sechs angesehenen), dazu `ton_start.png` und `schnitt_start.png`.
- Stufe „Erweitert“: kein Bild.
- „Professionell“ nur bei 25 Programmen und überwiegend nur der Kopfbereich (obere 58 % des Bildes).
- Trendo Text, Tabelle und Präsentation mit geöffnetem Dokument in „Professionell“; Briefe ab Schritt 2.
- Alles, was erst nach einem Klick erscheint: Menüs „Mehr“, „KI ▾“, „?“, Einführung (E8), Fehlermeldungen (E7), Dialoge, Rechtsklick-Menüs, Hinweise beim Überfahren.
- Bereiche unterhalb des sichtbaren Ausschnitts (kein Scrollen).
- Gefüllte Zustände der Seiten außerhalb der Hülle (Adressbuch mit Kontakten, Posteingang mit Mails, Geld mit Buchungen, Musik mit Liedern, TV mit Sendern).
- Andere Fenstergrößen (zum Beispiel 1366 × 768 oder 4K), heller Stil, Stile „Windows“, „macOS“, „GNOME“, große Schrift.
- Bedienung selbst: Tastatur, Ziehen, Reihenfolge beim Tabulator, Kontrast als Messwert.
- Die Zahlen der Bedienelemente sind von Hand aus dem Bild gezählt, nicht gemessen.
- Das Render-Werkzeug läuft im Testmodus. Texte wie „Testmodus: claude.ai wird nicht geladen“ oder fehlende Zusatzprogramme stammen daher und sind nicht bewertet.

## 7. Hinweise zum Lauf

- Das Render-Werkzeug löscht bei jedem Start seinen Datenordner. Ohne `TRENDO_RENDER_ROOT` ist das `%TEMP%\trendo-render`. Der erste Probelauf lief so wie im Auftrag angegeben, alle weiteren 187 mit eigenem Ordner im Scratch-Bereich. Die eigenen Datenordner sind wieder entfernt.
- `downloads_start.png` zeigt Dateinamen aus dem echten Download-Ordner dieses PCs. Das Bild sollte nicht weitergegeben werden.
- In mehreren Bildern scheint am oberen rechten Rand eine Uhrzeit und am unteren Rand die Desktop-Leiste durch (`dateien_start.png`, `desktop-symbole_start.png`, Seiten mit „Gehört hierher:“). Ob das in der echten App auch so ist, ist nicht geprüft.
- Die Dateinamen in den Spalten „Dateien“ der Befunde sind teils Muster (`MainWindow.Tv*.cs`). Sicher belegt sind `MainWindow.ProgramShell.cs`, `MainWindow.SimpleMode.cs`, `SimpleWords.cs`, `GlyphIcons.cs`, `MainWindow.Files.cs`, `PathFields.cs`, `MainWindow.StudioFolds.cs` und `MainWindow.StartMenu.cs`.
