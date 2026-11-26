using System.Text;
using PdfSharp.Drawing;
using Trendo.Core.Business;

namespace Trendo.Core.Wissen;

/// <summary>Eine gerechnete Aufgabe für den Export (Mathe-Helfer und Naturwissenschaft).</summary>
public sealed record BlattEintrag(string Art, string Aufgabe, IReadOnlyList<Schritt> Schritte, string Ergebnis, string ErgebnisLatex, IReadOnlyList<Probe> Proben, string? Fehler = null)
{
    public static BlattEintrag Aus(Loesung l) => new(MatheKern.ArtName(l.Art), l.Aufgabe, l.Schritte, l.Ergebnis, l.ErgebnisLatex, l.Proben);
}

/// <summary>T-0781: Rechenblatt mit Lösungswegen als PDF (PDFsharp, wie Brief und Rechnung), LaTeX und Markdown.</summary>
public static class Rechenblatt
{
    public static string Markdown(string titel, IEnumerable<BlattEintrag> eintraege)
    {
        var sb = new StringBuilder($"# {titel}\n\n");
        var n = 0;
        foreach (var e in eintraege)
        {
            n++;
            sb.Append($"## {n}. {e.Aufgabe}\n\n*{e.Art}*\n\n");
            if (e.Fehler is { } f) { sb.Append($"> Nicht gerechnet: {f}\n\n"); continue; }
            sb.Append("**Lösungsweg**\n\n");
            var k = 0;
            foreach (var s in e.Schritte) sb.Append($"{++k}. {s.Text}  \n   `{s.Formel.Replace("`", "'")}`\n");
            sb.Append($"\n**Ergebnis:** {e.Ergebnis}\n\n");
            if (e.ErgebnisLatex.Length > 0) sb.Append($"$${e.ErgebnisLatex}$$\n\n");
            sb.Append("**Probe**\n\n");
            foreach (var p in e.Proben) sb.Append($"- {p.Text}\n");
            sb.Append('\n');
        }
        sb.Append("---\nGerechnet mit dem eigenen Rechenkern (exakt mit Brüchen, Probe unabhängig vom Lösungsweg).\n");
        return sb.ToString();
    }

    static string Tex(string s) => s.Replace("\\", "\\textbackslash{}").Replace("&", "\\&").Replace("%", "\\%").Replace("$", "\\$").Replace("#", "\\#").Replace("_", "\\_").Replace("{", "\\{").Replace("}", "\\}").Replace("~", "\\textasciitilde{}").Replace("^", "\\textasciicircum{}");

    /// <summary>LaTeX-Dokument (mit LuaLaTeX oder XeLaTeX übersetzen – die Schritte enthalten Unicode-Zeichen wie ², √, ·).</summary>
    public static string Latex(string titel, IEnumerable<BlattEintrag> eintraege)
    {
        var sb = new StringBuilder();
        sb.Append("% Mit LuaLaTeX oder XeLaTeX übersetzen\n\\documentclass[11pt,a4paper]{article}\n\\usepackage{fontspec}\n\\usepackage{amsmath,amssymb}\n\\usepackage[ngerman]{babel}\n\\usepackage[margin=2cm]{geometry}\n");
        sb.Append($"\\title{{{Tex(titel)}}}\n\\date{{}}\n\\begin{{document}}\n\\maketitle\n");
        foreach (var e in eintraege)
        {
            sb.Append($"\\section*{{{Tex(e.Aufgabe)}}}\n\\textit{{{Tex(e.Art)}}}\n\n");
            if (e.Fehler is { } f) { sb.Append($"Nicht gerechnet: {Tex(f)}\n\n"); continue; }
            sb.Append("\\paragraph{Lösungsweg}\n\\begin{enumerate}\n");
            foreach (var s in e.Schritte) sb.Append($"  \\item {Tex(s.Text)}\\\\\n  \\texttt{{{Tex(s.Formel)}}}\n");
            sb.Append("\\end{enumerate}\n\\paragraph{Ergebnis}\n");
            sb.Append(e.ErgebnisLatex.Length > 0 ? $"\\[ {e.ErgebnisLatex} \\]\n" : Tex(e.Ergebnis) + "\n\n");
            sb.Append("\\paragraph{Probe}\n\\begin{itemize}\n");
            foreach (var p in e.Proben) sb.Append($"  \\item {Tex(p.Text)}\n");
            sb.Append("\\end{itemize}\n");
        }
        sb.Append("\\end{document}\n");
        return sb.ToString();
    }

    /// <summary>Zeichen, die die PDF-Schrift (Arial) nicht hat, lesbar ersetzen: tiefgestellte Ziffern (H₂O → H2O, x₁ → x1), ⇔ und ⇒
    /// (Befund der Gegenprobe mit pdftotext: diese Zeichen fehlten im PDF).</summary>
    public static string PdfText(string s)
    {
        const string hoch = "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻ⁿ", normal = "0123456789+-n";
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c is >= '₀' and <= '₉') { sb.Append((char)('0' + (c - '₀'))); continue; }
            if (hoch.IndexOf(c) < 0) { sb.Append(c); continue; }
            // T-0781 (Gegenprüfung): Folgen von Hochzeichen zusammenhängend ersetzen – 10⁻¹⁹ → 10^(-19), xⁿ⁻¹ → x^(n-1); einzelnes ¹ ² ³ bleibt (Arial kennt sie)
            var j = i; var lauf = new StringBuilder();
            while (j < s.Length && hoch.IndexOf(s[j]) is var h and >= 0) { lauf.Append(normal[h]); j++; }
            if (lauf.Length == 1 && "¹²³".Contains(c)) sb.Append(c);
            else if (lauf.Length == 1) sb.Append('^').Append(lauf);
            else sb.Append("^(").Append(lauf).Append(')');
            i = j - 1;
        }
        return sb.ToString().Replace("⇔", "<=>").Replace("⇒", "=>").Replace("⟶", "→");
    }

    /// <summary>PDF mit Lösungswegen (A4, Seitenumbruch, Seitenzahlen); gibt die Seitenzahl zurück.</summary>
    public static int Pdf(string pfad, string titel, IEnumerable<BlattEintrag> eintraege)
    {
        using var pdf = new BizPdf(titel, "") { Footer = "Gerechnet mit dem eigenen Rechenkern – exakt mit Brüchen, jede Lösung mit Probe." } /* T-0784: kein Trendo-Branding */;
        pdf.Write(PdfText(titel), 18, true, pdf.Accent); pdf.Y += 3;
        var n = 0;
        var gruen = XColor.FromArgb(0x1E, 0x7B, 0x4A); var rot = XColor.FromArgb(0xB0, 0x2A, 0x2A);
        foreach (var e in eintraege)
        {
            n++;
            pdf.Need(24);
            pdf.Write(PdfText($"{n}. {e.Aufgabe}"), 12.5, true);
            pdf.Write(e.Art, 8.5, false, BizPdf.Grey); pdf.Y += 1.5;
            if (e.Fehler is { } f) { pdf.Write(PdfText("Nicht gerechnet: " + f), 10, false, rot); pdf.Y += 4; continue; }
            var k = 0;
            foreach (var s in e.Schritte)
            {
                pdf.Need(10);
                pdf.Write(PdfText($"{++k}. {s.Text}"), 9.5, false, null, pdf.Left + 2, pdf.Right - pdf.Left - 2);
                pdf.Write(PdfText(s.Formel), 10, false, BizPdf.Dark, pdf.Left + 8, pdf.Right - pdf.Left - 8);
                pdf.Y += 0.8;
            }
            pdf.Need(10); pdf.Y += 1;
            pdf.Write(PdfText("Ergebnis: " + e.Ergebnis), 11.5, true, pdf.Accent);
            var gelb = XColor.FromArgb(0xA0, 0x6A, 0x00);
            foreach (var p in e.Proben) pdf.Write(PdfText(p.Text).Replace("✓", "Probe bestanden:").Replace("✗", "Probe NICHT bestanden:").Replace("⚠", "Probe unsicher:"), 8.5, false, p.Unsicher ? gelb : p.Ok ? gruen : rot);
            pdf.Y += 3; pdf.Rule(); pdf.Y += 4;
        }
        return pdf.Save(pfad);
    }
}
