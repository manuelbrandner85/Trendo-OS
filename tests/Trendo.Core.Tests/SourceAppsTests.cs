using Trendo.Core;
using Xunit;
using K = Trendo.Core.SourceApps.Kind;

namespace Trendo.Core.Tests;

/// T-0314: GitHub-Projekte ohne Windows-Programm – Erkennung, Einrichtungsplan, Laufzeiten mit Prüfsumme, Alternativen, Liste.
public class SourceAppsTests
{
    static SourceApps.Detection D(string repo, Dictionary<string, string> files) => SourceApps.Detect(repo, files.Keys.ToList(), f => files.GetValueOrDefault(f));
    static SourceApps.Plan P(string repo, Dictionary<string, string> files) => SourceApps.MakePlan(repo, files.Keys.ToList(), f => files.GetValueOrDefault(f));

    [Fact(DisplayName = "T-0314: Python mit [project.scripts] → startbar über uv; Plan legt Umgebung an, installiert das Projekt und startet das Skript")]
    public void Python_scripts()
    {
        var f = new Dictionary<string, string> { ["pyproject.toml"] = "[project]\nname = \"kalender-werkzeug\"\nrequires-python = \">=3.11\"\n\n[project.scripts]\nkalender = \"kw.cli:main\"\nhilfe = \"kw.cli:hilfe\"\n", ["kw/cli.py"] = "" };
        var d = D("erika/kalender", f);
        Assert.Equal(K.Python, d.Kind); Assert.True(d.Runnable); Assert.Equal("uv", d.Runtime); Assert.Equal("kalender-werkzeug", d.Package);
        var p = P("erika/kalender", f);
        Assert.Equal("venv .venv --python \">=3.11\"", p.Setup[0].Args);
        Assert.Equal("pip install --python .venv -e .", p.Setup[1].Args);
        Assert.Equal(".venv/Scripts/kalender.exe", p.Start!.Exe); // gleichnamiges Skript zuerst
        Assert.False(p.Web);
    }

    [Fact(DisplayName = "T-0314: Python app.py mit Gradio/Streamlit → Weboberfläche auf freiem Port nur an 127.0.0.1; requirements.txt wird installiert")]
    public void Python_web()
    {
        var g = new Dictionary<string, string> { ["app.py"] = "import gradio as gr\ngr.Interface(lambda x: x, 'text', 'text').launch()", ["requirements.txt"] = "gradio==5.0\n" };
        var p = P("max/bildchen", g);
        Assert.True(p.Web); Assert.Equal("{port}", p.Env["GRADIO_SERVER_PORT"]); Assert.Equal("127.0.0.1", p.Env["GRADIO_SERVER_NAME"]);
        Assert.Contains(p.Setup, s => s.Args == "pip install --python .venv -r requirements.txt");
        Assert.Equal(("\"app.py\"", ".venv/Scripts/python.exe"), (p.Start!.Args, p.Start.Exe));
        var s = P("max/tabelle", new() { ["main.py"] = "import streamlit as st", ["requirements.txt"] = "streamlit" });
        Assert.Contains("-m streamlit run \"main.py\" --server.port {port} --server.address 127.0.0.1", s.Start!.Args);
        Assert.Contains("Weboberfläche", D("max/tabelle", new() { ["main.py"] = "import streamlit as st" }).Text);
    }

    [Fact(DisplayName = "T-0314: Python-Paket mit __main__.py → „python -m paket“; reine Bibliothek ohne Startpunkt und C/Rust-Erweiterung sind ehrlich nicht startbar")]
    public void Python_module_library_native()
    {
        var m = P("erika/notiz", new() { ["pyproject.toml"] = "[project]\nname = \"notiz\"\n", ["src/notiz/__main__.py"] = "", ["src/notiz/__init__.py"] = "" });
        Assert.Equal("-m notiz", m.Start!.Args);
        var lib = D("erika/rechnen", new() { ["pyproject.toml"] = "[project]\nname = \"rechnen\"\n", ["rechnen/__init__.py"] = "", ["setup.py"] = "" });
        Assert.Equal(K.Bibliothek, lib.Kind); Assert.False(lib.Runnable); Assert.Contains("kein Programm", lib.Text);
        var nat = D("erika/schnell", new() { ["pyproject.toml"] = "[build-system]\nbuild-backend = \"maturin\"\n[project]\nname = \"schnell-py\"\n" });
        Assert.False(nat.Runnable); Assert.Equal("schnell-py", nat.Package); Assert.Contains("Visual-Studio-Build-Tools", nat.Why);
        Assert.Throws<InvalidOperationException>(() => P("erika/schnell", new() { ["pyproject.toml"] = "[build-system]\nbuild-backend = \"maturin\"\n" }));
        Assert.Equal("\"werkzeug.py\"", P("a/werkzeug", new() { ["werkzeug.py"] = "print(1)", ["test_werkzeug.py"] = "" }).Start!.Args); // einzelne Datei
    }

    [Fact(DisplayName = "T-0314: Node.js – Vite/Next als Web-App mit Port, Electron als Desktop-Programm, bin als Kommandozeile, Bibliothek ehrlich, index.html als Web-Seite")]
    public void Node_kinds()
    {
        var vite = P("max/planer", new() { ["package.json"] = """{"name":"planer","scripts":{"dev":"vite","build":"vite build"},"devDependencies":{"vite":"^7"}}""" });
        Assert.Equal(K.Web, vite.Kind); Assert.True(vite.Web); Assert.Equal("node", vite.Runtime);
        Assert.Equal("{npm} run dev -- --port {port} --host 127.0.0.1 --strictPort", vite.Start!.Args); Assert.Equal("{npm} install --no-fund --no-audit", vite.Setup[0].Args);
        var el = D("max/notizen", new() { ["package.json"] = """{"scripts":{"start":"electron ."},"devDependencies":{"electron":"^39"}}""" });
        Assert.Equal(K.Node, el.Kind); Assert.Contains("Electron", el.Text);
        var cli = P("max/umbenenner", new() { ["package.json"] = """{"name":"umbenenner","bin":{"umbenenner":"./bin/cli.js"}}""" });
        Assert.Equal("\"bin/cli.js\"", cli.Start!.Args);
        var lib = D("max/hilfen", new() { ["package.json"] = """{"name":"hilfen","main":"index.js"}""" });
        Assert.Equal(K.Bibliothek, lib.Kind); Assert.Equal("hilfen", lib.Package);
        var site = P("max/spiel", new() { ["package.json"] = """{"name":"spiel"}""", ["index.html"] = "<html>" });
        Assert.Equal(("", (string?)null), (site.StaticRoot, site.Runtime));
        Assert.Null(SourceApps.NodeInfo("{kaputt"));
    }

    [Fact(DisplayName = "T-0314: Reine Web-Seite (index.html, auch in docs/) läuft ohne Laufzeit über Trendos lokalen Server")]
    public void Static_site()
    {
        var d = D("erika/rezepte", new() { ["docs/index.html"] = "<html>", ["docs/app.js"] = "", ["README.md"] = "" });
        Assert.Equal(K.Web, d.Kind); Assert.True(d.Runnable); Assert.Null(d.Runtime); Assert.Contains("Ordner docs", d.Text);
        Assert.Equal("docs", P("erika/rezepte", new() { ["docs/index.html"] = "" }).StaticRoot);
    }

    [Fact(DisplayName = "T-0314: Go – package main im Hauptordner oder cmd/<name> wird zu einem Windows-Programm gebaut (ohne cgo); ohne main ist es eine Bibliothek")]
    public void Go()
    {
        var root = P("erika/zeit", new() { ["go.mod"] = "module x", ["main.go"] = "package main\nfunc main(){}" });
        Assert.Equal("build -o \"trendo-bin/zeit.exe\" .", root.Setup[0].Args); Assert.Equal("trendo-bin/zeit.exe", root.Start!.Exe); Assert.Equal("0", root.Env["CGO_ENABLED"]);
        var cmd = P("erika/werkzeugkiste", new() { ["go.mod"] = "", ["lib.go"] = "package kiste", ["cmd/server/main.go"] = "package main", ["cmd/werkzeugkiste/main.go"] = "package main" });
        Assert.EndsWith("./cmd/werkzeugkiste", cmd.Setup[0].Args);
        Assert.Equal(K.Bibliothek, D("erika/lib", new() { ["go.mod"] = "", ["lib.go"] = "package lib" }).Kind);
    }

    [Fact(DisplayName = "T-0314: .NET – SDK-Projekt (WinExe/Exe/Web) wird eigenständig für Windows gebaut; altes Framework-Format und Bibliotheken sind nicht startbar")]
    public void DotNet()
    {
        var p = P("max/rechner", new() { ["src/Rechner/Rechner.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>WinExe</OutputType></PropertyGroup></Project>",
            ["src/Hilfen/Hilfen.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>", ["tests/Rechner.Tests/Rechner.Tests.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>" });
        Assert.Equal("publish \"src/Rechner/Rechner.csproj\" -c Release -r {rid} --self-contained -o trendo-bin", p.Setup[0].Args); Assert.Equal("trendo-bin/Rechner.exe", p.Start!.Exe);
        var web = P("max/seite", new() { ["Seite.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>" });
        Assert.True(web.Web); Assert.Equal("http://127.0.0.1:{port}", web.Env["ASPNETCORE_URLS"]);
        var old = D("max/alt", new() { ["Alt.csproj"] = "<Project ToolsVersion=\"15.0\"><PropertyGroup><OutputType>WinExe</OutputType></PropertyGroup></Project>" });
        Assert.False(old.Runnable); Assert.Contains("Visual Studio", old.Why);
        Assert.Equal(K.Bibliothek, D("max/lib", new() { ["Lib.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>" }).Kind);
    }

    [Fact(DisplayName = "T-0314: Rust, C/C++, Docker und Unbekanntes werden ehrlich als nicht startbar erkannt – mit Begründung")]
    public void Not_runnable()
    {
        Assert.Equal((K.Rust, false), (D("a/b", new() { ["Cargo.toml"] = "", ["src/main.rs"] = "" }) is var r ? (r.Kind, r.Runnable) : default));
        Assert.Equal(K.Cpp, D("a/b", new() { ["CMakeLists.txt"] = "", ["main.cpp"] = "" }).Kind);
        Assert.Equal(K.Cpp, D("a/b", new() { ["Werkzeug.sln"] = "", ["x/x.vcxproj"] = "" }).Kind);
        var dk = D("a/b", new() { ["Dockerfile"] = "", ["README.md"] = "" }); Assert.Equal(K.Docker, dk.Kind); Assert.Contains("Admin", dk.Why);
        var u = D("a/b", new() { ["README.md"] = "", ["install.sh"] = "" }); Assert.Equal(K.Unbekannt, u.Kind); Assert.False(u.Runnable);
        // Python vor Docker: ein Python-Programm mit Dockerfile bleibt startbar
        Assert.True(D("a/b", new() { ["Dockerfile"] = "", ["main.py"] = "print(1)" }).Runnable);
    }

    [Fact(DisplayName = "T-0314: Laufzeiten – je Prozessor eine feste offizielle Fassung, nur HTTPS von erlaubten Servern, Prüfsumme sha256/sha512 wird geprüft")]
    public void Runtimes_and_hash()
    {
        foreach (var id in new[] { "uv", "node", "go", "dotnet" })
            foreach (var arm in new[] { false, true })
            {
                var r = SourceApps.RuntimeFor(id, arm);
                var u = new Uri(r.Url);
                Assert.Equal("https", u.Scheme);
                Assert.True(u.Host == "github.com" || SourceApps.RuntimeHosts.Contains(u.Host), u.Host);
                Assert.Matches(@"^sha(256:[0-9a-f]{64}|512:[0-9a-f]{128})$", r.Hash);
                Assert.Equal(arm, r.Url.Contains("arm64") || r.Url.Contains("aarch64"));
            }
        var f = Path.GetTempFileName();
        try
        {
            File.WriteAllText(f, "Trendo");
            Assert.True(SourceApps.HashOk(f, "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("Trendo"u8))));
            Assert.True(SourceApps.HashOk(f, "sha512:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA512.HashData("Trendo"u8)).ToUpperInvariant()));
            Assert.False(SourceApps.HashOk(f, "sha256:" + new string('0', 64)));
            Assert.False(SourceApps.HashOk(f, "md5:abc"));
        }
        finally { File.Delete(f); }
    }

    [Fact(DisplayName = "T-0314: GitHub-Antworten – Dateibaum, Forks, PyPI- und npm-Pakete werden robust gelesen; Adressen nur für gültige Projektnamen")]
    public void Parsers()
    {
        var t = SourceApps.ParseTree("""{"sha":"abc123","truncated":false,"tree":[{"path":"src","type":"tree"},{"path":"src/a.py","type":"blob"},{"path":"README.md","type":"blob"}]}""");
        Assert.Equal(["src/a.py", "README.md"], t.Files); Assert.Equal("abc123", t.Sha); Assert.False(t.Truncated);
        var forks = SourceApps.ParseForks("""[{"full_name":"max/werkzeug","stargazers_count":12,"license":{"spdx_id":"MIT"},"pushed_at":"2026-09-01T10:00:00Z"},{"full_name":"../x"},{"full_name":7}]""");
        Assert.Equal("max/werkzeug", Assert.Single(forks).FullName); Assert.Equal(("MIT", 12), (forks[0].LicenseId, forks[0].Stars));
        Assert.Empty(SourceApps.ParseForks("""{"message":"Not Found"}"""));
        Assert.Equal(("werkzeug", "2.1"), SourceApps.ParsePyPi("""{"info":{"name":"werkzeug","version":"2.1"}}"""));
        Assert.Null(SourceApps.ParsePyPi("kaputt"));
        Assert.Equal(("@erika/umbenenner", "1.2.0", "umbenenner"), SourceApps.ParseNpm("""{"name":"@erika/umbenenner","dist-tags":{"latest":"1.2.0"},"versions":{"1.2.0":{"bin":"cli.js"}}}"""));
        Assert.Equal("ren", SourceApps.ParseNpm("""{"name":"x","dist-tags":{"latest":"1"},"versions":{"1":{"bin":{"ren":"a.js"}}}}""")!.Value.Command);
        Assert.Null(SourceApps.ParseNpm("""{"name":"x","dist-tags":{"latest":"1"},"versions":{"1":{}}}"""));
        Assert.Throws<ArgumentException>(() => SourceApps.TreeUrl("../etc"));
        Assert.Equal("https://codeload.github.com/erika/werkzeug/zip/HEAD", SourceApps.ZipUrl("erika/werkzeug"));
        Assert.Equal("https://raw.githubusercontent.com/erika/w/HEAD/src/mein%20teil.py", SourceApps.RawUrl("erika/w", "src/mein teil.py"));
    }

    [Fact(DisplayName = "T-0314: Pakete als Alternative – PyPI über uv, npm mit Befehl; gefährliche Zeichen im Namen werden entfernt")]
    public void Package_plan()
    {
        var py = SourceApps.PackagePlan("pypi", "werkzeug-cli");
        Assert.Equal("pip install --python .venv werkzeug-cli", py.Setup[1].Args); Assert.Equal(".venv/Scripts/werkzeug-cli.exe", py.Start!.Exe);
        var np = SourceApps.PackagePlan("npm", "@erika/umbenenner", "umbenenner");
        Assert.Equal("{npm} install --no-fund --no-audit --prefix . @erika/umbenenner", np.Setup[0].Args); Assert.Equal("node_modules/.bin/umbenenner.cmd", np.Start!.Exe);
        Assert.Equal("pip install --python .venv xrm-rf", SourceApps.PackagePlan("pypi", "x\"&rm -rf").Setup[1].Args); // Leerzeichen, & und " fallen weg
        Assert.Equal("pip install --python .venv index-url", SourceApps.PackagePlan("pypi", "--index-url").Setup[1].Args); // nie als pip-Option
        Assert.Throws<ArgumentException>(() => SourceApps.PackagePlan("npm", "--"));
    }

    [Fact(DisplayName = "T-0314: Eintragen, Liste, Entfernen genau des Projektordners; ungenutzte Laufzeiten erkennen; Platzhalter füllen; Umgebung bleibt im Werkzeugordner")]
    public void Registry()
    {
        var tools = Path.Combine(Path.GetTempPath(), "trendo-src-" + Guid.NewGuid().ToString("N"));
        try
        {
            var audit = new AuditLog(Path.Combine(tools, "audit"));
            var plan = SourceApps.MakePlan("erika/zeit", ["go.mod", "main.go"], f => f == "main.go" ? "package main" : "");
            Directory.CreateDirectory(SourceApps.Dir(tools, SourceApps.IdFor("erika/zeit")));
            var e = SourceApps.Register(tools, "erika/zeit", "abc", plan, "MIT", "Zeiterfassung", audit);
            Assert.Equal("src-erika-zeit", e.Id);
            var back = Assert.Single(SourceApps.List(tools));
            Assert.Equal((K.Go, "go", "trendo-bin/zeit.exe", "0"), (back.Kind, back.Runtime, back.Start!.Exe, back.Env["CGO_ENABLED"]));
            var rt = SourceApps.RuntimeFor("go", false); var rtUv = SourceApps.RuntimeFor("uv", false);
            Directory.CreateDirectory(Path.Combine(SourceApps.RuntimeDir(tools, rt), "go", "bin")); File.WriteAllText(Path.Combine(SourceApps.RuntimeDir(tools, rt), "go", "bin", "go.exe"), "");
            Directory.CreateDirectory(SourceApps.RuntimeDir(tools, rtUv)); Directory.CreateDirectory(Path.Combine(tools, "laufzeiten", "uv-cache"));
            Assert.EndsWith(Path.Combine("go", "bin", "go.exe"), SourceApps.RuntimeExe(tools, rt));
            Assert.Equal(["uv-0.13.6"], SourceApps.UnusedRuntimes(tools).Select(Path.GetFileName));
            Assert.Throws<ArgumentException>(() => SourceApps.Remove(tools, "..\\x"));
            Assert.Throws<ArgumentException>(() => SourceApps.Remove(tools, "gh-erika-zeit"));
            SourceApps.Remove(tools, e.Id, audit);
            Assert.Empty(SourceApps.List(tools)); Assert.False(Directory.Exists(SourceApps.Dir(tools, e.Id)));
            Assert.Equal(2, SourceApps.UnusedRuntimes(tools).Count);
            Assert.Equal("run -p 8080 {unbekannt}", SourceApps.Fill("run -p {port} {unbekannt}", new Dictionary<string, string> { ["port"] = "8080" }));
            var env = SourceApps.RuntimeEnv(tools);
            Assert.All(env.Where(x => x.Key.EndsWith("DIR") || x.Key is "GOPATH" or "GOCACHE" or "NUGET_PACKAGES" or "npm_config_cache"), x => Assert.StartsWith(tools, x.Value));
            Assert.Equal("only-managed", env["UV_PYTHON_PREFERENCE"]);
        }
        finally { if (Directory.Exists(tools)) Directory.Delete(tools, true); }
    }
}
