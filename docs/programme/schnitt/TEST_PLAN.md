# Videoschnitt – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `schnitt` · Stand: 0.9.36 (Stand 434d2ab)

39 Tests sprechen dieses Programm an.

- A2/A4: Texte & Tabellen mit Neu-Karten (Dokument, Tabelle, Präsentation, PDF, Brief, Word-Vorlagen) und Leerzustand; Neues Projekt mit Ziel-Karten
- C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst
- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg
- G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“
- G3/G4: Vorschlags-Chips unter der Studio-KI-Zeile setzen den Auftrag ein (starten nichts) und verschwinden beim Tippen; Desktop „Weitermachen“ mit zuletzt benutzten Programmen
- G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus
- I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an
- K1: Musikspur im Videoschnitt zeigt die echte Wellenform der Tondatei (laut/leise erkennbar); ohne lesbare Datei nur Mittellinie statt erfundener Welle
- K3: Abspielleiste von Trendo Musik zeigt die echte Wellenform des gewählten Lieds; ohne Lied keine Welle
- S3-01/S3-03 Videoschnitt: Shotliste aus dem Drehbuch (CSV), Storyboard-Tafeln mit Shot-IDs in die Zeitleiste; nicht erzeugbare Tafel steht im Fehlprotokoll, der Rest wird übernommen
- S3-02 Multicam: zwei echte Kameradateien über den Ton synchronisieren (echtes FFmpeg), Wechsel 0:1, 3:2, 6:1 → drei Bild-Abschnitte mit richtigen Einstiegspunkten, Ton von Kamera 1 durchgehend; Unsinn bei den Wechseln wird gemeldet
- S3-02 Videoschnitt: Proxys erstellen (echtes FFmpeg) – Vorschau nutzt den Proxy, Export bleibt beim Original; ausschalten geht
- S3-09 Videoschnitt: Sprachaufnahme + Wortzeiten → Mundbilder (JSON, Moho), Prüfung OK, Vergleichsvorschau (echtes FFmpeg) in die Zeitleiste; Gesichtsvideo 150 ms versetzt → FAIL, nichts eingefügt; fehlende Aufnahme → FAIL
- S3-11 Videoschnitt: LUT setzen (beschädigte abgelehnt, Stück unverändert), Scopes mit Messwerten und Vergleich mit einem anderen Stück
- S3-12 Videoschnitt: Bild verbessern berechnet eine Fassung mit Messwerten; „verwenden“ tauscht die Datei, Rückgängig holt das Original; Hochskalieren mit Artefakten bietet kein Übernehmen an
- S3-13 Videoschnitt: KI-Clip mit Zufallswert in die Zeitleiste, Herkunftsnachweis daneben (rückgängig machbar); ohne Modell nur Hinweis; liefert die KI nichts, wird nichts eingefügt
- S3-17 Videoschnitt: Abspann aus „Rolle: Name“ als Clip ans Ende (rückgängig machbar); Schrift mit Einbett-Verbot wird vor der Freigabe abgewiesen
- S3-18 Videoschnitt: drei Fassungen in die Warteschlange (YouTube, Hochformat, ProRes) laufen nacheinander; entfernte Fassung läuft nicht; ein Fehler hält die Schlange nicht an
- S3-18/S3-20 Videoschnitt: „Film ausliefern“ legt einen Ordner mit Film, Stems, EDL, OTIO, Cue-Liste und Prüfbericht an; fehlendes Medium wird gemeldet statt verschleiert
- T4/T7: Geräte überwachen und Auto & Fahrten starten mit Leerzustand; Beispiel-Knopf darin füllt Dashboard bzw. Fahrzeug
- Videoschnitt Export X1/X11/X16: Vorlage Hochformat per Klick, Bereich und Lautheit gehen an den Export, Dateiname mit Vorlage, EDL und FCPXML werden geschrieben
- Videoschnitt UI: Medien auf die Spuren, Stück ziehen ordnet um, Kante ziehen kürzt, Lineal setzt Abspielkopf, S teilt, Strg+Z, Entf, Export, Einfach-Stufe
- Videoschnitt UI: falsche Dateien und leere Bildspur geben klare Meldungen
- Videoschnitt UI: Überblenden und Titel setzen (Länge kürzer, Titel in der Vorschau), Abspielen zeigt Bilder und Titel, Hintergrund-Export mit Fortschritt, Abbrechen
- Videoschnitt X10: Untertitel erzeugen (Whisper ersetzt), Anzahl und Beispiel sichtbar, Einbrennen geht an den Export, SRT liegt neben dem Video
- Videoschnitt X15: verschobene Datei wird gemeldet und per Ordnerwahl neu verknüpft (Rückgängig möglich)
- Videoschnitt X5: L spielt (zweimal = 2×), J rückwärts, K hält an, I/O setzen den Export-Bereich, M setzt eine Marke, Pos1/Ende springen
- Videoschnitt übersichtlich: Spurnamen, Abspielleiste unter der Vorschau, rechts Medien · Auswahl · Export – Auswahl öffnet sich beim Anklicken eines Stücks
- Videoschnitt „Meine Exporte“: fertiges Video bleibt in der Liste, Abspielen startet es, Ordner öffnen markiert die Datei im Explorer, Löschen in den Papierkorb
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
- W3: Wissensnetz – Seiten als Punkte, [[Verweise]] als Linien (ungerichtet, ohne Doppelte, fehlende Ziele nicht), Klick öffnet die Seite; leer mit Leerzustand
- Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“
- Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand
