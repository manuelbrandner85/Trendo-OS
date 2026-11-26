# Einstellungen – Testplan

Studio: Schrift & Farben anpassen (SYS-12) · Bereich: System & Sicherheit · Seite: `settings` · Stand: 0.9.110 (Stand 0eb9d52)

44 Tests sprechen dieses Programm an.

- Absturzschutz: abgefangener Oberflächenfehler zeigt einen Hinweis „Trendo läuft weiter“ – nur einmal bei Folgefehlern, Fenster bleibt bedienbar
- Bedienstil: in den Einstellungen wählbar; Mac setzt runde Ampel-Knöpfe links (Schließen zuerst), Linux runde Knöpfe rechts – Knöpfe funktionieren weiter
- Dateimanager-Verhalten je System: Ziehen verschiebt (Strg kopiert), Kopiename je System, Finder-Zeilenschalter benennt um, Entf/⌘⌫ legt in den Papierkorb, Leertaste zeigt Vorschau
- Dichte: Kompakt verkleinert Innenabstände von Knöpfen, Normal stellt sie wieder her, Einstellung bleibt gespeichert
- E12: „Einfach“ blendet rechte Spalte, Erdkugel und Schnellaktionen aus (einblendbar unter „Anpassen“); Einstellungen zeigen die Hauptseiten, Rest unter „Mehr“
- Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar
- Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück
- Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein
- Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster
- Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück
- Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck
- Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen
- Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe
- KI: die Seite lässt sich erneut öffnen, Status bleibt „Nicht verbunden“
- Mehrfachauswahl wie im System: Umschalt-Klick Bereich, Strg-Klick dazu/weg, Rahmen mit der Maus aufziehen, Alles auswählen, mehrere kopieren/einfügen und in den Papierkorb, Klick ins Leere hebt auf
- Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt
- Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü – Menüeinträge lösen dieselben Befehle aus
- Registerkarten wie Explorer/Finder/Dateien: Strg/⌘+T neu, Mittelklick-Ordner in neuer Karte, Wechsel per Klick und Strg+Bild↓, eigener Verlauf je Karte, Strg/⌘+W schließt erst die Karte, dann das Fenster
- SYS-12 (UI): Bericht über alle Seiten; keine Seite tiefer als 2 Ebenen; Prüfseite hat keine unbenannten Knöpfe
- SYS-12 (UI): Leise Benachrichtigung; Einstellung bleibt gespeichert; einfacher Modus zeigt weniger Kacheln
- SYS-12 (UI): Mindestschrift greift; Knöpfe haben Namen und sind per Tab erreichbar; Bewegung aus = keine Übergänge
- SYS-12-P03/P09/P18: Mindestschrift, Status mit Zeichen, leise Meldungen
- SYS-12-P07: WCAG-Kontrast (Schwarz/Weiß 21:1, AA-Grenzen)
- SYS-12-P10: Leichte Sprache – höchstens 2 kurze Sätze, einfache Wörter
- SYS-12-P14: Jede Akzentfarbe wird für Text auf jedem Design AA-tauglich
- SYS-12-P15/P16: Navigation >2 Ebenen und englische Beschriftungen werden gefunden
- SYS-12-P20: Vorlesen – Windows-SAPI-Befehl, Linux espeak oder nichts
- SYS-12-P22: Einstellungen exportieren/importieren verlustfrei, Unsinn abgelehnt
- SYS-12-P23: Fensterposition nur auf vorhandenem Bildschirm wiederherstellen
- SYS-12-P25: Bericht listet Befunde je Seite
- SYS-12: Anzeigegröße wird auf erlaubte Stufen begrenzt
- SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe
- Startmenü: Studio aus A–Z anheften, angeheftet öffnen, lösen; Zuletzt benutzt
- Stil Trendo bleibt unverändert: Trendo-Kopf, Seitenspalten, Dock – keine Fremd-Leisten
- Studio-Fenster wie im System: Mac und GNOME – KI-Zeile ist die Titelleiste (Ampel bzw. Schließen, Titel, Zurück), Windows behält die Titelleiste; Schließen in der Kopfleiste schließt das Fenster
- Studio-Ordnung: Das aufnehmende Studio und die Einstellungen führen die Bereiche unter „Mehr“ → „Verwandte Programme“, der Eintrag öffnet den Bereich
- Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog
- T-0107: Sparsam – keine Hintergrund-Unschärfe, keine Schatten und Effekte, keine Übergänge; Auswahl in den Einstellungen
- T-0686-Rest: Bedienstile Windows und GNOME zeigen den Akzent in Gold statt Blau
- T-0713 UI: Hinweis-Karte in den Einstellungen, wenn ein Erwachsenen-Profil ohne PIN neben einem Kinderprofil besteht; „PIN festlegen“ führt zur Anmeldung; mit PIN verschwindet sie
- Wie GNOME: schwarze Leiste mit Aktivitäten und Uhr in der Mitte, Übersicht mit Suche und Dash, nur Schließen-Knopf im Fenster, Dateien mit „Andere Orte“
- Wie Windows 11: Taskleiste mit Start/Suche/Uhr, Startmenü mit Angeheftet und Suche (Trendo KI), Widgets mit allen Trendo-Karten, eckige Fensterknöpfe, Datei-Explorer mit „Dieser PC“
- Wie im Original: eine Kopfleiste in GNOME-Dateien/Finder, Uhr öffnet Mitteilungen + Monatskalender, Schnelleinstellungen mit Kacheln, „Nicht stören“, GNOME-Suche per Lupe/Lostippen, „Andere Orte“, Explorer springt beim Tippen
- Wie macOS: Menüleiste mit Menüs und Uhr, Dock mit Launchpad und Papierkorb, Spotlight, Ampel-Knöpfe, ⌘W schließt, Finder mit Seitenleiste und Symbolansicht
