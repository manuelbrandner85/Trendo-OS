namespace Trendo.Core;

/// <summary>Arten von KI in der KI-Bibliothek (Vorgabe 2026-10-26: „chat ki, bild ki, video ki, sound, musik ki … alles schön kategorisiert“).</summary>
public enum KiKind { Chat, Denken, Code, BildVerstehen, Uebersetzen, Wissen, BildErzeugen, BildVerbessern, Video, SpracheZuText, TextZuSprache, Musik }

/// <summary>Womit Trendo ein Modell ausführt. Jede Laufzeit ist ein freies Programm, das Trendo selbst in „TrendoWerkzeuge“ einrichtet.</summary>
public enum KiRuntime { Ollama, StableDiffusionCpp, WhisperCpp, Piper, RealEsrgan, Python }

/// <summary>Eine Laufzeit: offizielles GitHub-Projekt mit fest gewählter Veröffentlichung (Tag, Datei, SHA-256, Größe), Hauptprogramm im ZIP.
/// Ollama und Python kommen aus dem Vollpaket (<see cref="Tag"/> leer).</summary>
public sealed record KiRuntimeInfo(KiRuntime Id, string Name, string Repo, string License, string WhatItDoes, string Tag, string Asset, string Sha256, long Bytes, string Exe)
{
    public bool FromVollpaket => Tag.Length == 0;
    public string DownloadUrl => $"https://github.com/{Repo}/releases/download/{Tag}/{Asset}";
}

/// <summary>Eine herunterladbare Datei eines Modells (offizielle Quelle, Größe in Bytes laut Anbieter).</summary>
public sealed record KiFile(string Url, string FileName, long Bytes);

/// <summary>Ein Modell der KI-Bibliothek. Bei Ollama ist <see cref="Tag"/> der Name in der Ollama-Bibliothek, sonst steckt der Download in <see cref="Files"/>.</summary>
public sealed record KiLibraryModel(string Id, KiKind Kind, KiRuntime Runtime, string Name, string Tag, double SizeGb, int MinRamGb, string License,
    string GoodFor, string HowTo, IReadOnlyList<KiFile> Files, bool Ready = true)
{
    /// <summary>Freie Lizenz ohne Zusatzbedingungen: Trendo darf ohne eigene Lizenz-Rückfrage laden (sonst zeigt Trendo die Bedingungen vorher).</summary>
    public bool OpenLicense => License is "Apache-2.0" or "MIT" or "CC0-1.0" or "BSD-3-Clause";
    public long Bytes => Files.Count > 0 ? Files.Sum(f => f.Bytes) : (long)(SizeGb * (1L << 30));
}

/// <summary>KI-Bibliothek: kostenlose Modelle nach Arten sortiert, jeweils mit Laufzeit, Lizenz, Größe, Speicherbedarf und Bedienhinweis.
/// Alle Adressen sind offizielle Quellen (Ollama-Bibliothek, Hugging Face der Hersteller, GitHub der Projekte), geprüft am 26.10.2026.</summary>
public static class KiLibrary
{
    public static readonly IReadOnlyDictionary<KiKind, (string Name, string Icon, string Description)> Kinds = new Dictionary<KiKind, (string, string, string)>
    {
        [KiKind.Chat] = ("Chat & Schreiben", "chat", "Fragen stellen, Texte schreiben, zusammenfassen und umformulieren."),
        [KiKind.Denken] = ("Nachdenken & Rechnen", "brain", "Löst knifflige Aufgaben Schritt für Schritt: Mathe, Logik, Planung."),
        [KiKind.Code] = ("Programmieren", "code", "Schreibt, erklärt und repariert Code in vielen Sprachen."),
        [KiKind.BildVerstehen] = ("Bilder verstehen", "eye", "Beschreibt Fotos, liest Schilder und Diagramme, beantwortet Fragen zum Bild."),
        [KiKind.Uebersetzen] = ("Übersetzen", "translate", "Übersetzt Texte zwischen vielen Sprachen, auf deinem PC."),
        [KiKind.Wissen] = ("Wissen & Suche", "book", "Macht deine Dokumente durchsuchbar nach Bedeutung, nicht nur nach Wörtern."),
        [KiKind.BildErzeugen] = ("Bilder erzeugen", "image", "Malt Bilder aus einer Beschreibung, z. B. „Leuchtturm bei Sonnenuntergang“."),
        [KiKind.BildVerbessern] = ("Bilder verbessern", "sparkles", "Vergrößert Fotos ohne Unschärfe (4-fach) und frischt alte Bilder auf."),
        [KiKind.Video] = ("Videos erzeugen", "video", "Erzeugt kurze Videoclips aus einer Beschreibung. Braucht eine starke Grafikkarte."),
        [KiKind.SpracheZuText] = ("Sprache zu Text", "microphone", "Schreibt Gesprochenes mit: Diktate, Interviews, Videos mit Untertiteln."),
        [KiKind.TextZuSprache] = ("Vorlesen", "speaker", "Liest Texte mit natürlicher deutscher Stimme vor und speichert sie als Audiodatei."),
        [KiKind.Musik] = ("Musik", "music", "Komponiert Musikstücke aus einer Beschreibung und Liedtext."),
    };

    public static readonly IReadOnlyList<KiRuntimeInfo> Runtimes =
    [
        new(KiRuntime.Ollama, "Ollama", "ollama/ollama", "MIT", "Führt Sprach- und Bildverstehens-Modelle aus.", "", "", "", 0, "ollama/ollama.exe"),
        new(KiRuntime.StableDiffusionCpp, "stable-diffusion.cpp", "leejet/stable-diffusion.cpp", "MIT", "Erzeugt Bilder und Videos aus Text, mit Grafikkarte (Vulkan) oder Prozessor.",
            "master-1015-e41fbcf", "sd-master-e41fbcf-bin-win-vulkan-x64.zip", "259ceeb62f1069190d24c85ea1c4549d2d61deb0ef712510dbe98d79ce93a38b", 29_744_345, "sd-cli.exe"),
        new(KiRuntime.WhisperCpp, "whisper.cpp", "ggml-org/whisper.cpp", "MIT", "Wandelt Sprache in Text, offline.",
            "b5454", "whisper-bin-x64.zip", "6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb", 8_928_640, "Release/whisper-cli.exe"),
        new(KiRuntime.Piper, "Piper", "rhasspy/piper", "MIT", "Liest Text mit natürlichen Stimmen vor, offline und schnell.",
            "2023.11.14-2", "piper_windows_amd64.zip", "f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea", 22_477_236, "piper/piper.exe"),
        new(KiRuntime.RealEsrgan, "Real-ESRGAN (ncnn)", "xinntao/Real-ESRGAN", "BSD-3-Clause", "Vergrößert Bilder 4-fach mit KI, Modelle sind enthalten.",
            "v0.2.5.0", "realesrgan-ncnn-vulkan-20220424-windows.zip", "abc02804e17982a3be33675e4d471e91ea374e65b70167abc09e31acb412802d", 45_474_481, "realesrgan-ncnn-vulkan.exe"),
        new(KiRuntime.Python, "Python", "python/cpython", "PSF-2.0", "Laufzeit für große Modelle wie Musik-KI (im Vollpaket).", "", "", "", 0, "python/python.exe"),
    ];

    const string Hf = "https://huggingface.co/";
    static KiLibraryModel O(string id, KiKind k, string name, string tag, double gb, int ram, string lic, string good) =>
        new(id, k, KiRuntime.Ollama, name, tag, gb, ram, lic, good, "Nach dem Laden in der Trendo KI unter „Modell“ auswählbar.", []);

    public static readonly IReadOnlyList<KiLibraryModel> Models =
    [
        // Chat & Schreiben
        O("qwen2.5-0.5b", KiKind.Chat, "Qwen 2.5 – Mini", "qwen2.5:0.5b", 0.4, 4, "Apache-2.0", "sehr schnell, einfache Fragen, läuft überall"),
        O("qwen3-4b", KiKind.Chat, "Qwen 3 – 4B", "qwen3:4b", 2.5, 8, "Apache-2.0", "gutes Deutsch, Alltag, Briefe"),
        O("qwen3-8b", KiKind.Chat, "Qwen 3 – 8B", "qwen3:8b", 5.2, 16, "Apache-2.0", "starkes Deutsch, längere Texte"),
        O("granite3.3-8b", KiKind.Chat, "IBM Granite 3.3 – 8B", "granite3.3:8b", 4.9, 16, "Apache-2.0", "sachliche Texte, Zusammenfassungen"),
        O("mistral-7b", KiKind.Chat, "Mistral 7B", "mistral:7b", 4.1, 16, "Apache-2.0", "europäisches Modell, längere Texte"),
        O("gemma3-4b", KiKind.Chat, "Google Gemma 3 – 4B", "gemma3:4b", 3.3, 8, "Gemma Terms", "kurze, genaue Antworten"),
        O("llama3.2-3b", KiKind.Chat, "Llama 3.2 – 3B", "llama3.2:3b", 2.0, 8, "Llama 3.2 Community", "Texte, Zusammenfassungen"),
        // Nachdenken
        O("phi4-mini", KiKind.Denken, "Microsoft Phi-4 mini", "phi4-mini", 2.5, 8, "MIT", "Mathe und Logik, sparsam"),
        O("deepseek-r1-8b", KiKind.Denken, "DeepSeek R1 – 8B", "deepseek-r1:8b", 5.2, 16, "MIT", "denkt Schritt für Schritt, zeigt den Lösungsweg"),
        // Programmieren
        O("qwen2.5-coder-3b", KiKind.Code, "Qwen 2.5 Coder – 3B", "qwen2.5-coder:3b", 1.9, 8, "Qwen Research", "Code-Hilfe auf schwächeren PCs"),
        O("qwen2.5-coder-7b", KiKind.Code, "Qwen 2.5 Coder – 7B", "qwen2.5-coder:7b", 4.7, 16, "Apache-2.0", "Programmieren in allen gängigen Sprachen"),
        // Bilder verstehen
        O("qwen2.5vl-7b", KiKind.BildVerstehen, "Qwen 2.5 VL – 7B", "qwen2.5vl:7b", 6.0, 16, "Apache-2.0", "Fotos beschreiben, Texte in Bildern lesen"),
        O("gemma3-4b-vision", KiKind.BildVerstehen, "Google Gemma 3 – 4B (sieht Bilder)", "gemma3:4b", 3.3, 8, "Gemma Terms", "Bilder beschreiben auf schwächeren PCs"),
        O("llama3.2-vision", KiKind.BildVerstehen, "Llama 3.2 Vision – 11B", "llama3.2-vision", 7.8, 16, "Llama 3.2 Community", "genaue Bildbeschreibungen"),
        // Übersetzen
        O("translategemma-4b", KiKind.Uebersetzen, "TranslateGemma – 4B", "translategemma:4b", 3.3, 8, "Gemma Terms", "Übersetzungen in 55 Sprachen"),
        O("qwen3-8b-translate", KiKind.Uebersetzen, "Qwen 3 – 8B", "qwen3:8b", 5.2, 16, "Apache-2.0", "Übersetzen mit Stil (Briefe, Fachtexte)"),
        // Wissen
        O("nomic-embed-text", KiKind.Wissen, "Nomic Embed Text", "nomic-embed-text", 0.3, 4, "Apache-2.0", "Dokumente nach Bedeutung durchsuchen"),
        O("bge-m3", KiKind.Wissen, "BGE-M3 (mehrsprachig)", "bge-m3", 1.2, 8, "MIT", "Suche in deutschen und gemischtsprachigen Dokumenten"),
        // Bilder erzeugen
        new("sd-turbo", KiKind.BildErzeugen, KiRuntime.StableDiffusionCpp, "SD Turbo (schnell)", "", 5.2, 8, "Stability AI Community",
            "Bild in wenigen Sekunden, 512 × 512", "Beschreibung eintippen, „Bild erzeugen“ – fertig in 1 Schritt.",
            [new(Hf + "stabilityai/sd-turbo/resolve/main/sd_turbo.safetensors", "sd_turbo.safetensors", 5_214_561_328)]),
        new("sdxl-turbo", KiKind.BildErzeugen, KiRuntime.StableDiffusionCpp, "SDXL Turbo (schöner)", "", 6.9, 16, "Stability AI Community",
            "detailreichere Bilder, 1024 × 1024", "Beschreibung eintippen, „Bild erzeugen“ – 1 bis 4 Schritte.",
            [new(Hf + "stabilityai/sdxl-turbo/resolve/main/sd_xl_turbo_1.0_fp16.safetensors", "sd_xl_turbo_1.0_fp16.safetensors", 6_938_081_905)]),
        // Bilder verbessern
        new("realesrgan-x4", KiKind.BildVerbessern, KiRuntime.RealEsrgan, "Real-ESRGAN 4× (Fotos und Zeichnungen)", "", 0.05, 4, "BSD-3-Clause",
            "Fotos und Grafiken 4-fach vergrößern", "Bild wählen, „Vergrößern“. Die Modelle stecken schon im Programm.", []),
        // Video
        new("wan2.1-t2v-1.3b", KiKind.Video, KiRuntime.StableDiffusionCpp, "Wan 2.1 – Text zu Video (1,3B)", "", 8.5, 16, "Apache-2.0",
            "kurze Clips (ca. 5 s, 832 × 480)", "Beschreibung eintippen, „Video erzeugen“. Braucht eine Grafikkarte mit mind. 8 GB.",
            [new(Hf + "Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/diffusion_models/wan2.1_t2v_1.3B_fp16.safetensors", "wan2.1_t2v_1.3B_fp16.safetensors", 2_838_303_560),
             new(Hf + "city96/umt5-xxl-encoder-gguf/resolve/main/umt5-xxl-encoder-Q8_0.gguf", "umt5-xxl-encoder-Q8_0.gguf", 6_043_068_256),
             new(Hf + "Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/vae/wan_2.1_vae.safetensors", "wan_2.1_vae.safetensors", 253_815_318)]),
        // Sprache zu Text
        new("whisper-base", KiKind.SpracheZuText, KiRuntime.WhisperCpp, "Whisper Base (schnell)", "", 0.14, 4, "MIT",
            "Diktate und klare Aufnahmen", "Audio- oder Videodatei wählen, „Mitschreiben“.",
            [new(Hf + "ggerganov/whisper.cpp/resolve/main/ggml-base.bin", "ggml-base.bin", 147_951_465)]),
        new("whisper-small", KiKind.SpracheZuText, KiRuntime.WhisperCpp, "Whisper Small (genauer)", "", 0.45, 8, "MIT",
            "Interviews, Dialekt, Hintergrundgeräusche", "Audio- oder Videodatei wählen, „Mitschreiben“.",
            [new(Hf + "ggerganov/whisper.cpp/resolve/main/ggml-small.bin", "ggml-small.bin", 487_601_967)]),
        new("whisper-large-v3-turbo", KiKind.SpracheZuText, KiRuntime.WhisperCpp, "Whisper Large v3 Turbo (beste Qualität)", "", 1.5, 8, "MIT",
            "beste Erkennung, auch Untertitel für Videos", "Audio- oder Videodatei wählen, „Mitschreiben“.",
            [new(Hf + "ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin", "ggml-large-v3-turbo.bin", 1_624_555_275)]),
        // Vorlesen
        new("piper-thorsten", KiKind.TextZuSprache, KiRuntime.Piper, "Deutsche Stimme „Thorsten“ (hohe Qualität)", "", 0.11, 4, "Datensatz CC0, Grundstimme lessac (eigene Bedingungen)",
            "natürliche Männerstimme", "Text eingeben oder Datei wählen, „Vorlesen“ oder „Als Audio speichern“.",
            [new(Hf + "rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/high/de_DE-thorsten-high.onnx", "de_DE-thorsten-high.onnx", 113_895_201),
             new(Hf + "rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/high/de_DE-thorsten-high.onnx.json", "de_DE-thorsten-high.onnx.json", 4_875)]),
        new("piper-eva", KiKind.TextZuSprache, KiRuntime.Piper, "Deutsche Stimme „Eva“ (klein)", "", 0.02, 4, "M-AILABS-Datensatz (eigene Bedingungen)",
            "Frauenstimme, sehr sparsam", "Text eingeben, „Vorlesen“.",
            [new(Hf + "rhasspy/piper-voices/resolve/main/de/de_DE/eva_k/x_low/de_DE-eva_k-x_low.onnx", "de_DE-eva_k-x_low.onnx", 20_628_813),
             new(Hf + "rhasspy/piper-voices/resolve/main/de/de_DE/eva_k/x_low/de_DE-eva_k-x_low.onnx.json", "de_DE-eva_k-x_low.onnx.json", 4_158)]),
        // Musik
        new("ace-step", KiKind.Musik, KiRuntime.Python, "ACE-Step (Musik aus Text)", "", 8.0, 16, "Apache-2.0",
            "Lieder mit Gesang aus Stil und Liedtext", "Stil und Liedtext eingeben, „Musik erzeugen“. Braucht eine Grafikkarte mit mind. 8 GB.", [], Ready: false),
    ];

    public static IEnumerable<KiLibraryModel> OfKind(KiKind k) => Models.Where(m => m.Kind == k);
    public static KiRuntimeInfo RuntimeInfo(KiRuntime r) => Runtimes.First(x => x.Id == r);
}
