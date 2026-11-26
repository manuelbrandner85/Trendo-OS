# Geräte überwachen – Testergebnisse

Studio: Geräte überwachen (SPEC-10) · Bereich: Technik & Fachgebiete · Seite: `zwilling` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 25 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Gegenprüfung: Datei lesen und erste PDH-Abfrage laufen nicht auf dem UI-Faden, Ergebnis kommt dort an, kein Doppelstart | ✓ bestanden |
| Gegenprüfung: Leistungsindikatoren werden beim Schließen des Fensters freigegeben | ✓ bestanden |
| Gegenprüfung: Netzwerkpfad als wachsende Datei wird abgelehnt, ohne darauf zuzugreifen | ✓ bestanden |
| Gegenprüfung: Regel-Datei mit leerem Eintrag stürzt nicht ab, sondern meldet es auf Deutsch | ✓ bestanden |
| MQTT-Oberfläche: Verknüpfungen lesen (Kommentare, JSON-Feld), Fehler verständlich; Verbinden legt Sensoren an, merkt Broker ohne Passwort; Trennen | ✓ bestanden |
| SPEC-10 F09: Speichergrenze | ✓ bestanden |
| SPEC-10 F10: Ereignisprotokoll | ✓ bestanden |
| SPEC-10 F11: Hysterese | ✓ bestanden |
| SPEC-10 F12: Zeitraum-Kennzahlen | ✓ bestanden |
| SPEC-10 F13: Betriebsstunden | ✓ bestanden |
| SPEC-10 F14: Veraltete Sensoren | ✓ bestanden |
| SPEC-10 F15: Abgeleiteter Sensor | ✓ bestanden |
| SPEC-10 F16: Export | ✓ bestanden |
| SPEC-10 F17: Sensor löschen/umbenennen | ✓ bestanden |
| SPEC-10 F18: Wartungsplan | ✓ bestanden |
| SPEC-10 F19: Was-wäre-wenn | ✓ bestanden |
| SPEC-10 F20: Energie | ✓ bestanden |
| SPEC-10 UI: Beispielanlagen laden, im Baum wählen, Kachel zeigt Zustand, Zeitraum 24 h, Grenze eintippen ändert die Ampel, Prognose | ✓ bestanden |
| SPEC-10 UI: Sensor anlegen, CSV einlesen, ableiten, umbenennen, CSV/SVG speichern, löschen nur mit Bestätigung; PC messen und Live | ✓ bestanden |
| SPEC-10: Zwilling mit Sensoren, CSV-Import, Ampel, Trend und Zeit bis Alarm | ✓ bestanden |
| T-0212: Schalter zeigt Zustand; „Status mit Zeichen“ setzt ✓ ⚠ ✕ an echte Statuspunkte | ✓ bestanden |
| T-0716 Anlagen-Monitor: Grenze gesetzt (gespeichert) + zweite getippt – Rückgängig/Wiederholen, Notsicherung, Sperre, Karte, Zurückholen; Messwerte bleiben unberührt | ✓ bestanden |
| T-0716 Fenster schließen, während im Feld getippt ist: das Programm übernimmt die Eingabe (wie vorher) – keine Karte, nichts geht verloren | ✓ bestanden |
| T-0750 Echtbeleg: echte CPU-/RAM-/Datenträger-/Netzwerte dieses PCs, künstliche Last löst die CPU-Regel aus (Benachrichtigung + Protokoll) | ✓ bestanden |
| T-0750 UI: wachsende Datei beobachten, Regel per Klick anlegen, Alarm löst bei echter Datei aus (Benachrichtigung, Protokoll), Regeln speichern/einlesen, Rückgängig | ✓ bestanden |
