# Bericht T-0225: KI automatisch einrichten, KI-Dienste mit Schlüssel

**Branch/Worktree:** `claude/T-02xx`, Worktree `integration`

## Umgesetzt
- **Freie Modelle automatisch:** `KiCatalog.AutoModels` wählt Modelle mit freier Lizenz (Apache-2.0, MIT), die zum Arbeitsspeicher passen, kleinste zuerst. Nach dem Start lädt Trendo sie ohne Klick nacheinander über die lokale Ollama-Schnittstelle (`/api/pull`), mit Eintrag im Audit-Log (`ki.model.auto.*`). Bei einem PC mit 32 GB sind das Mistral 7B, Qwen2.5 7B und Qwen2.5 Coder 7B, zusammen ca. 13,5 GB. Das kleine Qwen2.5 0.5B liegt schon im Vollpaket.
- **Modelle mit eigenen Lizenzbedingungen** (Qwen2.5 3B „Qwen Research“, Llama 3.2, Gemma 2): Knopf „Lizenz … bestätigen & laden“. Lizenzen mit Zusatzbedingungen nimmt Trendo nicht ungefragt im Namen des Nutzers an.
- **Abschaltbar:** `KiSourceSettings.AutoModels`, Standard „an“. Offline wird nichts geladen.
- **KI-Dienste mit Schlüssel** (`KiCatalog.Cloud`): OpenRouter, Google Gemini, Mistral AI und Hugging Face mit kostenloser Stufe, Claude (Anthropic) und OpenAI kostenpflichtig. Auf „Dienste & Schlüssel“ hat jeder Dienst vier Bausteine:
  - „Schlüssel holen“ öffnet die offizielle Schlüsselseite des Anbieters.
  - Den Schlüssel eintragen.
  - „Prüfen & speichern“: Abfrage `/models`; gespeichert wird im Anmeldeinformationsspeicher, nie im Protokoll.
  - Schalter „Als Ersatz-KI nutzen“: erst mit gültigem Schlüssel bedienbar.
- **Rückfallkette:** Zuerst alle Quellen auf dem PC, danach die eingeschalteten Dienste mit Schlüssel. Ein Dienst wird nur gefragt, wenn die lokale KI nicht antwortet.

## Sicherheit
- `CloudKiBackend` ist die bewusste Ausnahme von `LocalOnly` (CORE-02). Sie gilt nur über HTTPS, nur für genau die Adresse aus dem Katalog, ohne Weiterleitungen, nur eingeschaltet und mit gespeichertem Schlüssel.
- **Gefunden und behoben:** Die KI legt ihren Fragen Ausschnitte aus eigenen Dokumenten bei („Wissen“). Diese gingen sonst mit an den Dienst. Neu: `IRemoteKiBackend` bekommt nur Systemtext, Verlauf und Frage. Das Audit zeigt „extern“, und es werden keine Quellen genannt.
- **Unabhängige Sicherheitsprüfung** (Rolle 03-security-permissions, nur lesend). Die Befunde und was daraus wurde:
  - **hoch – behoben:** Abläufe (Dateien zusammenfassen), E-Mail, Agenten und Aufträge geben Dateiinhalte als *Frage* weiter. Sie wären bei schweigender lokaler KI an den Dienst gegangen.
    - Neu: `KiRequest.AllowRemote`, Standard aus. Nur der freie Chat setzt es. Alle anderen Teile fragen ausschließlich lokal.
    - Nach außen gehen nur Systemtext und die eine Frage, ohne Verlauf und ohne Dokumente.
  - **mittel – behoben:** Verlauf mit lokal erzeugten Wissens-Antworten ging mit hinaus. Entfernt, siehe oben.
  - **mittel – behoben:** Einschalten eines Dienstes und jedes automatische Laden haben jetzt einen Freigabe-Eintrag (`ki.cloud.on`, `ki.pull`).
  - **mittel – behoben, automatisches Laden:**
    - Vorher wird der freie Speicher geprüft (Größe + 5 GB Reserve).
    - Höchstens 3 Versuche je Modell, danach nur noch per Knopf.
    - Schalter „Freie Modelle automatisch laden“ auf der Seite „KI-Quellen“.
  - **mittel – behoben, Kosten:** Kostenpflichtige Dienste nutzen ein festes günstiges Modell (`gpt-4.1-mini`, `claude-haiku-4-5`). Fehlt es, weicht Trendo nicht auf ein anderes aus.
  - **niedrig – behoben:**
    - Die Schlüsselprüfung folgt keinen Weiterleitungen mehr. Für KI-Dienste läuft sie über dieselben Grenzen wie die Nutzung.
    - Port 443 ist Pflicht, kein `//`-Pfad, Antworten sind auf 8 MB begrenzt.
    - Eine Verbindung je Dienst und Schlüssel.
    - Anthropic bekommt zusätzlich `x-api-key`.
    - Die Chat-Seite sagt ehrlich, wann eine Frage den PC verlassen kann, und kennzeichnet Antworten „außerhalb des PCs“.
    - Die Eingabeleiste schaltet ihre KI nicht mehr ab, nur weil ein Dienst eingeschaltet ist; sie fragt Dienste ohnehin nie.
  - **ohne Befund:** Schlüssel landen weder im Protokoll noch in Dateien. Ohne Einschalten und offline wird kein Dienst benutzt. Tests und isolierte Fenster laden nichts.
  - **offen (niedrig):** Gleichzeitiges Speichern von `quellen.json` von zwei Seiten ist nicht gesperrt.

## Tests
- **Core `T0225_Ki` und `T0225_KiWissen`:** 4 Tests (Katalog, Auswahl der Modelle, Einstellungen, Dokumente bleiben lokal).
- **App `T0225_KiUi`:** 3 Tests.
  - Schlüsselseite, Prüfung, Schalter mit Freigabe. Rückfall nur bei schweigender lokaler KI und nur mit `AllowRemote`, ohne Verlauf.
  - Automatisches Laden mit Freigabe; zu wenig Speicher, 3 Fehlversuche, ausgeschaltet und offline: nichts.
  - HTTPS- und Adressgrenze.

## Nicht geprüft
- Echte Schlüssel bei den Anbietern und echtes Laden der großen Modelle, getestet ist mit nachgebildeten Diensten.
- Ob die Modellnamen der Anbieter (z. B. `gemini-2.5-flash`, `mistral-small-latest`, `gpt-4.1-mini`, `claude-haiku-4-5`) noch aktuell sind. Bei kostenlosen Diensten nimmt Trendo dann das erste angebotene Modell, bei kostenpflichtigen keins.

## Testergebnis
`dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler.

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 689 | 1 | 0 | 690 |
| Trendo.App.Tests | 334 | 2 | 0 | 336 (24 min 7 s, Exitcode 0) |
| Pakettests | 8 | – | 0 | 8 |
