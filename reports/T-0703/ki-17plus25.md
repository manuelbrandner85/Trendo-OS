# T-0703 Echtlauf: 17 + 25 an das automatisch gewählte Modell

Datum: 2026-11-16 13:13:50 +01:00
Weg: MainWindow.KiModelChoice → Intel.AskAsync (IntelligenceCore, Ausweichen aktiv) → echtes Ollama 127.0.0.1:11434 (/api/chat, Streaming)
Arbeitsspeicher: 31,9 GB · Grafikspeicher (nvidia-smi): 11,9 GB

Vorhandene Modelle (Manifeste):
- qwen3:14b · 8,64 GB · allgemein: ja
- qwen2.5-coder:14b · 8,37 GB · allgemein: nein (Spezialmodell)
- qwen2.5-coder:7b · 4,36 GB · allgemein: nein (Spezialmodell)
- qwen2.5:7b · 4,36 GB · allgemein: ja
- mistral:7b · 4,07 GB · allgemein: ja
- qwen2.5:3b · 1,80 GB · allgemein: ja
- qwen2.5:1.5b · 0,92 GB · allgemein: ja
- qwen2.5:0.5b · 0,37 GB · allgemein: ja

Automatisch gewählt: qwen3:14b – Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.
Ausweichmodelle: qwen2.5:7b, qwen2.5:0.5b

Frage: Was ist 17 + 25?
Antwort (Ollama, qwen3:14b, 8,6 s, ok=True): 17 + 25 = 42
