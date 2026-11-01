# Baukasten – Testergebnisse

Studio: Apps zusammenklicken (X-03) · Bereich: Entwickeln · Seite: `baukasten` · Stand: 0.9.30 (Stand 234e066)

Lauf: `volltest-0.9.30-api.trx, volltest-0.9.30-app.trx, volltest-0.9.30-core.trx`

Bestanden 23 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern | ✓ bestanden |
| Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche | ✓ bestanden |
| App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP | ✓ bestanden |
| Baukasten übernimmt die neue Seite ins Formular | ✓ bestanden |
| Briefseite zeigt den echten Brieftext in der Vorschau | ✓ bestanden |
| Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist | ✓ bestanden |
| Claude im Studio: Knopf in jedem Studio, Fläche rechts; nicht installiert → einrichten, nicht angemeldet → anmelden, Testmodus startet nichts | ✓ bestanden |
| Claude im Studio: echtes Terminal im Projektordner, Auftrag als Eingabe, Fläche bleibt beim Neuzeichnen dieselbe, Fenster schließen beendet sie | ✓ bestanden |
| Startbildschirm bekommt keine Auftragsleiste | ✓ bestanden |
| T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld | ✓ bestanden |
| T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter | ✓ bestanden |
| T-0201: Ordnerfeld öffnet den Ordner-Dialog | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen | ✓ bestanden |
| T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt | ✓ bestanden |
| T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“ | ✓ bestanden |
| T-0316: Ohne Anthropic-Freigabe – kein Abo im Hintergrund (keine Haupt-KI, Läufe gesperrt), Hinweis sichtbar; „Claude Code öffnen“ startet das echte Claude Code im Projektordner | ✓ bestanden |
| T-0316: Studio – „Claude Code“ immer in der Auftragsleiste; „Im eigenen Fenster“ öffnet Claude Code mit der Rolle des Studios | ✓ bestanden |
| T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Token erzeugen“, „Claude Code öffnen“ starten nie ein echtes Programm | ✓ bestanden |
| Trendo-Agent: Los → eine Rückfrage als Knopf → Umsetzen, Prüfung findet Fehler, bessert selbst nach → „Behalten?“; Nein stellt alles wieder her, Ja behält | ✓ bestanden |
| X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen | ✓ bestanden |
| X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden | ✓ bestanden |
| X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit | ✓ bestanden |
