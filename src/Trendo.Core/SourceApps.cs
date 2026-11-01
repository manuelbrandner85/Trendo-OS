using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>GitHub-Projekte ohne fertiges Windows-Programm trotzdem nutzen (T-0314, Masterprompt 131): Trendo erkennt am Dateibaum, was ein Projekt ist,
/// holt die passende offizielle Laufzeit portabel in den Werkzeugordner (uv für Python, Node.js, Go, .NET SDK – ohne Admin, mit fester Prüfsumme)
/// und richtet den Code dort ein. Was nicht geht (Rust/C++ brauchen die Visual-Studio-Build-Tools, Docker, reine Bibliotheken), sagt die Erkennung ehrlich
/// und nennt Alternativen. Netz und Prozesse liegen in der App; hier steckt nur die prüfbare Logik.</summary>
public static partial class SourceApps
{
    public enum Kind { Python, Node, Web, Go, DotNet, Rust, Cpp, Docker, Bibliothek, Unbekannt }

    /// <summary>Offizielle Laufzeit als portables Zip. <see cref="Hash"/> ist „sha256:…“ oder „sha512:…“ (wie vom Hersteller veröffentlicht).</summary>
    public sealed record Runtime(string Id, string Name, string Version, string License, string Url, string Hash, long Size, string Exe, string Home, bool Arm64);

    /// <summary>Feste Fassungen (Stand 01.11.2026) – Prüfsummen von den offiziellen Seiten (GitHub-Release von astral-sh/uv, nodejs.org SHASUMS256, go.dev/dl, dotnet release-metadata).</summary>
    public static readonly IReadOnlyList<Runtime> Runtimes =
    [
        new("uv", "uv (Python-Werkzeug von Astral)", "0.13.6", "MIT / Apache-2.0", "https://github.com/astral-sh/uv/releases/download/0.13.6/uv-x86_64-pc-windows-msvc.zip",
            "sha256:a2a8820d5eea32f08ef467ef3b7f66883a9b076d5737a2fc2fbb5f458d1bb50f", 15239500, "uv.exe", "https://docs.astral.sh/uv/", false),
        new("uv", "uv (Python-Werkzeug von Astral)", "0.13.6", "MIT / Apache-2.0", "https://github.com/astral-sh/uv/releases/download/0.13.6/uv-aarch64-pc-windows-msvc.zip",
            "sha256:c2426c0c8e28b53d8c440e47a18d96ad4db51e159866c0e81435b196d7f0081c", 15439263, "uv.exe", "https://docs.astral.sh/uv/", true),
        new("node", "Node.js 26 LTS", "26.13.0", "MIT", "https://nodejs.org/dist/v26.13.0/node-v26.13.0-win-x64.zip",
            "sha256:dcc60d59a0746999f99064a01c020445d54d84228c691cdbf25b49ac3b59150a", 41157375, "node.exe", "https://nodejs.org", false),
        new("node", "Node.js 26 LTS", "26.13.0", "MIT", "https://nodejs.org/dist/v26.13.0/node-v26.13.0-win-arm64.zip",
            "sha256:a9427139595156ef484e1fb08957676e09d4232368dd1743a2a9f93f3d241b14", 0, "node.exe", "https://nodejs.org", true),
        new("go", "Go", "1.27.3", "BSD-3-Clause", "https://go.dev/dl/go1.27.3.windows-amd64.zip",
            "sha256:f26081f438319757f8a1b46d7d0b2c5ec7583d4038e485e0aa725cfde7885fff", 79010309, "go.exe", "https://go.dev", false),
        new("go", "Go", "1.27.3", "BSD-3-Clause", "https://go.dev/dl/go1.27.3.windows-arm64.zip",
            "sha256:c507872ff21f8a7cd3c1c217b059c6f7271a4a9d8e692810ef9dd3e1f601197c", 75113075, "go.exe", "https://go.dev", true),
        new("dotnet", ".NET 10 SDK", "10.0.403", "MIT", "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip",
            "sha512:7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48", 300745255, "dotnet.exe", "https://dot.net", false),
        new("dotnet", ".NET 10 SDK", "10.0.403", "MIT", "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-arm64.zip",
            "sha512:f78ecd8b48e24fd72ee604687b75f1bbb81505e8343f7d52e4baf9ea32372557eb30e88facaaad195bf2646fcce117fd941e49da0d5bdee1d60d63d836968429", 0, "dotnet.exe", "https://dot.net", true),
    ];

    /// <summary>Server, von denen Laufzeiten und Quellcode kommen dürfen (zusätzlich zu GitHub).</summary>
    public static readonly string[] RuntimeHosts = ["nodejs.org", "go.dev", "dl.google.com", "builds.dotnet.microsoft.com", "codeload.github.com", "raw.githubusercontent.com"];

    public static Runtime RuntimeFor(string id, bool arm64) => Runtimes.First(r => r.Id == id && r.Arm64 == arm64);

    /// <summary>Ordner der Laufzeit im Werkzeugordner, z. B. „laufzeiten/node-26.13.0“.</summary>
    public static string RuntimeDir(string toolsRoot, Runtime r) => Path.Combine(toolsRoot, "laufzeiten", $"{r.Id}-{r.Version}");

    /// <summary>Programmdatei der eingerichteten Laufzeit (sucht bis drei Ebenen tief, weil die Zips verschieden aufgebaut sind).</summary>
    public static string? RuntimeExe(string toolsRoot, Runtime r)
    {
        var d = RuntimeDir(toolsRoot, r);
        if (!Directory.Exists(d)) return null;
        return Directory.EnumerateFiles(d, r.Exe, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, MatchCasing = MatchCasing.CaseInsensitive })
            .OrderBy(p => p.Length).FirstOrDefault();
    }

    /// <summary>Prüft eine geladene Datei gegen „sha256:…“ bzw. „sha512:…“.</summary>
    public static bool HashOk(string file, string hash)
    {
        var (algo, want) = hash.Split(':', 2) is [var a, var w] ? (a.ToLowerInvariant(), w.Trim()) : ("sha256", hash.Trim());
        using var s = File.OpenRead(file);
        var got = algo switch
        {
            "sha512" => Convert.ToHexStringLower(System.Security.Cryptography.SHA512.HashData(s)),
            "sha256" => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(s)),
            _ => "",
        };
        return got.Length > 0 && got.Equals(want, StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── Erkennung ─────────────────────────

    /// <summary>Was ein Projekt ist und ob Trendo es starten kann. <see cref="Text"/> steht als Abzeichen im Treffer.</summary>
    public sealed record Detection(Kind Kind, bool Runnable, string Text, string? Runtime, string? Why = null, string? Package = null);

    [GeneratedRegex(@"(?ms)^\[project\.scripts\]\s*$(.*?)(?=^\[|\z)")] private static partial Regex PyScripts();
    [GeneratedRegex(@"(?ms)^\[tool\.poetry\.scripts\]\s*$(.*?)(?=^\[|\z)")] private static partial Regex PoetryScripts();
    [GeneratedRegex(@"(?m)^\s*""?([A-Za-z0-9][A-Za-z0-9._-]*)""?\s*=")] private static partial Regex TomlKey();
    [GeneratedRegex(@"(?ms)^\[project\]\s*$(.*?)(?=^\[|\z)")] private static partial Regex PyProject();
    [GeneratedRegex(@"(?m)^\s*name\s*=\s*""([^""]+)""")] private static partial Regex TomlName();
    [GeneratedRegex(@"(?m)^\s*requires-python\s*=\s*""([^""]+)""")] private static partial Regex TomlRequiresPython();
    [GeneratedRegex(@"(?m)^\s*build-backend\s*=\s*""([^""]+)""")] private static partial Regex TomlBackend();
    [GeneratedRegex(@"<OutputType>\s*(\w+)\s*</OutputType>", RegexOptions.IgnoreCase)] private static partial Regex CsOutput();
    [GeneratedRegex(@"<Project\s+Sdk\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)] private static partial Regex CsSdk();
    [GeneratedRegex(@"(?m)^\s*package\s+main\b")] private static partial Regex GoMain();

    static readonly string[] PyEntries = ["app.py", "main.py", "run.py", "webui.py", "gui.py", "start.py", "__main__.py"];

    /// <summary>Erkennt das Projekt am Dateibaum (Pfade mit „/“, relativ zum Projekt). <paramref name="read"/> liefert den Inhalt einzelner Dateien (oder null).</summary>
    public static Detection Detect(string repoName, IReadOnlyCollection<string> paths, Func<string, string?> read)
    {
        var all = paths.Select(p => p.Replace('\\', '/').TrimStart('/')).ToList();
        var root = all.Where(p => !p.Contains('/')).Select(p => p.ToLowerInvariant()).ToHashSet();
        bool Has(string f) => root.Contains(f);
        string? Read(string f) { try { var t = read(f); return t is { Length: > 200_000 } ? t[..200_000] : t; } catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { return null; } }
        var name = repoName.Contains('/') ? repoName.Split('/')[1] : repoName;

        // Python
        if (Has("pyproject.toml") || Has("setup.py") || Has("requirements.txt") || Has("pipfile") || Has("setup.cfg") || root.Any(f => f.EndsWith(".py")))
        {
            var py = Has("pyproject.toml") ? Read("pyproject.toml") ?? "" : "";
            var backend = TomlBackend().Match(py) is { Success: true } b ? b.Groups[1].Value : "";
            var pkg = PyProject().Match(py) is { Success: true } pp && TomlName().Match(pp.Groups[1].Value) is { Success: true } pn ? pn.Groups[1].Value : null;
            if (backend.Contains("maturin") || backend.Contains("setuptools_rust") || backend.Contains("scikit_build") || backend.Contains("mesonpy"))
                return new(Kind.Python, false, "Python mit eigenem C/Rust-Teil – nicht aus dem Quellcode startbar", null,
                    "Der Code muss mit einem C- oder Rust-Compiler gebaut werden. Dafür bräuchte es die Visual-Studio-Build-Tools – eine systemweite Installation, die Trendo nicht vornimmt.", pkg);
            var p = PythonPlanInfo(name, all, Read);
            if (p.Entry is null)
                return new(Kind.Bibliothek, false, "Python-Bibliothek – ein Baustein für Programmierer, kein Programm zum Starten", null,
                    "Das Projekt enthält keinen Startpunkt (kein Skript, keine app.py/main.py).", pkg);
            return new(Kind.Python, true, $"Python-Programm{(p.Web ? " mit Weboberfläche" : "")} · läuft in Trendo (über uv)", "uv", Package: pkg);
        }

        // Node.js / Web
        if (Has("package.json") && Read("package.json") is { } pj && NodeInfo(pj) is { } n)
        {
            if (n.Kind == Kind.Web) return new(Kind.Web, true, "Web-App · läuft im Browser über Trendo", "node", Package: n.Package);
            if (n.Kind == Kind.Bibliothek)
                return root.Contains("index.html")
                    ? new(Kind.Web, true, "Web-Seite · läuft im Browser über Trendo", null)
                    : new(Kind.Bibliothek, false, "JavaScript-Bibliothek – ein Baustein für Programmierer, kein Programm zum Starten", null, "In der package.json steht weder ein Startbefehl noch ein Programm (bin).", n.Package);
            return new(Kind.Node, true, $"Node.js-Programm{(n.Electron ? " (Desktop, Electron)" : "")} · läuft in Trendo", "node", Package: n.Package);
        }
        if (StaticRoot(all) is { } sr && !Has("cargo.toml") && !Has("go.mod"))
            return new(Kind.Web, true, sr.Length == 0 ? "Web-Seite · läuft im Browser über Trendo" : $"Web-Seite (Ordner {sr}) · läuft im Browser über Trendo", null);

        // Go
        if (Has("go.mod"))
            return GoTarget(name, all, Read) is not null
                ? new(Kind.Go, true, "Go-Programm · Trendo baut daraus ein Windows-Programm", "go")
                : new(Kind.Bibliothek, false, "Go-Bibliothek – ein Baustein für Programmierer, kein Programm zum Starten", null, "Im Projekt gibt es kein „package main“.");

        // .NET
        var cs = all.Where(p => (p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)) && p.Count(c => c == '/') <= 3).ToList();
        if (cs.Count > 0)
        {
            var t = DotNetTarget(name, cs, Read);
            if (t is null) return new(Kind.Bibliothek, false, ".NET-Bibliothek – ein Baustein für Programmierer, kein Programm zum Starten", null, "Kein Projekt im Ordner baut ein Programm (OutputType Exe/WinExe).");
            if (!t.Value.SdkStyle) return new(Kind.DotNet, false, "Altes .NET-Framework-Projekt – nicht ohne Visual Studio baubar", null, "Das Projekt nutzt das alte Projektformat. Zum Bauen bräuchte es Visual Studio – das installiert Trendo nicht.");
            return new(Kind.DotNet, true, $".NET-Programm{(t.Value.Web ? " mit Weboberfläche" : "")} · Trendo baut daraus ein Windows-Programm", "dotnet");
        }

        if (Has("cargo.toml"))
            return new(Kind.Rust, false, "Rust-Projekt – nicht aus dem Quellcode startbar", null, "Rust braucht zum Bauen unter Windows die Visual-Studio-Build-Tools – eine systemweite Installation, die Trendo nicht vornimmt.");
        if (Has("cmakelists.txt") || Has("makefile") || Has("meson.build") || Has("configure") || Has("configure.ac") || all.Any(p => p.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            return new(Kind.Cpp, false, "C/C++-Projekt – nicht aus dem Quellcode startbar", null, "C/C++ braucht einen Compiler (Visual-Studio-Build-Tools oder MSYS2) – eine systemweite Installation, die Trendo nicht vornimmt.");
        if (Has("dockerfile") || Has("docker-compose.yml") || Has("compose.yaml") || Has("docker-compose.yaml"))
            return new(Kind.Docker, false, "Läuft nur in Docker – nicht in Trendo", null, "Das Projekt ist für Docker-Container gebaut. Docker braucht Admin-Rechte und eine systemweite Installation.");
        return new(Kind.Unbekannt, false, "Projektart nicht erkannt – nur Quellcode", null, "Trendo hat weder Python, Node.js, Go noch .NET gefunden.");
    }

    /// <summary>Startpunkt eines Python-Projekts: Skript aus [project.scripts], sonst app.py/main.py/… oder ein Paket mit __main__.py.</summary>
    public sealed record PyInfo(string? Script, string? Entry, string? Module, bool Web, string WebKind, string? RequiresPython, bool HasProject, bool HasRequirements);

    public static PyInfo PythonPlanInfo(string repoName, IReadOnlyCollection<string> all, Func<string, string?> read)
    {
        var root = all.Where(p => !p.Contains('/')).ToList();
        string? R(string f) => root.FirstOrDefault(x => x.Equals(f, StringComparison.OrdinalIgnoreCase));
        var py = R("pyproject.toml") is { } pf ? read(pf) ?? "" : "";
        var hasProject = PyProject().IsMatch(py) || R("setup.py") is not null || R("setup.cfg") is not null || py.Contains("[tool.poetry]");
        var scripts = (PyScripts().Match(py) is { Success: true } m ? m.Groups[1].Value : "") + "\n" + (PoetryScripts().Match(py) is { Success: true } m2 ? m2.Groups[1].Value : "");
        var keys = TomlKey().Matches(scripts).Select(k => k.Groups[1].Value).Distinct().ToList();
        var lower = repoName.ToLowerInvariant();
        var script = keys.OrderByDescending(k => k.Equals(lower, StringComparison.OrdinalIgnoreCase) || k.Replace('_', '-').Equals(lower.Replace('_', '-'), StringComparison.OrdinalIgnoreCase)).ThenBy(k => k.Length).FirstOrDefault();
        var req = R("requirements.txt") is { } rf ? read(rf) ?? "" : "";
        var rp = PyProject().Match(py) is { Success: true } pp && TomlRequiresPython().Match(pp.Groups[1].Value) is { Success: true } rq ? rq.Groups[1].Value : null;
        string? entry = null; string? module = null;
        if (script is null)
        {
            entry = PyEntries.Select(R).FirstOrDefault(x => x is not null) ?? R(lower + ".py") ?? R(lower.Replace('-', '_') + ".py");
            if (entry is null)
            {
                // Paket mit __main__.py (auch unter src/) → „python -m paket“
                var main = all.Where(p => p.EndsWith("/__main__.py", StringComparison.Ordinal) && p.Count(c => c == '/') <= 2 && !p.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.Count(c => c == '/')).FirstOrDefault();
                if (main is not null) { var parts = main.Split('/'); module = parts[^2]; }
            }
            // einzelne .py-Datei im Hauptordner (außer setup.py) gilt als Programm
            if (entry is null && module is null && !hasProject && root.Where(x => x.EndsWith(".py", StringComparison.OrdinalIgnoreCase) && !x.Equals("setup.py", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("test", StringComparison.OrdinalIgnoreCase)).ToList() is [var only]) entry = only;
        }
        var code = entry is not null ? read(entry) ?? "" : "";
        var deps = (py + "\n" + req + "\n" + code).ToLowerInvariant();
        var webKind = deps.Contains("streamlit") ? "streamlit" : deps.Contains("gradio") ? "gradio" : "";
        return new(script, script ?? entry ?? (module is not null ? "-m " + module : null), module, webKind.Length > 0 && entry is not null, webKind, rp, hasProject, req.Length > 0);
    }

    public sealed record NodeData(Kind Kind, bool Electron, string? Script, string? Bin, string? Package, string WebTool);

    /// <summary>Liest package.json: Electron-Programm, Web-App (vite/next/…), Kommandozeilenprogramm (bin) oder Bibliothek.</summary>
    public static NodeData? NodeInfo(string packageJson)
    {
        try
        {
            using var d = JsonDocument.Parse(packageJson, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var r = d.RootElement; if (r.ValueKind != JsonValueKind.Object) return null;
            var pkg = r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var scripts = r.TryGetProperty("scripts", out var s) && s.ValueKind == JsonValueKind.Object ? s.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!) : [];
            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in new[] { "dependencies", "devDependencies" })
                if (r.TryGetProperty(k, out var dd) && dd.ValueKind == JsonValueKind.Object) foreach (var x in dd.EnumerateObject()) deps.Add(x.Name);
            string? bin = r.TryGetProperty("bin", out var b) ? b.ValueKind == JsonValueKind.String ? b.GetString() : b.ValueKind == JsonValueKind.Object ? b.EnumerateObject().Select(x => x.Value.GetString()).FirstOrDefault(v => v is not null) : null : null;
            var electron = deps.Contains("electron");
            var webTool = deps.Contains("next") ? "next" : deps.Contains("vite") ? "vite" : deps.Contains("react-scripts") ? "react-scripts" : deps.Contains("@angular/cli") ? "angular" : deps.Contains("@sveltejs/kit") ? "vite" : "";
            if (electron && scripts.ContainsKey("start")) return new(Kind.Node, true, "start", null, pkg, "");
            if (webTool.Length > 0 && (scripts.ContainsKey("dev") || scripts.ContainsKey("start") || scripts.ContainsKey("serve")))
                return new(Kind.Web, false, scripts.ContainsKey("dev") ? "dev" : scripts.ContainsKey("start") ? "start" : "serve", null, pkg, webTool);
            if (bin is not null) return new(Kind.Node, false, null, bin.TrimStart('.', '/'), pkg, "");
            if (scripts.ContainsKey("start")) return new(deps.Contains("express") || deps.Contains("fastify") || deps.Contains("koa") ? Kind.Web : Kind.Node, false, "start", null, pkg, deps.Contains("express") || deps.Contains("fastify") || deps.Contains("koa") ? "server" : "");
            return new(Kind.Bibliothek, false, null, null, pkg, "");
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Ordner mit index.html für reine Web-Seiten: Hauptordner, sonst docs/, public/, dist/, site/, www/. „“ = Hauptordner.</summary>
    public static string? StaticRoot(IReadOnlyCollection<string> all)
    {
        var set = all.Select(p => p.Replace('\\', '/').ToLowerInvariant()).ToHashSet();
        if (set.Contains("index.html")) return "";
        return new[] { "docs", "public", "dist", "site", "www", "web", "build" }.FirstOrDefault(d => set.Contains(d + "/index.html"));
    }

    /// <summary>Go: welches Paket ein Programm ist – „.“ (package main im Hauptordner) oder „./cmd/&lt;name&gt;“.</summary>
    public static string? GoTarget(string repoName, IReadOnlyCollection<string> all, Func<string, string?> read)
    {
        var rootGo = all.Where(p => !p.Contains('/') && p.EndsWith(".go", StringComparison.OrdinalIgnoreCase) && !p.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Equals("main.go", StringComparison.OrdinalIgnoreCase)).Take(3);
        if (rootGo.Any(f => read(f) is { } c && GoMain().IsMatch(c))) return ".";
        var cmds = all.Where(p => p.StartsWith("cmd/", StringComparison.OrdinalIgnoreCase) && p.Count(c => c == '/') == 2 && p.EndsWith(".go", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Split('/')[1]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cmds.Count == 0) return null;
        var pick = cmds.FirstOrDefault(c => c.Equals(repoName, StringComparison.OrdinalIgnoreCase)) ?? cmds.OrderBy(c => c.Length).First();
        return "./cmd/" + pick;
    }

    /// <summary>.NET: das Projekt, das ein Programm baut (WinExe vor Exe, Name wie das Projekt bevorzugt).</summary>
    public static (string Path, bool SdkStyle, bool Web)? DotNetTarget(string repoName, IReadOnlyCollection<string> projects, Func<string, string?> read)
    {
        var found = new List<(string Path, bool Sdk, bool Web, int Score)>();
        foreach (var p in projects.Take(12))
        {
            if (p.Contains("test", StringComparison.OrdinalIgnoreCase) || p.Contains("sample", StringComparison.OrdinalIgnoreCase) || p.Contains("benchmark", StringComparison.OrdinalIgnoreCase)) continue;
            var x = read(p); if (x is null) continue;
            var sdk = CsSdk().Match(x) is { Success: true } sm ? sm.Groups[1].Value : null;
            var web = sdk?.Contains(".Web", StringComparison.OrdinalIgnoreCase) == true;
            var output = CsOutput().Match(x) is { Success: true } om ? om.Groups[1].Value.ToLowerInvariant() : "";
            if (output is not ("exe" or "winexe") && !web) continue;
            var score = (output == "winexe" ? 4 : 2) + (Path.GetFileNameWithoutExtension(p).Equals(repoName, StringComparison.OrdinalIgnoreCase) ? 3 : 0) - p.Count(c => c == '/');
            found.Add((p, sdk is not null, web, score));
        }
        return found.Count == 0 ? null : found.OrderByDescending(f => f.Score).Select(f => (f.Path, f.Sdk, f.Web)).First();
    }

    // ───────────────────────── Einrichtungsplan ─────────────────────────

    /// <summary>Ein Befehl. In <see cref="Exe"/> stehen Platzhalter „{uv}“, „{node}“, „{go}“, „{dotnet}“ oder ein Pfad relativ zum Projektordner.</summary>
    public sealed record Step(string Text, string Exe, string Args);

    /// <summary>Einrichtung und Start. Bei <see cref="Web"/> startet Trendo das Programm unsichtbar und öffnet danach http://127.0.0.1:{port} im Browser;
    /// <see cref="StaticRoot"/> ≠ null heißt: reine Web-Seite, Trendo liefert den Ordner selbst aus. Im Startbefehl steht „{port}“ für den freien Port.</summary>
    public sealed record Plan(Kind Kind, string? Runtime, IReadOnlyList<Step> Setup, Step? Start, bool Web, string? StaticRoot, IReadOnlyDictionary<string, string> Env);

    /// <summary>Plan für einen Projektordner (nach dem Laden). Wirft, wenn das Projekt nicht startbar ist.</summary>
    public static Plan MakePlan(string repoName, string dir)
    {
        var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 4, IgnoreInaccessible = true })
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + ".venv" + Path.DirectorySeparatorChar))
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).Take(20_000).ToList();
        string? Read(string rel) { var f = Path.Combine(dir, rel); return File.Exists(f) && new FileInfo(f).Length < 2_000_000 ? File.ReadAllText(f) : null; }
        return MakePlan(repoName, files, Read);
    }

    public static Plan MakePlan(string repoName, IReadOnlyCollection<string> files, Func<string, string?> read)
    {
        var name = repoName.Contains('/') ? repoName.Split('/')[1] : repoName;
        var d = Detect(repoName, files, read);
        if (!d.Runnable) throw new InvalidOperationException(d.Why ?? d.Text);
        var env = new Dictionary<string, string>();
        switch (d.Kind)
        {
            case Kind.Python:
            {
                var p = PythonPlanInfo(name, files, read);
                var setup = new List<Step> { new("Python-Umgebung anlegen", "{uv}", $"venv .venv --python \"{Safe(p.RequiresPython ?? "3.12", @"[0-9.<>=!~, *]")}\"") };
                if (p.HasProject) setup.Add(new("Projekt und Abhängigkeiten installieren", "{uv}", "pip install --python .venv -e ."));
                if (p.HasRequirements) setup.Add(new("Abhängigkeiten installieren (requirements.txt)", "{uv}", "pip install --python .venv -r requirements.txt"));
                Step start;
                if (p.Script is { } sc) start = new("Starten", $".venv/Scripts/{Safe(sc)}.exe", "");
                else if (p.Module is { } mod) start = new("Starten", ".venv/Scripts/python.exe", $"-m {Safe(mod)}");
                else if (p.WebKind == "streamlit") start = new("Starten", ".venv/Scripts/python.exe", $"-m streamlit run \"{p.Entry}\" --server.port {{port}} --server.address 127.0.0.1 --server.headless true");
                else start = new("Starten", ".venv/Scripts/python.exe", $"\"{p.Entry}\"");
                if (p.WebKind == "gradio") { env["GRADIO_SERVER_NAME"] = "127.0.0.1"; env["GRADIO_SERVER_PORT"] = "{port}"; }
                env["PYTHONUTF8"] = "1";
                return new(Kind.Python, "uv", setup, start, p.Web, null, env);
            }
            case Kind.Node:
            case Kind.Web when d.Runtime == "node":
            {
                var n = NodeInfo(read("package.json") ?? "{}")!;
                var setup = new List<Step> { new("Abhängigkeiten laden (npm install)", "{node}", "{npm} install --no-fund --no-audit") };
                env["PORT"] = "{port}"; env["BROWSER"] = "none";
                if (n.Kind == Kind.Web)
                {
                    var extra = n.WebTool switch { "vite" => " -- --port {port} --host 127.0.0.1 --strictPort", "next" => " -- -p {port} -H 127.0.0.1", "angular" => " -- --port {port}", _ => "" };
                    return new(Kind.Web, "node", setup, new("Starten", "{node}", $"{{npm}} run {Safe(n.Script!)}{extra}"), true, null, env);
                }
                if (n.Bin is { } bin) return new(Kind.Node, "node", setup, new("Starten", "{node}", $"\"{bin}\""), false, null, env);
                return new(Kind.Node, "node", setup, new("Starten", "{node}", $"{{npm}} run {Safe(n.Script ?? "start")}"), false, null, env);
            }
            case Kind.Web:
                return new(Kind.Web, null, [], null, true, StaticRoot(files) ?? "", env);
            case Kind.Go:
            {
                var target = GoTarget(name, files, read)!;
                env["CGO_ENABLED"] = "0";
                return new(Kind.Go, "go", [new("Programm bauen (go build)", "{go}", $"build -o \"trendo-bin/{Safe(name)}.exe\" {target}")], new("Starten", $"trendo-bin/{Safe(name)}.exe", ""), false, null, env);
            }
            case Kind.DotNet:
            {
                var t = DotNetTarget(name, files.Where(f => f.EndsWith("proj", StringComparison.OrdinalIgnoreCase)).ToList(), read)!.Value;
                var exe = Path.GetFileNameWithoutExtension(t.Path);
                env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"; env["DOTNET_NOLOGO"] = "1"; env["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
                if (t.Web) env["ASPNETCORE_URLS"] = "http://127.0.0.1:{port}";
                return new(Kind.DotNet, "dotnet", [new("Programm bauen (dotnet publish)", "{dotnet}", $"publish \"{t.Path}\" -c Release -r {{rid}} --self-contained -o trendo-bin")],
                    new("Starten", $"trendo-bin/{Safe(exe)}.exe", ""), t.Web, null, env);
            }
            default: throw new InvalidOperationException(d.Why ?? d.Text);
        }
    }

    /// <summary>Plan für ein Paket aus PyPI oder npm (Alternative, wenn der Quellcode selbst nicht baubar ist – dort liegen fertig gebaute Pakete).</summary>
    public static Plan PackagePlan(string registry, string package, string? command = null)
    {
        var pkg = Safe(package, @"[A-Za-z0-9._@/-]").TrimStart('-', '.', '/'); // nie als Option („--index-url=…“) oder Pfad lesbar
        var cmd = Safe(command ?? (pkg.Contains('/') ? pkg.Split('/')[1] : pkg)).TrimStart('-', '.');
        if (pkg.Length == 0 || cmd.Length == 0) throw new ArgumentException("Ungültiger Paketname.");
        return registry == "pypi"
            ? new(Kind.Python, "uv", [new("Python-Umgebung anlegen", "{uv}", "venv .venv --python 3.12"), new($"Paket {pkg} installieren", "{uv}", $"pip install --python .venv {pkg}")], new("Starten", $".venv/Scripts/{cmd}.exe", ""), false, null, new Dictionary<string, string> { ["PYTHONUTF8"] = "1" })
            : new(Kind.Node, "node", [new($"Paket {pkg} installieren", "{node}", $"{{npm}} install --no-fund --no-audit --prefix . {pkg}")], new("Starten", $"node_modules/.bin/{cmd}.cmd", ""), false, null, new Dictionary<string, string>());
    }

    static string Safe(string s, string allowed = @"[A-Za-z0-9._-]") => string.Concat(s.Where(c => Regex.IsMatch(c.ToString(), allowed)));

    // ───────────────────────── GitHub-Adressen ─────────────────────────

    public static string TreeUrl(string repo) => GitHubApps.ValidRepo(repo) ? $"https://api.github.com/repos/{repo}/git/trees/HEAD?recursive=1" : throw new ArgumentException("Ungültiger Projektname.");
    public static string RawUrl(string repo, string path) => $"https://raw.githubusercontent.com/{repo}/HEAD/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}";
    public static string ZipUrl(string repo) => GitHubApps.ValidRepo(repo) ? $"https://codeload.github.com/{repo}/zip/HEAD" : throw new ArgumentException("Ungültiger Projektname.");
    public static string ForksUrl(string repo) => GitHubApps.ValidRepo(repo) ? $"https://api.github.com/repos/{repo}/forks?sort=stargazers&per_page=10" : throw new ArgumentException("Ungültiger Projektname.");
    public static string PyPiUrl(string package) => $"https://pypi.org/pypi/{Uri.EscapeDataString(package)}/json";
    public static string NpmUrl(string package) => $"https://registry.npmjs.org/{package.Replace("/", "%2F")}";

    /// <summary>Dateibaum aus der GitHub-Antwort (nur Dateien) und Kennung des Stands (für Updates).</summary>
    public static (IReadOnlyList<string> Files, string Sha, bool Truncated) ParseTree(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var files = new List<string>();
        if (r.TryGetProperty("tree", out var t) && t.ValueKind == JsonValueKind.Array)
            foreach (var e in t.EnumerateArray())
                if (e.TryGetProperty("type", out var ty) && ty.GetString() == "blob" && e.TryGetProperty("path", out var p) && p.GetString() is { } s) files.Add(s);
        return (files, r.TryGetProperty("sha", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString()! : "", r.TryGetProperty("truncated", out var tr) && tr.ValueKind == JsonValueKind.True);
    }

    /// <summary>Forks (meistbesternt zuerst) – unter ihnen findet sich manchmal eine Fassung mit fertigem Windows-Programm.</summary>
    public static IReadOnlyList<OssRepo> ParseForks(string json)
    {
        using var d = JsonDocument.Parse(json);
        if (d.RootElement.ValueKind != JsonValueKind.Array) return [];
        var list = new List<OssRepo>();
        foreach (var e in d.RootElement.EnumerateArray())
        {
            string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
            var lic = e.TryGetProperty("license", out var l) && l.ValueKind == JsonValueKind.Object && l.TryGetProperty("spdx_id", out var sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString()! : "";
            var full = S("full_name"); if (!GitHubApps.ValidRepo(full)) continue;
            list.Add(new OssRepo(full, S("description"), S("html_url"), S("clone_url"), e.TryGetProperty("stargazers_count", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : 0, lic, S("language"),
                e.TryGetProperty("pushed_at", out var pa) && pa.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(pa.GetString(), out var dt) ? dt : DateTimeOffset.MinValue));
        }
        return list;
    }

    /// <summary>PyPI: gibt es das Paket mit einem Startbefehl? Liefert (Name, Version) oder null.</summary>
    public static (string Name, string Version)? ParsePyPi(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var info = d.RootElement.GetProperty("info");
            return (info.GetProperty("name").GetString() ?? "", info.GetProperty("version").GetString() ?? "");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    /// <summary>npm: Paket mit Programm (bin) in der neuesten Fassung? Liefert (Name, Version, Befehl) oder null.</summary>
    public static (string Name, string Version, string Command)? ParseNpm(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json); var r = d.RootElement;
            var latest = r.GetProperty("dist-tags").GetProperty("latest").GetString()!;
            var v = r.GetProperty("versions").GetProperty(latest);
            if (!v.TryGetProperty("bin", out var b)) return null;
            var name = r.GetProperty("name").GetString()!;
            var cmd = b.ValueKind == JsonValueKind.Object ? b.EnumerateObject().Select(x => x.Name).FirstOrDefault() : b.ValueKind == JsonValueKind.String ? (name.Contains('/') ? name.Split('/')[1] : name) : null;
            return cmd is null ? null : (name, latest, cmd);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    // ───────────────────────── Eingerichtete Projekte ─────────────────────────

    public const string ListName = "quellcode-programme.json";
    public const string Prefix = "src-";

    public sealed record Installed(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("repo")] string Repo,
        [property: JsonPropertyName("stand")] string Sha,
        [property: JsonPropertyName("art")] Kind Kind,
        [property: JsonPropertyName("laufzeit")] string? Runtime,
        [property: JsonPropertyName("start")] Step? Start,
        [property: JsonPropertyName("web")] bool Web,
        [property: JsonPropertyName("statisch")] string? StaticRoot,
        [property: JsonPropertyName("umgebung")] Dictionary<string, string> Env,
        [property: JsonPropertyName("lizenz")] string License,
        [property: JsonPropertyName("beschreibung")] string Description,
        [property: JsonPropertyName("eingerichtet")] DateTimeOffset InstalledAt);

    public static string IdFor(string repo) => Prefix + Regex.Replace(repo.ToLowerInvariant().Replace('/', '-'), "[^a-z0-9-]", "-");
    public static string Dir(string toolsRoot, string id) => Path.Combine(toolsRoot, id);

    public static IReadOnlyList<Installed> List(string toolsRoot)
    {
        var f = Path.Combine(toolsRoot, ListName);
        try { return File.Exists(f) ? JsonSerializer.Deserialize<List<Installed>>(File.ReadAllText(f), J) ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    static readonly JsonSerializerOptions J = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Installed Register(string toolsRoot, string repo, string sha, Plan plan, string license, string description, AuditLog? audit = null)
    {
        var e = new Installed(IdFor(repo), repo, sha, plan.Kind, plan.Runtime, plan.Start, plan.Web, plan.StaticRoot, new(plan.Env), license, description, DateTimeOffset.Now);
        var l = List(toolsRoot).Where(x => x.Id != e.Id).Append(e).OrderBy(x => x.Repo, StringComparer.OrdinalIgnoreCase).ToList();
        Directory.CreateDirectory(toolsRoot);
        File.WriteAllText(Path.Combine(toolsRoot, ListName), JsonSerializer.Serialize(l, J));
        audit?.Append("app", "source.install", $"{repo} {plan.Kind} {sha[..Math.Min(12, sha.Length)]}");
        return e;
    }

    /// <summary>Entfernt genau den Projektordner und den Eintrag. Laufzeiten bleiben (andere Projekte nutzen sie), siehe <see cref="UnusedRuntimes"/>.</summary>
    public static void Remove(string toolsRoot, string id, AuditLog? audit = null)
    {
        if (!id.StartsWith(Prefix, StringComparison.Ordinal) || id.Contains("..") || id.Contains('/') || id.Contains('\\')) throw new ArgumentException("Ungültiger Eintrag.");
        var d = Dir(toolsRoot, id);
        if (Directory.Exists(d)) { foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)) try { File.SetAttributes(f, FileAttributes.Normal); } catch (IOException) { } Directory.Delete(d, true); }
        var l = List(toolsRoot).Where(x => x.Id != id).ToList();
        File.WriteAllText(Path.Combine(toolsRoot, ListName), JsonSerializer.Serialize(l, J));
        audit?.Append("app", "source.remove", id);
    }

    /// <summary>Eingerichtete Laufzeiten, die kein Projekt mehr braucht (zum Aufräumen).</summary>
    public static IReadOnlyList<string> UnusedRuntimes(string toolsRoot)
    {
        var dir = Path.Combine(toolsRoot, "laufzeiten"); if (!Directory.Exists(dir)) return [];
        var used = List(toolsRoot).Select(x => x.Runtime).Where(x => x is not null).ToHashSet();
        return Directory.GetDirectories(dir).Where(d => Path.GetFileName(d) is var n && Runtimes.Any(r => n == $"{r.Id}-{r.Version}") && !used.Contains(Path.GetFileName(d).Split('-')[0])).ToList();
    }

    /// <summary>Ersetzt Platzhalter in einem Befehl: Laufzeiten, „{npm}“, „{port}“, „{rid}“.</summary>
    public static string Fill(string s, IReadOnlyDictionary<string, string> values) => Regex.Replace(s, @"\{(\w+)\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>Umgebung, damit alles im Werkzeugordner bleibt (kein Eintrag im Benutzerprofil): Python-Versionen, Caches, Go- und NuGet-Pakete.</summary>
    public static Dictionary<string, string> RuntimeEnv(string toolsRoot)
    {
        var lz = Path.Combine(toolsRoot, "laufzeiten");
        return new()
        {
            ["UV_PYTHON_INSTALL_DIR"] = Path.Combine(lz, "uv-python"), ["UV_CACHE_DIR"] = Path.Combine(lz, "uv-cache"), ["UV_PYTHON_PREFERENCE"] = "only-managed", ["UV_NO_MODIFY_PATH"] = "1",
            ["npm_config_cache"] = Path.Combine(lz, "npm-cache"), ["npm_config_update_notifier"] = "false",
            ["GOPATH"] = Path.Combine(lz, "go-pfad"), ["GOCACHE"] = Path.Combine(lz, "go-cache"), ["GOTELEMETRY"] = "off",
            ["NUGET_PACKAGES"] = Path.Combine(lz, "nuget"), ["DOTNET_CLI_HOME"] = Path.Combine(lz, "dotnet-home"), ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        };
    }
}
