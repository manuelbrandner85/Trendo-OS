# Live streamen – Testplan

Studio: Live streamen (SPEC-15) · Bereich: Kreativ & Medien · Seite: `sendung` · Stand: 0.9.36 (Stand 9ed3c01)

39 Tests sprechen dieses Programm an.

- SPEC-15 F12: Was läuft gerade
- SPEC-15 F13: Untertitel verschieben
- SPEC-15 F14: Sprechzeit
- SPEC-15 F15: Lautheit
- SPEC-15 F16: Wochenplan
- SPEC-15 F17: Seitenverhältnis
- SPEC-15 F18: Hochkant-Ausschnitt
- SPEC-15 F19: Moderationskarten
- SPEC-15 F20: Timecode
- SPEC-15 Live: FFmpeg über die bestehende Prüfung, ohne selbst zu installieren
- SPEC-15 Live: Gerätelisten aus ffmpeg-Text
- SPEC-15 Live: Mikrofon nur bei Aufnahme und Sendung, nie in der Vorschau
- SPEC-15 Live: Schlüssel nur im Speicher, Aufnahme stoppt den Prozess
- SPEC-15 Live: Senden nur nach Freigabe, genau einmal, Schlüssel nicht im Protokoll
- SPEC-15 Live: Stream-Befehl enthält Plattformadresse, Schlüssel und Stopp-fähige Ausgabe
- SPEC-15 Live: Vorschau und Aufnahme bauen echte ffmpeg-Befehle, ohne RTMP
- SPEC-15 Live: fehlendes FFmpeg startet keinen Prozess
- SPEC-15 Live: neue Sitzung ist ruhig und startet nichts
- SPEC-15 UI: FFmpeg wird erst nach Zustimmung installiert
- SPEC-15 UI: Fenster, Kamera und Mikrofon lassen sich wählen
- SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll
- SPEC-15 UI: Mikrofone suchen setzt das Häkchen nicht
- SPEC-15 UI: On-Air-Uhr zeigt laufenden Punkt mit Countdown und Nächstes; Weitergabe CSV/Kapitel/Karten; Rechner sofort
- SPEC-15 UI: Programmpunkte eintippen (Enter), Dauer und Titel in der Tabelle ändern, verschieben, entfernen; Sendeplatz zu kurz wird rot
- SPEC-15 UI: Vorschau, Aufnahme und Stopp tun etwas, ohne von allein zu senden
- SPEC-15 UI: offline kein Stream, Aufnahme bleibt
- SPEC-15: Sendeablauf mit Zeiten, Überziehung, Kapitelmarken, Bitrate, Speicher
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
