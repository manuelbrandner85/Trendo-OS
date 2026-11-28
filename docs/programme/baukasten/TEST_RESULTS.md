# Baukasten – Testergebnisse

Studio: Apps zusammenklicken (X-03) · Bereich: Entwickeln · Seite: `baukasten` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 21 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP | ✓ bestanden |
| Baukasten übernimmt die neue Seite ins Formular | ✓ bestanden |
| Claude im Studio: Fenster schließen schließt Claude | ✓ bestanden |
| Claude im Studio: wie Claude im Web – claude.ai im ganzen Fenster, kein Terminal; anmelden, Projekte des Studios anlegen, fortsetzen, entfernen | ✓ bestanden |
| Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html | ✓ bestanden |
| Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude | ✓ bestanden |
| Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb | ✓ bestanden |
| Claude-Chat: „GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „Netlify“ nur im Web-Studio, veröffentlicht nach Ja | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0316: Studio – „Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal | ✓ bestanden |
| T-0716 Baukasten: Farbe (gespeichert) + Text getippt – Rückgängig/Wiederholen der Hülle, Notsicherung, Sperre, Karte, Zurückholen in das Feld, Enter speichert | ✓ bestanden |
| T-0716 Baukasten: Rückgängig hat jetzt die Grenze der Hülle (100 Schritte) – vorher eigener Stapel ohne Grenze | ✓ bestanden |
| T-0716 Menüleiste (T-0710) auf Baukasten und Anlagen-Monitor (Fahrzeugbuch mit T-0720 entfernt): „Bearbeiten“ ruft Rückgängig/Wiederholen der Hülle | ✓ bestanden |
| T-0718 UI Baukasten: „Veröffentlichen …“ fragt im echten Dialog; nach Ja liegt die Seite im Zielordner, Prüfung grün | ✓ bestanden |
| T-0765 (UI): KI „seite.schreiben“ ändert die App im Projekt – im Baukasten sichtbar und mit einem Klick rückgängig, Seiten und Daten bleiben | ✓ bestanden |
| T-0765 (UI): erster Schritt → Vorlage Einkaufsliste → Seite wechseln → Doppelklick-Text → Rückgängig/Wiederholen → Seiten verwalten → Knopf-Ziel → Felder → Export → ZIP wieder öffnen | ✓ bestanden |
| T-0765 Gegenprüfung (UI): eine goldene Hauptaktion je Zustand, Speicher-Hinweis, zu große ZIP wird sichtbar abgelehnt, Import überschreibt nichts und schreibt bei neuerer seite.json gar nichts | ✓ bestanden |
| Trendo-Agent: Los → eine Rückfrage als Knopf → Umsetzen, Prüfung findet Fehler, bessert selbst nach → „Behalten?“; Nein stellt alles wieder her, Ja behält | ✓ bestanden |
| X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen | ✓ bestanden |
| X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden | ✓ bestanden |
| X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit | ✓ bestanden |
