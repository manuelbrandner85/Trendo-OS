using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 6 – SYS-10: Geschäftsbrief nach DIN 5008 (Form A/B) mit typst. Konzept typst-letter-pro, eigener Code, offline.
/// Betreff = Titel-Feld, Text = Text-Feld der typst-Karte; Absender wird aus den Briefstudio-Angaben vorbelegt (nichts wird überschrieben).</summary>
public partial class MainWindow
{
    /// <summary>DIN-5008-Brief als PDF in den Ordner „Dokumente“ setzen (öffentlich für Tests). Rückgabe: PDF-Pfad oder null.</summary>
    public string? TypstDinLetter(string sender, string recipient, string subject, string body, bool formA, string yourRef = "", string ourRef = "")
    {
        var l = new TypstDoc.DinLetter(sender, recipient, subject, body, DateOnly.FromDateTime(DateTime.Today), formA, yourRef, ourRef);
        if (TypstDoc.DinCheck(l) is { Count: > 0 } errors) { _typstMsg = "⚠ " + string.Join(" ", errors); Show("doku-schreiben"); return null; }
        var dir = DokuOut; Directory.CreateDirectory(dir);
        var name = "brief-" + string.Concat(subject.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        if (name.Length > 60) name = name[..60].TrimEnd('-');
        var typ = Path.Combine(dir, name + ".typ"); File.WriteAllText(typ, TypstDoc.Din5008(l));
        var (ok, msg) = TypstDoc.Compile(ToolExe("typst") ?? "", typ, Path.Combine(dir, name + ".pdf"), dir, run: ToolRunOverride);
        _typstMsg = ok ? $"✓ Brief nach DIN 5008 (Form {(formA ? "A" : "B")}) gespeichert: {Path.GetFileName(msg)} – passt ins Fensterkuvert DL/C6; Quelle {Path.GetFileName(typ)} bleibt bearbeitbar." : "⚠ " + msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "office.typst.din5008", ok ? Path.GetFileName(msg) : "fehler");
        Show("doku-schreiben"); return ok ? msg : null;
    }

    Control DinCard(TextBox title, TextBox body)
    {
        var known = string.Join("\n", new[] { _settings.Name, _settings.SenderStreet, _settings.SenderCity, _settings.SenderContact }.Where(s => s.Length > 0));
        var sender = new TextBox { Name = "din-sender", Text = known, AcceptsReturn = true, Width = 240, Height = 72, PlaceholderText = "Absender: Name, Straße, PLZ Ort, Telefon" };
        var recipient = new TextBox { Name = "din-recipient", AcceptsReturn = true, Width = 240, Height = 72, PlaceholderText = "Empfänger (höchstens 6 Zeilen)" };
        var your = new TextBox { Name = "din-your", PlaceholderText = "Ihr Zeichen (optional)", Width = 160 };
        var our = new TextBox { Name = "din-our", PlaceholderText = "Unser Zeichen (optional)", Width = 160 };
        var formA = new CheckBox { Name = "din-form-a", Content = new TextBlock { Text = "Form A (kleiner Briefkopf)", Foreground = Text } };
        var go = Act("din-run", "Geschäftsbrief als PDF setzen", () => TypstDinLetter(sender.Text ?? "", recipient.Text ?? "", title.Text ?? "", body.Text ?? "", formA.IsChecked == true, your.Text ?? "", our.Text ?? ""));
        var sp = new StackPanel { Spacing = 4, Children = {
            P("Normgerecht nach DIN 5008: Anschrift im Fenster, Falz- und Lochmarken, Datum und Zeichen rechts. Betreff = Titel, Text = Textfeld oben.", Muted, 11),
            new WrapPanel { Children = { sender, new Border { Width = 6 }, recipient } }, new WrapPanel { Children = { your, new Border { Width = 4 }, our, new Border { Width = 8 }, formA } }, go } };
        return new Expander { Name = "din-box", Header = "Geschäftsbrief nach DIN 5008", Content = sp };
    }
}
