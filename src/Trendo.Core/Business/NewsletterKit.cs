using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

/// <summary>STUDIO-NEWSLETTER-01: ein Baustein des Newsletters. Felder je Art: kopf (Title, Sub, Color), text (Text), bild (Src, Alt, Url, Width),
/// knopf (Label, Url, Color), spalten (Text, Text2), trenner, fuss (Text = Impressum, Url = Abmeldelink).</summary>
public sealed record NlBlock(string Id, string Kind, string Title = "", string Sub = "", string Text = "", string Text2 = "", string Src = "", string Alt = "", string Url = "",
    string Label = "", string Color = "", int Width = 600);

public sealed record NlDoc(string Subject, string Preheader, string From, string Accent, IReadOnlyList<NlBlock> Blocks);

public sealed record NlFinding(string Text, bool Error);

/// <summary>Empfänger mit Double-Opt-in-Status – nur Verwaltung, Trendo verschickt nichts an diese Liste.</summary>
public sealed record NlRecipient(string Email, string Name, string Status, string Since);

/// <summary>Newsletter wie in Mailchimp/Brevo (Clean-Room): Bausteine → E-Mail-taugliches HTML (Tabellen, Inline-CSS, 600 px), Textfassung, Prüfung,
/// Empfängerliste mit Double-Opt-in und EML-Entwurf. Kein Versand an Listen.</summary>
public static partial class NewsletterKit
{
    public const string Unsub = "{{abmelden}}";
    public static readonly (string Id, string Name)[] Kinds =
        [("kopf", "Kopf"), ("text", "Text"), ("bild", "Bild"), ("knopf", "Knopf"), ("spalten", "Zwei Spalten"), ("trenner", "Trenner"), ("fuss", "Fußzeile")];

    public const string Pending = "unbestätigt", Confirmed = "bestätigt", Unsubscribed = "abgemeldet";

    static string E(string s) => WebUtility.HtmlEncode(s ?? "");
    static string Attr(string s) => WebUtility.HtmlEncode(s ?? "").Replace("'", "&#39;");
    static string Col(string c, string fallback) => c is not null && Regex.IsMatch(c, "^#[0-9A-Fa-f]{6}$") ? c : fallback;

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")] private static partial Regex LinkRx();
    [GeneratedRegex(@"\*\*(.+?)\*\*")] private static partial Regex BoldRx();

    /// <summary>Einfacher Text mit **fett** und [Linktext](https://…) als HTML-Absätze mit Inline-Stil.</summary>
    public static string Rich(string text, string accent)
    {
        var paras = (text ?? "").Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in paras)
        {
            var h = E(p.Trim()).Replace("\n", "<br>");
            h = BoldRx().Replace(h, "<strong>$1</strong>");
            h = LinkRx().Replace(h, m => !Regex.IsMatch(m.Groups[2].Value, "^(https?://|mailto:)", RegexOptions.IgnoreCase) ? m.Groups[1].Value : $"<a href=\"{m.Groups[2].Value}\" style=\"color:{accent};text-decoration:underline;\">{m.Groups[1].Value}</a>");
            sb.Append("<p style=\"margin:0 0 14px 0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:24px;color:#1F2937;\">").Append(h).Append("</p>");
        }
        return sb.ToString();
    }

    static string Plain(string text) => LinkRx().Replace(BoldRx().Replace(text ?? "", "$1"), "$1 ($2)").Trim();

    /// <summary>E-Mail-taugliches HTML: Tabellen, Inline-CSS, 600 px breit, auf dem Handy volle Breite. <paramref name="img"/> liefert die Bildadresse (Datei, cid: oder URL).</summary>
    public static string Html(NlDoc d, Func<NlBlock, string>? img = null, string unsubUrl = Unsub)
    {
        img ??= b => b.Src;
        var acc = Col(d.Accent, "#B45309");
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<meta name=\"x-apple-disable-message-reformatting\"><title>").Append(E(d.Subject)).Append("</title>");
        sb.Append("<style>@media only screen and (max-width:620px){.nl-wrap{width:100% !important}.nl-col{display:block !important;width:100% !important}.nl-img{width:100% !important;height:auto !important}}</style></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#F3F4F6;\">");
        if (d.Preheader.Trim().Length > 0)
            sb.Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:#F3F4F6;\">").Append(E(d.Preheader.Trim())).Append("</div>");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#F3F4F6;\"><tr><td align=\"center\" style=\"padding:16px 8px;\">");
        sb.Append("<table role=\"presentation\" class=\"nl-wrap\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:600px;max-width:600px;background:#FFFFFF;\">");
        const string Pad = "padding:16px 24px;";
        foreach (var b in d.Blocks)
        {
            switch (b.Kind)
            {
                case "kopf":
                    var bg = Col(b.Color, acc);
                    sb.Append($"<tr><td style=\"background:{bg};padding:28px 24px;text-align:center;\">");
                    sb.Append("<h1 style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:28px;line-height:34px;color:#FFFFFF;\">").Append(E(b.Title)).Append("</h1>");
                    if (b.Sub.Length > 0) sb.Append("<p style=\"margin:8px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:22px;color:#FFFFFF;\">").Append(E(b.Sub)).Append("</p>");
                    sb.Append("</td></tr>");
                    break;
                case "text":
                    sb.Append($"<tr><td style=\"{Pad}\">");
                    if (b.Title.Length > 0) sb.Append("<h2 style=\"margin:0 0 10px 0;font-family:Arial,Helvetica,sans-serif;font-size:20px;line-height:26px;color:#111827;\">").Append(E(b.Title)).Append("</h2>");
                    sb.Append(Rich(b.Text, acc)).Append("</td></tr>");
                    break;
                case "bild":
                    var w = Math.Clamp(b.Width, 50, 600);
                    var tag = $"<img class=\"nl-img\" src=\"{Attr(img(b))}\" width=\"{w}\" alt=\"{Attr(b.Alt)}\" style=\"display:block;width:{w}px;max-width:100%;height:auto;border:0;margin:0 auto;\">";
                    sb.Append($"<tr><td align=\"center\" style=\"padding:{(w == 600 ? "0" : "8px 24px")};\">");
                    sb.Append(b.Url.Length > 0 ? $"<a href=\"{Attr(b.Url)}\">{tag}</a>" : tag).Append("</td></tr>");
                    break;
                case "knopf":
                    var kc = Col(b.Color, acc);
                    sb.Append("<tr><td align=\"center\" style=\"padding:12px 24px 20px 24px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
                    sb.Append($"<td style=\"background:{kc};border-radius:6px;\"><a href=\"{Attr(b.Url)}\" style=\"display:inline-block;padding:12px 28px;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold;color:#FFFFFF;text-decoration:none;\">{E(b.Label)}</a></td>");
                    sb.Append("</tr></table></td></tr>");
                    break;
                case "spalten":
                    sb.Append("<tr><td style=\"padding:8px 16px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
                    sb.Append("<td class=\"nl-col\" width=\"50%\" valign=\"top\" style=\"width:50%;padding:8px;\">").Append(Rich(b.Text, acc)).Append("</td>");
                    sb.Append("<td class=\"nl-col\" width=\"50%\" valign=\"top\" style=\"width:50%;padding:8px;\">").Append(Rich(b.Text2, acc)).Append("</td>");
                    sb.Append("</tr></table></td></tr>");
                    break;
                case "trenner":
                    sb.Append("<tr><td style=\"padding:8px 24px;\"><div style=\"border-top:1px solid #E5E7EB;font-size:0;line-height:0;\">&nbsp;</div></td></tr>");
                    break;
                case "fuss":
                    var u = b.Url.Trim().Length > 0 ? b.Url.Trim() : unsubUrl;
                    sb.Append("<tr><td style=\"background:#F9FAFB;padding:20px 24px;text-align:center;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#6B7280;\">");
                    sb.Append(E(b.Text).Replace("\n", "<br>"));
                    sb.Append($"<br><br><a href=\"{Attr(u)}\" style=\"color:#6B7280;text-decoration:underline;\">Newsletter abbestellen</a></td></tr>");
                    break;
            }
        }
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    /// <summary>Textfassung (für Programme ohne HTML und als zweiter Teil der E-Mail).</summary>
    public static string Text(NlDoc d, string unsubUrl = Unsub)
    {
        var sb = new StringBuilder();
        foreach (var b in d.Blocks)
        {
            switch (b.Kind)
            {
                case "kopf": sb.Append(b.Title.ToUpper(CultureInfo.GetCultureInfo("de-DE"))).Append('\n'); if (b.Sub.Length > 0) sb.Append(b.Sub).Append('\n'); sb.Append('\n'); break;
                case "text": if (b.Title.Length > 0) sb.Append(b.Title).Append('\n').Append(new string('-', Math.Min(60, b.Title.Length))).Append('\n'); sb.Append(Plain(b.Text)).Append("\n\n"); break;
                case "bild": if (b.Alt.Length > 0) sb.Append('[').Append(b.Alt).Append(']').Append(b.Url.Length > 0 ? " " + b.Url : "").Append("\n\n"); break;
                case "knopf": sb.Append(b.Label).Append(": ").Append(b.Url).Append("\n\n"); break;
                case "spalten": sb.Append(Plain(b.Text)).Append("\n\n").Append(Plain(b.Text2)).Append("\n\n"); break;
                case "trenner": sb.Append("----------------------------------------\n\n"); break;
                case "fuss": sb.Append("--\n").Append(b.Text.Trim()).Append("\n\nAbbestellen: ").Append(b.Url.Trim().Length > 0 ? b.Url.Trim() : unsubUrl).Append('\n'); break;
            }
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    static readonly string[] SpamWords =
    [
        "100% kostenlos", "100 % kostenlos", "gratis!!!", "jetzt kaufen!!!", "garantiert", "risikofrei", "schnell reich", "geld verdienen", "gewinner", "sie haben gewonnen",
        "nur heute", "letzte chance", "klicken sie hier", "hier klicken", "keine kosten", "ohne risiko", "sofort bargeld", "abnehmen", "viagra", "kredit ohne schufa",
    ];

    /// <summary>Prüfung vor dem Entwurf: Abmeldelink und Impressum (Pflicht), Betreff, Alt-Texte, Bildgröße, Knopf-Links, Spam-Wörter, HTML-Größe (Gmail kürzt ab etwa 102 KB).</summary>
    public static List<NlFinding> Check(NlDoc d, Func<string, (long Bytes, int W, int H)?>? imageInfo = null)
    {
        var f = new List<NlFinding>();
        if (d.Subject.Trim().Length == 0) f.Add(new("Der Betreff fehlt.", true));
        else if (d.Subject.Trim().Length > 60) f.Add(new($"Der Betreff ist {d.Subject.Trim().Length} Zeichen lang – auf dem Handy sind etwa 40 bis 60 sichtbar.", false));
        if (d.Preheader.Trim().Length == 0) f.Add(new("Kein Vorschautext – viele Programme zeigen ihn neben dem Betreff.", false));
        var foot = d.Blocks.LastOrDefault(b => b.Kind == "fuss");
        if (foot is null) f.Add(new("Die Fußzeile mit Impressum und Abmeldelink fehlt (Pflicht).", true));
        else
        {
            var imp = foot.Text.Trim();
            if (imp.Length < 25 || !Regex.IsMatch(imp, @"\d{4,5}\s+\p{L}") || !imp.Contains('@'))
                f.Add(new("Im Impressum der Fußzeile fehlen Name, Anschrift (mit Postleitzahl und Ort) oder E-Mail.", true));
            var u = foot.Url.Trim();
            if (u.Length > 0 && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) && u != Unsub)
                f.Add(new("Der Abmeldelink muss mit https:// oder mailto: beginnen.", true));
            if (u.Length == 0) f.Add(new($"Abmeldelink: Platzhalter {Unsub} – dein Versanddienst setzt dort den echten Link ein.", false));
            if (d.Blocks[^1].Kind != "fuss") f.Add(new("Die Fußzeile steht nicht ganz unten.", false));
        }
        if (Regex.IsMatch(d.From ?? "", @"^\S+@\S+\.\S+$") is false) f.Add(new("Absender als E-Mail-Adresse eintragen.", true));
        var i = 0;
        foreach (var b in d.Blocks)
        {
            i++;
            if (b.Kind == "bild")
            {
                if (b.Src.Trim().Length == 0) f.Add(new($"Baustein {i}: Bild fehlt.", true));
                if (b.Alt.Trim().Length == 0) f.Add(new($"Baustein {i}: Alt-Text fehlt – viele Programme zeigen Bilder erst nach Klick.", true));
                if (imageInfo?.Invoke(b.Src) is { } info)
                {
                    if (info.Bytes > 1_000_000) f.Add(new($"Baustein {i}: Bild ist {info.Bytes / 1024} KB groß – unter 300 KB lädt es schnell.", false));
                    if (info.W < b.Width) f.Add(new($"Baustein {i}: Bild ist nur {info.W} px breit, angezeigt werden {b.Width} px – es wirkt unscharf.", false));
                    if (info.W > 1800) f.Add(new($"Baustein {i}: Bild ist {info.W} px breit – 1200 px reichen auch für scharfe Bildschirme.", false));
                }
            }
            if (b.Kind == "knopf")
            {
                if (b.Label.Trim().Length == 0) f.Add(new($"Baustein {i}: Der Knopf hat keine Beschriftung.", true));
                if (!b.Url.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase)) f.Add(new($"Baustein {i}: Der Knopf braucht einen Link mit https://.", true));
            }
            if (b.Kind == "text" && b.Text.Trim().Length == 0 && b.Title.Trim().Length == 0) f.Add(new($"Baustein {i}: Der Textbaustein ist leer.", false));
        }
        var all = (d.Subject + "\n" + string.Join("\n", d.Blocks.Select(b => b.Title + " " + b.Sub + " " + b.Text + " " + b.Text2 + " " + b.Label))).ToLowerInvariant();
        var hits = SpamWords.Where(w => all.Contains(w, StringComparison.Ordinal)).ToList();
        if (hits.Count > 0) f.Add(new("Wörter, die Spamfilter mögen: " + string.Join(", ", hits.Select(h => "„" + h + "“")) + ".", false));
        if (Regex.IsMatch(d.Subject, "!!|\\?\\?|€€")) f.Add(new("Mehrere Ausrufe- oder Fragezeichen im Betreff wirken wie Spam.", false));
        var letters = d.Subject.Where(char.IsLetter).ToList();
        if (letters.Count >= 8 && letters.Count(char.IsUpper) > letters.Count * 0.6) f.Add(new("Der Betreff ist fast ganz in Großbuchstaben – das wirkt wie Spam.", false));
        var size = Encoding.UTF8.GetByteCount(Html(d));
        if (size > 102_000) f.Add(new($"Das HTML ist {size / 1024} KB groß – Gmail kürzt ab etwa 102 KB.", true));
        if (d.Blocks.Count(b => b.Kind == "bild") > 0 && d.Blocks.Count(b => b.Kind is "text" or "spalten") == 0) f.Add(new("Nur Bilder ohne Text landen leichter im Spam.", false));
        return f;
    }

    // ---------- Empfänger (Double-Opt-in) ----------

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")] private static partial Regex MailRx();
    public static bool ValidEmail(string e) => MailRx().IsMatch((e ?? "").Trim());

    public static List<NlRecipient> Add(IReadOnlyList<NlRecipient> list, string email, string name, DateTime now)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (!ValidEmail(e)) throw new ArgumentException($"„{email}“ ist keine gültige E-Mail-Adresse.");
        if (list.Any(r => r.Email == e)) throw new ArgumentException($"{e} steht schon in der Liste.");
        return [.. list, new NlRecipient(e, (name ?? "").Trim(), Pending, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))];
    }

    /// <summary>Status setzen: bestätigt (Klick im Bestätigungslink) oder abgemeldet. Wer abgemeldet ist, kann nicht wieder auf „bestätigt“ – nur neu anmelden.</summary>
    public static List<NlRecipient> SetStatus(IReadOnlyList<NlRecipient> list, string email, string status, DateTime now)
    {
        if (status is not (Pending or Confirmed or Unsubscribed)) throw new ArgumentException("Unbekannter Status: " + status);
        var e = (email ?? "").Trim().ToLowerInvariant();
        var r = list.FirstOrDefault(x => x.Email == e) ?? throw new ArgumentException($"{e} steht nicht in der Liste.");
        if (r.Status == Unsubscribed && status == Confirmed) throw new InvalidOperationException($"{e} hat sich abgemeldet – nur mit einer neuen Anmeldung (Double-Opt-in) wieder aufnehmen.");
        return [.. list.Select(x => x.Email == e ? x with { Status = status, Since = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) } : x)];
    }

    /// <summary>CSV einlesen (E-Mail;Name oder E-Mail,Name, Kopfzeile wird erkannt). Neue Adressen sind unbestätigt – sie brauchen die Bestätigung.</summary>
    public static (List<NlRecipient> List, int Added, int Skipped) ImportCsv(IReadOnlyList<NlRecipient> list, string csv, DateTime now)
    {
        var res = list.ToList(); int add = 0, skip = 0;
        foreach (var raw in (csv ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split(raw.Contains(';') ? ';' : ',').Select(p => p.Trim().Trim('"')).ToArray();
            var mail = parts.FirstOrDefault(ValidEmail);
            if (mail is null) { if (!raw.Contains('@')) continue; skip++; continue; }
            var name = parts.FirstOrDefault(p => p != mail && p.Length > 0 && !ValidEmail(p)) ?? "";
            try { res = Add(res, mail, name, now); add++; } catch (ArgumentException) { skip++; }
        }
        return (res, add, skip);
    }

    public static string ExportCsv(IEnumerable<NlRecipient> list) =>
        "E-Mail;Name;Status;Seit\r\n" + string.Concat(list.Select(r => $"{r.Email};{r.Name.Replace(";", ",")};{r.Status};{r.Since}\r\n"));

    /// <summary>Nur bestätigte Empfänger dürfen Werbung bekommen (Double-Opt-in).</summary>
    public static int Sendable(IEnumerable<NlRecipient> list) => list.Count(r => r.Status == Confirmed);

    /// <summary>Text der Bestätigungs-E-Mail (Double-Opt-in) – zum Kopieren in den Versanddienst.</summary>
    public static string OptInText(string sender, string link = "{{bestaetigen}}") =>
        $"Hallo,\n\nbitte bestätige deine Anmeldung zum Newsletter von {sender}: {link}\n\nWenn du dich nicht angemeldet hast, ignoriere diese E-Mail – dann bekommst du nichts weiter.\n";

    // ---------- EML ----------

    static string Q(string s) => s.All(c => c < 128) ? s : "=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) + "?=";
    static string B64(byte[] b) { var s = Convert.ToBase64String(b); var sb = new StringBuilder(); for (var i = 0; i < s.Length; i += 76) sb.Append(s, i, Math.Min(76, s.Length - i)).Append("\r\n"); return sb.ToString(); }

    /// <summary>E-Mail-Entwurf (.eml) mit Text- und HTML-Teil; Bilder aus Dateien werden eingebettet (cid). „X-Unsent: 1“ öffnet ihn als Entwurf.
    /// <paramref name="to"/> ist höchstens eine Adresse (Probe an dich) – kein Versand an Listen.</summary>
    public static string Eml(NlDoc d, string to, DateTimeOffset now, Func<string, byte[]?>? readImage = null)
    {
        if (to.Contains(',') || to.Contains(';')) throw new ArgumentException("Der Entwurf geht nur an eine Adresse (Probe an dich) – kein Massenversand.");
        var images = new List<(string Cid, string Name, byte[] Data)>();
        string Src(NlBlock b)
        {
            if (b.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase) || readImage is null) return b.Src;
            var data = readImage(b.Src); if (data is null) return b.Src;
            var cid = "bild" + (images.Count + 1) + "@newsletter"; images.Add((cid, Path.GetFileName(b.Src), data)); return "cid:" + cid;
        }
        var html = Html(d, Src); var text = Text(d);
        string bAlt = "alt-" + Guid.NewGuid().ToString("N")[..12], bRel = "rel-" + Guid.NewGuid().ToString("N")[..12];
        var sb = new StringBuilder();
        sb.Append("X-Unsent: 1\r\n");
        if (to.Trim().Length > 0) sb.Append("To: ").Append(to.Trim()).Append("\r\n");
        sb.Append("From: ").Append(d.From).Append("\r\nSubject: ").Append(Q(d.Subject)).Append("\r\nDate: ").Append(now.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append($"Content-Type: multipart/related; boundary=\"{bRel}\"\r\n\r\n");
        sb.Append($"--{bRel}\r\nContent-Type: multipart/alternative; boundary=\"{bAlt}\"\r\n\r\n");
        sb.Append($"--{bAlt}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n").Append(B64(Encoding.UTF8.GetBytes(text)));
        sb.Append($"--{bAlt}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n").Append(B64(Encoding.UTF8.GetBytes(html)));
        sb.Append($"--{bAlt}--\r\n");
        foreach (var (cid, name, data) in images)
        {
            var type = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif" : "image/jpeg";
            sb.Append($"--{bRel}\r\nContent-Type: {type}; name=\"{name}\"\r\nContent-Transfer-Encoding: base64\r\nContent-ID: <{cid}>\r\nContent-Disposition: inline; filename=\"{name}\"\r\n\r\n").Append(B64(data));
        }
        sb.Append($"--{bRel}--\r\n");
        return sb.ToString();
    }
}
