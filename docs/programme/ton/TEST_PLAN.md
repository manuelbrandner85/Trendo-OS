# Audio bearbeiten – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `ton` · Stand: 0.9.36 (Stand 434d2ab)

23 Tests sprechen dieses Programm an.

- Audio Mehrspur: Projekt anlegen, zwei Spuren, Clip mit der Maus verschieben, Stumm und Solo per Knopf, Abmischen öffnet eine Spur
- Audio spektral retuschieren: Spektrogramm an, Rechteck um den Pfeifton ziehen, Bereich absenken – Pfeifton weg, Stimme bleibt, rückgängig
- Audio-Editor Reparieren: ohne Profil freundlicher Hinweis, Rauschprofil merken, Rauschen entfernen, Brummen, Stille kürzen, Lautheit −16 LUFS, Spektrogramm, Aufnahme an der Marke
- Audio-Editor UI: WAV öffnen, Bereich mit der Maus ziehen, Löschen, Normalisieren, Einblenden, Kopieren/Einfügen, Abspielen, Rückgängig, Export WAV
- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- Einfach zuerst: neue Nutzer öffnen Programme in der Stufe „Einfach“ (weniger Werkzeuge, alle Funktionen über „Erweitert“/„Professionell“ erreichbar)
- Einfachheit: jede Seite höchstens drei Reiterleisten auf einmal, Aufklapper und Scrollbereiche höchstens zwei Ebenen tief; Bericht der verschachteltsten Seiten
- Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente
- Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt
- S4-01 Mehrspur: Aufnahme landet als neue Spur, Automation per Feld (rückgängig machbar, Unsinn abgelehnt); ohne Mikrofon keine Scheinaufnahme
- S4-06 Audioeditor: Plugin prüfen (Netz-Hinweis), ohne Freigabe kein Start, freigeben, anwenden → Schritt „Plugin: …“ (rückgängig); Absturz → Bericht, Projekt unverändert
- S4-06 Audioeditor: „Plugins prüfen“ findet VST3 und schreibt einen Bericht – ohne Plugin zu laden; leerer Ordner: klare Meldung
- S4-09 Mischpult: Bus anlegen, Spur darauf leiten, EQ übernehmen (rückgängig machbar); Routing-Kreis über die Auswahl wird abgewiesen
- S4-17 Audioeditor: Schritte erzeugen und öffnen, mit Ereignis-Tag in die Bibliothek; Fremdgeräusch ohne Lizenz wird im Paket ausgeschlossen und genannt
- S4-19 Audioeditor: Melodie → Noten schreibt MIDI und Unsicherheitsbericht, legt die Noten in Trendo Musik; Akkord wird als NICHT exakt gemeldet
- S4-20 Audioeditor: ohne Lizenz und bei Übersteuerung keine Freigabe (Grund steht da, kein Ordner); mit Angabe Freigabe mit WAV 24 Bit und Prüfbericht
- Spiel-Effekte: Laser würfeln → öffnet sich mit Lizenz „selbst erzeugt“ und Tag Spiel.Schuss; neue Variante ist anders; in die Bibliothek und ins Paket
- X17 Versionen: nach dem zweiten Speichern zeigt die Datei-Gruppe „Versionen (1)“, Wiederherstellen holt den alten Stand (rückgängig machbar)
