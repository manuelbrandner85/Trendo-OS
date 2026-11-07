# Audio bearbeiten – Testergebnisse

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `ton` · Stand: 0.9.36 (Stand 2963379)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 22 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Audio Mehrspur: Projekt anlegen, zwei Spuren, Clip mit der Maus verschieben, Stumm und Solo per Knopf, Abmischen öffnet eine Spur | ✓ bestanden |
| Audio spektral retuschieren: Spektrogramm an, Rechteck um den Pfeifton ziehen, Bereich absenken – Pfeifton weg, Stimme bleibt, rückgängig | ✓ bestanden |
| Audio-Editor Reparieren: ohne Profil freundlicher Hinweis, Rauschprofil merken, Rauschen entfernen, Brummen, Stille kürzen, Lautheit −16 LUFS, Spektrogramm, Aufnahme an der Marke | ✓ bestanden |
| Audio-Editor UI: WAV öffnen, Bereich mit der Maus ziehen, Löschen, Normalisieren, Einblenden, Kopieren/Einfügen, Abspielen, Rückgängig, Export WAV | ✓ bestanden |
| CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei | ✓ bestanden |
| CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen | ✓ bestanden |
| CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt | ✓ bestanden |
| CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich | ✓ bestanden |
| CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben | ✓ bestanden |
| CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell | ✓ bestanden |
| Einfach zuerst: neue Nutzer öffnen Programme in der Stufe „Einfach“ (weniger Werkzeuge, alle Funktionen über „Erweitert“/„Professionell“ erreichbar) | ✓ bestanden |
| Einfachheit: jede Seite höchstens drei Reiterleisten auf einmal, Aufklapper und Scrollbereiche höchstens zwei Ebenen tief; Bericht der verschachteltsten Seiten | ✓ bestanden |
| Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente | ✓ bestanden |
| Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt | ✓ bestanden |
| S4-01 Mehrspur: Aufnahme landet als neue Spur, Automation per Feld (rückgängig machbar, Unsinn abgelehnt); ohne Mikrofon keine Scheinaufnahme | ✓ bestanden |
| S4-06 Audioeditor: Plugin prüfen (Netz-Hinweis), ohne Freigabe kein Start, freigeben, anwenden → Schritt „Plugin: …“ (rückgängig); Absturz → Bericht, Projekt unverändert | ✓ bestanden |
| S4-06 Audioeditor: „Plugins prüfen“ findet VST3 und schreibt einen Bericht – ohne Plugin zu laden; leerer Ordner: klare Meldung | ✓ bestanden |
| S4-09 Mischpult: Bus anlegen, Spur darauf leiten, EQ übernehmen (rückgängig machbar); Routing-Kreis über die Auswahl wird abgewiesen | ✓ bestanden |
| S4-17 Audioeditor: Schritte erzeugen und öffnen, mit Ereignis-Tag in die Bibliothek; Fremdgeräusch ohne Lizenz wird im Paket ausgeschlossen und genannt | ✓ bestanden |
| S4-19 Audioeditor: Melodie → Noten schreibt MIDI und Unsicherheitsbericht, legt die Noten in Trendo Musik; Akkord wird als NICHT exakt gemeldet | ✓ bestanden |
| S4-20 Audioeditor: ohne Lizenz und bei Übersteuerung keine Freigabe (Grund steht da, kein Ordner); mit Angabe Freigabe mit WAV 24 Bit und Prüfbericht | ✓ bestanden |
| X17 Versionen: nach dem zweiten Speichern zeigt die Datei-Gruppe „Versionen (1)“, Wiederherstellen holt den alten Stand (rückgängig machbar) | ✓ bestanden |
