# Live streamen – Testergebnisse

Studio: Live streamen (SPEC-15) · Bereich: Kreativ & Medien · Seite: `sendung` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 36 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Live streamen wie OBS: Szene wählen und anlegen, Bauchbinde einblenden (im FFmpeg-Graph), Tonmischer mit Lautstärke und Stumm, Pegel und Aussetzer-Warnung, Probelauf mit Ampel, Sendeplan | ✓ bestanden |
| SPEC-15 F12: Was läuft gerade | ✓ bestanden |
| SPEC-15 F13: Untertitel verschieben | ✓ bestanden |
| SPEC-15 F14: Sprechzeit | ✓ bestanden |
| SPEC-15 F15: Lautheit | ✓ bestanden |
| SPEC-15 F16: Wochenplan | ✓ bestanden |
| SPEC-15 F17: Seitenverhältnis | ✓ bestanden |
| SPEC-15 F18: Hochkant-Ausschnitt | ✓ bestanden |
| SPEC-15 F19: Moderationskarten | ✓ bestanden |
| SPEC-15 F20: Timecode | ✓ bestanden |
| SPEC-15 Live: FFmpeg über die bestehende Prüfung, ohne selbst zu installieren | ✓ bestanden |
| SPEC-15 Live: Gerätelisten aus ffmpeg-Text | ✓ bestanden |
| SPEC-15 Live: Mikrofon nur bei Aufnahme und Sendung, nie in der Vorschau | ✓ bestanden |
| SPEC-15 Live: Schlüssel nur im Speicher, Aufnahme stoppt den Prozess | ✓ bestanden |
| SPEC-15 Live: Senden nur nach Freigabe, genau einmal, Schlüssel nicht im Protokoll | ✓ bestanden |
| SPEC-15 Live: Stream-Befehl enthält Plattformadresse, Schlüssel und Stopp-fähige Ausgabe | ✓ bestanden |
| SPEC-15 Live: Vorschau und Aufnahme bauen echte ffmpeg-Befehle, ohne RTMP | ✓ bestanden |
| SPEC-15 Live: fehlendes FFmpeg startet keinen Prozess | ✓ bestanden |
| SPEC-15 Live: neue Sitzung ist ruhig und startet nichts | ✓ bestanden |
| SPEC-15 UI: FFmpeg wird erst nach Zustimmung installiert | ✓ bestanden |
| SPEC-15 UI: Fenster, Kamera und Mikrofon lassen sich wählen | ✓ bestanden |
| SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll | ✓ bestanden |
| SPEC-15 UI: Mikrofone suchen setzt das Häkchen nicht | ✓ bestanden |
| SPEC-15 UI: On-Air-Uhr zeigt laufenden Punkt mit Countdown und Nächstes; Weitergabe CSV/Kapitel/Karten; Rechner sofort | ✓ bestanden |
| SPEC-15 UI: Programmpunkte eintippen (Enter), Dauer und Titel in der Tabelle ändern, verschieben, entfernen; Sendeplatz zu kurz wird rot | ✓ bestanden |
| SPEC-15 UI: Vorschau, Aufnahme und Stopp tun etwas, ohne von allein zu senden | ✓ bestanden |
| SPEC-15 UI: offline kein Stream, Aufnahme bleibt | ✓ bestanden |
| SPEC-15: Claude als Regie – Briefing mit Szenen, Ton und Ton-Wache, Plan zeigt neue Szenen, „Ausführen“ legt Szenen mit Bauchbinde an und setzt den Sendeplan; ungültige Quelle fällt weg | ✓ bestanden |
| SPEC-15: Sendeablauf mit Zeiten, Überziehung, Kapitelmarken, Bitrate, Speicher | ✓ bestanden |
| T-0203: OBS startet nicht, wenn die Dateiversion fehlt | ✓ bestanden |
| T-0203: OBS unter Windows liest die Dateiversion und öffnet ohne Prozess in der Prüfung | ✓ bestanden |
| T-0203: „In OBS öffnen“ startet die Sammlung, wenn --version schnell antwortet | ✓ bestanden |
| T-0715 Live streamen: alte ablauf.json lesbar; Programmpunkte und Szene – Rückgängig/Wiederholen mehrere Schritte (auch über das Menü), sofort gespeichert (keine Karte nötig, zweites Fenster sieht den Stand), Export atomar | ✓ bestanden |
| T-0726 A9: Live streamen – Text in der Zelle mit Fokus ist nach der Tipp-Pause gespeichert (Fenster zu ohne Verlassen → da) und ein Rückgängig-Schritt; Unfertiges wartet | ✓ bestanden |
| T-0728 C6: Ergebnisse der Rechner (LiveCalc) in Gold, nicht blau | ✓ bestanden |
| T-0746 ECHT Live streamen: App sendet an lokalen RTMP-Empfänger (127.0.0.1), verbindet nach Abbruch neu, ffprobe liest die Aufzeichnung (nur mit TRENDO_ECHT_STREAM) | übersprungen |
| mediamtx-Oberfläche: Sendeserver starten → OBS-Adresse und Ansehen-Adresse, nur 127.0.0.1; beenden; unsicherer Name abgelehnt | ✓ bestanden |
