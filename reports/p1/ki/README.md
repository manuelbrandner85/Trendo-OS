# Lokale KI auf dem Windows-PC – 21.10.2026 (Freigabe: „Manuel via Grok Bot chat 16:08“)
| Prüfung | Ergebnis |
|---|---|
| Ollama 0.40.7 pro Benutzer über Trendo-Abhängigkeits-Manager (`winget --scope user`), ohne Admin | ✅ `install-ki.txt` (Freigabe im Approval-Gate) |
| Modell qwen2.5:0.5b (397 MB, Apache-2.0) geladen | ⚠ erster Versuch FAIL (Dienst lief nicht) → Fix: Dienst wird jetzt automatisch gestartet; zweiter Versuch ✅ `ollama-list.txt` |
| `ollama --version` | ✅ 0.40.7 |
| Echte lokale Anfrage an 127.0.0.1:11434, Listener nur 127.0.0.1 | ✅ beantwortet (58 s, Kaltstart) – **inhaltlich falsch** („Hauptstadt … München“) `local-prompt.txt` |
| Trendo OS 0.4.1 (Update per Setup, still): KI-Panel „Verbunden (lokal)“, deutsche Testfrage beantwortet | ✅ `ki-panel.png`, `ki-panel-ausschnitt.png`, `audit-ki.txt` |
Bewertung: Technik funktioniert offline-fähig ohne Cloud/Kosten. Das 0,5B-Modell ist sehr klein; Antworten sind oft fehlerhaft/holprig → größeres freies Modell (z. B. 3B) als Option empfohlen.
Nicht geprüft: Betrieb mit getrenntem Netzwerk (QA-019 bleibt NOT_RUN). Andere Software nicht verändert (`baseline-before.txt`).
