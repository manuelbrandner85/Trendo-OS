# Geräte überwachen – Testplan

Studio: Geräte überwachen (SPEC-10) · Bereich: Technik & Fachgebiete · Seite: `zwilling` · Stand: 0.9.112 (Stand d6f9fcd)

25 Tests sprechen dieses Programm an.

- Gegenprüfung: Datei lesen und erste PDH-Abfrage laufen nicht auf dem UI-Faden, Ergebnis kommt dort an, kein Doppelstart
- Gegenprüfung: Leistungsindikatoren werden beim Schließen des Fensters freigegeben
- Gegenprüfung: Netzwerkpfad als wachsende Datei wird abgelehnt, ohne darauf zuzugreifen
- Gegenprüfung: Regel-Datei mit leerem Eintrag stürzt nicht ab, sondern meldet es auf Deutsch
- MQTT-Oberfläche: Verknüpfungen lesen (Kommentare, JSON-Feld), Fehler verständlich; Verbinden legt Sensoren an, merkt Broker ohne Passwort; Trennen
- SPEC-10 F09: Speichergrenze
- SPEC-10 F10: Ereignisprotokoll
- SPEC-10 F11: Hysterese
- SPEC-10 F12: Zeitraum-Kennzahlen
- SPEC-10 F13: Betriebsstunden
- SPEC-10 F14: Veraltete Sensoren
- SPEC-10 F15: Abgeleiteter Sensor
- SPEC-10 F16: Export
- SPEC-10 F17: Sensor löschen/umbenennen
- SPEC-10 F18: Wartungsplan
- SPEC-10 F19: Was-wäre-wenn
- SPEC-10 F20: Energie
- SPEC-10 UI: Beispielanlagen laden, im Baum wählen, Kachel zeigt Zustand, Zeitraum 24 h, Grenze eintippen ändert die Ampel, Prognose
- SPEC-10 UI: Sensor anlegen, CSV einlesen, ableiten, umbenennen, CSV/SVG speichern, löschen nur mit Bestätigung; PC messen und Live
- SPEC-10: Zwilling mit Sensoren, CSV-Import, Ampel, Trend und Zeit bis Alarm
- T-0212: Schalter zeigt Zustand; „Status mit Zeichen“ setzt ✓ ⚠ ✕ an echte Statuspunkte
- T-0716 Anlagen-Monitor: Grenze gesetzt (gespeichert) + zweite getippt – Rückgängig/Wiederholen, Notsicherung, Sperre, Karte, Zurückholen; Messwerte bleiben unberührt
- T-0716 Fenster schließen, während im Feld getippt ist: das Programm übernimmt die Eingabe (wie vorher) – keine Karte, nichts geht verloren
- T-0750 Echtbeleg: echte CPU-/RAM-/Datenträger-/Netzwerte dieses PCs, künstliche Last löst die CPU-Regel aus (Benachrichtigung + Protokoll)
- T-0750 UI: wachsende Datei beobachten, Regel per Klick anlegen, Alarm löst bei echter Datei aus (Benachrichtigung, Protokoll), Regeln speichern/einlesen, Rückgängig
