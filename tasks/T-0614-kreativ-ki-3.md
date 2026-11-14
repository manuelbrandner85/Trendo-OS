# T-0614: Etappe „Kreativ & KI“ – Teil 3

**Freigabe:** 79 neue Studios, Ja im Chat am 09.11.2026; „alles Offene durchziehen“ am 14.11.2026.
**Zweig:** `claude/T-0614-kreativ-ki-3`

| Studio | ID | Programm | Bereich |
|---|---|---|---|
| Videos mit KI | STUDIO-VIDEOAI-01 | `video-ki-programm` | KI & Automatik |
| KI-Agenten bauen | STUDIO-AGENTS-01 | `agenten-bauen-programm` | KI & Automatik |
| KI-Modelle vergleichen | STUDIO-MODELS-01 | `modelle-vergleichen-programm` | KI & Automatik |
| Eigene KI trainieren | STUDIO-TRAIN-01 | `ki-trainieren-programm` | KI & Automatik |

Vorhandenes wird genutzt statt doppelt gebaut: lokale Bild-KI wie „Bilder mit KI“ (`CoverModel`, `ImageAiKit`, stable-diffusion.cpp), FFmpeg (`FfmpegPath`), KI-Quellen (`DefaultKiBackends`, `KiBackendsOverride`) über den gemeinsamen Kern `IntelligenceCore.AskAsync`, eigene Rollen des KI-Teams (`AgentCenter.AddCustomRole`), Claude über den Zugang der Person (`ClaudeFromStudioBar`), kie.ai-Leiste (`KieModels.ForStudio`).

## Grenzen

- Videos: höchstens 60 Sekunden, keine echten Personen/Marken, Herkunftsnachweis je Bild und Video, FFmpeg mit „-n“ (nie überschreiben). Echte KI-Clips nur über das eigene kie.ai-Konto.
- Agenten: feste, sichere Werkzeugliste (mindestens eins, „Befehle ausführen“ nur mit Warnung), keine Schlüssel im Text, Speichern erst nach Ordnerwahl, nie überschreiben.
- Modelle: nur eingerichtete Quellen; fehlende Quelle wird gemeldet, kein Absturz. Blind bewerten, erst danach aufdecken.
- Training: echtes lokales Modell (Naive Bayes), feste Prüfmenge, nichts verlässt den Rechner.
