# T-0220: KI-Laden, lokale KI und kostenlose Online-Dienste

**Ziel:** Beim Einrichten eine lokale KI passend zum PC vorbereiten, im KI-Laden freie Modelle holen und entfernen, kostenlose Online-Dienste nur nach ausdrücklicher Erlaubnis nutzen und für jede Aufgabe die beste freie KI wählen.
**Branch:** cursor/ki-laden-lokal-online-117e
**Dateien/Bereich:** vorhandener KI-Dienst (`IntelligenceCore.AskAsync`), KI-Katalog, Abhängigkeiten, Secret-Store, Einrichtung, Einstellungen. Kein zweiter Router, kein zweites Schlüssel-Fenster.

## Was gebaut ist

- Hardware-Stufen klein, mittel und groß. Schwache PCs bleiben bei kleinen Modellen, starke bekommen größere. Nur frei nutzbare Lizenzen. Vor dem Laden wird der freie Platz geprüft. Abbrechen und Weitermachen stehen in `ki/downloads.json`.
- Ollama auf Linux als Archiv nur für den Benutzer (aktuelle Datei `.tar.zst`, ältere `.tgz` bleibt gültig). Der von Trendo gestartete Dienst bekommt `OLLAMA_HOST=127.0.0.1:11434` und `OLLAMA_NO_CLOUD=1`. Windows bleibt bei winget. llama.cpp wird nicht selbst geladen; die Oberfläche sagt das.
- KI-Laden in den Einstellungen: Größe, Zweck, Passung, Empfehlung, Holen, Entfernen. Liste geprüft am 22.10.2026, keine Live-Abfrage.
- Kostenlose Online-Dienste: Google Gemini, Groq, OpenRouter (Modelle mit „free“), Mistral. Beide Häkchen nötig. „Schlüssel holen“ öffnet die offizielle Seite. Der Schlüssel liegt im Secret-Store, nicht im Code und nicht in `ki/online.json`. GitHub Models, Cerebras und Hugging Face Inference stehen als nicht kostenlos bzw. abgeschaltet daneben. xAI bleibt optional und kostet Geld, die automatische Wahl nutzt es nicht.
- Automatische Wahl: lokal zuerst, danach freigeschaltete Online-Dienste nach Güte. Bei leerem Gratis-Kontingent springt Trendo weiter. Anzeige „Gerade aktiv: …“. Die Prompt-Leiste fragt weiter nur lokal.

## Testergebnis

- `dotnet test tests/Trendo.Api.Tests -c Release`: 15 bestanden, 0 Fehler, 0 übersprungen.
- `dotnet test tests/Trendo.Core.Tests -c Release`: 526 bestanden, 10 übersprungen (nur Windows), 0 Fehler.
- `dotnet test tests/Trendo.App.Tests -c Release`: 215 bestanden, 0 Fehler.
- Lauf danach erneut als Gesamtlösung, gleicher Stand: Api 15, Core 526/10, App 215, Exit 0. Dauer der App-Tests 8 min 32 s.

Echter Lauf auf dieser Linux-VM, über `DependencyManager` und danach `IntelligenceCore.AskAsync`:

- Ollama 0.40.8 aus `ollama-linux-amd64.tar.zst`, entpackt mit zstd, Datei unter dem Benutzerordner der Sitzung.
- Dienstprotokoll des ersten Starts: `Listening on 127.0.0.1:11434`. In `/proc/net/tcp` nur `0100007F:2CAA` (127.0.0.1:11434, Zustand LISTEN), kein Eintrag auf 0.0.0.0. Dieser erste Start hatte die Cloud noch an (`OLLAMA_NO_CLOUD:false`), weil die Prüfdatei nur die Host-Variable setzte.
- Danach derselbe Dienst neu mit `OLLAMA_NO_CLOUD=1`: Protokoll `Ollama cloud disabled: true`, weiter nur 127.0.0.1:11434. Die Produktstarts setzen Host und Cloud-Aus über `LoopbackAi.BindLocal`.
- `qwen2.5:0.5b` (397 MB) geladen, Manifest erkannt. Frage „Antworte mit genau einem Wort: Hallo“ über `AskAsync`: `modell=qwen2.5:0.5b`, `lokal=True`. Der zweite Aufruf der Prüfdatei, gegen den Dienst ohne Cloud, endete mit Exit 0 und dem Text „Hallo! Wie kann ich Ihnen heute helfen?“. Die Shell des ersten Laufs schrieb EXIT:1, obwohl die Antwort schon im Protokoll stand; der direkte zweite Lauf ist der gültige Exit-Code.

## Offene Punkte

- Der Katalog ist die geprüfte Liste vom 22.10.2026, kein automatischer Abruf von Ollama oder Hugging Face.
- Linux braucht zum Entpacken `zstd`. Fehlt es, bricht die Installation mit einem klaren Satz ab und lädt nichts.
- Die vier empfohlenen Modelle für diesen PC (etwa 6,7 GB) wurden nicht geladen. Geladen ist nur Qwen2.5 0.5B.
- `requirements.yaml` ist unverändert. Nichts ist als TESTED, PC_VERIFIED oder QA-019 markiert.

## Nicht geprüft

- Kein echter Download und keine winget-Installation auf einem Windows-PC. Der Windows-Zweig startet Ollama weiter über winget.
- Kein Windows-Anmeldeinformations-Manager. Auf Linux liegt der Schlüssel in einer Datei nur für den Benutzer, Modus 0600, und die Sicherung lässt sie aus.
- Kein llama.cpp-Download und kein echtes GGUF. Der Hinweis in der Oberfläche ist absichtlich so.
- Keine echten Schlüssel bei Gemini, Groq, OpenRouter oder Mistral. Die Prüfung der Online-Dienste lief mit Fakes, einschließlich der Antwort 429 und 402.
- Abbruch und Fortsetzen eines echten Downloads nur mit Fake, nicht an einem abgebrochenen 397-MB-Lauf.
- Keine Grafikkarte auf dieser VM, daher keine echte VRAM-Messung. Die Stufe „groß“ ist nur im Test mit eingesetzten Werten geprüft.
- Kein Velopack-Setup.exe, keine Codesignatur, kein QA-017, kein QA-019.
- Die Oberfläche des KI-Ladens und der Einrichtung ist auf dieser VM als Bild gespeichert, nicht auf einem Windows-PC.
