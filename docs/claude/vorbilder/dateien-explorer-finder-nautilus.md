# Vorbild-Analyse Dateien: Datei-Explorer, Finder, GNOME „Dateien“ (Clean-Room)

- Programme: „Dateien“
- Vorbilder: Windows-11-Datei-Explorer, macOS Finder, GNOME „Dateien“ (Nautilus); dazu Total Commander und Dolphin als Profi-Maßstab (Funktionsumfang aus öffentlicher Doku und Bedienkonzept)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Symbole oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026. Bedienstile im Ganzen: `docs/claude/bedienstile-windows-mac-gnome.md`.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Seitenleiste mit Start/persönlichem Ordner, Desktop, Downloads, Dokumente, Bilder, Musik, Videos und Laufwerken
- ✓ „Dieser PC“ (Windows), „Orte“ (Mac), „Andere Orte“ (GNOME) mit Laufwerken und Füllstand
- ✓ Adresszeile mit Brotkrumen (Windows), Pfad-Pille (GNOME), Pfadleiste unten (Finder); Zurück, Vor, Auf, Aktualisieren
- ✓ Ansicht Liste (Name, Änderungsdatum, Typ, Größe; nach Spalte sortieren) und Symbole; ausgeblendete Elemente
- ✓ Öffnen (Doppelklick), neuer Ordner, umbenennen, ausschneiden, kopieren, einfügen, duplizieren – Namen von Kopien und neuen Ordnern wie im jeweiligen System
- ✓ Löschen nur in den Papierkorb (Windows-Papierkorb, freedesktop-Papierkorb, ~/.Trash)
- ✓ Ziehen und Ablegen: verschieben oder kopieren nach den Regeln des Systems (Laufwerk, Strg/Umschalt bzw. Wahltaste/⌘); Dateien von außen ablegen
- ✓ Mehrfachauswahl: Umschalt-Klick, Strg-/⌘-Klick, Auswahlrahmen, Alles auswählen
- ✓ Registerkarten mit eigenem Verlauf (Windows in der Titelleiste, Finder/GNOME ab zwei Tabs), Mittelklick öffnet/schließt
- ✓ Vorschau mit der Leertaste (Bilder, Texte, Datei-Infos), Infos (Alt+Enter / ⌘I)
- ✓ Suchen im Ordner (Windows/Mac Feld, GNOME hinter der Lupe und Lostippen), Tippen springt zum Element
- ✓ Tastenkürzel wie im System (Finder: Zeilenschalter benennt um, ⌘⌫; Explorer/GNOME: F2, Entf, Alt+↑)
- ◐ Mit Server verbinden (smb://, sftp://) – Hinweis vorhanden, noch keine eigene Verbindung
- ✗ Rückgängig für Dateiaktionen (Strg+Z nach Verschieben/Löschen)
- ✗ Komprimieren/Entpacken direkt im Kontextmenü (gibt es im Werkzeug „Entpacken“)

## Bedienkonzept (Profi-Bauplan A1–A3)
Seitenleiste links, Inhalt in der Mitte, Statusleiste unten; Kopf je System (Explorer: Registerkarten in der Titelleiste, Adresszeile, Befehlsleiste; Finder: eine Werkzeugleiste mit Ampel in der Seitenleiste; GNOME: eine Kopfleiste mit Pfad, Lupe und Hauptmenü).

## Nächste Schritte
1. Rückgängig für Verschieben, Umbenennen und Papierkorb
2. Komprimieren/Entpacken im Kontextmenü
3. Netzwerkorte (SMB/SFTP) verbinden
