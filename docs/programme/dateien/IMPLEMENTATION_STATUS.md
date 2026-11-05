# Dateien – Umsetzungsstand

Studio: Desktop einrichten (CORE-01) · Bereich: System & Sicherheit · Seite: `dateien` · Stand: 0.9.36 (Stand 208ed9e)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| DA-01 | Seitenleiste mit bekannten Ordnern und Laufwerken | Implementiert | `OsStyles_Ui` |
| DA-02 | „Dieser PC“ / „Orte“ / „Andere Orte“ mit Füllstand | Implementiert | `OsStyles_Ui` |
| DA-03 | Adresszeile, Zurück, Vor, Auf, Aktualisieren | Implementiert | `OsStyles_Ui` |
| DA-04 | Liste mit Spalten und Sortierung, Symbolansicht | Implementiert | `OsBehaviorTests` |
| DA-05 | Neuer Ordner, Umbenennen mit Systemnamen | Implementiert | `OsBehaviorTests` |
| DA-06 | Kopieren, Ausschneiden, Einfügen, Duplizieren mit Kopienamen je System | Implementiert | `OsStyles_Ui` |
| DA-07 | Löschen nur in den Papierkorb | Implementiert | `OsStyles_Ui` |
| DA-08 | Ziehen und Ablegen: verschieben/kopieren nach Systemregel | Implementiert | `OsBehaviorTests` |
| DA-09 | Mehrfachauswahl (Umschalt, Strg/⌘, Rahmen, Alles) | Implementiert | `OsStyles_Ui` |
| DA-10 | Registerkarten mit eigenem Verlauf | Implementiert | `OsStyles_Ui` |
| DA-11 | Vorschau mit der Leertaste | Implementiert | `OsStyles_Ui` |
| DA-12 | Suchen im Ordner, Tippen springt zum Element | Implementiert | `OsStyles_Ui` |
| DA-13 | Tastenkürzel je System | Implementiert | `OsBehaviorTests` |
| DA-14 | Mit Server verbinden (SMB, SFTP) | Geplant | `–` |
| DA-15 | Rückgängig für Dateiaktionen | Geplant | `–` |
| SB-01 | Startmenü: Startknopf, Esc und Klick daneben schließen, Windows-Taste schaltet um | Implementiert | `StartMenu_Ui` |
| SB-02 | Startmenü-Suche findet Studio und Seite, Klick öffnet | Implementiert | `StartMenu_Ui` |
| SB-03 | Anheften, Lösen, Zuletzt benutzt, alle Studios A–Z | Implementiert | `StartMenu_Ui` |
| SB-04 | Studios öffnen in eigenen Fenstern aus dem Startmenü | Implementiert | `StartMenu_StudioFenster` |
| SB-05 | Tastatur: Strg+1…5, Tab, Enter, zugängliche Namen | Implementiert | `W0_SetupWizard` |
| ST-01 | Bildkarten je Studio, jede Karte führt aus der Liste heraus | Implementiert | `StudioClickTests` |
| ST-02 | Jedes fertige Studio hat einen eigenen Bereich (keine Sackgasse) | Implementiert | `StudioClickTests` |
| DS-01 | Desktop anfangs aufgeräumt; Symbole über Startmenü, Seite und Studio Manager hinzufügen, Rechtsklick entfernt, bleibt gespeichert | Implementiert | `DesktopIconsUiTests` |
| DS-02 | Seite schaltet jedes Symbol hin und zurück, kein doppeltes Symbol | Implementiert | `DesktopIconsUiTests` |
| DS-03 | Unbekannte oder nicht installierte Einträge erscheinen nicht | Implementiert | `DesktopIconsUiTests` |
| DS-04 | Studio und Kernfunktion gleichzeitig, Reihenfolge bleibt, Klick öffnet | Implementiert | `DesktopIconsUiTests` |
| EE-01 | Design-Wahl mit sofortiger Vorschau, Paket-Wahl in der Zusammenfassung | Implementiert | `SetupFeedbackTests` |
| EE-02 | Reparieren meldet das Ergebnis, Ordnerwahl im Assistenten | Implementiert | `SetupFeedbackTests` |
| EE-03 | Daten vom alten PC übernehmen (Umzugs-Assistent) | Geplant | `–` |
| PR-01 | Projekt mit lesbarem Namen, Dateien und Ordner hinzufügen, Notiz, Umbenennen | Implementiert | `Projektmappe_Ui` |
| PR-02 | Ordnername aus dem Namen (Umlaute, Nummer bei belegtem Namen), nichts außerhalb des Projekts | Implementiert | `SystemPagesTests` |

0 von 31 Funktionen getestet.
