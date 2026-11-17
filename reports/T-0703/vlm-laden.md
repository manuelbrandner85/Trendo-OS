# T-0703 Echtlauf: Qwen 2.5 VL 7B über die KI-Bibliothek laden

Datum: 2026-11-16 13:33:58 +01:00 · Dauer: 20,0 min
Weg: MainWindow.KiLibraryInstall (Knopf „Laden“) → Freigabe ki.pull → OllamaBackend.PullStreamAsync → Ollama /api/pull (registry.ollama.ai)
Lizenz laut Katalog: Apache-2.0 (Lizenzschicht im Ollama-Manifest: Apache License 2.0)
Vorher vorhanden: False · nachher vorhanden: True · Manifestgröße: 5969233408 Bytes
Meldung: Qwen 2.5 VL – 7B ist jetzt da und in Trendo nutzbar (Prüfsumme von Ollama kontrolliert).

## Audit

13:33:56 ki.model.pull.ok qwen2.5vl:7b Apache-2.0 quelle=registry.ollama.ai gewichte=sha256:a99b7f834d754b88f122d865f32758ba9f0994a83f8363df2c1e71c17605a025 5969233408B
13:33:58 ki.scan ollama=9M, koboldcpp=aus, jan=aus, llamacpp=aus, lmstudio=aus
