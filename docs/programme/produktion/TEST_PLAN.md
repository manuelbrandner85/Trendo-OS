# Produktion – Testplan

Studio: Großprojekte umsetzen (X-01) · Bereich: KI & Automatik · Seite: `produktion` · Stand: 0.9.114 (Stand 99cbb5c)

15 Tests sprechen dieses Programm an.

- Gegenprüfung 1: übergeben → Ergebnis → Rückgängig → Rückgängig → Wiederholen ×2 – Tafel, uebergaben.json und Dateien stimmen bei jedem Schritt überein
- Gegenprüfung 2: eine kaputte Tafel in einem anderen Projekt stört den Export eines normalen Dokuments nicht
- Gegenprüfung 3: erneuter Export wird neue Fassung (alte bleibt als Beleg), „Karte wieder öffnen“ übergibt erneut
- Gegenprüfung 5: Alt+↓ in der aufgeklappten Studio-Auswahl sortiert die Kette nicht um
- Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme
- Großprojekt-Kette frei: Vorlage bekommt Studios, Rechtsklick und Alt+↑ sortieren um, Strg+Enter übergibt, abgelegte Datei wird Ergebnis
- Großprojekt: nach einem Neustart kommt der Export des wieder geöffneten Dokuments an die Karte zurück
- Kanban: Großprojekt aus Ablauf-Vorlage, Karte in „Läuft“ ziehen, Rolle und Fortschritt (100 % = Fertig), Beleg mit Prüfsumme, neue Karte, Papierkorb und zurück; Abläufe bleiben als Reiter
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0717 Produktion: Angaben eines Ablaufs – Rückgängig/Wiederholen, Notsicherung, Karte (auch im Reiter Tafel), Zurückholen
- T-0717 Produktion: Tafel – Getipptes wird nach der Tipp-Pause gespeichert (vorher erst beim Verlassen des Felds), Rückgängig/Wiederholen (vorher keins), nichts verloren nach dem Schließen
- X-01 (UI): Ohne Angaben gesperrt; Plan freigeben → Ja → alle Schritte ✓
- X-01: Fehlende Angaben werden genannt; Fehler hält die Kette an und steht im Protokoll
- X-01: Idee → Bauplan → Webseite → Schrittliste läuft durch, Protokoll geschrieben
- X-01: Video-Paket mit echtem FFmpeg und 3D-Schriftzug mit echtem Blender
