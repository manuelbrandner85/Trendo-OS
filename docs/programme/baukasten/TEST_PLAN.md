# Baukasten – Testplan

Studio: Apps zusammenklicken (X-03) · Bereich: Entwickeln · Seite: `baukasten` · Stand: 0.9.36 (Stand 208ed9e)

30 Tests sprechen dieses Programm an.

- 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern
- Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche
- App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP
- Baukasten übernimmt die neue Seite ins Formular
- Briefseite zeigt den echten Brieftext in der Vorschau
- Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist
- Claude im Studio: Fenster schließen schließt Claude
- Claude im Studio: wie Claude im Web – claude.ai im ganzen Fenster, kein Terminal; anmelden, Projekte des Studios anlegen, fortsetzen, entfernen
- Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html
- Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text
- Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio
- Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude
- Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb
- Claude-Chat: „⬆ GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „🌐 Netlify“ nur im Web-Studio, veröffentlicht nach Ja
- Claude-Regeln 01.11.2026: Claude Code aus Trendo erlaubt (eigene Anmeldung je Nutzer) – kein Sperr-Hinweis; „Claude Code öffnen“ startet das echte Claude Code im Projektordner
- Claude-Regeln 01.11.2026: Trendo speichert keine Claude-Tokens – ein alter wird beim Start gelöscht, kein Token-Feld mehr
- Startbildschirm bekommt keine Auftragsleiste
- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0316: Studio – „✳ Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal
- T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Claude Code öffnen“ starten nie ein echtes Programm
- Trendo-Agent: Los → eine Rückfrage als Knopf → Umsetzen, Prüfung findet Fehler, bessert selbst nach → „Behalten?“; Nein stellt alles wieder her, Ja behält
- Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert
- X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen
- X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden
- X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit
