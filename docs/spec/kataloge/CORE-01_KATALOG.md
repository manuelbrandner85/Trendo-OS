# CORE-01 – Desktop einrichten: Funktionskatalog

Funktionen nach den Vorbildern (Windows-11-Datei-Explorer, macOS Finder, GNOME „Dateien“; Windows-11-Start und -Ersteinrichtung, GNOME Shell, macOS Launchpad, VS Code Arbeitsbereiche – nur Funktionen, Clean-Room), Stand 05.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

## Dateien

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | Explorer | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | Explorer, Finder | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | Explorer, Finder, Dateien | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | Explorer, Finder, Dateien | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | Finder (Übersicht), Dateien (Sushi) | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | Explorer, Finder, Dateien | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | Explorer, Finder, Dateien | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | Dateien, Finder | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | Explorer | offen | – |

## Startbildschirm und Startmenü

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | Windows 11 | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | Windows 11, GNOME | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | Windows 11 | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | Windows 11 | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | Windows 11 | umgesetzt | W0_SetupWizard |

## Alle Studios

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | macOS Launchpad | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | – | umgesetzt | StudioClickTests |

## Desktop-Symbole

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | Windows, GNOME | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | Windows | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | – | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | Windows | umgesetzt | DesktopIconsUiTests |

## Ersteinrichtung

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | Windows-Ersteinrichtung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | Windows-Ersteinrichtung | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | Windows-Migration | offen | – |

## Projekte

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | VS Code Arbeitsbereiche | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | – | umgesetzt | SystemPagesTests |
