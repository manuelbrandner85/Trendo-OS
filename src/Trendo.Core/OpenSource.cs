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
}
