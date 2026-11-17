# T-0698 – Belege: Agenten laufen wirklich

Stand 16.11.2026, Zweig `claude/T-0698-agenten`.

## Echte Läufe (nichts ersetzt außer der Fensterdarstellung – headless)

Test `Trendo.App.Tests.AgentEchtlauf_Ui` (läuft nur mit `TRENDO_AGENT_ECHT=<Belegordner>`): Agent in „KI-Agenten bauen“ anlegen,
Knopf „Agent starten“ drücken, Agent arbeitet mit den Trendo-Werkzeugen „Dateien im Projekt lesen/schreiben“.
Auftrag: `einkauf.md` mit drei Artikeln anlegen, lesen, vierten Artikel ergänzen, fertig melden.

| Lauf | Modell | Ergebnis | Dauer | Dateien |
|---|---|---|---|---|
| Ollama (127.0.0.1:11434) | `qwen3:14b` (bestes vorhandenes Modell; `qwen2.5:1.5b` nicht genutzt) | Fertig, 4 Schritte, `einkauf.md` = Milch, Brot, Eier, Zucker | 91 s | `ollama-lauf.md`, `ollama-lauf.json`, `ollama-einkauf.md`, `ollama-ergebnis.md`, `ollama-SHA256.txt` |
| Claude Code über das eigene Abo (bereits angemeldet, Trendo meldet nie an) | Standardmodell des Abos, Frage-Modus ohne Schreibwerkzeuge | Fertig, 5 Schritte, `einkauf.md` = Milch, Brot, Eier, Butter | 33 s | `claude-lauf.md`, `claude-lauf.json`, `claude-einkauf.md`, `claude-ergebnis.md`, `claude-SHA256.txt` |

Ehrlich: Ein **Erstversuch** mit Ollama (`ollama-erstversuch-lauf.md`) lief technisch sauber durch (4 Schritte, fertig), aber das Modell hat den
Platzhalter „- Artikel“ aus der mehrdeutigen Zielformulierung wörtlich übernommen. Nach klarerer Formulierung („je Zeile ein echter Artikel, z. B. „- Milch““) stimmte das Ergebnis.

Beim Claude-Lauf ersetzt der Test nur die Sperre der Testumgebung (`ClaudeRunOverride`/`ClaudeStatusOverride`): Er startet das echte
`claude.exe` mit genau den Trendo-Argumenten (`ClaudeCode.Args`, Frage-Modus) und liest den echten Anmeldestand (`claude auth status`).
Die Werkzeuge führt Trendo aus, nicht Claude.

## Tests mit ersetztem Modell (ERSETZT: das Modell gibt vorgegebene Werkzeugaufrufe zurück)

- `rohlog-core-agentrun.txt` – `Trendo.Core.Tests.AgentRunTests`, 13 Tests bestanden: Datei anlegen → lesen → verbessern, Agent nur mit „lesen“ kann nicht schreiben,
  Projektgrenze, Freigabe-Tor blockiert ohne Zustimmung (auch Lauf ohne Person) und führt mit Zustimmung aus, Stopp, Schritt- und Zeitgrenze, Not-Stopp,
  E-Mail nur als Entwurf, Web nur im Lesemodus (Heimnetz abgewiesen), Studio-Aktion über den echten Adapter SPEC-11, Ablauf-Schritt „Agent ausführen“ mit
  Neue-Datei-Auslöser, alte Ablaufdateien lesbar, Team mit Datei-Übergabe/Prüfer-Rückgabe/paralleler Prüfung, Rückgabe-Grenze und Stopp.
- `rohlog-app-agentrun-ui.txt` – `Trendo.App.Tests.AgentRun_Ui`, 5 Tests bestanden: vom Klick „Agent starten“ bis zur echten Datei, Ja/Nein-Freigabe in der
  Lauf-Ansicht, Stopp-Knopf, Ablauf-Baustein „Agent ausführen“, KI-Team mit Datei-Übergabe.

Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt.
