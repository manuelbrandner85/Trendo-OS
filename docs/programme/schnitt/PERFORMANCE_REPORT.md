# Videoschnitt – Leistung

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `schnitt` · Stand: 0.9.36 (Stand 20e850a)

Kein eigener Leistungsmesslauf für dieses Programm. Zielhardware und Budgets sind noch nicht festgelegt – Messung offen.

Tests mit Leistungsbezug:
- S3-09 Videoschnitt: Sprachaufnahme + Wortzeiten → Mundbilder (JSON, Moho), Prüfung OK, Vergleichsvorschau (echtes FFmpeg) in die Zeitleiste; Gesichtsvideo 150 ms versetzt → FAIL, nichts eingefügt; fehlende Aufnahme → FAIL
- S3-18/S3-20 Videoschnitt: „Film ausliefern“ legt einen Ordner mit Film, Stems, EDL, OTIO, Cue-Liste und Prüfbericht an; fehlendes Medium wird gemeldet statt verschleiert
- Vorschau V1: Ohne Bildquelle (Tests, Abschottung) bleibt die bisherige Wiedergabe; schnelleres Tempo wechselt dorthin
