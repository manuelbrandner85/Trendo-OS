# Bericht T-0630: Team und Engine automatisch

**Auftrag:** `tasks/T-0630-auto-team.md`.
**Branch/Worktree:** `claude/T-0630-auto-team`, Basis a3a361b2d, `C:\Users\<user>\TrendoEntwicklung\wt-T-0620-web-ki`.
**Status:** Gebaut und getestet. Erste QA-Runde nicht bestanden, Befunde bearbeitet. Nicht in `main`, kein Release.

## Was gebaut wurde

- `GameTools` (neu): `Decide`, `Line`, `Brief`. Die KI liefert `engine`, `engineReason` und `blender`. Gespeichert wird nur diese Wahl der KI. Ohne sie entscheiden Regeln bei jedem Blick neu nach Umfang, Stil, Plattform und dem, was gerade installiert ist. Heruntergeladen wird nichts.
- Eine gültige, installierte Wahl der KI gilt. Fehlt die gewählte Engine, nimmt Trendo die nächste vorhandene und sagt das. Bei Browser-Umfang ist der Browser immer der Rückfall. Ändert jemand den Umfang von Hand, verfällt die alte Wahl.
- `Route`: Das, was Trendos eigener Bau kann. Godot und Unity nur beim Plattformer und nur, wenn installiert, sonst Browser oder Unreal nach Umfang. Weicht die Route von der Wahl ab, sagt die Notiz das.
- `GameBuild`: Beim Plattformer entsteht das gewählte Godot- oder Unity-Projekt einmal mit. Besteht es schon, bleibt es unverändert und Trendo sagt das. Ein Fehler in diesem Schritt bricht Bauen und Spielen nicht ab.
- Die Zeile `game-tool-choice` und die Vorschau zeigen, was tatsächlich gilt.
- Fachleute: Modus „Automatisch“ ist Standard, 2 bis 6 Personen nach Auftrag. Stichwörter nur am Wortanfang, kurze nur als ganzes Wort. Im Automatikmodus verdrängen Engine-Spezialisten ohne Treffer nicht die Standardrollen.
- Studio-Team: `StudioTeams.Pick` und `Brief` geben der lokalen KI im Programm-Assistenten die passenden Rollen mit, Füllwörter zählen nicht. `ClaudeTeamRule` weist Claude an, die Agenten ohne Rückfrage einzusetzen, nur auf den beiden Wegen, die sie vorher exportieren.
- MCP-Server für Claude richten sich im Spiele-Studio nach der gewählten Engine.
- Die Suche nach Engines läuft beim Zeichnen höchstens alle 30 Sekunden, Fehler dabei stören das Zeichnen nicht.

## Kommandos

```
dotnet test Trendo.sln -c Release --nologo
python -m unittest discover -s tests/claude_handoff
```

## Testergebnis

Wird nach dem Volltest des letzten Stands ergänzt.

## QA-Prüfung

Runde 1, nicht bestanden (15 Befunde). Die drei schweren: zweites „Bauen“ brach ab, Regelentscheidungen froren ein, `blender:false` blieb dauerhaft.

Bearbeitet: 1 bis 12, 13 (ungültige Engine, alter Grund), 14, 15 teilweise.

Nicht geändert: Der Status im Programm und die Überschrift im Bereich „Bauen“ nennen weiter Browser oder Unreal nach Umfang, weil das die Route beschreibt.

## Was NICHT geprüft wurde

- Kein Lauf mit echtem Claude und keiner mit einem echten lokalen Modell, alle KI-Antworten sind nachgebildet. Ob Modelle die Engine sinnvoll wählen, ist offen.
- Der Claude-Weg selbst: Dort läuft keine Übernahme aus der Antwort, Claude arbeitet direkt im Projekt.
- Die App wurde nicht im Fenster bedient.
- Kein echter Lauf einer Engine; Godot und Unity sind in den Tests nachgebildet.
- Die Laufzeit der Engine-Suche ist nicht gemessen.

## Offene Punkte und Grenzen

- Für 3D-Spiele in Godot oder Unity gibt es ohne Claude keinen eigenen Bau. Dann entsteht ein Unreal-Projekt, und die Notiz sagt das.
- Ein schon angelegtes Engine-Projekt wird beim erneuten Bauen nicht aktualisiert.
- Die lokale KI bekommt die Rollen nur im Programm-Assistenten und im Spiele-Prompt. Festgelegte Studio-Abläufe ohne KI-Planung sind unverändert.
- Die Rollenwahl für die lokale KI ist ein Wortabgleich, kein Verständnis des Auftrags.
- Der Team-Lauf mit Ablaufplan und „KI einsetzen“ bleiben eigene, manuell startbare Wege.
