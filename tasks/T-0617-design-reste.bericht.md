# Bericht T-0617: Reste aus dem ruhigen Design (T-0613)

**Auftrag:** offene Punkte aus T-0613 abarbeiten („alles Offene durchziehen“, 14.11.2026).
**Zweig:** `claude/T-0617-design-reste` von `main` (cce5c6261).

## Inhalt

- **Dichte „Kompakt“ wirkt jetzt überall:** neuer Baustein `src/Trendo.App/Density.cs` verkleinert Innenabstände, die im Code gesetzt sind (Knöpfe, Felder, Auswahllisten), auf etwa zwei Drittel, merkt sich den Ursprung und stellt ihn bei „Normal“ genau wieder her; nie doppelt. Läuft nach jedem Seitenwechsel mit. Stil-Werte regelt weiter `App.axaml`.
- **Letzte Schmuckzeichen** aus neuem Code von main entfernt: Überschriften in „Webseite bauen“ (Medien, KI auf diesem PC, Komponenten, Gestaltung, Geräte-Ansichten), Handy-Seite „TV-Fernbedienung“, TV-Mehrfachbild (Lautstärke als Wort, Hinweistext ohne Symbol).
- Bewusst belassen: Zeichen in `GlyphIcons` (Zuordnungstabelle), Werkzeugnamen der Bildbearbeitung (werden am Leerzeichen zerlegt; Sitzung B), Ordner-Zeichen im Code-Editor-Baum, der frühere Claude-Name für alte Chats.
- Zwei niedrige Prüf-Befunde aus T-0613 bleiben dokumentiert, ohne Änderung: Verläufe aus Stil-Zuständen gibt es im Code nicht; Bild-Steuerelemente stehen beim Ruhe-Durchgang schon im Baum, auch wenn das Bild später lädt.

## Tests

- Neu `tests/Trendo.App.Tests/DensityUiTests.cs` (Density_Ui): 2 bestanden.
- Betroffene Klassen (Webseite bauen, TV, Handy, Einstellungen; 12 Klassen): 42 bestanden, 0 Fehler.
- Volltest: gemeinsam mit T-0614/T-0615/T-0616 am Ende (siehe deren Bericht bzw. Sammelbericht).
