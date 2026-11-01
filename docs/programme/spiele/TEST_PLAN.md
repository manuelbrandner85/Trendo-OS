# Werkstatt – Testplan

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele` · Stand: 0.9.28 (Stand bea73f9)

20 Tests sprechen dieses Programm an.

- CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen
- CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check)
- CORE-06: Unreal-Engines aus Launcher-Liste erkannt, neueste zuerst; .uproject ist gültig
- Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen
- Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an
- Spiele: Unreal öffnen und die Herstellerseite bleiben der alte Weg
- T-0203: Abbrechen installiert nichts
- T-0203: LibreOffice bleibt zu, wenn die Versionsabfrage zu lange dauert
- T-0203: OBS startet nicht, wenn die Dateiversion fehlt
- T-0203: OBS unter Windows liest die Dateiversion und öffnet ohne Prozess in der Prüfung
- T-0203: „Einrichten“ fragt, „Ja“ lädt nichts, solange die Prüfung nicht besteht
- T-0203: „In LibreOffice öffnen“ startet die Datei, wenn --version schnell antwortet
- T-0203: „In OBS öffnen“ startet die Sammlung, wenn --version schnell antwortet
- T-0203: „In VS Code öffnen“ startet den Ordner, wenn --version schnell antwortet
- T-0203: „Ja, einrichten“ öffnet keine Herstellerseite, solange die Prüfung nicht besteht
- T-0203: „Starten“ öffnet LibreOffice und Blender erst nach bestandener Prüfung und Freigabe
- T-0203: „Starten“ öffnet nicht, wenn die Prüfung scheitert
- T-0204 (UI): Anlegen → Editor; Tempo und Level ändern, speichern, als ZIP exportieren
- T-0204 (UI): Unreal – vorhandenes Projekt öffnen; ohne Unreal klarer Hinweis
- T-0204: Einstellungen landen in config.js, Bild wird kopiert, Export enthält alles
