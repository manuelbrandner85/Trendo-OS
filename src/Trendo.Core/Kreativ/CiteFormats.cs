using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Kreativ;

/// <summary>T-0735: Austauschformate für „Recherche mit Quellen“ – RIS (Zotero, Citavi, EndNote, Mendeley) hin und zurück,
/// CSL-JSON (Zotero, Pandoc/citeproc) als Export, und das Füllen einer Quelle aus den öffentlichen Crossref-Angaben einer DOI.
/// Alles ohne Netz; den Abruf macht die App über ihren vorhandenen Lese-Weg.</summary>
public static partial class CiteKit
{
    // ---------- RIS ----------

    static string RisType(Source s) => s.Kind switch { "artikel" => "JOUR", "webseite" => "ELEC", _ => "BOOK" };

    /// <summary>RIS-Text: je Quelle ein Block „TY  - …“ bis „ER  - “, Zeilenende CRLF wie in der Spezifikation.</summary>
    public static string Ris(IEnumerable<Source> sources)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            void T(string tag, string v) { v = OneLine(v); if (v.Length > 0) sb.Append(tag).Append("  - ").Append(v).Append("\r\n"); }
            T("TY", RisType(s));
            foreach (var p in People(s.Authors)) T("AU", p.First.Length == 0 ? p.Last : p.Last + ", " + p.First);
            T("TI", s.Title); T("PY", s.Year);
            if (s.Kind == "artikel") T("JO", s.Journal); else if (s.Kind == "webseite") T("T2", s.Journal);
            T("PB", s.Publisher); T("CY", s.Place); T("ET", s.Edition); T("VL", s.Volume); T("IS", s.Issue);
            var pages = PageRange(s.Pages).Split('–');
            if (pages[0].Length > 0) T("SP", pages[0]);
            if (pages.Length > 1) T("EP", pages[1]);
            T("UR", s.Url); T("DO", s.Doi.Trim().Length > 0 ? NormDoi(s.Doi) : "");
            if (Date(s.Accessed) is { } d) T("Y2", d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
            T("N1", s.Origin.Length > 0 ? "Herkunft: " + s.Origin : "");
            sb.Append("ER  - \r\n\r\n");
        }
        return sb.ToString();
    }

    static string OneLine(string? v) => System.Text.RegularExpressions.Regex.Replace((v ?? "").Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();

    public const int MaxRisChars = 5_000_000;

    /// <summary>Liest RIS (Zotero, Citavi, EndNote …). Unbekannte Kennzeichen werden übergangen; ein Block ohne „TY“ wird übersprungen und gemeldet.</summary>
    public static (List<Source> Sources, List<string> Warnings) ParseRis(string text)
    {
        var res = new List<Source>(); var warn = new List<string>();
        text ??= "";
        if (text.Length > MaxRisChars) { warn.Add("Die Datei ist zu groß (höchstens 5 MB)."); return (res, warn); }
        Dictionary<string, List<string>>? cur = null; var n = 0; string? lastTag = null;
        void Flush()
        {
            if (cur is null) return;
            n++;
            try { res.Add(FromRis(cur, res.Count)); }
            catch (FormatException e) { warn.Add($"Eintrag {n} übersprungen: {e.Message}"); }
            cur = null;
        }
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimStart('﻿');
            var m = System.Text.RegularExpressions.Regex.Match(line, @"^([A-Z][A-Z0-9])  -(?: (.*))?$");
            if (!m.Success)
            {
                // Fortsetzungszeile (manche Programme brechen lange Werte um)
                if (cur is not null && lastTag is not null && line.Trim().Length > 0 && cur[lastTag].Count > 0) cur[lastTag][^1] += " " + line.Trim();
                continue;
            }
            var tag = m.Groups[1].Value; var val = m.Groups[2].Value.Trim();
            if (tag == "TY") { Flush(); cur = new(StringComparer.Ordinal); }
            if (tag == "ER") { if (cur is null) warn.Add("„ER“ ohne Anfang übersprungen."); Flush(); lastTag = null; continue; }
            if (cur is null) { warn.Add($"Zeile „{tag}“ außerhalb eines Eintrags übersprungen."); continue; }
            if (!cur.TryGetValue(tag, out var list)) cur[tag] = list = [];
            list.Add(val); lastTag = tag;
            if (res.Count >= 5000) break;
        }
        if (cur is not null) { warn.Add($"Eintrag {n + 1} ohne „ER“ am Ende – trotzdem gelesen."); Flush(); }
        return (res, warn);
    }

    static Source FromRis(Dictionary<string, List<string>> f, int index)
    {
        string G(params string[] tags) { foreach (var t in tags) if (f.TryGetValue(t, out var v) && v.FirstOrDefault(x => x.Length > 0) is { } x) return x; return ""; }
        var ty = G("TY").ToUpperInvariant();
        if (ty.Length == 0) throw new FormatException("Art (TY) fehlt.");
        var url = G("UR", "L2");
        var kind = ty switch
        {
            "JOUR" or "JFULL" or "MGZN" or "NEWS" or "CONF" or "CPAPER" or "EJOUR" or "ABST" or "INPR" => "artikel",
            "ELEC" or "WEB" or "BLOG" => "webseite",
            "GEN" when url.Length > 0 => "webseite",
            _ => "buch",
        };
        var people = new List<string>();
        foreach (var t in new[] { "AU", "A1", "A2", "ED" })
        {
            if (!f.TryGetValue(t, out var l)) continue;
            people.AddRange(l.Where(x => x.Trim().Length > 0).Select(x => x.Trim()).Select(x => x.StartsWith('{') && x.EndsWith('}') ? x : x.Contains(',') ? x.TrimEnd(',') : "{" + x + "}"));
            if (people.Count > 0 && t is "AU" or "A1") break; // Herausgeber nur, wenn es keine Autoren gibt
        }
        var year = G("PY", "Y1", "DA");
        var ym = System.Text.RegularExpressions.Regex.Match(year, @"\d{4}"); year = ym.Success ? ym.Value : "";
        var sp = G("SP"); var ep = G("EP");
        var pages = sp.Contains('-') || sp.Contains('–') ? PageRange(sp) : sp.Length > 0 && ep.Length > 0 ? sp + "–" + ep : sp;
        var acc = G("Y2");
        var am = System.Text.RegularExpressions.Regex.Match(acc, @"(\d{4})/(\d{1,2})/(\d{1,2})");
        var accessed = am.Success ? $"{am.Groups[1].Value}-{int.Parse(am.Groups[2].Value, CultureInfo.InvariantCulture):00}-{int.Parse(am.Groups[3].Value, CultureInfo.InvariantCulture):00}" : "";
        var origin = G("N1"); origin = origin.StartsWith("Herkunft: ", StringComparison.Ordinal) ? origin["Herkunft: ".Length..] : "";
        var doi = G("DO", "M3");
        return new Source
        {
            Id = "q" + (index + 1).ToString(CultureInfo.InvariantCulture) + "-ris",
            Kind = kind, Authors = string.Join("; ", people), Title = G("TI", "T1", "CT", "BT"), Year = year,
            Publisher = G("PB"), Place = G("CY", "PP"), Edition = G("ET"),
            Journal = kind == "artikel" ? G("JO", "JF", "T2", "JA", "J2") : kind == "webseite" ? G("T2", "PB") : "",
            Volume = G("VL"), Issue = G("IS"), Pages = pages, Url = url,
            Doi = ValidDoi(doi) ? NormDoi(doi) : "", Accessed = accessed, Origin = origin,
        };
    }

    // ---------- CSL-JSON ----------

    /// <summary>CSL-JSON (Array) für Zotero, Pandoc/citeproc und andere CSL-Programme.</summary>
    public static string CslJson(IEnumerable<Source> sources)
    {
        var arr = new JsonArray(); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sources)
        {
            var o = new JsonObject { ["id"] = KeyFor(s, used), ["type"] = s.Kind switch { "artikel" => "article-journal", "webseite" => "webpage", _ => "book" } };
            void F(string name, string v) { v = OneLine(v); if (v.Length > 0) o[name] = v; }
            F("title", s.Title);
            var people = People(s.Authors);
            if (people.Count > 0) o["author"] = new JsonArray(people.Select(p => (JsonNode)(p.First.Length == 0 ? new JsonObject { ["literal"] = p.Last } : new JsonObject { ["family"] = p.Last, ["given"] = p.First })).ToArray());
            var y = System.Text.RegularExpressions.Regex.Match(s.Year, @"^\d{4}");
            if (y.Success) o["issued"] = new JsonObject { ["date-parts"] = new JsonArray(new JsonArray(int.Parse(y.Value, CultureInfo.InvariantCulture))) };
            F("publisher", s.Publisher); F("publisher-place", s.Place); F("edition", s.Edition); F("container-title", s.Journal);
            F("volume", s.Volume); F("issue", s.Issue); F("page", PageRange(s.Pages).Replace('–', '-')); F("URL", s.Url);
            F("DOI", s.Doi.Trim().Length > 0 ? NormDoi(s.Doi) : "");
            if (Date(s.Accessed) is { } d) o["accessed"] = new JsonObject { ["date-parts"] = new JsonArray(new JsonArray(d.Year, d.Month, d.Day)) };
            F("note", s.Origin.Length > 0 ? "Herkunft: " + s.Origin : "");
            arr.Add(o);
        }
        return arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    // ---------- DOI über Crossref ----------

    /// <summary>Öffentliche Crossref-Adresse (ohne Konto) für die Angaben zu einer DOI.</summary>
    public static Uri CrossrefUrl(string doi)
    {
        if (!ValidDoi(doi)) throw new ArgumentException("Das ist keine DOI. Sie sieht so aus: 10.1234/abc");
        return new Uri("https://api.crossref.org/works/" + Uri.EscapeDataString(NormDoi(doi)));
    }

    /// <summary>Füllt eine Quelle aus der Crossref-Antwort. Die Herkunft (Dienst, Adresse, Zeit, SHA-256 der Antwort) steht danach in <see cref="Source.Origin"/>.</summary>
    public static Source FromCrossref(byte[] json, Uri from, DateTimeOffset at, string id)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { throw new InvalidDataException("Crossref hat keine lesbaren Angaben geschickt."); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Crossref kennt diese DOI nicht.");
            string Str(string name) => m.TryGetProperty(name, out var v) ? v.ValueKind switch { JsonValueKind.String => v.GetString() ?? "", JsonValueKind.Array => v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").FirstOrDefault(x => x.Length > 0) ?? "", JsonValueKind.Number => v.GetRawText(), _ => "" } : "";
            var type = Str("type");
            var kind = type is "journal-article" or "proceedings-article" or "posted-content" or "peer-review" or "journal-issue" ? "artikel" : type is "dataset" or "report" && Str("URL").Length > 0 && Str("publisher").Length == 0 ? "webseite" : "buch";
            var people = new List<string>();
            foreach (var role in new[] { "author", "editor" })
            {
                if (!m.TryGetProperty(role, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var a in arr.EnumerateArray())
                {
                    string P(string n) => a.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
                    var fam = P("family"); var giv = P("given"); var lit = P("name");
                    if (fam.Length > 0) people.Add(giv.Length > 0 ? fam + ", " + giv : fam);
                    else if (lit.Length > 0) people.Add("{" + lit + "}");
                }
                if (people.Count > 0) break;
            }
            var year = "";
            foreach (var k in new[] { "published-print", "published-online", "published", "issued", "created" })
                if (m.TryGetProperty(k, out var dp) && dp.TryGetProperty("date-parts", out var parts) && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0
                    && parts[0].ValueKind == JsonValueKind.Array && parts[0].GetArrayLength() > 0 && parts[0][0].ValueKind == JsonValueKind.Number) { year = parts[0][0].GetInt32().ToString(CultureInfo.InvariantCulture); break; }
            var title = OneLine(System.Text.RegularExpressions.Regex.Replace(Str("title"), "<[^>]+>", ""));
            var sub = OneLine(Str("subtitle"));
            if (sub.Length > 0 && !title.Contains(sub, StringComparison.OrdinalIgnoreCase)) title = title.TrimEnd(':', ' ') + ": " + sub;
            if (title.Length == 0) throw new InvalidDataException("Crossref nennt keinen Titel zu dieser DOI.");
            var doi = Str("DOI");
            var sha = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
            return new Source
            {
                Id = id, Kind = kind, Authors = string.Join("; ", people), Title = title, Year = year,
                Publisher = kind == "artikel" ? "" : Str("publisher"), Place = kind == "artikel" ? "" : Str("publisher-location"), Edition = Str("edition-number"),
                Journal = kind == "artikel" ? OneLine(Str("container-title")) : "",
                Volume = Str("volume"), Issue = Str("issue"), Pages = PageRange(Str("page")),
                Url = "", Doi = ValidDoi(doi) ? NormDoi(doi) : "",
                Origin = $"Crossref · {from} · abgerufen {at.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)} · SHA-256 {sha[..16]}…",
            };
        }
    }
}
