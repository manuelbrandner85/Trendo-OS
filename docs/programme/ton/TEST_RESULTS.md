# Audio bearbeiten – Testergebnisse

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `ton` · Stand: 0.9.36 (Stand df8e056)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 15 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

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
| X17 Versionen: nach dem zweiten Speichern zeigt die Datei-Gruppe „Versionen (1)“, Wiederherstellen holt den alten Stand (rückgängig machbar) | ✓ bestanden |
