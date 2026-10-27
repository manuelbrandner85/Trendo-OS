using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record LegalInfo(string Owner, string Street, string City, string Email, string Phone = "", string VatId = "", bool SmallBusiness = true);
public sealed record Check(string Area, string Label, bool Ok, string Fix = "");

/// <summary>BUS-016 Webseite bauen, BUS-026 Recht & Datenschutz prüfen, BUS-027 Qualität prüfen, BUS-028 Veröffentlichungspaket, BUS-029 Sicherung, BUS-030 Startklar-Prüfung.</summary>
public static partial class BusinessLaunch
{
    static string E(string s) => WebUtility.HtmlEncode(s);

    static string Layout(string brand, string title, string body, string css) =>
        $"<!doctype html>\n<html lang=\"de\">\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{E(title)} – {E(brand)}</title><link rel=\"stylesheet\" href=\"style.css\"></head>\n<body>\n<header><a href=\"index.html\" class=\"logo\">{E(brand)}</a><nav><a href=\"index.html\">Start</a> <a href=\"preise.html\">Preise</a> <a href=\"kontakt.html\">Kontakt</a></nav></header>\n<main>\n{body}</main>\n<footer><a href=\"impressum.html\">Impressum</a> · <a href=\"datenschutz.html\">Datenschutz</a></footer>\n</body>\n</html>\n";

    /// <summary>BUS-016: Erzeugt eine vollständige, statische Geschäfts-Webseite (5 Seiten) aus Idee, Marke, Tarifen und Pflichtangaben.</summary>
    public static string BuildSite(string parent, BusinessIdea i, LegalInfo legal, IReadOnlyList<PlanTier> tiers, string color = "#2F86F6")
    {
        var dir = Path.Combine(parent, DevStudio.SafeName(i.Name) + "-webseite");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var pal = BusinessBrand.Palette(color);
        var css = BusinessBrand.BrandCss(pal) + "body{margin:0;font-family:system-ui,sans-serif;color:var(--text);background:var(--hintergrund);line-height:1.6}header,footer{display:flex;gap:1rem;justify-content:space-between;padding:1rem 2rem;background:#fff}main{max-width:820px;margin:0 auto;padding:2rem 1rem}.logo{font-weight:700;color:var(--hauptfarbe);text-decoration:none}a.btn{display:inline-block;background:var(--hauptfarbe);color:#fff;padding:.7rem 1.3rem;border-radius:10px;text-decoration:none}.tiers{display:flex;gap:1rem;flex-wrap:wrap}.tier{background:#fff;border-radius:12px;padding:1rem 1.5rem;min-width:180px}\n";
        void W(string f, string t) => File.WriteAllText(Path.Combine(dir, f), t, new UTF8Encoding(false));
        var vatNote = legal.SmallBusiness ? "Gemäß § 19 UStG wird keine Umsatzsteuer berechnet." : "Alle Preise inkl. MwSt.";
        W("style.css", css);
        W("index.html", Layout(i.Name, "Start", $"<h1>{E(i.Name)}</h1>\n<p class=\"lead\">{E(BusinessBrand.Taglines(i)[0])}</p>\n<h2>Das Problem</h2><p>{E(i.Problem)}</p>\n<h2>Unsere Lösung</h2><p>{E(i.Solution)}</p>\n<p>Für {E(i.Audience)}.</p>\n<p><a class=\"btn\" href=\"kontakt.html\">Jetzt anfragen</a></p>\n", css));
        var tierHtml = tiers.Count == 0 ? $"<div class=\"tier\"><h3>Preis</h3><p><b>{i.Price:0.00} €</b></p></div>" : string.Concat(tiers.Select(t => $"<div class=\"tier\"><h3>{E(t.Name)}</h3><p><b>{t.PriceMonth:0.00} €</b> pro Monat</p></div>"));
        W("preise.html", Layout(i.Name, "Preise", $"<h1>Preise</h1>\n<div class=\"tiers\">{tierHtml.Replace(".", ",").Replace(",css", ".css")}</div>\n<p>{vatNote}</p>\n", css));
        W("kontakt.html", Layout(i.Name, "Kontakt", $"<h1>Kontakt</h1>\n<p>Schreib uns: <a href=\"mailto:{E(legal.Email)}\">{E(legal.Email)}</a></p>\n" + (legal.Phone.Length > 0 ? $"<p>Telefon: {E(legal.Phone)}</p>\n" : ""), css));
        W("impressum.html", Layout(i.Name, "Impressum", $"<h1>Impressum</h1>\n<p>Angaben gemäß § 5 DDG</p>\n<p>{E(legal.Owner)}<br>{E(legal.Street)}<br>{E(legal.City)}</p>\n<p>E-Mail: <a href=\"mailto:{E(legal.Email)}\">{E(legal.Email)}</a>" + (legal.Phone.Length > 0 ? $"<br>Telefon: {E(legal.Phone)}" : "") + "</p>\n" + (legal.VatId.Length > 0 ? $"<p>USt-IdNr.: {E(legal.VatId)}</p>\n" : "") + $"<p>Verantwortlich für den Inhalt: {E(legal.Owner)}</p>\n", css));
        W("datenschutz.html", Layout(i.Name, "Datenschutz", $"<h1>Datenschutzerklärung</h1>\n<h2>Verantwortlicher</h2><p>{E(legal.Owner)}, {E(legal.Street)}, {E(legal.City)}, {E(legal.Email)}</p>\n<h2>Welche Daten?</h2><p>Diese Seite setzt keine Cookies und bindet keine fremden Dienste ein. Beim Aufruf speichert der Webserver technisch nötige Daten (z. B. IP-Adresse, Zeitpunkt) für höchstens 7 Tage (Art. 6 Abs. 1 lit. f DSGVO).</p>\n<h2>Kontakt per E-Mail</h2><p>Wenn du uns schreibst, nutzen wir deine Angaben nur zur Beantwortung (Art. 6 Abs. 1 lit. b DSGVO).</p>\n<h2>Deine Rechte</h2><p>Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Datenübertragbarkeit und Beschwerde bei einer Aufsichtsbehörde.</p>\n<p><small>Vorlage von Trendo OS – keine Rechtsberatung. Bitte prüfen lassen.</small></p>\n", css));
        W("README.md", $"# {i.Name} – Webseite\n\nStatische Seiten, keine Cookies, keine fremden Dienste.\nÖffne index.html im Browser. Zum Veröffentlichen siehe VEROEFFENTLICHEN.md (nach „Paket erstellen“).\n");
        return dir;
    }

    [GeneratedRegex("(?:href|src)=\"([^\"]*)\"")] private static partial Regex LinkRx();
    [GeneratedRegex("<img(?![^>]*\\balt=\"[^\"]+\")[^>]*>")] private static partial Regex ImgNoAlt();

    /// <summary>BUS-026: Pflichtangaben und Datenschutz (Deutschland). Technische Prüfung, keine Rechtsberatung.</summary>
    public static IReadOnlyList<Check> Compliance(string site, LegalInfo legal, bool sellsToConsumers)
    {
        string R(string f) => File.Exists(Path.Combine(site, f)) ? File.ReadAllText(Path.Combine(site, f)) : "";
        var imp = R("impressum.html"); var ds = R("datenschutz.html"); var pages = Directory.GetFiles(site, "*.html");
        var all = string.Concat(pages.Select(File.ReadAllText));
        var c = new List<Check>
        {
            new("Recht", "Impressum vorhanden", imp.Length > 0, "Seite impressum.html anlegen."),
            new("Recht", "Name und vollständige Anschrift im Impressum", legal.Owner.Length > 1 && legal.Street.Any(char.IsDigit) && Regex.IsMatch(legal.City, @"\d{5}") && imp.Contains(E(legal.Street)), "Name, Straße mit Hausnummer und PLZ/Ort eintragen."),
            new("Recht", "E-Mail im Impressum", legal.Email.Contains('@') && imp.Contains(E(legal.Email)), "E-Mail-Adresse eintragen."),
            new("Recht", "Impressum und Datenschutz von jeder Seite verlinkt", pages.All(p => File.ReadAllText(p).Contains("impressum.html") && File.ReadAllText(p).Contains("datenschutz.html")), "Links im Fußbereich jeder Seite."),
            new("Datenschutz", "Datenschutzerklärung vorhanden", ds.Contains("Verantwortlicher") && ds.Contains("Rechte"), "Seite datenschutz.html mit Verantwortlichem und Rechten."),
            new("Datenschutz", "Keine fremden Dienste (Schriften, Tracker, Videos) eingebunden", !Regex.IsMatch(all, "(src|href)=\"https?://(?!(www\\.)?(dpma|ec\\.europa))", RegexOptions.IgnoreCase), "Externe Einbindungen entfernen oder in der Datenschutzerklärung nennen und Einwilligung einholen."),
            new("Preise", "Hinweis zur Mehrwertsteuer bei Preisen", R("preise.html").Contains("MwSt") || R("preise.html").Contains("§ 19 UStG"), "„inkl. MwSt.“ oder Kleinunternehmer-Hinweis ergänzen."),
        };
        if (sellsToConsumers)
        {
            c.Add(new("Verkauf", "Widerrufsbelehrung vorhanden", all.Contains("Widerruf"), "Bei Verkauf an Privatkunden: Seite mit Widerrufsbelehrung und Muster-Formular."));
            c.Add(new("Verkauf", "AGB vorhanden", all.Contains("AGB") || all.Contains("Geschäftsbedingungen"), "AGB-Seite ergänzen (prüfen lassen)."));
        }
        return c;
    }

    /// <summary>BUS-027: Qualität – tote Links, Bilder ohne Beschreibung, unsichere Links, Titel, Sprache, Größe.</summary>
    public static IReadOnlyList<Check> Quality(string site)
    {
        var c = new List<Check>();
        foreach (var p in Directory.GetFiles(site, "*.html").OrderBy(x => x))
        {
            var h = File.ReadAllText(p); var n = Path.GetFileName(p);
            var dead = LinkRx().Matches(h).Select(m => m.Groups[1].Value).Where(l => !l.Contains(':') && !l.StartsWith('#') && l.Length > 0).Select(l => l.Split('#', '?')[0]).Where(l => !File.Exists(Path.Combine(site, l))).ToList();
            c.Add(new("Qualität", $"{n}: keine toten Links", dead.Count == 0, "Fehlt: " + string.Join(", ", dead)));
            c.Add(new("Qualität", $"{n}: Bilder haben eine Beschreibung", !ImgNoAlt().IsMatch(h), "alt=\"…\" ergänzen."));
            c.Add(new("Sicherheit", $"{n}: nur sichere Links (https)", !h.Contains("=\"http://"), "http:// durch https:// ersetzen."));
            c.Add(new("Qualität", $"{n}: Titel und Sprache gesetzt", Regex.IsMatch(h, "<title>[^<]{3,}</title>") && h.Contains("lang=\"de\""), "<title> und lang=\"de\" setzen."));
            c.Add(new("Qualität", $"{n}: kleiner als 300 KB", h.Length < 300_000, "Seite verkleinern."));
            foreach (Match m in Regex.Matches(h, "mailto:([^\"]*)")) if (!Regex.IsMatch(WebUtility.HtmlDecode(m.Groups[1].Value), @"^[^@\s]+@[^@\s]+\.[a-z]{2,}$", RegexOptions.IgnoreCase)) c.Add(new("Qualität", $"{n}: E-Mail-Link gültig", false, "E-Mail-Adresse prüfen: " + m.Groups[1].Value));
        }
        return c;
    }

    /// <summary>BUS-028: Paket zum Hochladen (ZIP), GitHub-Pages-Ablauf, Sicherheits-Kopfzeilen, Anleitung. Lädt nichts hoch.</summary>
    public static string Package(string site, string domain = "")
    {
        if (domain.Length > 0 && !Regex.IsMatch(domain, @"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$")) throw new ArgumentException("Domain sieht ungültig aus (Beispiel: meine-firma.de).");
        File.WriteAllText(Path.Combine(site, "_headers"), "/*\n  X-Content-Type-Options: nosniff\n  X-Frame-Options: DENY\n  Referrer-Policy: strict-origin-when-cross-origin\n  Content-Security-Policy: default-src 'self'\n");
        if (domain.Length > 0) File.WriteAllText(Path.Combine(site, "CNAME"), domain + "\n");
        var wf = Path.Combine(site, ".github", "workflows"); Directory.CreateDirectory(wf);
        File.WriteAllText(Path.Combine(wf, "pages.yml"), "name: Webseite veröffentlichen\non:\n  push:\n    branches: [main]\npermissions:\n  contents: read\n  pages: write\n  id-token: write\njobs:\n  deploy:\n    runs-on: ubuntu-latest\n    environment: github-pages\n    steps:\n      - uses: actions/checkout@v4\n      - uses: actions/upload-pages-artifact@v3\n        with:\n          path: .\n      - uses: actions/deploy-pages@v4\n");
        File.WriteAllText(Path.Combine(site, "VEROEFFENTLICHEN.md"), $"# Veröffentlichen\n\nTrendo hat nichts hochgeladen. Wähle einen Weg:\n\n1. **Eigener Webspace:** Inhalt von `webseite.zip` per FTP/Dateimanager hochladen.\n2. **GitHub Pages (kostenlos):** Ordner als Repository hochladen, unter Settings → Pages „GitHub Actions“ wählen. Der Ablauf `.github/workflows/pages.yml` liegt bei.\n3. **Netlify (kostenloser Tarif):** Ordner per Drag & Drop hochladen. `_headers` setzt Sicherheits-Kopfzeilen.\n" + (domain.Length > 0 ? $"\nDomain: {domain} – beim Anbieter einen CNAME/A-Eintrag setzen.\n" : ""));
        var zip = Path.Combine(Path.GetDirectoryName(site)!, Path.GetFileName(site) + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(site, zip, CompressionLevel.Optimal, includeBaseDirectory: false);
        return zip;
    }

    /// <summary>BUS-029: Sicherung aller Geschäftsdaten einer Idee (behält die letzten 10), Wiederherstellen.</summary>
    public static string Backup(string dataRoot, string ideaId, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var src = Path.Combine(dataRoot, "business");
        var zip = Path.Combine(targetDir, $"business-{DevStudio.SafeName(ideaId)}-{DateTime.Now:yyyyMMdd-HHmmssfff}.zip");
        for (var k = 2; File.Exists(zip); k++) zip = Path.Combine(targetDir, $"business-{DevStudio.SafeName(ideaId)}-{DateTime.Now:yyyyMMdd-HHmmssfff}-{k}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var f in new[] { Path.Combine(src, "ideen.json") }.Concat(Directory.Exists(Path.Combine(src, DevStudio.SafeName(ideaId))) ? Directory.GetFiles(Path.Combine(src, DevStudio.SafeName(ideaId))) : []))
                if (File.Exists(f)) z.CreateEntryFromFile(f, Path.GetRelativePath(src, f).Replace('\\', '/'));
            var ledger = Path.Combine(dataRoot, "finanzen", "buchungen.json"); if (File.Exists(ledger)) z.CreateEntryFromFile(ledger, "finanzen/buchungen.json");
        }
        foreach (var old in Directory.GetFiles(targetDir, $"business-{DevStudio.SafeName(ideaId)}-*.zip").OrderByDescending(File.GetLastWriteTimeUtc).ThenByDescending(x => x).Skip(10)) File.Delete(old);
        return zip;
    }

    public static int Restore(string zip, string dataRoot)
    {
        var n = 0;
        using var z = ZipFile.OpenRead(zip);
        foreach (var e in z.Entries)
        {
            if (!FreeAssets.SafeRel(e.FullName)) throw new InvalidDataException("Unsicherer Pfad in der Sicherung.");
            var t = e.FullName.StartsWith("finanzen/") ? Path.Combine(dataRoot, e.FullName) : Path.Combine(dataRoot, "business", e.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(t)!); e.ExtractToFile(t, true); n++;
        }
        return n;
    }

    /// <summary>BUS-030: Startklar? Fasst alle Prüfungen zusammen.</summary>
    public static (bool Ready, IReadOnlyList<Check> Checks) Handover(BusinessIdea i, IReadOnlyList<Experiment> tests, string? site, LegalInfo legal, bool consumers, string backupDir)
    {
        var v = BusinessStudio.Evaluate(i); var val = BusinessMarket.Validation(tests);
        var c = new List<Check>
        {
            new("Idee", "Bewertung erlaubt den Start", v.Decision is "Starten" or "Erst testen", $"Bewertung: {v.Decision} – Seite „Geschäftsidee“."),
            new("Markt", "Mindestens ein Markttest bestätigt", val.Confirmed >= 1, "Seite „Markt“ → Am Markt testen."),
            new("Webseite", "Webseite gebaut", site is not null && File.Exists(Path.Combine(site, "index.html")), "„Webseite bauen“ klicken."),
        };
        if (site is not null && Directory.Exists(site))
        {
            var comp = Compliance(site, legal, consumers); var q = Quality(site);
            c.Add(new("Recht", "Recht & Datenschutz ohne offene Punkte", comp.All(x => x.Ok), string.Join("; ", comp.Where(x => !x.Ok).Select(x => x.Label))));
            c.Add(new("Qualität", "Qualitätsprüfung ohne Fehler", q.All(x => x.Ok), string.Join("; ", q.Where(x => !x.Ok).Select(x => x.Label).Take(3))));
            c.Add(new("Veröffentlichen", "Paket erstellt", File.Exists(site + ".zip"), "„Paket erstellen“ klicken."));
        }
        c.Add(new("Sicherung", "Sicherung vorhanden", Directory.Exists(backupDir) && Directory.GetFiles(backupDir, $"business-{DevStudio.SafeName(i.Id)}-*.zip").Length > 0, "„Jetzt sichern“ klicken."));
        return (c.All(x => x.Ok), c);
    }
}
