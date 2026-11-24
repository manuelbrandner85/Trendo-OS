using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trendo.Core;

/// <summary>T-0688: Software-Studio – ein Projekt wirklich bauen, ausführen, testen und als Paket ausgeben.
/// Erkennt die Projektart an den Dateien, nennt den echten Befehl (dotnet, python, node) und wertet dessen Ergebnis aus:
/// Tests einzeln (bestanden/fehlgeschlagen, Datei und Zeile), Fehlerliste aus der Ausgabe, Paket mit Größe, Prüfsumme und Startprobe.
/// Fehlt ein Werkzeug, gibt es keinen Befehl, sondern eine Meldung mit dem Weg zum Einrichten – nichts wird zum Schein ausgeführt.</summary>
public static partial class DevBuild
{
    public enum Kind { None, DotNet, Python, Node, Web }

    /// <param name="Main">.NET: Projektdatei des Programms. Python/Node: Startdatei. Web: index.html.</param>
    /// <param name="Tests">.NET: Projektdatei der Tests. Python: „pytest“ oder „unittest“. Node/Web: „node“. Ohne Tests null.</param>
    /// <param name="Gui">.NET: Programm mit Fenster (WinExe).</param>
    public sealed record Project(Kind Kind, string Root, string? Main, string? Tests, bool Gui)
    {
        public string Name => Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        public string Label => Kind switch { Kind.DotNet => Gui ? ".NET-Programm mit Fenster" : ".NET-Programm", Kind.Python => "Python-Programm", Kind.Node => "Node-Programm", Kind.Web => "Web-App", _ => "unbekannte Projektart" };
    }

    public sealed record Cmd(string Exe, string[] Args, string Cwd, IDictionary<string, string>? Env = null, bool Utf8 = false)
    {
        public string Display => Exe + " " + string.Join(" ", Args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a));
    }

    /// <summary>Entweder ein Befehl – oder der Grund, warum es keinen gibt (<see cref="Blocked"/>: Werkzeug fehlt, <see cref="Note"/>: nichts zu tun).</summary>
    public sealed record Plan(Cmd? Cmd, string? Blocked = null, string? Note = null);

    public const string PackageFolder = "trendo-paket";
    static string Py => OperatingSystem.IsWindows() ? "python" : "python3";

    /// <summary>Tests: Werkzeuge, die als „nicht vorhanden“ gelten sollen (z. B. „dotnet“) – prüft die Meldung für fehlende Werkzeuge.</summary>
    public static ISet<string>? HideToolsForTests { get; set; }
    static bool Has(string tool) => HideToolsForTests?.Contains(tool) != true && Cli.Find(tool) is not null;

    static bool? _sdk;
    /// <summary>Ist ein .NET SDK da? (Die Laufzeit allein reicht zum Bauen nicht.)</summary>
    public static bool HasDotNetSdk()
    {
        if (!Has("dotnet")) return false;
        return _sdk ??= Cli.Run("dotnet", ["--list-sdks"], null, 20000) is { Ok: true } r && r.Out.Trim().Length > 0;
    }

    /// <summary>Meldung, wenn ein Werkzeug fehlt – mit dem Weg zum Einrichten. null = vorhanden.</summary>
    public static string? Missing(string tool) => tool switch
    {
        "dotnet" => HasDotNetSdk() ? null : "BLOCKIERT: Zum Bauen fehlt das .NET SDK auf diesem PC. Einrichten: das „.NET SDK“ von https://dotnet.microsoft.com/download installieren (kostenlos), danach Trendo neu starten. Bis dahin baut und testet Trendo dieses Projekt nicht.",
        "python" => Has(Py) ? null : "BLOCKIERT: Python fehlt auf diesem PC. Einrichten: unter „Zusatzprogramme“ Python laden oder von https://www.python.org installieren, danach Trendo neu starten. Bis dahin startet und testet Trendo dieses Projekt nicht.",
        "node" => Has("node") ? null : "BLOCKIERT: Node.js fehlt auf diesem PC. Einrichten: Node.js (LTS) von https://nodejs.org installieren, danach Trendo neu starten. Bis dahin startet und testet Trendo dieses Projekt nicht.",
        _ => Has(tool) ? null : $"BLOCKIERT: „{tool}“ fehlt auf diesem PC.",
    };

    // ───────────── Projekt erkennen ─────────────

    static IEnumerable<string> FilesOf(string root, int depth, string pattern)
    {
        IEnumerable<string> here; IEnumerable<string> dirs;
        try { here = Directory.GetFiles(root, pattern).Order(StringComparer.OrdinalIgnoreCase).ToList(); dirs = depth > 0 ? Directory.GetDirectories(root).Where(d => !CodeProject.Hidden.Contains(Path.GetFileName(d))).Order(StringComparer.OrdinalIgnoreCase).ToList() : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }
        foreach (var f in here) yield return f;
        foreach (var d in dirs) foreach (var f in FilesOf(d, depth - 1, pattern)) yield return f;
    }

    static string ReadSmall(string file) { try { return new FileInfo(file).Length < 2_000_000 ? File.ReadAllText(file) : ""; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; } }

    static bool IsTestProject(string csproj)
    {
        var t = ReadSmall(csproj);
        return t.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) || t.Contains("xunit", StringComparison.OrdinalIgnoreCase) || t.Contains("MSTest", StringComparison.OrdinalIgnoreCase)
            || t.Contains("NUnit", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(t, @"<IsTestProject>\s*true", RegexOptions.IgnoreCase);
    }

    public static Project Detect(string root)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) return new(Kind.None, root, null, null, false);
        var projects = FilesOf(root, 3, "*.csproj").ToList();
        if (projects.Count > 0)
        {
            var tests = projects.FirstOrDefault(IsTestProject);
            var apps = projects.Where(p => !IsTestProject(p)).ToList();
            // das Programm: zuerst eins direkt im Projektordner, sonst das erste mit Programm-Ausgabe
            var main = apps.FirstOrDefault(p => Path.GetDirectoryName(p) == root && Regex.IsMatch(ReadSmall(p), @"<OutputType>\s*(Win)?Exe", RegexOptions.IgnoreCase))
                ?? apps.FirstOrDefault(p => Regex.IsMatch(ReadSmall(p), @"<OutputType>\s*(Win)?Exe", RegexOptions.IgnoreCase)) ?? apps.FirstOrDefault();
            var gui = main is not null && Regex.IsMatch(ReadSmall(main), @"<OutputType>\s*WinExe", RegexOptions.IgnoreCase);
            return new(Kind.DotNet, root, main, tests, gui);
        }
        var nodeTests = FilesOf(root, 3, "*.test.js").Concat(FilesOf(root, 3, "*.test.mjs")).Any() ? "node" : null;
        if (File.Exists(Path.Combine(root, "index.html"))) return new(Kind.Web, root, Path.Combine(root, "index.html"), nodeTests, false);
        if (File.Exists(Path.Combine(root, "package.json")))
        {
            var entry = new[] { "index.js", "main.js", "app.js", "index.mjs" }.Select(n => Path.Combine(root, n)).FirstOrDefault(File.Exists);
            return new(Kind.Node, root, entry, nodeTests, false);
        }
        var py = FilesOf(root, 3, "*.py").ToList();
        if (py.Count > 0)
        {
            var entry = new[] { "main.py", "app.py", "__main__.py" }.Select(n => Path.Combine(root, n)).FirstOrDefault(File.Exists)
                ?? py.FirstOrDefault(f => Path.GetDirectoryName(f) == root && ReadSmall(f).Contains("__main__"));
            var hasTests = py.Any(f => Path.GetFileName(f).StartsWith("test", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).EndsWith("_test.py", StringComparison.OrdinalIgnoreCase));
            var wantsPytest = File.Exists(Path.Combine(root, "pytest.ini")) || File.Exists(Path.Combine(root, "conftest.py"))
                || new[] { "pyproject.toml", "requirements.txt", "requirements-dev.txt", "setup.cfg" }.Any(n => ReadSmall(Path.Combine(root, n)).Contains("pytest", StringComparison.OrdinalIgnoreCase));
            return new(Kind.Python, root, entry, wantsPytest ? "pytest" : hasTests ? "unittest" : null, false);
        }
        if (nodeTests is not null || FilesOf(root, 1, "*.js").Any()) return new(Kind.Node, root, new[] { "index.js", "main.js" }.Select(n => Path.Combine(root, n)).FirstOrDefault(File.Exists), nodeTests, false);
        return new(Kind.None, root, null, null, false);
    }

    static readonly Dictionary<string, string> PyEnv = new() { ["PYTHONIOENCODING"] = "utf-8", ["PYTHONUTF8"] = "1", ["PYTHONDONTWRITEBYTECODE"] = "1" };
    // Ausgaben von dotnet auf Deutsch und ohne Begrüßungstext; UTF-8, damit Umlaute in Meldungen stimmen
    static readonly Dictionary<string, string> NetEnv = new() { ["DOTNET_CLI_UI_LANGUAGE"] = "de", ["DOTNET_NOLOGO"] = "1", ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1", ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1" };

    // ───────────── Befehle ─────────────

    /// <summary>„Bauen“: .NET kompiliert, Python und Node prüfen die Schreibweise aller Dateien.</summary>
    public static Plan Build(Project p)
    {
        switch (p.Kind)
        {
            case Kind.DotNet:
                if (Missing("dotnet") is { } m) return new(null, m);
                if (p.Main is null) return new(null, Note: "In diesem Ordner liegt nur ein Testprojekt – „Testen“ baut es mit.");
                return new(new(Cli.Find("dotnet")!, ["build", p.Main, "--nologo", "-v", "m", "-clp:NoSummary"], p.Root, NetEnv, true));
            case Kind.Python:
                if (Missing("python") is { } mp) return new(null, mp);
                return new(new(Py, ["-I", "-X", "utf8", "-m", "compileall", "-q", "-x", @"(trendo-paket|\.venv|venv|\.git)[\\/]", "."], p.Root, PyEnv, true));
            case Kind.Node:
                if (Missing("node") is { } mn) return new(null, mn);
                return p.Main is null ? new(null, Note: "Keine Startdatei (index.js) gefunden.") : new(new("node", ["--check", p.Main], p.Root));
            case Kind.Web:
                return new(null, Note: "Eine Web-App braucht keinen Bau – „Ausführen“ zeigt sie, „Paket bauen“ verpackt sie.");
            default: return new(null, Note: "Für diesen Ordner kennt Trendo kein „Bauen“ (.NET, Python, Node oder Web-App).");
        }
    }

    /// <summary>„Ausführen“ für das ganze Projekt.</summary>
    public static Plan Run(Project p)
    {
        switch (p.Kind)
        {
            case Kind.DotNet:
                if (Missing("dotnet") is { } m) return new(null, m);
                if (p.Main is null) return new(null, Note: "In diesem Ordner liegt kein startbares Programm.");
                return new(new(Cli.Find("dotnet")!, ["run", "--project", p.Main], p.Root, NetEnv, true));
            case Kind.Python:
                if (Missing("python") is { } mp) return new(null, mp);
                return p.Main is null ? new(null, Note: "Keine Startdatei (main.py) gefunden – öffne die Datei, die starten soll.") : new(new(Py, [p.Main], p.Root, PyEnv, true));
            case Kind.Node:
                if (Missing("node") is { } mn) return new(null, mn);
                return p.Main is null ? new(null, Note: "Keine Startdatei (index.js) gefunden – öffne die Datei, die starten soll.") : new(new("node", [p.Main], p.Root));
            case Kind.Web:
                // mit Node: der kleine Server des Projekts auf einem freien Anschluss; ohne Node öffnet die Oberfläche index.html im Browser
                if (File.Exists(Path.Combine(p.Root, "server.js")) && Has("node")) return new(new("node", ["server.js", ".", "0"], p.Root));
                return new(null, Note: "browser");
            default: return new(null, Note: "Für diesen Ordner kennt Trendo kein „Ausführen“ (.NET, Python, Node oder Web-App).");
        }
    }

    /// <summary>„Testen“. Mit <paramref name="resultsDir"/> schreibt der Lauf zusätzlich eine maschinenlesbare Ergebnisdatei dorthin (für <see cref="ReadResults"/>).</summary>
    public static Plan Test(Project p, string? resultsDir = null)
    {
        switch (p.Kind)
        {
            case Kind.DotNet:
                if (Missing("dotnet") is { } m) return new(null, m);
                if (p.Tests is null) return new(null, Note: "Dieses Projekt hat noch kein Testprojekt. „Bauen“ prüft, ob es sich übersetzen lässt – Tests entstehen in einem eigenen Projekt (siehe Vorlage „C#-Programm“).");
                var a = new List<string> { "test", p.Tests, "--nologo" };
                if (resultsDir is not null) { a.Add("--logger"); a.Add("trx;LogFileName=ergebnis.trx"); a.Add("--results-directory"); a.Add(resultsDir); }
                return new(new(Cli.Find("dotnet")!, [.. a], p.Root, NetEnv, true));
            case Kind.Python:
                if (Missing("python") is { } mp) return new(null, mp);
                if (p.Tests == "pytest")
                {
                    if (Cli.Run(Py, ["-c", "import pytest"], p.Root, 20000).Ok) return new(new(Py, ["-m", "pytest", "-q", "-rA", "--no-header"], p.Root, PyEnv, true));
                    return new(null, "BLOCKIERT: Dieses Projekt testet mit pytest, aber pytest ist nicht eingerichtet. Einrichten: im Terminal „python -m pip install pytest“ eingeben.");
                }
                return p.Tests is null ? new(null, Note: "Dieses Projekt hat noch keine Tests (Dateien, die mit „test“ beginnen).") : new(new(Py, ["-m", "unittest", "-v"], p.Root, PyEnv, true));
            case Kind.Node or Kind.Web:
                if (p.Tests is null) return new(null, Note: p.Kind == Kind.Web ? "Diese Web-App hat noch keine Tests (Dateien mit „.test.js“)." : "Dieses Projekt hat noch keine Tests (Dateien mit „.test.js“).");
                if (Missing("node") is { } mn) return new(null, mn);
                return new(resultsDir is null ? new("node", ["--test"], p.Root)
                    : new("node", ["--test", "--test-reporter=spec", "--test-reporter-destination=stdout", "--test-reporter=tap", "--test-reporter-destination=" + Path.Combine(resultsDir, "ergebnis.tap")], p.Root));
            default: return new(null, Note: "Für dieses Projekt kennt Trendo keinen Test.");
        }
    }

    // ───────────── Testergebnisse ─────────────

    public sealed record TestCase(string Name, bool Passed, bool Skipped, string Message, string? File, int Line);
    public sealed record TestRun(IReadOnlyList<TestCase> Cases)
    {
        public int Passed => Cases.Count(c => c.Passed);
        public int Skipped => Cases.Count(c => c.Skipped);
        public int Failed => Cases.Count(c => !c.Passed && !c.Skipped);
        public string Summary => Cases.Count == 0 ? "Kein Test gefunden." : $"{Passed} bestanden, {Failed} fehlgeschlagen" + (Skipped > 0 ? $", {Skipped} übersprungen" : "");
    }

    /// <summary>Ergebnis eines Testlaufs lesen: aus der Ergebnisdatei (dotnet: TRX, node: TAP), sonst aus der Ausgabe (Python).</summary>
    public static TestRun ReadResults(Project p, string? resultsDir, string output)
    {
        try
        {
            if (resultsDir is not null && Directory.Exists(resultsDir))
            {
                if (Directory.GetFiles(resultsDir, "*.trx", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() is { } trx) return ParseTrx(File.ReadAllText(trx), p.Root);
                if (Path.Combine(resultsDir, "ergebnis.tap") is var tap && File.Exists(tap)) return ParseTap(File.ReadAllText(tap), p.Root);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException) { }
        return p.Kind == Kind.Python ? (p.Tests == "pytest" ? ParsePytest(output, p.Root) : ParseUnittest(output, p.Root)) : new([]);
    }

    static bool Inside(string root, string file)
    {
        try { return Path.GetFullPath(file).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    [GeneratedRegex(@"\s(?:in|bei)\s+(?<f>(?:[A-Za-z]:)?[^:\r\n]+?):(?:line|Zeile)\s+(?<l>\d+)")] private static partial Regex NetFrame();

    /// <summary>Ergebnisdatei von „dotnet test“ (TRX): je Test Ergebnis, Meldung und die erste Stelle im Projekt.</summary>
    public static TestRun ParseTrx(string xml, string root)
    {
        var doc = XDocument.Parse(xml);
        var list = new List<TestCase>();
        foreach (var r in doc.Descendants().Where(e => e.Name.LocalName == "UnitTestResult"))
        {
            var outcome = (string?)r.Attribute("outcome") ?? "";
            var err = r.Descendants().FirstOrDefault(e => e.Name.LocalName == "ErrorInfo");
            var msg = (err?.Elements().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value ?? "").Trim();
            var stack = err?.Elements().FirstOrDefault(e => e.Name.LocalName == "StackTrace")?.Value ?? "";
            string? file = null; var line = 0;
            foreach (Match m in NetFrame().Matches(stack))
            {
                var f = m.Groups["f"].Value.Trim();
                if (file is null || (!Inside(root, file) && Inside(root, f))) { file = f; line = int.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture); }
                if (Inside(root, f)) break;
            }
            list.Add(new((string?)r.Attribute("testName") ?? "?", outcome == "Passed", outcome is "NotExecuted" or "Skipped" or "Inconclusive", msg, file, line));
        }
        return new(list.OrderBy(c => c.Passed).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [GeneratedRegex(@"^(?<n>\w+) \((?<c>[^)]+)\)(?:\s*\n?.*?)? \.\.\. (?<r>ok|FAIL|ERROR|skipped.*|expected failure|unexpected success)\s*$", RegexOptions.Multiline)] private static partial Regex UtLine();
    [GeneratedRegex(@"^(?:FAIL|ERROR): (?<n>\w+) \((?<c>[^)]+)\)\r?\n-+\r?\n(?<b>.*?)(?=^(?:=+|-+)\r?$)", RegexOptions.Multiline | RegexOptions.Singleline)] private static partial Regex UtBlock();
    [GeneratedRegex(@"File ""(?<f>[^""]+)"", line (?<l>\d+)")] private static partial Regex PyFrame();

    /// <summary>Ausgabe von „python -m unittest -v“.</summary>
    public static TestRun ParseUnittest(string output, string root)
    {
        var text = output.Replace("\r\n", "\n");
        var blocks = new Dictionary<string, (string Msg, string? File, int Line)>();
        foreach (Match b in UtBlock().Matches(text))
        {
            var body = b.Groups["b"].Value.TrimEnd();
            string? file = null; var line = 0;
            foreach (Match f in PyFrame().Matches(body)) { var p = Path.IsPathRooted(f.Groups["f"].Value) ? f.Groups["f"].Value : Path.Combine(root, f.Groups["f"].Value); if (Inside(root, p)) { file = p; line = int.Parse(f.Groups["l"].Value, CultureInfo.InvariantCulture); } }
            var msg = body.Split('\n').LastOrDefault(l => l.Trim().Length > 0 && !l.StartsWith(' '))?.Trim() ?? body;
            blocks[b.Groups["n"].Value + "|" + b.Groups["c"].Value] = (msg, file, line);
        }
        var list = new List<TestCase>();
        foreach (Match m in UtLine().Matches(text))
        {
            var res = m.Groups["r"].Value; var key = m.Groups["n"].Value + "|" + m.Groups["c"].Value;
            var passed = res is "ok" or "expected failure"; var skipped = res.StartsWith("skipped", StringComparison.Ordinal);
            blocks.TryGetValue(key, out var b);
            list.Add(new(m.Groups["c"].Value.Contains('.') && m.Groups["c"].Value.EndsWith("." + m.Groups["n"].Value, StringComparison.Ordinal) ? m.Groups["c"].Value : m.Groups["c"].Value + "." + m.Groups["n"].Value,
                passed, skipped, passed ? "" : skipped ? res : b.Msg ?? res, b.File, b.Line));
        }
        return new(list.OrderBy(c => c.Passed).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [GeneratedRegex(@"^(?<r>PASSED|FAILED|ERROR|SKIPPED)\s+(?:\[\d+\]\s+)?(?<n>\S+)(?:\s+-\s+(?<m>.*))?$", RegexOptions.Multiline)] private static partial Regex PtLine();

    /// <summary>Kurzübersicht von „pytest -rA“ (PASSED/FAILED je Test).</summary>
    public static TestRun ParsePytest(string output, string root)
    {
        var text = output.Replace("\r\n", "\n");
        var list = new List<TestCase>();
        foreach (Match m in PtLine().Matches(text))
        {
            var name = m.Groups["n"].Value; var res = m.Groups["r"].Value;
            string? file = null; var line = 0;
            if (res is "FAILED" or "ERROR")
            {
                var rel = name.Split("::")[0];
                // Stelle des Fehlers: „datei.py:12: AssertionError“
                var at = Regex.Match(text, "^" + Regex.Escape(rel) + @":(?<l>\d+): ", RegexOptions.Multiline);
                file = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)); line = at.Success ? int.Parse(at.Groups["l"].Value, CultureInfo.InvariantCulture) : 1;
            }
            list.Add(new(name, res == "PASSED", res == "SKIPPED", m.Groups["m"].Value.Trim(), file, line));
        }
        return new(list.OrderBy(c => c.Passed).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [GeneratedRegex(@"^(?<i>\s*)(?<r>ok|not ok) \d+ - (?<n>.*?)(?<s>\s+# (?:SKIP|TODO).*)?$")] private static partial Regex TapLine();

    /// <summary>TAP-Ausgabe von „node --test“. Dateien und Gruppen (mit Untertests) zählen nicht als Test.</summary>
    public static TestRun ParseTap(string tap, string root)
    {
        var lines = tap.Replace("\r\n", "\n").Split('\n');
        var list = new List<TestCase>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = TapLine().Match(lines[i]);
            if (!m.Success) continue;
            var indent = m.Groups["i"].Value.Length;
            string type = "test", error = ""; string? file = null; var line = 0;
            // YAML-Block darunter
            for (var k = i + 1; k < lines.Length && (lines[k].StartsWith(new string(' ', indent + 2), StringComparison.Ordinal) || lines[k].Trim().Length == 0); k++)
            {
                var t = lines[k].Trim();
                if (t.StartsWith("type:", StringComparison.Ordinal)) type = t[5..].Trim().Trim('\'', '"');
                else if (t.StartsWith("error:", StringComparison.Ordinal) && error.Length == 0)
                {
                    error = t[6..].Trim().Trim('\'', '"');
                    if (error is "|-" or "|" or ">-") { var sb = new StringBuilder(); for (var q = k + 1; q < lines.Length && lines[q].StartsWith(new string(' ', indent + 6), StringComparison.Ordinal); q++) sb.Append(lines[q].Trim()).Append(' '); error = sb.ToString().Trim(); }
                }
                else if (t.StartsWith("location:", StringComparison.Ordinal))
                {
                    var loc = Regex.Match(t[9..].Trim().Trim('\'', '"'), @"^(?<f>.+?):(?<l>\d+):\d+$");
                    if (loc.Success) { file = loc.Groups["f"].Value.Replace("\\\\", "\\"); line = int.Parse(loc.Groups["l"].Value, CultureInfo.InvariantCulture); }
                }
            }
            if (type == "suite") continue;
            var name = m.Groups["n"].Value.Trim();
            // eine Testdatei als Ganzes (Name = Pfad): nur zählen, wenn sie selbst fehlschlug, ohne dass ein Test darin gemeldet wurde
            if (name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase)) { if (m.Groups["r"].Value == "ok" || error.Contains("subtest", StringComparison.OrdinalIgnoreCase)) continue; }
            if (file is not null && !Path.IsPathRooted(file)) file = Path.Combine(root, file);
            list.Add(new(name, m.Groups["r"].Value == "ok" && !m.Groups["s"].Success, m.Groups["s"].Success, error, file, line));
        }
        return new(list.OrderBy(c => c.Passed).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    // ───────────── Fehlerliste ─────────────

    public sealed record Problem(string File, int Line, int Column, bool Error, string Code, string Message);

    [GeneratedRegex(@"^\s*(?<f>(?:[A-Za-z]:)?[^:(\r\n]+?)\((?<l>\d+),(?<c>\d+)(?:,\d+,\d+)?\):\s*(?<s>error|warning|Fehler|Warnung)\s+(?<k>[A-Za-z]+\d+):\s*(?<m>.*?)(?:\s+\[[^\]]+\])?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex MsBuildLine();
    [GeneratedRegex(@"File ""(?<f>[^""]+)"", line (?<l>\d+)[^\n]*\n(?:[ \t]+[^\n]*\n)*?(?<m>[A-Za-z_][\w.]*(?:Error|Exception|Warning)[^\n]*)", RegexOptions.Multiline)] private static partial Regex PyError();
    [GeneratedRegex(@"^(?<f>(?:[A-Za-z]:)?[^:\r\n]+?\.(?:js|mjs|cjs|ts)):(?<l>\d+)\r?\n(?:.*\r?\n){1,6}?(?<m>\w*Error: [^\r\n]*)", RegexOptions.Multiline)] private static partial Regex NodeError();

    /// <summary>Fehler und Warnungen aus der Ausgabe von Bau, Lauf oder Test – mit Datei und Zeile zum Hinspringen. Doppelte Meldungen nur einmal.</summary>
    public static List<Problem> Problems(string output, string root)
    {
        var text = output.Replace("\r\n", "\n");
        var list = new List<Problem>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string file, int line, int col, bool error, string code, string msg)
        {
            file = file.Trim();
            if (file.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)) file = Uri.UnescapeDataString(file[8..]).Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(file)) file = Path.Combine(root, file);
            try { file = Path.GetFullPath(file); } catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return; }
            if (seen.Add($"{file}|{line}|{code}|{msg}")) list.Add(new(file, line, col, error, code, msg.Trim()));
        }
        foreach (Match m in MsBuildLine().Matches(text))
            Add(m.Groups["f"].Value, int.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["c"].Value, CultureInfo.InvariantCulture),
                m.Groups["s"].Value.ToLowerInvariant() is "error" or "fehler", m.Groups["k"].Value, m.Groups["m"].Value);
        foreach (Match m in PyError().Matches(text))
        {
            var f = Path.IsPathRooted(m.Groups["f"].Value) ? m.Groups["f"].Value : Path.Combine(root, m.Groups["f"].Value);
            if (Inside(root, f)) Add(f, int.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture), 1, true, "Python", m.Groups["m"].Value);
        }
        foreach (Match m in NodeError().Matches(text))
            Add(m.Groups["f"].Value, int.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture), 1, true, "Node", m.Groups["m"].Value);
        return list.OrderByDescending(p => p.Error).ThenBy(p => p.File, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Line).ToList();
    }

    // ───────────── Paket ─────────────

    /// <param name="Start">Datei, mit der das Paket startet (Programm, Startskript oder index.html).</param>
    /// <param name="Probe">Was die Startprobe getan und gesehen hat.</param>
    public sealed record Package(bool Ok, string Kind, string Folder, string? Zip, string? Start, long Bytes, long ZipBytes, string Sha256, bool ProbeOk, string Probe, string Message)
    {
        public static Package Fail(string message) => new(false, "", "", null, null, 0, 0, "", false, "", message);
    }

    public static string Rid()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        return (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-") + arch;
    }

    public static string Sha256(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }
    static long FolderBytes(string dir) => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);

    static string FreshOut(Project p, string suffix)
    {
        var parent = Path.Combine(p.Root, PackageFolder); Directory.CreateDirectory(parent);
        var dir = Path.Combine(parent, DevStudio.SafeName(p.Name) + "-" + suffix);
        if (Directory.Exists(dir)) Directory.Delete(dir, true); // nur der eigene Ausgabeordner im Projekt
        if (File.Exists(dir + ".zip")) File.Delete(dir + ".zip");
        Directory.CreateDirectory(dir);
        return dir;
    }

    static string Zip(string dir) { var zip = dir + ".zip"; ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, includeBaseDirectory: false); return zip; }

    static void CopyProject(Project p, string dst, Func<string, bool> skip)
    {
        foreach (var e in CodeProject.Tree(p.Root).Where(e => !e.IsDir))
        {
            var rel = Path.GetRelativePath(p.Root, e.Path);
            if (rel.Split(Path.DirectorySeparatorChar).Any(s => s.StartsWith('.')) || skip(rel.Replace('\\', '/'))) continue;
            var to = Path.Combine(dst, rel); Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(e.Path, to, true);
        }
    }

    /// <summary>„Paket bauen“: .NET als eigenständiges Programm (läuft ohne installiertes .NET) samt ZIP, Python und Node als ZIP mit Startskript,
    /// Web-App als Ordner und ZIP. Danach die Startprobe: das Paket in einen leeren Ordner auspacken und wirklich starten.</summary>
    public static async Task<Package> PublishAsync(Project p, Action<string> line, CancellationToken ct, bool probe = true)
    {
        try
        {
            string dir; string? start; string kind;
            switch (p.Kind)
            {
                case Kind.DotNet:
                {
                    if (Missing("dotnet") is { } m) return Package.Fail(m);
                    if (p.Main is null) return Package.Fail("In diesem Ordner liegt kein Programm, das sich verpacken lässt.");
                    var rid = Rid(); dir = FreshOut(p, rid); kind = $"Eigenständiges Programm ({rid}), läuft ohne installiertes .NET";
                    var cmd = new Cmd(Cli.Find("dotnet")!, ["publish", p.Main, "-c", "Release", "-r", rid, "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
                        "-p:DebugType=none", "-p:DebugSymbols=false", "--nologo", "-v", "m", "-o", dir], p.Root, NetEnv, true);
                    line("$ " + cmd.Display);
                    var r = await Cli.RunStreamAsync(cmd.Exe, cmd.Args, cmd.Cwd, line, ct, 900000, null, cmd.Env, new UTF8Encoding(false)).ConfigureAwait(false);
                    if (!r.Ok) return Package.Fail(r.Exit == Cli.Cancelled ? "Abgebrochen." : $"Das Verpacken ist fehlgeschlagen (Exit {r.Exit}). Die Meldungen stehen in der Ausgabe.");
                    var name = Path.GetFileNameWithoutExtension(p.Main);
                    start = OperatingSystem.IsWindows() ? Directory.GetFiles(dir, "*.exe").OrderByDescending(f => string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase)).ThenByDescending(f => new FileInfo(f).Length).FirstOrDefault()
                        : File.Exists(Path.Combine(dir, name)) ? Path.Combine(dir, name) : null;
                    if (start is null) return Package.Fail("Das Verpacken lief durch, aber im Ausgabeordner liegt kein Programm.");
                    break;
                }
                case Kind.Python or Kind.Node:
                {
                    if (p.Main is null) return Package.Fail(p.Kind == Kind.Python ? "Keine Startdatei (main.py) gefunden." : "Keine Startdatei (index.js) gefunden.");
                    var py = p.Kind == Kind.Python; var tool = py ? "python" : "node"; var entry = Path.GetRelativePath(p.Root, p.Main).Replace('\\', '/');
                    dir = FreshOut(p, py ? "python" : "node"); kind = py ? "ZIP mit Startskript (braucht Python auf dem Ziel-PC)" : "ZIP mit Startskript (braucht Node.js auf dem Ziel-PC)";
                    CopyProject(p, dir, _ => false);
                    var site = py ? "https://www.python.org" : "https://nodejs.org";
                    File.WriteAllText(Path.Combine(dir, "start.cmd"), $"@echo off\r\nwhere {tool} >nul 2>nul || (echo {(py ? "Python" : "Node.js")} fehlt. Bitte von {site} installieren. & exit /b 9009)\r\n{tool} \"%~dp0{entry.Replace('/', '\\')}\" %*\r\n", new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(dir, "start.sh"), $"#!/bin/sh\ncd \"$(dirname \"$0\")\" || exit 1\nexec {(py ? "python3" : "node")} \"{entry}\" \"$@\"\n", new UTF8Encoding(false));
                    line($"{Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count()} Dateien mit Startskript nach {dir}");
                    start = Path.Combine(dir, OperatingSystem.IsWindows() ? "start.cmd" : "start.sh");
                    break;
                }
                case Kind.Web:
                {
                    dir = FreshOut(p, "web"); kind = "Web-App als Ordner und ZIP (für jeden Webspeicher)";
                    CopyProject(p, dir, rel => rel is "package.json" or "package-lock.json" or "server.js" or "README.md" || rel.StartsWith("test/", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".test.mjs", StringComparison.OrdinalIgnoreCase));
                    line($"{Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count()} Dateien nach {dir}");
                    start = Path.Combine(dir, "index.html");
                    break;
                }
                default: return Package.Fail("Für diesen Ordner kennt Trendo kein Paket (.NET, Python, Node oder Web-App).");
            }
            ct.ThrowIfCancellationRequested();
            var zip = Zip(dir);
            var sha = Sha256(zip);
            line($"Paket: {zip} ({new FileInfo(zip).Length:N0} Bytes, SHA-256 {sha})");
            var (ok, text) = probe ? await ProbeAsync(p, zip, Path.GetRelativePath(dir, start), line, ct).ConfigureAwait(false) : (false, "Startprobe nicht ausgeführt.");
            line((ok ? "✓ Startprobe: " : "✗ Startprobe: ") + text);
            return new(true, kind, dir, zip, start, FolderBytes(dir), new FileInfo(zip).Length, sha, ok, text, ok ? "Paket fertig und gestartet." : "Paket fertig – die Startprobe ist nicht gelungen.");
        }
        catch (OperationCanceledException) { return Package.Fail("Abgebrochen."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { return Package.Fail("Das Verpacken ist fehlgeschlagen: " + e.Message); }
    }

    /// <summary>Startprobe: ZIP in einen leeren Ordner auspacken und das Paket von dort wirklich starten.
    /// Konsolenprogramm/Skript: muss mit Exit 0 enden, die Ausgabe wird gezeigt. Programm mit Fenster: das Fenster muss erscheinen, dann wird es beendet.
    /// Web-App: alle verknüpften Dateien müssen da sein; mit Node liefert zusätzlich ein Server die Seite wirklich aus.</summary>
    public static async Task<(bool Ok, string Text)> ProbeAsync(Project p, string zip, string startRel, Action<string>? line, CancellationToken ct)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-startprobe-" + Guid.NewGuid().ToString("N")[..10]);
        try
        {
            Directory.CreateDirectory(tmp); ZipFile.ExtractToDirectory(zip, tmp);
            var start = Path.Combine(tmp, startRel);
            if (!File.Exists(start)) return (false, $"Im ausgepackten Paket fehlt „{startRel}“.");
            string First(string s) { var l = s.Replace("\r\n", "\n").Split('\n').Where(x => x.Trim().Length > 0).Take(8).ToList(); return string.Join(" | ", l); }
            switch (p.Kind)
            {
                case Kind.DotNet when p.Gui: return await ProbeWindow(start, tmp, ct).ConfigureAwait(false);
                case Kind.DotNet:
                {
                    if (!OperatingSystem.IsWindows()) try { File.SetUnixFileMode(start, File.GetUnixFileMode(start) | UnixFileMode.UserExecute); } catch (IOException) { }
                    var r = await Cli.RunStreamAsync(start, [], tmp, line, ct, 60000).ConfigureAwait(false);
                    return (r.Ok, r.Ok ? $"gestartet aus leerem Ordner, Exit 0. Ausgabe: {First(r.Out)}" : $"Exit {r.Exit}. {First(r.Err + "\n" + r.Out)}");
                }
                case Kind.Python or Kind.Node:
                {
                    if (Missing(p.Kind == Kind.Python ? "python" : "node") is { } m) return (false, m);
                    var r = OperatingSystem.IsWindows() ? await Cli.RunStreamAsync("cmd", ["/c", start], tmp, line, ct, 60000).ConfigureAwait(false)
                        : await Cli.RunStreamAsync("sh", [start], tmp, line, ct, 60000).ConfigureAwait(false);
                    return (r.Ok, r.Ok ? $"Startskript aus leerem Ordner gestartet, Exit 0. Ausgabe: {First(r.Out)}" : $"Exit {r.Exit}. {First(r.Err + "\n" + r.Out)}");
                }
                case Kind.Web:
                {
                    var html = File.ReadAllText(start);
                    var refs = Regex.Matches(html, @"(?:src|href)\s*=\s*[""'](?<u>[^""'#?]+)", RegexOptions.IgnoreCase).Select(m => m.Groups["u"].Value)
                        .Where(u => !Regex.IsMatch(u, @"^(?:[a-z]+:|//)", RegexOptions.IgnoreCase)).Distinct().ToList();
                    var missing = refs.Where(u => !File.Exists(Path.Combine(tmp, u.Replace('/', Path.DirectorySeparatorChar)))).ToList();
                    if (missing.Count > 0) return (false, "Im Paket fehlen verknüpfte Dateien: " + string.Join(", ", missing));
                    var server = Path.Combine(p.Root, "server.js");
                    if (!File.Exists(server) || !Has("node")) return (true, $"index.html und {refs.Count} verknüpfte Dateien sind im Paket (nur Dateiprüfung – ohne Node kein Server-Lauf).");
                    return await ProbeWeb(server, tmp, refs, ct).ConfigureAwait(false);
                }
                default: return (false, "Für diese Projektart gibt es keine Startprobe.");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception or InvalidOperationException || e.GetType().Name == "HttpRequestException") { return (false, e.Message); } // QA-019: Kern ohne Netz-Bibliothek, Typ nur über den Namen
        finally { for (var i = 0; i < 20; i++) try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); break; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { await Task.Delay(150, CancellationToken.None).ConfigureAwait(false); } }
    }

    static async Task<(bool, string)> ProbeWindow(string exe, string cwd, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) try { File.SetUnixFileMode(exe, File.GetUnixFileMode(exe) | UnixFileMode.UserExecute); } catch (IOException) { }
        using var p = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = cwd }) ?? throw new InvalidOperationException("Das Programm ließ sich nicht starten.");
        var sw = Stopwatch.StartNew();
        try
        {
            while (sw.Elapsed < TimeSpan.FromSeconds(45))
            {
                ct.ThrowIfCancellationRequested();
                if (p.HasExited) return (false, $"Das Programm hat sich nach {sw.Elapsed.TotalSeconds:0.0} s selbst beendet (Exit {p.ExitCode}) – kein Fenster.");
                if (OperatingSystem.IsWindows())
                {
                    p.Refresh();
                    if (p.MainWindowHandle != IntPtr.Zero) return (true, $"gestartet aus leerem Ordner, Fenster „{p.MainWindowTitle}“ nach {sw.Elapsed.TotalSeconds:0.0} s offen, danach beendet.");
                }
                else if (sw.Elapsed > TimeSpan.FromSeconds(5)) return (true, "gestartet aus leerem Ordner, läuft nach 5 s noch, danach beendet (Fenster auf diesem System nicht prüfbar).");
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            return (false, "Nach 45 s ist kein Fenster erschienen.");
        }
        finally { try { if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException) { } }
    }

    static async Task<(bool, string)> ProbeWeb(string server, string dir, IReadOnlyList<string> refs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = Cli.RunStreamAsync("node", [server, dir, "0"], dir, l => { var m = Regex.Match(l, @"https?://127\.0\.0\.1:\d+"); if (m.Success) ready.TrySetResult(m.Value); }, cts.Token, 60000);
        try
        {
            if (await Task.WhenAny(ready.Task, run, Task.Delay(20000, ct)).ConfigureAwait(false) != ready.Task) return (false, "Der Server des Projekts hat sich nicht gemeldet.");
            var url = await ready.Task.ConfigureAwait(false);
            var (status, html) = await LocalGet(url + "/", ct).ConfigureAwait(false);
            if (status is < 200 or > 299) return (false, $"Die Seite antwortet mit {status}.");
            foreach (var u in refs) { var (st, _) = await LocalGet(url + "/" + u.TrimStart('/'), ct).ConfigureAwait(false); if (st is < 200 or > 299) return (false, $"„{u}“ antwortet mit {st}."); }
            var title = Regex.Match(html, @"<title>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["t"].Value.Trim();
            return (true, $"aus leerem Ordner über den Server des Projekts ausgeliefert ({url}): Seite „{System.Net.WebUtility.HtmlDecode(title)}“ und {refs.Count} verknüpfte Dateien antworten mit 200.");
        }
        finally { cts.Cancel(); try { await run.ConfigureAwait(false); } catch (OperationCanceledException) { } }
    }

    /// <summary>QA-019: Der Kern hat keine Netz-Bibliothek. Die Seite des lokalen Projekt-Servers holt deshalb Node (das den Server ohnehin
    /// ausführt) mit fetch – nur 127.0.0.1, Zeitgrenze 10 s. Erste Zeile der Ausgabe ist der HTTP-Status, danach der Inhalt.</summary>
    static async Task<(int Status, string Body)> LocalGet(string url, CancellationToken ct)
    {
        if (!Regex.IsMatch(url, @"^https?://127\.0\.0\.1:\d+/")) throw new InvalidOperationException("Abgefragt wird nur der lokale Server des Projekts.");
        const string js = "fetch(process.argv[1],{signal:AbortSignal.timeout(10000)}).then(async r=>{process.stdout.write(r.status+'\\n'+await r.text())}).catch(e=>{process.stderr.write(String(e));process.exit(2)})";
        var r = await Task.Run(() => Cli.Run("node", ["-e", js, url], null, 15000, encoding: System.Text.Encoding.UTF8), ct).ConfigureAwait(false);
        if (!r.Ok) return (0, r.Err);
        var cut = r.Out.IndexOf('\n');
        return int.TryParse(cut < 0 ? r.Out : r.Out[..cut], out var status) ? (status, cut < 0 ? "" : r.Out[(cut + 1)..]) : (0, r.Out);
    }

    public static string Size(long bytes) => bytes >= 1 << 20 ? (bytes / 1048576.0).ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + " MB" : bytes >= 1024 ? (bytes / 1024.0).ToString("0", CultureInfo.GetCultureInfo("de-DE")) + " KB" : bytes + " Bytes";
}
