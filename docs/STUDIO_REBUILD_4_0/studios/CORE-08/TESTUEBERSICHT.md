# Testübersicht: Videos & Fotos bearbeiten

Studio CORE-08 · Videos & Fotos bearbeiten · Bereich Kreativ & Medien

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 117. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- A2/A4: Texte & Tabellen mit Neu-Karten (Dokument, Tabelle, Präsentation, PDF, Brief, Word-Vorlagen) und Leerzustand; Neues Projekt mit Ziel-Karten
- Audio Mehrspur: Projekt anlegen, zwei Spuren, Clip mit der Maus verschieben, Stumm und Solo per Knopf, Abmischen öffnet eine Spur
- Audio spektral retuschieren: Spektrogramm an, Rechteck um den Pfeifton ziehen, Bereich absenken – Pfeifton weg, Stimme bleibt, rückgängig
- Audio-Editor Reparieren: ohne Profil freundlicher Hinweis, Rauschprofil merken, Rauschen entfernen, Brummen, Stille kürzen, Lautheit −16 LUFS, Spektrogramm, Aufnahme an der Marke
- Audio-Editor UI: WAV öffnen, Bereich mit der Maus ziehen, Löschen, Normalisieren, Einblenden, Kopieren/Einfügen, Abspielen, Rückgängig, Export WAV
- Bild-Dokument: Ebenen-Reihenfolge, Treffer oberste Ebene, Zuschnitt verschiebt Ebenen und Striche, Filter-Grenzen, Farbmatrix Invertieren
- Bildbearbeitung Profi (Profi 24): Zauberstab wählt die Farbfläche, Maske daraus, Lasso umfährt, inhaltsbasiert füllen entfernt Fleck, Kurvenpunkte frei ziehen
- Bildbearbeitung UI: Bild als Ebene, Pinsel malt (Pixel), Rechteck aufziehen, Text setzen, Ebene verschieben, Filter Schwarzweiß, Zuschneiden, Rückgängig, Export PNG
- Bildbearbeitung wie Krita/Inkscape: Vektorpinsel ergibt glatten Pfad, Mischmodus je Ebene, Pinsel-Deckkraft, SVG speichern und wieder öffnen
- Bildbearbeitung wie Photoshop/GIMP: Füllen mit Toleranz, Reparieren entfernt Fleck, Klonen mit Quelle, Verlauf ziehen, Maske aus Auswahl, Tonwerte, Kurve, Auto-Tonwerte
- C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst
- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- Dateimanager-Verhalten je System: Ziehen verschiebt (Strg kopiert), Kopiename je System, Finder-Zeilenschalter benennt um, Entf/⌘⌫ legt in den Papierkorb, Leertaste zeigt Vorschau
- Einfach zuerst: neue Nutzer öffnen Programme in der Stufe „Einfach“ (weniger Werkzeuge, alle Funktionen über „Erweitert“/„Professionell“ erreichbar)
- Einfachheit: jede Seite höchstens drei Reiterleisten auf einmal, Aufklapper und Scrollbereiche höchstens zwei Ebenen tief; Bericht der verschachteltsten Seiten
- G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg
- G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“
- G3/G4: Vorschlags-Chips unter der Studio-KI-Zeile setzen den Auftrag ein (starten nichts) und verschwinden beim Tippen; Desktop „Weitermachen“ mit zuletzt benutzten Programmen
- G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite
- G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe)
- G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint
- G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus
- G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus
- I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an
- K1: Musikspur im Videoschnitt zeigt die echte Wellenform der Tondatei (laut/leise erkennbar); ohne lesbare Datei nur Mittellinie statt erfundener Welle
- K3: Abspielleiste von Trendo Musik zeigt die echte Wellenform des gewählten Lieds; ohne Lied keine Welle
- Mehrfachauswahl wie im System: Umschalt-Klick Bereich, Strg-Klick dazu/weg, Rahmen mit der Maus aufziehen, Alles auswählen, mehrere kopieren/einfügen und in den Papierkorb, Klick ins Leere hebt auf
- Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt
- Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente
- Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt
- Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü ☰ – Menüeinträge lösen dieselben Befehle aus
- Registerkarten wie Explorer/Finder/Dateien: Strg/⌘+T neu, Mittelklick-Ordner in neuer Karte, Wechsel per Klick und Strg+Bild↓, eigener Verlauf je Karte, Strg/⌘+W schließt erst die Karte, dann das Fenster
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
- S4-01 Mehrspur: Aufnahme landet als neue Spur, Automation per Feld (rückgängig machbar, Unsinn abgelehnt); ohne Mikrofon keine Scheinaufnahme
- S4-06 Audioeditor: Plugin prüfen (Netz-Hinweis), ohne Freigabe kein Start, freigeben, anwenden → Schritt „Plugin: …“ (rückgängig); Absturz → Bericht, Projekt unverändert
- S4-06 Audioeditor: „Plugins prüfen“ findet VST3 und schreibt einen Bericht – ohne Plugin zu laden; leerer Ordner: klare Meldung
- S4-09 Mischpult: Bus anlegen, Spur darauf leiten, EQ übernehmen (rückgängig machbar); Routing-Kreis über die Auswahl wird abgewiesen
- S4-17 Audioeditor: Schritte erzeugen und öffnen, mit Ereignis-Tag in die Bibliothek; Fremdgeräusch ohne Lizenz wird im Paket ausgeschlossen und genannt
- S4-19 Audioeditor: Melodie → Noten schreibt MIDI und Unsicherheitsbericht, legt die Noten in Trendo Musik; Akkord wird als NICHT exakt gemeldet
- S4-20 Audioeditor: ohne Lizenz und bei Übersteuerung keine Freigabe (Grund steht da, kein Ordner); mit Angabe Freigabe mit WAV 24 Bit und Prüfbericht
- Spiel-Effekte: Laser würfeln → öffnet sich mit Lizenz „selbst erzeugt“ und Tag Spiel.Schuss; neue Variante ist anders; in die Bibliothek und ins Paket
- Stil Trendo bleibt unverändert: Trendo-Kopf, Seitenspalten, Dock – keine Fremd-Leisten
- Studio-Fenster wie im System: Mac und GNOME – KI-Zeile ist die Titelleiste (Ampel bzw. Schließen, Titel, Zurück), Windows behält die Titelleiste; Schließen in der Kopfleiste schließt das Fenster
- T-0205 (UI): Bild wählen → Bearbeiten-Bereich, Speichern zeigt Ergebnis
- T-0205 (UI): Video → Länge, Zeitleiste mit Bildern, Ausschnitt Start–Ende, zwei Clips aneinanderhängen
- T-0205: Clipliste maskiert Anführungszeichen, Vorschaubild-Aufruf ist korrekt

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
