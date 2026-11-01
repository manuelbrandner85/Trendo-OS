# Baukasten – Testplan

Studio: Apps zusammenklicken (X-03) · Bereich: Entwickeln · Seite: `baukasten` · Stand: 0.9.28 (Stand 9511c3a)

20 Tests sprechen dieses Programm an.

- 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern
- Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche
- App-Designer: Knopf aus der Leiste auf den ersten Baustein ziehen fügt ihn dort ein, Ziehen in der Vorschau sortiert um, Eigenschaften und Akzentfarbe wirken in der Vorschau, Rückgängig, ZIP
- Baukasten übernimmt die neue Seite ins Formular
- Briefseite zeigt den echten Brieftext in der Vorschau
- Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist
- Startbildschirm bekommt keine Auftragsleiste
- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen
- T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0316: Code-Studio – „In Claude Code öffnen“ in der Auftragsleiste nimmt Auftrag und Studio-Rolle mit; ohne Claude Code kein Knopf
- T-0316: Ohne Anthropic-Freigabe – kein Abo im Hintergrund (keine Haupt-KI, Läufe gesperrt), Hinweis sichtbar; „Claude Code öffnen“ startet das echte Claude Code im Projektordner
- T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Token erzeugen“, „Claude Code öffnen“ starten nie ein echtes Programm
- X-03 (UI): Baustein hinzufügen, verschieben, entfernen, veröffentlichen
- X-03: Bausteine werden zu sicherem HTML, Reihenfolge änderbar, Speichern und Laden
- X-03: Veröffentlichen schreibt index.html und kopiert lokale Bilder mit
