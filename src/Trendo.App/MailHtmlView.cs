using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0689: Zeichnet eine geprüfte HTML-Nachricht (<see cref="MailSafeHtml"/>) mit eigenen Bedienelementen – ohne Browser, ohne Skript-Maschine.
/// Es gibt hier schlicht nichts, das Skripte ausführen oder von selbst etwas aus dem Internet laden könnte: Bilder kommen nur über
/// <paramref name="image"/> (eingebettet, oder nach ausdrücklicher Freigabe geladen), Links melden sich nur über <paramref name="link"/>.</summary>
public sealed class MailHtmlView(IBrush text, IBrush muted, IBrush accent, Func<MailHtmlElement, byte[]?> image, Action<string> link)
{
    int _links, _images;
    sealed record Style(bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false, double Size = 13, bool Mono = false, bool Dim = false);

    public Control Build(MailSafeHtml safe) => Block(safe.Root, new Style(), 0);

    Control Block(MailHtmlElement e, Style st, int depth)
    {
        var panel = new StackPanel { Spacing = 6 };
        InlineCollection? run = null; var hasUi = false;
        void Flush()
        {
            if (run is null || run.Count == 0) { run = null; return; }
            // führende/abschließende Leerzeichen eines Absatzes weg
            if (run[0] is Run first && first.Text is { } ft) first.Text = ft.TrimStart();
            if (run[^1] is Run last && last.Text is { } lt) last.Text = lt.TrimEnd();
            if (run.All(i => i is Run r && string.IsNullOrEmpty(r.Text))) { run = null; hasUi = false; return; }
            TextBlock tb = hasUi ? new TextBlock() : new SelectableTextBlock();
            tb.TextWrapping = e.Tag == "pre" ? TextWrapping.NoWrap : TextWrapping.Wrap; tb.Foreground = st.Dim ? muted : text; tb.FontSize = st.Size; tb.LineHeight = Math.Round(st.Size * 1.5);
            if (st.Mono) tb.FontFamily = new FontFamily("Consolas,Cascadia Mono,monospace");
            tb.Inlines = run; panel.Children.Add(tb); run = null; hasUi = false;
        }
        void Inline(MailHtmlNode n, Style s)
        {
            run ??= [];
            if (n is MailHtmlText t)
            {
                var r = new Run(t.Text);
                if (s.Bold) r.FontWeight = FontWeight.SemiBold;
                if (s.Italic) r.FontStyle = FontStyle.Italic;
                if (s.Underline || s.Strike) r.TextDecorations = s.Strike ? TextDecorations.Strikethrough : TextDecorations.Underline;
                if (Math.Abs(s.Size - st.Size) > 0.1) r.FontSize = s.Size;
                run.Add(r); return;
            }
            var el = (MailHtmlElement)n;
            switch (el.Tag)
            {
                case "br": run.Add(new LineBreak()); return;
                case "a" when el.Attr.TryGetValue("href", out var href):
                {
                    var label = MailHtml.TextOf(el); if (label.Length == 0) label = href;
                    var b = new Button { Name = $"mail-link-{_links++}", Content = new TextBlock { Text = label, Foreground = accent, TextDecorations = TextDecorations.Underline, FontSize = s.Size, TextWrapping = TextWrapping.Wrap },
                        Padding = new Thickness(0), Margin = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), VerticalAlignment = VerticalAlignment.Bottom };
                    ToolTip.SetTip(b, "Führt zu: " + href);
                    b.Click += (_, _) => link(href);
                    run.Add(new InlineUIContainer(b) { BaselineAlignment = BaselineAlignment.Bottom }); hasUi = true;
                    // Bilder innerhalb des Links trotzdem zeigen
                    foreach (var img in MailHtml.All(el).OfType<MailHtmlElement>().Where(x => x.Tag == "img")) { Flush(); AddImage(img); run ??= []; }
                    return;
                }
                case "img": Flush(); AddImage(el); return;
            }
            var ns = el.Tag switch
            {
                "b" or "strong" => s with { Bold = true }, "i" or "em" => s with { Italic = true }, "u" or "ins" => s with { Underline = true },
                "s" or "strike" or "del" => s with { Strike = true }, "small" => s with { Size = Math.Max(10, s.Size - 2) }, "big" => s with { Size = s.Size + 2 }, "code" => s, _ => s,
            };
            foreach (var c in el.Children)
            {
                if (c is MailHtmlElement ce && MailHtml.Blocks.Contains(ce.Tag)) { Flush(); AddBlock(ce, ns); run ??= []; }
                else Inline(c, ns);
            }
        }
        void AddImage(MailHtmlElement img)
        {
            _images++;
            if (img.Attr.ContainsKey("blocked")) { panel.Children.Add(new TextBlock { Text = "▢ Bild aus dem Internet – nicht geladen" + (img.Attr.TryGetValue("alt", out var alt) && alt.Length > 0 ? $" ({alt})" : ""), Foreground = muted, FontSize = 11.5 }); return; }
            var data = image(img);
            if (data is null) { if (img.Attr.TryGetValue("alt", out var a2) && a2.Length > 0) panel.Children.Add(new TextBlock { Text = $"[Bild: {a2}]", Foreground = muted, FontSize = 11.5 }); return; }
            try
            {
                using var ms = new MemoryStream(data);
                var bmp = new Bitmap(ms);
                var real = SizeOf(data);
                if (real is { W: <= 2, H: <= 2 }) return; // Zählpixel: nichts zeigen
                var w = img.Attr.TryGetValue("width", out var ws) && int.TryParse(ws, out var wi) ? wi : real?.W ?? bmp.PixelSize.Width;
                panel.Children.Add(new Image { Name = $"mail-image-{_images - 1}", Source = bmp, MaxWidth = Math.Min(Math.Max(16, w), 640), Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { panel.Children.Add(new TextBlock { Text = "[Bild kann nicht angezeigt werden]", Foreground = muted, FontSize = 11.5 }); }
        }
        void AddBlock(MailHtmlElement b, Style s)
        {
            if (depth > 40) { foreach (var c in b.Children) Inline(c, s); Flush(); return; }
            switch (b.Tag)
            {
                case "hr": panel.Children.Add(new Border { Height = 1, Background = muted, Opacity = 0.4, Margin = new Thickness(0, 4) }); return;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    panel.Children.Add(Block(b, s with { Bold = true, Size = b.Tag switch { "h1" => 20, "h2" => 17, "h3" => 15, _ => 13.5 } }, depth + 1)); return;
                case "blockquote":
                    panel.Children.Add(new Border { BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 2, 0, 2), Child = Block(b, s with { Dim = true }, depth + 1) }); return;
                case "pre": panel.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = Block(b, s with { Mono = true }, depth + 1) }); return;
                case "ul" or "ol":
                {
                    var list = new StackPanel { Spacing = 3, Margin = new Thickness(6, 0, 0, 0) }; var n = 0;
                    foreach (var c in b.Children.OfType<MailHtmlElement>())
                    {
                        if (c.Tag != "li") { list.Children.Add(Block(c, s, depth + 1)); continue; }
                        n++;
                        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("22,*"), Children = {
                            new TextBlock { Text = b.Tag == "ol" ? n + "." : "•", Foreground = muted, FontSize = s.Size },
                            Col(Block(c, s, depth + 1), 1) } });
                    }
                    panel.Children.Add(list); return;
                }
                case "table" or "thead" or "tbody" or "tfoot":
                    foreach (var c in b.Children.OfType<MailHtmlElement>()) AddBlock(c, s);
                    return;
                case "tr":
                {
                    var cells = b.Children.OfType<MailHtmlElement>().Where(c => c.Tag is "td" or "th").ToList();
                    if (cells.Count == 0) return;
                    if (cells.Count == 1 || cells.Count > 6) { foreach (var c in cells) panel.Children.Add(Block(c, c.Tag == "th" ? s with { Bold = true } : s, depth + 1)); return; }
                    var g = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", cells.Select(_ => "*"))) };
                    for (var i = 0; i < cells.Count; i++) { var cell = Block(cells[i], cells[i].Tag == "th" ? s with { Bold = true } : s, depth + 1); cell.Margin = new Thickness(0, 0, 10, 0); g.Children.Add(Col(cell, i)); }
                    panel.Children.Add(g); return;
                }
                case "center": { var c = Block(b, s, depth + 1); c.HorizontalAlignment = HorizontalAlignment.Center; panel.Children.Add(c); return; }
                default: panel.Children.Add(Block(b, s, depth + 1)); return;
            }
        }
        foreach (var c in e.Children)
        {
            if (c is MailHtmlElement ce && MailHtml.Blocks.Contains(ce.Tag)) { Flush(); AddBlock(ce, st); }
            else Inline(c, st);
        }
        Flush();
        return panel;
    }

    /// <summary>Bildgröße aus den ersten Bytes (PNG, GIF) – unabhängig davon, wie das Bild gezeichnet wird.</summary>
    static (int W, int H)? SizeOf(byte[] d)
    {
        if (d.Length >= 24 && d[0] == 0x89 && d[1] == 'P' && d[2] == 'N' && d[3] == 'G') return ((d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19], (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23]);
        if (d.Length >= 10 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F') return (d[6] | (d[7] << 8), d[8] | (d[9] << 8));
        return null;
    }

    static Control Col(Control c, int col) { Grid.SetColumn(c, col); return c; }
}
