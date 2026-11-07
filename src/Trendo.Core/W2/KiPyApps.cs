namespace Trendo.Core;

/// <summary>Paket E (0.9.49, Wunsch 02.11.2026): freie KIs, die als Python-Programm laufen (3D aus Bild, Geräusche).
/// Trendo richtet jede in einer eigenen Umgebung unter „TrendoWerkzeuge/ki/laufzeit/&lt;name&gt;“ ein – mit uv (Astral, MIT/Apache):
/// Quellcode aus dem offiziellen GitHub-Projekt auf einem fest gewählten Stand (Commit wird geprüft), eigenes Python 3.12,
/// PyTorch vom offiziellen PyTorch-Server, übrige Pakete von PyPI. Nichts landet im Benutzerordner oder im System.</summary>
public sealed record KiPyApp(KiRuntime Id, string Repo, string Commit, string Python, string[] Torch, string[] Packages, bool InstallRepo, string Script)
{
    public string RepoUrl => $"https://github.com/{Repo}.git";
    /// <summary>Ohne GitHub-Quellcode (nur Pakete von PyPI).</summary>
    public bool PackagesOnly => Repo.Length == 0;
}

public static class KiPyApps
{
    public static readonly IReadOnlyList<KiPyApp> All =
    [
        // TripoSR: torchmcubes (braucht unter Windows einen C++-Compiler) ersetzt Trendos Startskript durch scikit-image;
        // transformers 4.35 (laut Projekt) lässt sich unter Python 3.12 nicht ohne Compiler einrichten – 4.44 bis 4.46 laufen (geprüft 03.11.2026, RTX 5070)
        new(KiRuntime.TripoSR, "VAST-AI-Research/TripoSR", "107cefdc244c39106fa830359024f6a2f1c78871", "3.12", ["torch", "torchvision"],
            ["omegaconf", "einops", "transformers>=4.44,<4.47", "trimesh", "rembg", "onnxruntime", "huggingface-hub", "imageio[ffmpeg]", "xatlas", "moderngl", "scikit-image"], false, "trendo_triposr.py"),
        // MMAudio: Text (oder Video) zu Geräusch; das Projekt bringt seine Abhängigkeiten in pyproject.toml mit
        new(KiRuntime.MMAudio, "hkchengrex/MMAudio", "974010a026c731054592d8f777218bd9d85a6c24", "3.12", ["torch", "torchvision", "torchaudio"],
            ["soundfile"], true, "trendo_mmaudio.py"),
        // Trendo Musik M5: kein Quellcode von GitHub, nur das PyPI-Paket librosa auf fester Fassung (Merker = Fassung)
        new(KiRuntime.MusikAnalyse, "", "librosa-0.11.0", "3.12", [], ["librosa==0.11.0", "soundfile"], false, "trendo_analyse.py"),
        // KI-Musikvideo G: Artefakt-Prüfung mit MediaPipe 0.10.35 (Apache-2.0) (bringt OpenCV mit), geprüft 04.11.2026
        new(KiRuntime.VideoPruefung, "", "mediapipe-0.10.35", "3.12", [], ["mediapipe==0.10.35"], false, "trendo_pruefung.py"),
        // Spuren trennen: Demucs 4.0.1 von PyPI (MIT) mit PyTorch; Laden/Speichern über soundfile, darum ohne ffmpeg (geprüft 03.11.2026)
        new(KiRuntime.Spuren, "", "demucs-4.0.1", "3.12", ["torch", "torchaudio"], ["demucs==4.0.1", "soundfile"], false, "trendo_spuren.py"),
        // Eigene Stimme M6: Applio (RVC, MIT) auf festem Stand, Pakete wie requirements-engine.txt dieses Stands (ohne Gradio-Oberfläche); Start über trendo_stimme.py → core.py
        new(KiRuntime.Stimme, "IAHispano/Applio", "140404bda545cbb3a7c438bf2c77209d11cee242", "3.12", ["torch==2.11.0", "torchaudio==2.11.0"],
            ["click==8.1.8", "numpy==2.4.6", "requests==2.34.2", "tqdm==4.68.4", "faiss-cpu==1.14.3", "librosa==0.11.0", "scipy==1.18.0", "soundfile==0.14.0", "stftpitchshift==2.0", "soxr==1.1.0",
             "torchcrepe==0.0.24", "torchfcpe==0.0.4", "einops==0.8.2", "transformers==5.13.1", "matplotlib==3.11.0", "tensorboard==2.21.0", "onnx==1.23.1", "onnxruntime-gpu==1.22.0",
             "beautifulsoup4==4.15.0", "psutil==7.2.2", "webrtcvad-wheels==2.0.14"], false, "trendo_stimme.py"),
    ];

    public static KiPyApp? For(KiRuntime r) => All.FirstOrDefault(a => a.Id == r);

    /// <summary>NVIDIA-Grafikkarte: PyTorch mit CUDA 12.8 (auch RTX 50), sonst die Prozessor-Fassung.</summary>
    public static string TorchIndex(bool nvidia) => nvidia ? "https://download.pytorch.org/whl/cu128" : "https://download.pytorch.org/whl/cpu";

    public sealed record Paths(string Uv, string Git, string Dir, string UvHome)
    {
        public string Repo => Path.Combine(Dir, "repo");
        public string Env => Path.Combine(Dir, "env");
        public string PythonExe => Path.Combine(Env, "Scripts", "python.exe");
        public string Marker => Path.Combine(Dir, "trendo-fertig.txt");
    }

    /// <summary>Umgebung für uv: alles in Trendos eigenem Ordner, keine Verknüpfungen im Benutzerordner, PATH bleibt unverändert.</summary>
    public static Dictionary<string, string> UvEnv(string uvHome) => new()
    {
        ["UV_CACHE_DIR"] = Path.Combine(uvHome, "cache"),
        ["UV_PYTHON_INSTALL_DIR"] = Path.Combine(uvHome, "python"),
        ["UV_PYTHON_BIN_DIR"] = Path.Combine(uvHome, "bin"),
        ["UV_TOOL_DIR"] = Path.Combine(uvHome, "tools"),
        ["UV_NO_MODIFY_PATH"] = "1",
        ["UV_PYTHON_PREFERENCE"] = "only-managed",
        ["HF_HOME"] = Path.GetFullPath(Path.Combine(uvHome, "..", "huggingface")), // Modelle der KIs bleiben im Werkzeugordner
        // Befund 07.11.2026 (echter Demucs-Lauf): ohne diese Schalter schreibt Python unter Windows umgeleitet in cp1252 – Trendo liest UTF-8,
        // aus „…“, „ä“ und „–“ in Meldungen wurde „�“. So kommen Meldungen und Fehlertexte der KIs lesbar an.
        ["PYTHONUTF8"] = "1",
        ["PYTHONIOENCODING"] = "utf-8",
    };

    public sealed record Step(string Text, string Exe, string[] Args, string Cwd);

    /// <summary>Alle Schritte zum Einrichten – in dieser Reihenfolge. Der Commit wird danach mit <see cref="CommitOk"/> geprüft.</summary>
    public static List<Step> Steps(KiPyApp a, Paths p, bool nvidia)
    {
        var s = new List<Step>();
        if (!a.PackagesOnly)
        {
            if (!Directory.Exists(Path.Combine(p.Repo, ".git"))) s.Add(new("Quellcode von GitHub holen", p.Git, ["clone", "--quiet", a.RepoUrl, p.Repo], p.Dir));
            s.Add(new("Fest gewählten Stand auswählen", p.Git, ["-C", p.Repo, "checkout", "--quiet", "--detach", a.Commit], p.Dir));
        }
        s.Add(new($"Python {a.Python} einrichten", p.Uv, ["python", "install", "--no-bin", a.Python], p.Dir));
        s.Add(new("Eigene Umgebung anlegen", p.Uv, ["venv", "--quiet", "--clear", "--python", a.Python, p.Env], p.Dir));
        if (a.Torch.Length > 0) s.Add(new("PyTorch laden (offizieller Server, groß)", p.Uv, ["pip", "install", "--quiet", "--python", p.Env, .. a.Torch, "--index-url", TorchIndex(nvidia)], p.Dir));
        var pk = new List<string> { "pip", "install", "--quiet", "--python", p.Env };
        if (a.InstallRepo) { pk.Add("--editable"); pk.Add(p.Repo); }
        pk.AddRange(a.Packages);
        s.Add(new("Weitere Pakete laden", p.Uv, [.. pk], p.Dir));
        return s;
    }

    /// <summary>Stimmt der ausgecheckte Stand („git rev-parse HEAD“) mit dem fest gewählten überein?</summary>
    public static bool CommitOk(KiPyApp a, string revParseOutput) => string.Equals(revParseOutput.Trim(), a.Commit, StringComparison.OrdinalIgnoreCase);

    /// <summary>Fertig eingerichtet = Python der Umgebung da und Merker mit dem passenden Stand.</summary>
    public static bool Ready(KiPyApp a, Paths p) => File.Exists(p.PythonExe) && File.Exists(p.Marker) && File.ReadAllText(p.Marker).Trim() == a.Commit;
}
