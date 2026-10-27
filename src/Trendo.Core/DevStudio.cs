using System.Text;

namespace Trendo.Core;

/// <summary>CORE-07: Software-Studio – Projektvorlagen anlegen, Git-Stand lesen, bauen/testen mit vorhandenen Werkzeugen.</summary>
public sealed record DevTemplate(string Id, string Name, string Tool, string Description);
public sealed record GitState(bool IsRepo, string Branch, int Changed, IReadOnlyList<string> Files, string LastCommit);

public static class DevStudio
{
    public static readonly IReadOnlyList<DevTemplate> Templates =
    [
        new("web", "Webseite (HTML, CSS, JavaScript)", "", "Eine Seite, die sofort im Browser läuft."),
        new("python", "Python-Programm", "python3", "Ein kleines Programm mit Test."),
        new("dotnet-console", "C#-Programm (.NET)", "dotnet", "Ein Konsolenprogramm, das du bauen und starten kannst."),
        new("node", "JavaScript-Programm (Node)", "node", "Ein Node-Programm mit Test."),
    ];

    public static string SafeName(string name)
    {
        var s = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return string.IsNullOrEmpty(s) ? "projekt" : s.Length > 60 ? s[..60] : s;
    }

    /// <summary>Legt das Projekt aus der Vorlage an. Überschreibt nie einen vorhandenen Ordner.</summary>
    public static string Create(string parent, string templateId, string name)
    {
        var t = Templates.FirstOrDefault(x => x.Id == templateId) ?? throw new ArgumentException("Unbekannte Vorlage: " + templateId);
        var safe = SafeName(name);
        var dir = Path.Combine(parent, safe);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{safe}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        void W(string rel, string text) { var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, text.Replace("\r\n", "\n"), new UTF8Encoding(false)); }
        W("README.md", $"# {name}\n\nVorlage: {t.Name}.\n");
        W(".gitignore", "bin/\nobj/\nnode_modules/\n__pycache__/\n*.pyc\n");
        switch (t.Id)
        {
            case "web":
                W("index.html", $"<!doctype html>\n<html lang=\"de\">\n<head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(name)}</title><link rel=\"stylesheet\" href=\"style.css\"></head>\n<body><h1>{System.Net.WebUtility.HtmlEncode(name)}</h1><button id=\"b\">Klick mich</button><p id=\"o\"></p><script src=\"app.js\"></script></body>\n</html>\n");
                W("style.css", "body{font-family:system-ui,sans-serif;background:#0b1730;color:#eef;padding:2rem}button{padding:.5rem 1rem}\n");
                W("app.js", "let n=0;document.getElementById('b').onclick=()=>{n++;document.getElementById('o').textContent=n+' mal geklickt';};\n");
                break;
            case "python":
                W("main.py", "def gruss(name: str) -> str:\n    return f\"Hallo {name}!\"\n\n\nif __name__ == \"__main__\":\n    print(gruss(\"Welt\"))\n");
                W("test_main.py", "import unittest\nfrom main import gruss\n\n\nclass T(unittest.TestCase):\n    def test_gruss(self):\n        self.assertEqual(gruss(\"Anna\"), \"Hallo Anna!\")\n\n\nif __name__ == \"__main__\":\n    unittest.main()\n");
                break;
            case "dotnet-console":
                W(safe + ".csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <OutputType>Exe</OutputType>\n    <TargetFramework>net10.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n</Project>\n");
                W("Program.cs", "Console.WriteLine(\"Hallo aus Trendo OS!\");\n");
                break;
            case "node":
                W("package.json", "{\n  \"name\": \"" + safe.ToLowerInvariant() + "\",\n  \"version\": \"0.1.0\",\n  \"type\": \"module\",\n  \"scripts\": { \"start\": \"node index.js\", \"test\": \"node --test\" }\n}\n");
                W("index.js", "export const gruss = (n) => `Hallo ${n}!`;\nif (import.meta.url === `file://${process.argv[1]}`) console.log(gruss('Welt'));\n");
                W("index.test.js", "import test from 'node:test';\nimport assert from 'node:assert';\nimport { gruss } from './index.js';\ntest('gruss', () => assert.equal(gruss('Anna'), 'Hallo Anna!'));\n");
                break;
        }
        return dir;
    }

    /// <summary>Was „Testen“ für den Projektordner bedeutet (erkannt an den Dateien).</summary>
    public static (string Exe, string[] Args)? TestCommand(string dir)
    {
        if (Directory.GetFiles(dir, "*.csproj").Length > 0) return ("dotnet", ["build", "--nologo", "-v", "q"]);
        if (File.Exists(Path.Combine(dir, "package.json"))) return ("node", ["--test"]);
        if (Directory.GetFiles(dir, "test_*.py").Length > 0) return (OperatingSystem.IsWindows() ? "python" : "python3", ["-m", "unittest", "-q"]);
        return null;
    }

    public static Cli.Result RunTests(string dir, int timeoutMs = 180000) =>
        TestCommand(dir) is { } c ? Cli.Run(c.Exe, c.Args, dir, timeoutMs) : new(-1, "", "Für dieses Projekt kennt Trendo keinen Test.");

    public static Cli.Result GitInit(string dir)
    {
        var r = Cli.Run("git", ["init", "-q", "-b", "main"], dir);
        if (!r.Ok) return r;
        Cli.Run("git", ["add", "-A"], dir);
        return Cli.Run("git", ["-c", "user.name=Trendo", "-c", "user.email=trendo@localhost", "commit", "-q", "-m", "Erster Stand"], dir);
    }

    public static GitState Git(string dir)
    {
        var top = Cli.Run("git", ["rev-parse", "--is-inside-work-tree"], dir, 10000);
        if (!top.Ok || top.Out.Trim() != "true") return new(false, "", 0, [], "");
        var br = Cli.Run("git", ["branch", "--show-current"], dir, 10000).Out.Trim();
        var st = Cli.Run("git", ["status", "--porcelain"], dir, 10000).Out.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Length > 3 ? l[3..] : l).ToList();
        var last = Cli.Run("git", ["log", "-1", "--format=%s (%cr)"], dir, 10000);
        return new(true, br, st.Count, st, last.Ok ? last.Out.Trim() : "noch kein Stand");
    }

    public static Cli.Result Commit(string dir, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return new(-1, "", "Bitte eine kurze Beschreibung eingeben.");
        Cli.Run("git", ["add", "-A"], dir);
        return Cli.Run("git", ["-c", "user.name=Trendo", "-c", "user.email=trendo@localhost", "commit", "-q", "-m", message], dir);
    }
}
