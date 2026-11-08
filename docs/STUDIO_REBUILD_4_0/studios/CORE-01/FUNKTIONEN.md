# Funktionsliste: Desktop einrichten

Studio CORE-01 · Desktop einrichten · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | umgesetzt | OsStyles_Ui |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | umgesetzt | OsStyles_Ui |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | umgesetzt | OsStyles_Ui |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | umgesetzt | OsBehaviorTests |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | umgesetzt | OsBehaviorTests |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | umgesetzt | OsStyles_Ui |
| DA-07 | Löschen nur in den Papierkorb | umgesetzt | OsStyles_Ui |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | umgesetzt | OsBehaviorTests |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | umgesetzt | OsStyles_Ui |
| DA-10 | Registerkarten mit eigenem Verlauf | umgesetzt | OsStyles_Ui |
| DA-11 | Vorschau mit der Leertaste | umgesetzt | OsStyles_Ui |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | umgesetzt | OsStyles_Ui |
| DA-13 | Tastenkürzel je System | umgesetzt | OsBehaviorTests |
| DA-14 | Mit Server verbinden (SMB, SFTP) | offen | – |
| DA-15 | Rückgängig für Dateiaktionen | offen | – |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | umgesetzt | StartMenu_Ui |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | umgesetzt | StartMenu_Ui |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | umgesetzt | StartMenu_Ui |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | umgesetzt | StartMenu_StudioFenster |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | umgesetzt | W0_SetupWizard |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | umgesetzt | StudioClickTests |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | umgesetzt | StudioClickTests |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | umgesetzt | DesktopIconsUiTests |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | umgesetzt | DesktopIconsUiTests |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | umgesetzt | DesktopIconsUiTests |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | umgesetzt | DesktopIconsUiTests |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | umgesetzt | SetupFeedbackTests |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | umgesetzt | SetupFeedbackTests |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | offen | – |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | umgesetzt | Projektmappe_Ui |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | umgesetzt | SystemPagesTests |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Komprimieren/Entpacken direkt im Kontextmenü (gibt es im Werkzeug „Entpacken“)
- ✗ Rückgängig für Dateiaktionen (Strg+Z nach Verschieben/Löschen)

## Teilweise vorhanden

- ◐ Daten von einem alten PC übernehmen – über Sicherung/Wiederherstellen, kein eigener Umzugs-Assistent
- ◐ Mit Server verbinden (smb://, sftp://) – Hinweis vorhanden, noch keine eigene Verbindung
