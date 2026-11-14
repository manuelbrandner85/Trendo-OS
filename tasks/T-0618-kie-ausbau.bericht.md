# Bericht T-0618: kie.ai – Veo 3.1, Geräusche, eigene Stimme

**Auftrag:** offene Punkte aus T-0612 („alles Offene durchziehen“, 14.11.2026). S2 (eigene Stimme für Suno) war am 13.11.2026 freigegeben, aber mangels belegter Schnittstelle nicht gebaut; Veo 3.1 und Soundeffekte fehlten wegen widersprüchlicher Quellen.
**Zweig:** `claude/T-0618-kie-ausbau` von `main`.

## Grundlage

Die offizielle Beschreibung docs.kie.ai war diesmal im eingebauten Browser lesbar (Stand 14.11.2026): Veo 3.1 (`/api/v1/jobs/createTask`, Modell `veo-3-1`, Stufe `input.model` veo3_fast/veo3_lite/veo3), Suno-Geräusche (`ai-music-api/sounds`, V6, `sound_loop`, `sound_tempo`, Beschreibung bis 500 Zeichen), Suno Voice (`ai-music-api/validation-phrase` → Prüfsatz in `resultJson.data.verify_phrase_text`, Auftrag bleibt „generating“; `ai-music-api/create-voice` mit `task_id`/`verify_url`; Stimm-ID = Auftrag des Prüfsatzes; im Lied `persona_id` + `persona_model: voice_persona`; beide Schritte kostenlos).

## Inhalt

- `src/Trendo.Core/Kie/Kie.cs`: Modelle Veo 3.1 Fast (Text oder Start-/Endbild, 16:9/9:16, mit Ton) und Suno Geräusche; neue Art „Geräusche & Klänge“ (Musik-, Film-, Podcast-Studio und Standard); Regeln (500 Zeichen, keine fremden Stimmen), Dateiprüfung, Endung; `KieVoice` (Eingaben mit Grenzen 5–60 s, Prüfsatz lesen); Lied mit eigener Stimme nur mit Gesang; `KieTask.ResultJson`; Modellabgleich kennt die Doku-Adressen.
- `src/Trendo.App/MainWindow.KieVoice.cs`: Ablauf „Eigene Stimme“ – nur nach Bestätigung „meine eigene Stimme“, Upload nur eigener Audiodateien, Prüfsatz holen (wartet höchstens 3 min), Prüfsatz-Aufnahme, Stimme anlegen (wartet auf Prüfung), Stimm-ID im verschlüsselten Speicher der Person, „Stimme entfernen“; Protokolleinträge.
- `src/Trendo.App/MainWindow.Kie.cs`: Formular für Geräusche (Beschreibung, Loop, Tempo), im Musik-Formular Abschnitt „Eigene Stimme“ (anlegen in vier Schritten oder „Mit meiner eigenen Stimme singen“).
- Veo-Preis ist vorab nicht belegt → wie bei anderen Modellen Zusatzhäkchen „trotz unbekanntem Preis“.

## Tests

- Neu `tests/Trendo.Core.Tests/KieAusbauTests.cs` (4), angepasst `KieTests.Models` (Modellabgleich).
- Neu `tests/Trendo.App.Tests/KieVoiceUiTests.cs` (2): ohne Bestätigung kein Netz, Prüfsatz, Anlage, Speichern nur bei bestandener Prüfung, Entfernen, falsche Dateiart.
- Core gesamt: 2455 bestanden, 5 übersprungen, 0 Fehler. App: Kie_Ui + KieVoice_Ui 5 bestanden.
- Volltest: gemeinsam mit den übrigen Zweigen am Ende.

## Nicht geprüft

- Kein echter Auftrag (braucht den kie.ai-Schlüssel der Person); Ablauf mit nachgebildetem kie.ai geprüft.
- Aufnahme direkt in Trendo (Mikrofon) ist nicht eingebaut – die Person wählt eine Aufnahme-Datei.
