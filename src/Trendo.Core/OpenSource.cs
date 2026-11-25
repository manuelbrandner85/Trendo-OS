using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-06: Offene Projekte auf GitHub finden (ohne Konto), Lizenz verständlich erklären, Kopie mit Herkunftsnachweis holen.</summary>
public sealed record OssRepo(string FullName, string Description, string Url, string CloneUrl, int Stars, string LicenseId, string Language, DateTimeOffset Updated);

public static class OpenSource
{
    public enum LicenseKind { Frei, FreiMitWeitergabe, Unklar }

    public static LicenseKind Classify(string spdx) => spdx.ToUpperInvariant() switch
    {
        "MIT" or "APACHE-2.0" or "BSD-2-CLAUSE" or "BSD-3-CLAUSE" or "ISC" or "UNLICENSE" or "0BSD" or "ZLIB" or "CC0-1.0" or "MPL-2.0" or "BSL-1.0" => LicenseKind.Frei,
        "GPL-2.0" or "GPL-3.0" or "AGPL-3.0" or "LGPL-2.1" or "LGPL-3.0" or "EPL-2.0" => LicenseKind.FreiMitWeitergabe,
        _ => LicenseKind.Unklar,
    };

    public static string Explain(string spdx) => Classify(spdx) switch
    {
        LicenseKind.Frei => $"{spdx}: Du darfst es nutzen und ändern, auch für Geld. Nenne die Urheber.",
        LicenseKind.FreiMitWeitergabe => $"{spdx}: Nutzen erlaubt. Gibst du Geändertes weiter, muss es unter derselben Lizenz offen bleiben.",
        _ => "Keine klare Lizenz: nur ansehen, nicht weiterverwenden.",
    };

    public static string SearchUrl(string query, string? language = null, bool onlyLicensed = true)
    {
        var q = query.Trim();
        if (!string.IsNullOrWhiteSpace(language)) q += " language:" + language.Trim();
        q += " archived:false";
        return "https://api.github.com/search/repositories?per_page=20&sort=stars&q=" + Uri.EscapeDataString(q);
    }

    public static IReadOnlyList<OssRepo> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<OssRepo>();
        if (!doc.RootElement.TryGetProperty("items", out var items)) return list;
        foreach (var it in items.EnumerateArray())
        {
            string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
            var lic = it.TryGetProperty("license", out var l) && l.ValueKind == JsonValueKind.Object ? S(l, "spdx_id") : "";
            list.Add(new(S(it, "full_name"), S(it, "description"), S(it, "html_url"), S(it, "clone_url"),
                it.TryGetProperty("stargazers_count", out var st) ? st.GetInt32() : 0, lic is "NOASSERTION" ? "" : lic, S(it, "language"),
                DateTimeOffset.TryParse(S(it, "pushed_at"), out var d) ? d : DateTimeOffset.MinValue));
        }
        return list;
    }

    /// <summary>Holt eine Kopie (nur neuester Stand) und legt eine Herkunftsdatei daneben.</summary>
    public static Cli.Result Fetch(string cloneUrl, string targetParent, string fullName, string licenseId)
    {
        if (!(cloneUrl.StartsWith("https://", StringComparison.Ordinal) || cloneUrl.StartsWith("file://", StringComparison.Ordinal)))
            return new(-1, "", "Nur sichere Adressen (https) sind erlaubt.");
        var dir = Path.Combine(targetParent, DevStudio.SafeName(fullName.Replace('/', '_')));
        if (Directory.Exists(dir)) return new(-1, "", "Das Projekt ist schon da.");
        var r = Cli.Run("git", ["clone", "--depth", "1", "--quiet", cloneUrl, dir], targetParent, 300000);
        if (r.Ok)
            File.WriteAllText(Path.Combine(targetParent, Path.GetFileName(dir) + ".herkunft.txt"),
                $"Quelle: {cloneUrl}\nProjekt: {fullName}\nLizenz: {(licenseId == "" ? "unklar" : licenseId)}\n{Explain(licenseId)}\nGeholt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n");
        return r;
    }

    /// <summary>T-0762: Herkunftsdatei eines geholten Projekts (liegt neben dem Ordner).</summary>
    public static string OriginFile(string projectDir) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectDir))!, Path.GetFileName(Path.GetFullPath(projectDir)) + ".herkunft.txt");

    /// <summary>T-0762: Geholtes Projekt restlos entfernen – Ordner (Git legt schreibgeschützte Dateien an) und Herkunftsdatei.
    /// Nur Ordner direkt in <paramref name="parent"/>; Verknüpfungen werden nur gelöst, nie verfolgt.</summary>
    public static void Remove(string parent, string projectDir)
    {
        var full = Path.GetFullPath(projectDir);
        if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nur Projekte im Ordner „Open Source“ lassen sich hier entfernen.");
        var info = new DirectoryInfo(full);
        if (info.Exists)
        {
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) info.Delete();
            else
            {
                // B7: selbst gehen – Verknüpfungen (Junction, Symlink) werden einzeln gelöst und nie betreten; Schreibschutz nur im eigenen Baum aufheben
                var todo = new Stack<DirectoryInfo>(); todo.Push(info);
                while (todo.Count > 0)
                {
                    var d = todo.Pop();
                    foreach (var e in d.EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = 0, IgnoreInaccessible = true }))
                    {
                        if (e.Attributes.HasFlag(FileAttributes.ReparsePoint)) { e.Delete(); continue; } // nur die Verknüpfung, nicht das Ziel
                        if (e is DirectoryInfo sub) todo.Push(sub);
                        else if (e.Attributes.HasFlag(FileAttributes.ReadOnly)) e.Attributes &= ~FileAttributes.ReadOnly;
                    }
                }
                info.Delete(true);
            }
        }
        if (File.Exists(OriginFile(full))) File.Delete(OriginFile(full));
    }

    /// <summary>Fester Hinweis über der Bauanleitung (B1).</summary>
    public const string BuildWarning = "Bauen führt Code aus diesem fremden Projekt mit deinen Rechten aus.";

    /// <summary>B1: Erster Bau-Lauf je geholtem Projekt braucht eine eigene Bestätigung – vermerkt in der Herkunftsdatei (liegt außerhalb des Projekts).</summary>
    public static bool BuildApproved(string projectDir) => File.Exists(OriginFile(projectDir)) && File.ReadAllLines(OriginFile(projectDir)).Any(l => l.StartsWith("Bauen erlaubt:", StringComparison.Ordinal));
    public static void ApproveBuild(string projectDir) => File.AppendAllText(OriginFile(projectDir), $"Bauen erlaubt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n");

    /// <summary>Erkannter Bauschritt eines Quellcode-Projekts: Werkzeug, Befehl (läuft im Projektordner), wozu, ob das Werkzeug auf diesem PC da ist.</summary>
    public sealed record BuildHint(string Tool, string Command, string Why, bool Available);

    /// <summary>T-0762 (X-06): Bauanleitung aus den Projektdateien erkennen (dotnet, npm, cargo, go, Python, CMake, make, Gradle, Maven).
    /// Nur Vorschläge – nichts wird global installiert; Python-Pakete landen in einer eigenen Umgebung im Projektordner.</summary>
    public static IReadOnlyList<BuildHint> BuildHints(string dir, Func<string, bool>? hasTool = null)
    {
        hasTool ??= t => Cli.Find(t) is not null;
        var res = new List<BuildHint>();
        bool Has(string n) => File.Exists(Path.Combine(dir, n));
        void Add(string tool, string cmd, string why) => res.Add(new(tool, cmd, why, hasTool(tool)));
        var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var sln = Directory.EnumerateFiles(dir, "*.sln", new EnumerationOptions { IgnoreInaccessible = true }).Concat(Directory.EnumerateFiles(dir, "*.slnx", new EnumerationOptions { IgnoreInaccessible = true })).FirstOrDefault();
        const string msb = " – Achtung: Projektdateien (MSBuild-Ziele, Analyzer, Quellgeneratoren) können beim Bauen beliebige Befehle ausführen.";
        if (sln is not null) Add("dotnet", $"dotnet build \"{Path.GetFileName(sln)}\"", ".NET-Lösung übersetzen" + msb);
        else if (Directory.EnumerateFiles(dir, "*.csproj", opt).FirstOrDefault() is { } csproj) Add("dotnet", $"dotnet build \"{Path.GetRelativePath(dir, csproj)}\"", ".NET-Projekt übersetzen" + msb);
        if (Has("package.json"))
        {
            Add("npm", "npm install", "Bausteine des Projekts in den Projektordner laden (node_modules) – Achtung: Installationsskripte (preinstall/postinstall) der Bausteine laufen dabei mit deinen Rechten.");
            try
            {
                using var pj = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")));
                if (pj.RootElement.TryGetProperty("scripts", out var sc) && sc.ValueKind == JsonValueKind.Object)
                {
                    if (sc.TryGetProperty("build", out _)) Add("npm", "npm run build", "Projekt bauen – führt das Skript „build“ aus package.json aus (beliebige Befehle möglich)");
                    if (sc.TryGetProperty("test", out _)) Add("npm", "npm test", "Tests des Projekts – führt das Skript „test“ aus package.json aus");
                }
            }
            catch (Exception e) when (e is JsonException or IOException) { }
        }
        if (Has("Cargo.toml")) Add("cargo", "cargo build --release", "Rust-Projekt bauen – Achtung: build.rs und Makros des Projekts laufen dabei mit deinen Rechten");
        if (Has("go.mod")) Add("go", "go build ./...", "Go-Projekt bauen (cgo kann C-Compiler aufrufen)");
        if (Has("pyproject.toml") || Has("setup.py") || Has("requirements.txt"))
        {
            // B1: -I (isoliert) – sonst lädt Python eine projekteigene compileall.py/venv.py aus dem Arbeitsordner statt des eigenen Moduls
            Add("python", "python -I -m compileall -q .", "Python-Dateien auf Schreibfehler prüfen (isoliert: Python lädt dabei keine Module aus dem Projekt)");
            if (Has("requirements.txt")) Add("python", OperatingSystem.IsWindows() ? "python -I -m venv .venv && .venv\\Scripts\\python -I -m pip install -r requirements.txt" : "python -I -m venv .venv && .venv/bin/python -I -m pip install -r requirements.txt", "Bausteine in eine eigene Umgebung im Projektordner laden – Achtung: Bausteine, die erst gebaut werden (setup.py), führen dabei Code aus");
        }
        if (Has("CMakeLists.txt")) Add("cmake", "cmake -B build && cmake --build build", "C/C++-Projekt mit CMake bauen – CMakeLists.txt kann beliebige Befehle ausführen");
        if (Has("Makefile") && !Has("CMakeLists.txt")) Add("make", "make", "Projekt mit make bauen – das Makefile führt beliebige Befehle aus");
        // N1: Skripte aus dem Projekt immer mit ausdrücklichem Pfad (.\…) – das Terminal sucht Programme absichtlich nicht im Projektordner
        var win = OperatingSystem.IsWindows();
        if (Has("gradlew.bat") || Has("gradlew"))
            res.Add(new("gradlew", win ? @".\gradlew.bat build" : "./gradlew build", "Java/Kotlin-Projekt mit Gradle bauen – gradlew ist ein Skript aus dem Projekt selbst und lädt Gradle nach", hasTool("java") && (!win || Has("gradlew.bat"))));
        else if (Has("pom.xml"))
        {
            if (win ? Has("mvnw.cmd") : Has("mvnw")) res.Add(new("mvnw", win ? @".\mvnw.cmd package" : "./mvnw package", "Java-Projekt mit dem Maven-Wrapper bauen – mvnw ist ein Skript aus dem Projekt selbst und lädt Maven nach", hasTool("java")));
            else Add("mvn", "mvn package", "Java-Projekt mit Maven bauen – Erweiterungen aus pom.xml laufen mit deinen Rechten");
        }
        return res.OrderByDescending(h => h.Available).ToList();
    }
}
