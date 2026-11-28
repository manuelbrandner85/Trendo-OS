# Referate & Hausarbeiten – Testplan

Studio: Referate & Hausarbeiten (STUDIO-REFERAT-01) · Bereich: Wissen & Lernen · Seite: `referate-programm` · Stand: 0.9.114 (Stand 99cbb5c)

17 Tests sprechen dieses Programm an.

- T-0780 (3. Gegenprüfung): Plan im Chat läuft durch denselben Wächter – [[trendo:klick]] auf „Buch“ und [[trendo:feld]] mit erfundenem Beleg werden zurückgerollt, ein erlaubter Plan wird übernommen
- T-0780 (4. Gegenprüfung): Claude schreibt die Studio-.tdoc im Projektordner – neue Quelle oder erfundener Beleg wird nicht übernommen, die Datei steht wieder auf dem alten Stand, Claude bekommt die Meldung, Protokolleintrag; erlaubte Änderung wird übernommen
- T-0780 (4. Gegenprüfung): Hilfe des Studios verspricht nicht mehr, als die Prüfung hält – nur echte Quellen im Verzeichnis, eindeutige Formen gesperrt, lose Klammern nur markiert
- T-0780 ECHT: lokale KI (Ollama) – Referate „Gliederung“ und „Abschnitt“, Zeitleisten „Ereignisse aus Text“ im offenen Programm, Rückgängig (nur mit TRENDO_T0780_ECHT)
- T-0780 H1 (2. Gegenprüfung): Claude-Brücke – „befehl“, „klicken“ und „feld_setzen“ kommen an Quellen-Sperre, Zitatprüfung und Ordner-Sperre nicht vorbei; Schritt wird zurückgerollt und Claude bekommt den Fehler
- T-0780 H1/H2: Claude-Brücke und Assistent („dokument.setzen“) prüfen Zitate wie die Fachaktion – freier Kurzbeleg und unbekannter Verweis abgelehnt, Verweis auf eigene Quelle übernommen
- T-0780 H3: Claude-Brücke hält KiLocked ein – „sources“ (Referate) und „folder“ (Dokumente scannen) setzt Claude nicht, freie Felder weiter schon
- T-0780 Referate (UI): Abschnitt mit der Maus in der Gliederung verschieben, Rechtsklick-Menü, Rückgängig; Verweis ohne Quelle sperrt die Ausgabe sichtbar
- T-0780 Referate (UI): Quellen aus „Recherche mit Quellen“ übernehmen (offenes Dokument) und RIS aus Zotero einlesen – Kennungen bleiben, Doppelte nicht doppelt
- T-0780 Referate (UI): Stufe Einfach – eine goldene Hauptaktion, Kette und Gliederung sichtbar, Thema auch ohne rechte Spalte eintragbar
- T-0780 Referate (UI): leere Vorlage → Thema → Gliederung → Text → Quelle → Zitat per Klick → Word, PDF, ODT, PPTX, ODP als echte Dateien
- T-0780 Referate KI: „Abschnitt schreiben“ mit Zitat aus der Liste kommt ins Dokument; erfundene Quelle ([@neu] oder „(Müller, 2019)“) und neue Quellen per „dokument.setzen“ werden abgelehnt (KI ersetzt)
- T-0780 Referate KI: „Gliederung“ – Vorschau ändert nichts, „Ja, behalten“ schreibt die Gliederung ins offene Dokument als EIN Rückgängig-Schritt, Rückgängig stellt alles her (KI ersetzt)
- T-0780 Zeitleisten (UI): Eintrag mit der Maus in der Zeit verschieben, Mausrad zoomt, Ziehen auf freier Fläche verschiebt die Ansicht, Rechtsklick-Menü, Entf löscht, Rückgängig
- T-0780 Zeitleisten (UI): TimelineJS-Tabelle einlesen, Bild an ein Ereignis hängen (verkleinert, in SVG eingebettet), Zeilen aus Text einfügen
- T-0780 Zeitleisten (UI): leere Vorlage → erstes Ereignis (44 v. Chr.) → Zeitraum → Spur → SVG, PNG, PDF, CSV, JSON als echte Dateien
- T-0780 Zeitleisten KI: „Ereignisse aus Text einfügen“ – Vorschau ändert nichts, „Ja, behalten“ fügt die Ereignisse als EIN Rückgängig-Schritt ein; unlesbare Daten werden abgelehnt (KI ersetzt)
