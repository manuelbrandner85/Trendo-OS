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
                foreach (var f in info.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true })) if (f.IsReadOnly) f.IsReadOnly = false;
                info.Delete(true);
            }
        }
        if (File.Exists(OriginFile(full))) File.Delete(OriginFile(full));
    }

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
        if (sln is not null) Add("dotnet", $"dotnet build \"{Path.GetFileName(sln)}\"", ".NET-Lösung übersetzen");
        else if (Directory.EnumerateFiles(dir, "*.csproj", opt).FirstOrDefault() is { } csproj) Add("dotnet", $"dotnet build \"{Path.GetRelativePath(dir, csproj)}\"", ".NET-Projekt übersetzen");
        if (Has("package.json"))
        {
            Add("npm", "npm install", "Bausteine des Projekts in den Projektordner laden (node_modules)");
            try
            {
                using var pj = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")));
                if (pj.RootElement.TryGetProperty("scripts", out var sc) && sc.ValueKind == JsonValueKind.Object)
                {
                    if (sc.TryGetProperty("build", out _)) Add("npm", "npm run build", "Projekt bauen (Skript „build“)");
                    if (sc.TryGetProperty("test", out _)) Add("npm", "npm test", "Tests des Projekts");
                }
            }
            catch (Exception e) when (e is JsonException or IOException) { }
        }
        if (Has("Cargo.toml")) Add("cargo", "cargo build --release", "Rust-Projekt bauen");
        if (Has("go.mod")) Add("go", "go build ./...", "Go-Projekt bauen");
        if (Has("pyproject.toml") || Has("setup.py") || Has("requirements.txt"))
        {
            Add("python", "python -m compileall -q .", "Python-Dateien auf Schreibfehler prüfen");
            if (Has("requirements.txt")) Add("python", OperatingSystem.IsWindows() ? "python -m venv .venv && .venv\\Scripts\\python -m pip install -r requirements.txt" : "python -m venv .venv && .venv/bin/python -m pip install -r requirements.txt", "Bausteine in eine eigene Umgebung im Projektordner laden");
        }
        if (Has("CMakeLists.txt")) Add("cmake", "cmake -B build && cmake --build build", "C/C++-Projekt mit CMake bauen");
        if (Has("Makefile") && !Has("CMakeLists.txt")) Add("make", "make", "Projekt mit make bauen");
        if (Has("gradlew.bat") || Has("gradlew")) res.Add(new("gradlew", OperatingSystem.IsWindows() ? "gradlew.bat build" : "./gradlew build", "Java/Kotlin-Projekt mit Gradle bauen", hasTool("java")));
        else if (Has("pom.xml")) Add("mvn", "mvn package", "Java-Projekt mit Maven bauen");
        return res.OrderByDescending(h => h.Available).ToList();
    }
}
