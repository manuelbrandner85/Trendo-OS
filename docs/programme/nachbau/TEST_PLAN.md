# Sauberer Nachbau – Testplan

Studio: Programme nachbauen (X-10) · Bereich: Entwickeln · Seite: `nachbau` · Stand: 0.9.36 (Stand 63090ad)

8 Tests sprechen dieses Programm an.

- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung ohne Treffer schreibt Bericht
- X-10: Projekt mit Rollen-Ordnern, Tagebuch, Vergleich zweier Ordner und Prüfbericht
- X-10: Sauberer Nachbau – gleicher Code hat Ähnlichkeit 1, eigener Code mit anderer Struktur niedrig; Vergleich meldet nur Dateien über der Schwelle
- X-10: Ähnlichkeit erkennt kopierten Code trotz Umbenennen, eigener Code bleibt unauffällig
