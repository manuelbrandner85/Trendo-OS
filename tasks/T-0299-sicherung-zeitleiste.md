# T-0299 – Sicherung mit Zeitleiste

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Sicherung mit Zeitleiste“).
- Vorher: Liste der Sicherungen, nur „alles wiederherstellen“.
- Jetzt (Seite `sicherung`, Kern `Backup`):
  - Zeitleiste: jede Sicherung als Punkt auf einer Zeitachse, die gewählte gold hervorgehoben, Datum und Größe als Tipp.
  - Vergleich mit heute: Dateien der Sicherung mit Status geändert / seither gelöscht / seither neu / gleich (Byte-Vergleich), Filter und Suche, Zusammenfassung.
  - Vorschau der alten Fassung (Text, Bild).
  - Einzelne Datei zurückholen – erst nach „Ja“; die heutige Fassung wird vorher unter `backups/einzeln/<Zeit>/` aufgehoben, das Protokoll (`audit/`) nie, kein Pfad außerhalb der Daten.
  - „Alles wiederherstellen“ bleibt wie bisher (mit Rückfrage und Vorab-Sicherung).
- Tests: `BackupTimelineTests` (Kern: Vergleich, Vorschau, Einzeldatei, Schutz von Protokoll und Pfaden), `BackupTimeline_Ui` (echte Klicks: Punkt, Datei, Vorschau, Nein ändert nichts, Ja holt zurück); `BackupUiTests`, `BackupRestoreTests` grün.
- Nicht geprüft: Aussehen per Render-Bild (Render-Werkzeug legt keine Sicherungen an).
