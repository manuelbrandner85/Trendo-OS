# Bericht T-0630: Team und Engine automatisch

**Auftrag:** `tasks/T-0630-auto-team.md`.
**Branch/Worktree:** `claude/T-0630-auto-team`, Basis a3a361b2d, `C:\Users\<user>\TrendoEntwicklung\wt-T-0620-web-ki`.
**Status:** Gebaut und getestet. Nicht in `main`, kein Release, QA-Review steht aus.

## Was gebaut wurde

- `GameTools` (neu): `Decide`, `Apply`, `Line`, `Brief`. Die KI liefert `engine`, `engineReason` und `blender`. Eine gültige, installierte Wahl gilt; sonst entscheiden Regeln nach Umfang, Stil und Plattform. Nicht Installiertes wird nicht gewählt, heruntergeladen wird nichts.
- `Route`: Das, was Trendos eigener Bau kann. Godot und Unity nur beim Plattformer, sonst Browser oder Unreal nach Umfang. Weicht die Route von der Wahl ab, sagt die Notiz das.
- `GameBuild`: Beim Plattformer entsteht das gewählte Godot- oder Unity-Projekt ohne weiteren Klick.
- Spiele-Prompt und „Daraus Spiel machen“ halten die Wahl fest, die Zeile `game-tool-choice` zeigt sie samt Grund.
- Fachleute: Modus „Automatisch“ ist Standard, 2 bis 6 Personen nach Auftrag. Die anderen Modi bleiben wählbar.
- Studio-Team: `StudioTeams.Pick` und `Brief` geben der lokalen KI im Programm-Assistenten die passenden Rollen mit. `ClaudeRoleFor` weist Claude an, die vorhandenen Agenten ohne Rückfrage einzusetzen.

## Kommandos

```
dotnet test Trendo.sln -c Release --nologo
python -m unittest discover -s tests/claude_handoff
```

## Testergebnis (Volltest, Exitcodes 0)

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---|---|---|---|
| Trendo.Api.Tests | 16 | 0 | 0 | 16 |
| Trendo.Core.Tests | 2091 | 5 | 0 | 2096 |
| Trendo.App.Tests | 918 | 4 | 0 | 922 |
| tests/claude_handoff | 9 | 0 | 0 | 9 |

## Was NICHT geprüft wurde

- Kein Lauf mit echtem Claude und keiner mit einem echten lokalen Modell, alle KI-Antworten sind nachgebildet. Ob Modelle die Engine sinnvoll wählen, ist offen.
- Die App wurde nicht im Fenster bedient.
- Kein echter Lauf einer Engine; Godot und Unity sind in den Tests nachgebildet.
- Unabhängiges QA-Review noch nicht erfolgt.

## Offene Punkte und Grenzen

- Für 3D-Spiele in Godot oder Unity gibt es ohne Claude keinen eigenen Bau. Dann entsteht ein Unreal-Projekt, und die Notiz sagt das.
- Die lokale KI bekommt die Rollen nur im Programm-Assistenten und im Spiele-Prompt. Festgelegte Studio-Abläufe ohne KI-Planung sind unverändert.
- Die Rollenwahl für die lokale KI ist ein Wortabgleich, kein Verständnis des Auftrags.
- Der Team-Lauf mit Ablaufplan und „KI einsetzen“ bleiben eigene, manuell startbare Wege.
