# T-0741 – Belege: KI-Team mit Stopp, Fortschritt je Rolle, Werkzeugen

Stand 17.11.2026, Zweig `claude/T-0741-ki-team`. Alle Dateien hier mit LF-Zeilenenden, `.gitattributes` (`* -text`) hält die Bytes unverändert;
`SHA256.txt` passt auf die Commit-Blobs. Pfade mit Benutzernamen sind durch `<user>` ersetzt.

## Echtlauf (nichts ersetzt außer der Fensterdarstellung – headless)

Test `Trendo.App.Tests.KiTeamEchtlauf_Ui` (läuft nur mit `TRENDO_TEAM_ECHT=<Belegordner>`), echtes Ollama auf 127.0.0.1:11434, Modell `qwen3:14b`.
Bedienung über echte Klicks auf der Seite „KI-Team einsetzen“: Projekt wählen, Ziel eintragen, „Team: Text schreiben & prüfen“, sobald der Planer fertig ist
und der Texter arbeitet „■ Stopp“, dann „Zwischenstand speichern“, dann „Team-Lauf fortsetzen“ bis fertig. Dauer 121 s.

| Datei | Inhalt |
|---|---|
| `team-ablauf.log` | Zeitlinie: jede Fortschrittsänderung je Rolle (wartet/arbeitet/fertig/gestoppt mit Zwischenergebnis), Klicks, Meldungen, Bildschirmzeilen vor/nach Stopp und am Ende |
| `team-stand.json` | gespeicherter Lauf-Stand (`ki/team-laeufe/<id>.json`) am Ende: alle Rollen fertig, Texter Runde 2, Planer Runde 1 |
| `team-zwischenstand-nach-stopp.md` | die über „Zwischenstand speichern“ erzeugte Datei (Planer fertig mit Ergebnis, Texter gestoppt, Lektor wartet) |
| `uebergabe-*.md` | Übergabe-Dateien im Projekt (`team/<lauf>/`): 01 Planer, 02 Texter (gestoppt, „Bisher erarbeitet“), 03 Texter (nach Fortsetzen), 04 Lektor, Zwischenstand |
| `einladung.md` | vom Texter mit dem Werkzeug „datei.schreiben“ angelegte Datei |
| `agenten-protokolle.md` | Protokoll jedes Agenten-Laufs (Werkzeugaufrufe `datei.lesen`/`datei.schreiben`, Ergebnisse, Stopp) |
| `erstversuch-planer-grenze.md` | ehrlich: erster Echtlauf vor der Anpassung der Rollen-Anweisungen – der Planer (ohne Schreibrecht) blieb an der Schrittgrenze hängen |

Ergebnis: Stopp griff im Texter (Schritt 1), der fertige Planer blieb erhalten, nach dem Fortsetzen lief der Planer **nicht** noch einmal (Runden 1),
Texter und Lektor arbeiteten mit den Übergabe-Dateien weiter, Team „fertig“.

Ehrlich: Der Stopp kam 0,6 s nach dem Start des Texters – der gestoppte Texter hatte noch nichts geschrieben („Bisher erarbeitet: noch nichts“);
dass Geschriebenes nach einem Stopp bleibt, belegen die Tests mit ersetztem Modell (`AgentTeamT0741Tests`, `KiTeamT0741_Ui`).
Ein zweiter Versuch zwischendurch scheiterte ohne Protokoll (Datenordner des Testlaufs war schon gelöscht; Meldung „Eine Rolle hat nicht geklappt (KI nicht erreichbar?)“,
parallel liefen andere Ollama-Läufe). Danach schreibt der Echtlauf-Test seinen Beleg immer, auch beim Scheitern.

Nicht echt geprüft: „im Internet nachlesen“ mit echtem Netz (in den Tests ersetzt), Claude als Modell des Teams.

## Tests (ERSETZT: das Modell und der Internet-Abruf)

- `rohlog-core-tests.txt` – Core-Testklassen, darunter `AgentTeamT0741Tests` (4): Fortschritt je Rolle; Stopp mitten in Rolle 2 → Erarbeitetes bleibt,
  Zwischenstand-Datei, Fortsetzen nach Neustart ohne Wiederholung des Planers; Rechercheur: „Nein“ blockiert ohne Abruf (Prüfprotokoll `execution.blocked`),
  „Ja“ ruft genau einmal ab; ohne Person blockiert; unterbrochener Lauf gilt als gestoppt und ist fortsetzbar.
- `rohlog-app-tests.txt` – UI-Testklassen, darunter `KiTeamT0741_Ui` (2): echte Klicks Start → Fortschritt → Stopp → Zwischenstand → Fortsetzen → fertig;
  Freigabe-Karte „Zustimmen“ vor dem Internet-Abruf, Ergebnis in der Übergabe-Datei.
