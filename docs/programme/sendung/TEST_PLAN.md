# Live streamen – Testplan

Studio: Live streamen (SPEC-15) · Bereich: Kreativ & Medien · Seite: `sendung` · Stand: 0.9.110 (Stand 0eb9d52)

37 Tests sprechen dieses Programm an.

- Live streamen wie OBS: Szene wählen und anlegen, Bauchbinde einblenden (im FFmpeg-Graph), Tonmischer mit Lautstärke und Stumm, Pegel und Aussetzer-Warnung, Probelauf mit Ampel, Sendeplan
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
- SPEC-15: Claude als Regie – Briefing mit Szenen, Ton und Ton-Wache, Plan zeigt neue Szenen, „Ausführen“ legt Szenen mit Bauchbinde an und setzt den Sendeplan; ungültige Quelle fällt weg
- SPEC-15: Sendeablauf mit Zeiten, Überziehung, Kapitelmarken, Bitrate, Speicher
- T-0203: OBS startet nicht, wenn die Dateiversion fehlt
- T-0203: OBS unter Windows liest die Dateiversion und öffnet ohne Prozess in der Prüfung
- T-0203: „In OBS öffnen“ startet die Sammlung, wenn --version schnell antwortet
- T-0715 Live streamen: alte ablauf.json lesbar; Programmpunkte und Szene – Rückgängig/Wiederholen mehrere Schritte (auch über das Menü), sofort gespeichert (keine Karte nötig, zweites Fenster sieht den Stand), Export atomar
- T-0726 A9: Live streamen – Text in der Zelle mit Fokus ist nach der Tipp-Pause gespeichert (Fenster zu ohne Verlassen → da) und ein Rückgängig-Schritt; Unfertiges wartet
- T-0728 C6: Ergebnisse der Rechner (LiveCalc) in Gold, nicht blau
- T-0746 ECHT Live streamen: App sendet an lokalen RTMP-Empfänger (127.0.0.1), verbindet nach Abbruch neu, ffprobe liest die Aufzeichnung (nur mit TRENDO_ECHT_STREAM)
- mediamtx-Oberfläche: Sendeserver starten → OBS-Adresse und Ansehen-Adresse, nur 127.0.0.1; beenden; unsicherer Name abgelehnt
