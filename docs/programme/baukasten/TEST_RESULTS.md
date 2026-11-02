# Baukasten – Testergebnisse

Studio: Apps zusammenklicken (X-03) · Bereich: Entwickeln · Seite: `baukasten` · Stand: 0.9.36 (Stand 2e53f9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 28 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern | ✓ bestanden |
| Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche | ✓ bestanden |
| App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP | ✓ bestanden |
| Baukasten übernimmt die neue Seite ins Formular | ✓ bestanden |
| Briefseite zeigt den echten Brieftext in der Vorschau | ✓ bestanden |
| Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist | ✓ bestanden |
| Claude im Studio: Fenster schließen schließt Claude | ✓ bestanden |
| Claude im Studio: wie Claude im Web – claude.ai im ganzen Fenster, kein Terminal; anmelden, Projekte des Studios anlegen, fortsetzen, entfernen | ✓ bestanden |
| Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html | ✓ bestanden |
| Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text | ✓ bestanden |
| Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio | ✓ bestanden |
| Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude | ✓ bestanden |
| Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb | ✓ bestanden |
| Claude-Regeln 01.11.2026: Claude Code aus Trendo erlaubt (eigene Anmeldung je Nutzer) – kein Sperr-Hinweis; „Claude Code öffnen“ startet das echte Claude Code im Projektordner | ✓ bestanden |
| Claude-Regeln 01.11.2026: Trendo speichert keine Claude-Tokens – ein alter wird beim Start gelöscht, kein Token-Feld mehr | ✓ bestanden |
| Startbildschirm bekommt keine Auftragsleiste | ✓ bestanden |
| T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld | ✓ bestanden |
| T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter | ✓ bestanden |
| T-0201: Ordnerfeld öffnet den Ordner-Dialog | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt | ✓ bestanden |
| T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“ | ✓ bestanden |
| T-0316: Studio – „✳ Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal | ✓ bestanden |
| T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Claude Code öffnen“ starten nie ein echtes Programm | ✓ bestanden |
| Trendo-Agent: Los → eine Rückfrage als Knopf → Umsetzen, Prüfung findet Fehler, bessert selbst nach → „Behalten?“; Nein stellt alles wieder her, Ja behält | ✓ bestanden |
| X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen | ✓ bestanden |
| X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden | ✓ bestanden |
| X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit | ✓ bestanden |
