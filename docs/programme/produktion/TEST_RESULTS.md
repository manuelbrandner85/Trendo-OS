# Produktion – Testergebnisse

Studio: Großprojekte umsetzen (X-01) · Bereich: KI & Automatik · Seite: `produktion` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 15 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Gegenprüfung 1: übergeben → Ergebnis → Rückgängig → Rückgängig → Wiederholen ×2 – Tafel, uebergaben.json und Dateien stimmen bei jedem Schritt überein | ✓ bestanden |
| Gegenprüfung 2: eine kaputte Tafel in einem anderen Projekt stört den Export eines normalen Dokuments nicht | ✓ bestanden |
| Gegenprüfung 3: erneuter Export wird neue Fassung (alte bleibt als Beleg), „Karte wieder öffnen“ übergibt erneut | ✓ bestanden |
| Gegenprüfung 5: Alt+↓ in der aufgeklappten Studio-Auswahl sortiert die Kette nicht um | ✓ bestanden |
| Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme | ✓ bestanden |
| Großprojekt-Kette frei: Vorlage bekommt Studios, Rechtsklick und Alt+↑ sortieren um, Strg+Enter übergibt, abgelegte Datei wird Ergebnis | ✓ bestanden |
| Großprojekt: nach einem Neustart kommt der Export des wieder geöffneten Dokuments an die Karte zurück | ✓ bestanden |
| Kanban: Großprojekt aus Ablauf-Vorlage, Karte in „Läuft“ ziehen, Rolle und Fortschritt (100 % = Fertig), Beleg mit Prüfsumme, neue Karte, Papierkorb und zurück; Abläufe bleiben als Reiter | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0717 Produktion: Angaben eines Ablaufs – Rückgängig/Wiederholen, Notsicherung, Karte (auch im Reiter Tafel), Zurückholen | ✓ bestanden |
| T-0717 Produktion: Tafel – Getipptes wird nach der Tipp-Pause gespeichert (vorher erst beim Verlassen des Felds), Rückgängig/Wiederholen (vorher keins), nichts verloren nach dem Schließen | ✓ bestanden |
| X-01 (UI): Ohne Angaben gesperrt; Plan freigeben → Ja → alle Schritte ✓ | ✓ bestanden |
| X-01: Fehlende Angaben werden genannt; Fehler hält die Kette an und steht im Protokoll | ✓ bestanden |
| X-01: Idee → Bauplan → Webseite → Schrittliste läuft durch, Protokoll geschrieben | ✓ bestanden |
| X-01: Video-Paket mit echtem FFmpeg und 3D-Schriftzug mit echtem Blender | ✓ bestanden |
