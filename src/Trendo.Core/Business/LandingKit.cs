using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

/// <summary>STUDIO-LANDING-01: Baustein einer Verkaufsseite. Arten: held (Title, Text, Src, Label), nutzen (Items: Titel|Text je Zeile), preis (Title, Price, OldPrice, Unit, Items = Leistungen, Label),
/// bewertungen (Items: Name|Text|Sterne je Zeile), faq (Items: Frage|Antwort je Zeile), kaufen (Title, Label), text (Title, Text), bild (Src, Alt).</summary>
public sealed record LpBlock(string Id, string Kind, string Title = "", string Text = "", string Src = "", string Alt = "", string Label = "", string Price = "", string OldPrice = "",
    string Unit = "", string Items = "");

/// <summary>Seite mit Grundangaben: Name, Beschreibung (für Suchmaschinen), Farbe, Kauf-Link (extern, z. B. Stripe-Bezahllink), Impressum und Datenschutz.</summary>
public sealed record LpDoc(string Name, string Description, string Accent, string BuyUrl, string Impressum, string Privacy, IReadOnlyList<LpBlock> Blocks, bool SmallBusiness = false);

public sealed record LpFinding(string Text, bool Error);

/// <summary>Verkaufsseite wie Carrd/Leadpages/Shopify-Produktseite (Clean-Room): eine schnelle, statische Seite aus Bausteinen, mobil zuerst, ohne Skripte,
/// mit Pflichtseiten Impressum und Datenschutz. Bezahlt wird nie in Trendo – der Kaufen-Knopf führt zu einem externen Link.</summary>
public static partial class LandingKit
{
    public static readonly (string Id, string Name)[] Kinds =
        [("held", "Held (Aufmacher)"), ("nutzen", "Nutzen"), ("preis", "Preis"), ("bewertungen", "Bewertungen"), ("faq", "Häufige Fragen"), ("kaufen", "Kaufen-Knopf"), ("text", "Text"), ("bild", "Bild")];

    static string E(string s) => WebUtility.HtmlEncode(s ?? "");
    static string Col(string c) => c is not null && Regex.IsMatch(c, "^#[0-9A-Fa-f]{6}$") ? c : "#B45309";
    public static bool SafeUrl(string u) => Regex.IsMatch((u ?? "").Trim(), @"^https://[^\s""'<>]+$", RegexOptions.IgnoreCase);

    /// <summary>Zeilen „A | B | C“ eines Bausteins.</summary>
    public static List<string[]> Rows(string items) =>
        [.. (items ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('|').Select(p => p.Trim()).ToArray()).Where(p => p.Length > 0 && p[0].Length > 0)];

    /// <summary>Bilddatei → Name im Ordner „bilder/“ (gleiche Datei = gleicher Name).</summary>
    public static string ImageName(string src) => "bilder/" + Regex.Replace(Path.GetFileName(src ?? "").ToLowerInvariant(), @"[^a-z0-9._-]", "-");

    const string Css = """
*{box-sizing:border-box}body{margin:0;font-family:system-ui,-apple-system,"Segoe UI",Roboto,Arial,sans-serif;color:#1f2937;line-height:1.6;background:#fff}
img{max-width:100%;height:auto;display:block}.w{max-width:960px;margin:0 auto;padding:0 20px}section{padding:56px 0}
h1{font-size:2.2rem;line-height:1.2;margin:0 0 16px}h2{font-size:1.6rem;margin:0 0 20px;line-height:1.25}
.hero{background:var(--a);color:#fff;text-align:center}.hero p{font-size:1.15rem;max-width:640px;margin:0 auto 24px}.hero img{margin:32px auto 0;border-radius:12px}
.btn{display:inline-block;background:var(--a);color:#fff;text-decoration:none;font-weight:700;padding:14px 32px;border-radius:999px;font-size:1.05rem}
.hero .btn{background:#fff;color:var(--a)}.grid{display:grid;gap:20px;grid-template-columns:1fr}.card{border:1px solid #e5e7eb;border-radius:12px;padding:20px}
.price{text-align:center}.price .now{font-size:2.6rem;font-weight:800;color:var(--a)}.price .old{text-decoration:line-through;color:#6b7280;margin-right:8px}
.price ul{list-style:none;padding:0;margin:20px auto;max-width:420px;text-align:left}.price li{padding:6px 0 6px 28px;position:relative}.price li:before{content:"✓";position:absolute;left:0;color:var(--a);font-weight:700}
.note{font-size:.85rem;color:#6b7280}.stars{color:#f59e0b;letter-spacing:2px}details{border-bottom:1px solid #e5e7eb;padding:14px 0}summary{font-weight:600;cursor:pointer}
.buy{text-align:center;background:#f9fafb}footer{padding:28px 0;font-size:.9rem;color:#6b7280;border-top:1px solid #e5e7eb}footer a{color:inherit;margin-right:16px}
@media(min-width:720px){h1{font-size:3rem}.grid{grid-template-columns:repeat(3,1fr)}}
""";

    static string Head(LpDoc d, string title) =>
        "<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{E(title)}</title><meta name=\"description\" content=\"{E(d.Description)}\"><meta property=\"og:title\" content=\"{E(d.Name)}\"><meta property=\"og:description\" content=\"{E(d.Description)}\">" +
        $"<link rel=\"stylesheet\" href=\"style.css\"><style>:root{{--a:{Col(d.Accent)}}}</style></head><body>";

    static string Foot(LpDoc d) =>
        $"<footer><div class=\"w\"><a href=\"impressum.html\">Impressum</a><a href=\"datenschutz.html\">Datenschutz</a><span>© {DateTime.Now.Year} {E(d.Name)}</span></div></footer></body></html>";

    static string Buy(LpDoc d, string label, string cls = "btn") =>
        SafeUrl(d.BuyUrl) ? $"<a class=\"{cls}\" href=\"{E(d.BuyUrl.Trim())}\" rel=\"noopener\">{E(label.Length > 0 ? label : "Jetzt kaufen")}</a>" : $"<span class=\"{cls}\" aria-disabled=\"true\">{E(label.Length > 0 ? label : "Jetzt kaufen")}</span>";

    static string Para(string t) => string.Concat((t ?? "").Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(p => "<p>" + E(p.Trim()).Replace("\n", "<br>") + "</p>"));

    public static string PriceNote(LpDoc d) => d.SmallBusiness ? "Kein Ausweis der Umsatzsteuer (Kleinunternehmerregelung § 19 UStG)." : "inkl. MwSt.";

    /// <summary>Hauptseite als HTML (ohne Skripte). Bilder als „bilder/…“.</summary>
    public static string Index(LpDoc d)
    {
        var sb = new StringBuilder(Head(d, d.Name));
        foreach (var b in d.Blocks)
        {
            switch (b.Kind)
            {
                case "held":
                    sb.Append("<section class=\"hero\"><div class=\"w\"><h1>").Append(E(b.Title.Length > 0 ? b.Title : d.Name)).Append("</h1>");
                    if (b.Text.Length > 0) sb.Append("<p>").Append(E(b.Text)).Append("</p>");
                    sb.Append(Buy(d, b.Label));
                    if (b.Src.Length > 0) sb.Append($"<img src=\"{E(ImageName(b.Src))}\" alt=\"{E(b.Alt)}\" loading=\"eager\">");
                    sb.Append("</div></section>");
                    break;
                case "nutzen":
                    sb.Append("<section><div class=\"w\">");
                    if (b.Title.Length > 0) sb.Append("<h2>").Append(E(b.Title)).Append("</h2>");
                    sb.Append("<div class=\"grid\">");
                    foreach (var r in Rows(b.Items)) sb.Append("<div class=\"card\"><h3>").Append(E(r[0])).Append("</h3>").Append(r.Length > 1 ? "<p>" + E(r[1]) + "</p>" : "").Append("</div>");
                    sb.Append("</div></div></section>");
                    break;
                case "preis":
                    sb.Append("<section class=\"price\"><div class=\"w\">");
                    if (b.Title.Length > 0) sb.Append("<h2>").Append(E(b.Title)).Append("</h2>");
                    sb.Append("<div>").Append(b.OldPrice.Length > 0 ? $"<span class=\"old\">{E(b.OldPrice)}</span>" : "").Append($"<span class=\"now\">{E(b.Price)}</span>")
                      .Append(b.Unit.Length > 0 ? $" <span>{E(b.Unit)}</span>" : "").Append("</div>");
                    sb.Append("<div class=\"note\">").Append(E(PriceNote(d))).Append("</div>");
                    var feats = Rows(b.Items); if (feats.Count > 0) sb.Append("<ul>").Append(string.Concat(feats.Select(f => "<li>" + E(f[0]) + "</li>"))).Append("</ul>");
                    sb.Append(Buy(d, b.Label)).Append("</div></section>");
                    break;
                case "bewertungen":
                    sb.Append("<section><div class=\"w\"><h2>").Append(E(b.Title.Length > 0 ? b.Title : "Das sagen Kundinnen und Kunden")).Append("</h2><div class=\"grid\">");
                    foreach (var r in Rows(b.Items))
                    {
                        var stars = r.Length > 2 && int.TryParse(r[2], out var s) ? Math.Clamp(s, 1, 5) : 5;
                        sb.Append("<figure class=\"card\"><div class=\"stars\" aria-label=\"").Append(stars).Append(" von 5 Sternen\">").Append(new string('★', stars)).Append(new string('☆', 5 - stars)).Append("</div>");
                        sb.Append("<blockquote>").Append(E(r.Length > 1 ? r[1] : "")).Append("</blockquote><figcaption>– ").Append(E(r[0])).Append("</figcaption></figure>");
                    }
                    sb.Append("</div></div></section>");
                    break;
                case "faq":
                    sb.Append("<section><div class=\"w\"><h2>").Append(E(b.Title.Length > 0 ? b.Title : "Häufige Fragen")).Append("</h2>");
                    foreach (var r in Rows(b.Items)) sb.Append("<details><summary>").Append(E(r[0])).Append("</summary><p>").Append(E(r.Length > 1 ? r[1] : "")).Append("</p></details>");
                    sb.Append("</div></section>");
                    break;
                case "kaufen":
                    sb.Append("<section class=\"buy\"><div class=\"w\">");
                    if (b.Title.Length > 0) sb.Append("<h2>").Append(E(b.Title)).Append("</h2>");
                    sb.Append(Buy(d, b.Label)).Append("</div></section>");
                    break;
                case "text":
                    sb.Append("<section><div class=\"w\">").Append(b.Title.Length > 0 ? "<h2>" + E(b.Title) + "</h2>" : "").Append(Para(b.Text)).Append("</div></section>");
                    break;
                case "bild":
                    if (b.Src.Length > 0) sb.Append($"<section><div class=\"w\"><img src=\"{E(ImageName(b.Src))}\" alt=\"{E(b.Alt)}\" loading=\"lazy\"></div></section>");
                    break;
            }
        }
        return sb.Append(Foot(d)).ToString();
    }

    public static string LegalPage(LpDoc d, string title, string text) =>
        Head(d, title + " – " + d.Name) + $"<section><div class=\"w\"><p><a href=\"index.html\">← Zurück</a></p><h1>{E(title)}</h1>{Para(text)}</div></section>" + Foot(d);

    /// <summary>Ordner schreiben: index.html, impressum.html, datenschutz.html, style.css, bilder/. Gibt die Hauptseite zurück.</summary>
    public static string Write(LpDoc d, string dir, Func<string, byte[]?> readImage)
    {
        Directory.CreateDirectory(Path.Combine(dir, "bilder"));
        File.WriteAllText(Path.Combine(dir, "index.html"), Index(d), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "impressum.html"), LegalPage(d, "Impressum", d.Impressum), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "datenschutz.html"), LegalPage(d, "Datenschutz", d.Privacy), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "style.css"), Css, new UTF8Encoding(false));
        foreach (var src in d.Blocks.Where(b => b.Kind is "held" or "bild" && b.Src.Length > 0).Select(b => b.Src).Distinct())
            if (readImage(src) is { } data) File.WriteAllBytes(Path.Combine(dir, ImageName(src).Replace('/', Path.DirectorySeparatorChar)), data);
        return Path.Combine(dir, "index.html");
    }

    /// <summary>Gesamtgröße der Seite (HTML + CSS + Bilder) in Bytes.</summary>
    public static long Weight(LpDoc d, Func<string, long?> imageBytes) =>
        Encoding.UTF8.GetByteCount(Index(d)) + Encoding.UTF8.GetByteCount(Css) + d.Blocks.Where(b => b.Src.Length > 0).Select(b => b.Src).Distinct().Sum(s => imageBytes(s) ?? 0);

    /// <summary>Pflicht- und Qualitätsprüfung: Impressum (Name, Anschrift, E-Mail) und Datenschutz, Kauf-Link mit https, Preis mit Steuerhinweis,
    /// Alt-Texte, Bewertungen mit Namen, Beschreibung für Suchmaschinen, Seitengewicht.</summary>
    public static List<LpFinding> Check(LpDoc d, Func<string, long?>? imageBytes = null)
    {
        var f = new List<LpFinding>();
        if (d.Name.Trim().Length == 0) f.Add(new("Die Seite hat keinen Namen.", true));
        var imp = d.Impressum.Trim();
        if (imp.Length == 0) f.Add(new("Das Impressum fehlt – bei geschäftlichen Seiten Pflicht (§ 5 DDG).", true));
        else if (!Regex.IsMatch(imp, @"\d{4,5}\s+\p{L}") || !imp.Contains('@')) f.Add(new("Im Impressum fehlen Anschrift (Postleitzahl und Ort) oder E-Mail.", true));
        if (d.Privacy.Trim().Length < 40) f.Add(new("Die Datenschutzerklärung fehlt oder ist zu kurz.", true));
        if (!d.Blocks.Any(b => b.Kind is "held" or "preis" or "kaufen")) f.Add(new("Es gibt keinen Kaufen-Knopf (Held, Preis oder Kaufen-Baustein).", true));
        if (d.BuyUrl.Trim().Length == 0) f.Add(new("Kein Kauf-Link – der Knopf führt noch nirgends hin (z. B. Bezahllink aus „Online verkaufen“).", true));
        else if (!SafeUrl(d.BuyUrl)) f.Add(new("Der Kauf-Link muss mit https:// beginnen.", true));
        foreach (var p in d.Blocks.Where(b => b.Kind == "preis"))
        {
            if (p.Price.Trim().Length == 0) f.Add(new("Im Preis-Baustein fehlt der Preis.", true));
            else if (!Regex.IsMatch(p.Price, @"\d")) f.Add(new($"„{p.Price}“ ist kein Preis.", true));
            if (p.OldPrice.Trim().Length > 0) f.Add(new("Streichpreis: Nur zulässig, wenn es der niedrigste Preis der letzten 30 Tage war (§ 11 PAngV).", false));
        }
        foreach (var b in d.Blocks.Where(b => b.Kind is "held" or "bild" && b.Src.Length > 0 && b.Alt.Trim().Length == 0)) f.Add(new("Ein Bild hat keinen Alt-Text (Beschreibung für Vorleseprogramme und Suchmaschinen).", false));
        foreach (var b in d.Blocks.Where(b => b.Kind == "bewertungen"))
        {
            var rows = Rows(b.Items);
            if (rows.Any(r => r.Length < 2 || r[1].Length == 0)) f.Add(new("Eine Bewertung hat keinen Text.", true));
            f.Add(new("Bewertungen: nur echte, von Kundinnen und Kunden freigegebene Stimmen – erfundene Bewertungen sind verboten (UWG).", false));
        }
        foreach (var b in d.Blocks.Where(b => b.Kind == "faq")) if (Rows(b.Items).Any(r => r.Length < 2 || r[1].Length == 0)) f.Add(new("Eine Frage im FAQ hat keine Antwort.", true));
        if (d.Description.Trim().Length == 0) f.Add(new("Keine Beschreibung für Suchmaschinen.", false));
        else if (d.Description.Trim().Length > 160) f.Add(new($"Die Beschreibung ist {d.Description.Trim().Length} Zeichen lang – Suchmaschinen zeigen etwa 155.", false));
        if (imageBytes is not null)
        {
            var kb = Weight(d, imageBytes) / 1024;
            if (kb > 1500) f.Add(new($"Die Seite wiegt {kb} KB – auf dem Handy lädt sie langsam. Bilder kleiner speichern.", false));
            foreach (var s in d.Blocks.Where(b => b.Src.Length > 0).Select(b => b.Src).Distinct()) if (imageBytes(s) is null) f.Add(new($"Bild „{Path.GetFileName(s)}“ wurde nicht gefunden.", true));
        }
        return f;
    }
}
