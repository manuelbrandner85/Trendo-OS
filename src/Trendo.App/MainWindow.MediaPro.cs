using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>Freie Bilder & Klänge – Profi 29 (05.11.2026): ähnliche Bilder, Merklisten wie Unsplash Collections, Schriftvorschau mit eigenem Text.</summary>
public partial class MainWindow
{
    MediaCollections Collections => new(Path.Combine(AssetFolder, "Medien"));
    string _mbList = "", _mbNewList = "", _mbListSel = "", _mbListFmt = "text";
    string _fontSample = "Franz jagt im komplett verwahrlosten Taxi quer durch Bayern – 0123456789";
    double _fontSize = 28;
    readonly Dictionary<string, string?> _fontFiles = [];
    /// <summary>Für Tests: Merklisten-Zugriff.</summary>
    public MediaCollections MediaCollectionsForTests => Collections;

    /// <summary>Ähnliche Bilder bzw. Klänge zum gewählten Treffer (öffentlich für Tests und Claude).</summary>
    public Task MediaRelated(MediaHit h) => MediaGuard($"Suche Ähnliches zu „{h.Title}“ …", async () =>
    {
        if (!MediaOnline) throw new InvalidOperationException("Offline – die Suche braucht Internet.");
        var lib = MediaLib;
        _mbAll = await lib.Related(h, _mbLicense);
        MediaHits = MediaLibrary.Filter(_mbAll, MediaOrient); MediaSel = MediaHits.Count > 0 ? 0 : -1; _mbQuery = "ähnlich: " + h.Title;
        Show(MB);
        foreach (var x in MediaHits.Take(50).Where(x => x.ThumbUrl.Length > 0 && !_mbThumbs.ContainsKey(x.Id)))
            try { _mbThumbs[x.Id] = await lib.Thumb(x); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException) { _mbThumbs[x.Id] = null; }
        return MediaHits.Count == 0 ? "Nichts Ähnliches gefunden." : $"{MediaHits.Count} ähnliche {(h.Kind == "klang" ? "Klänge" : "Bilder")} zu „{h.Title}“";
    });

    /// <summary>Treffer in eine Merkliste (öffentlich für Tests und Claude).</summary>
    public void MediaCollect(MediaHit h, string list)
    {
        var added = Collections.Add(list, h);
        _mbList = list.Trim();
        _core.Audit.Append(Principal.LocalOwner.Name, "medien.merken", $"{_mbList}: {h.Provider}/{h.Id}");
        _mbMsg = added ? $"☆ „{h.Title}“ in „{_mbList}“ gemerkt ({h.License})." : $"„{h.Title}“ steht schon in „{_mbList}“.";
    }

    /// <summary>Alle Einträge einer Merkliste in die Bibliothek laden; Namensnennungen als Datei daneben.</summary>
    public Task MediaCollectionGetAll(string list) => MediaGuard($"„{list}“ wird geladen …", async () =>
    {
        if (!MediaOnline) throw new InvalidOperationException("Offline – Laden geht erst mit Internet.");
        var c = Collections.All().FirstOrDefault(x => x.Name == list) ?? throw new InvalidOperationException("Diese Merkliste gibt es nicht.");
        var lib = MediaLib; var n = 0; var failed = 0;
        foreach (var e in c.Entries)
            try { await lib.Download(e.Hit); n++; } catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or TaskCanceledException) { failed++; }
        var credits = Path.Combine(AssetFolder, "Medien", $"Namensnennungen – {string.Concat(list.Split(Path.GetInvalidFileNameChars()))}.txt");
        await File.WriteAllTextAsync(credits, Collections.Credits(list));
        _core.Audit.Append(Principal.LocalOwner.Name, "medien.merkliste.laden", $"{list}: {n}");
        return $"✓ {n} Einträge aus „{list}“ in der Bibliothek" + (failed > 0 ? $", {failed} gingen nicht" : "") + $" – Namensnennungen in „{Path.GetFileName(credits)}“.";
    });

    /// <summary>Preview-Teil „Ähnliche“ und „Merken“ unter dem gewählten Treffer.</summary>
    Control MediaHitTools(MediaHit h)
    {
        var lists = Collections.All().Select(c => c.Name).ToList();
        if (_mbList.Length == 0 && lists.Count > 0) _mbList = lists[0];
        var sp = new StackPanel { Name = "mb-hit-tools", Spacing = 4 };
        sp.Children.Add(Act("mb-related", h.Kind == "klang" ? "≈ Ähnliche Klänge" : "≈ Ähnliche Bilder", () => _ = MediaRelated(h)));
        var pick = new ComboBox { Name = "mb-collect-list", ItemsSource = lists.Append("＋ Neue Merkliste …").ToList(), SelectedIndex = lists.Count == 0 ? 0 : Math.Max(0, lists.IndexOf(_mbList)), MinWidth = 170 };
        var name = new TextBox { Name = "mb-collect-new", Text = _mbNewList, PlaceholderText = "Name, z. B. Projekt Sommerfest", IsVisible = lists.Count == 0, MinWidth = 170 };
        pick.SelectionChanged += (_, _) => { name.IsVisible = pick.SelectedIndex >= lists.Count; if (pick.SelectedIndex >= 0 && pick.SelectedIndex < lists.Count) _mbList = lists[pick.SelectedIndex]; };
        name.TextChanged += (_, _) => _mbNewList = name.Text ?? "";
        var add = Act("mb-collect", "☆ Merken", () => { Try(MB, () => MediaCollect(h, pick.SelectedIndex >= lists.Count || lists.Count == 0 ? name.Text ?? "" : lists[pick.SelectedIndex])); Show(MB); });
        sp.Children.Add(new WrapPanel { Children = { pick, new Border { Width = 4 }, add } });
        sp.Children.Add(name);
        return sp;
    }

    Control MediaCollectionsView()
    {
        var all = Collections.All();
        var sp = new StackPanel { Name = "mb-lists", Spacing = 6 };
        if (all.Count == 0) { sp.Children.Add(P("Noch keine Merkliste. Bei einem Bild oder Klang „☆ Merken“ antippen – z. B. eine Liste je Projekt. Lizenz und Namensnennung bleiben immer dabei.", Muted, 12)); return Card(sp); }
        if (!all.Any(c => c.Name == _mbListSel)) _mbListSel = all[0].Name;
        var tabs = new WrapPanel { Name = "mb-list-tabs" };
        foreach (var c in all)
        {
            var n = c.Name; var on = n == _mbListSel;
            var b = new Button { Name = "mb-list-" + all.IndexOf(c), Content = $"{n} ({c.Entries.Count})", Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(12),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1), Foreground = Text };
            b.Click += (_, _) => { _mbListSel = n; Show(MB); };
            tabs.Children.Add(b);
        }
        sp.Children.Add(tabs);
        var cur = all.First(c => c.Name == _mbListSel);
        var free = cur.Entries.Count(e => e.Hit.Free);
        sp.Children.Add(P($"{cur.Entries.Count} Einträge · {free} frei ohne Bedingungen · {cur.Entries.Count - free} mit Namensnennung", Muted, 11.5));
        var fmt = new ComboBox { Name = "mb-list-fmt", ItemsSource = MediaLibrary.CreditFormats.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(MediaLibrary.CreditFormats, x => x.Id == _mbListFmt)), MinWidth = 130 };
        fmt.SelectionChanged += (_, _) => _mbListFmt = MediaLibrary.CreditFormats[Math.Max(0, fmt.SelectedIndex)].Id;
        var list = cur.Name;
        sp.Children.Add(new WrapPanel { Children = {
            Act("mb-list-getall", "Alle in die Bibliothek", () => _ = MediaCollectionGetAll(list), true), new Border { Width = 6 },
            fmt, new Border { Width = 4 }, Act("mb-list-credits", "Namensnennungen kopieren", () => MediaCopy(Collections.Credits(list, _mbListFmt), "Namensnennungen")), new Border { Width = 6 },
            Act("mb-list-delete", "Liste löschen", () => { Try(MB, () => { Collections.Delete(list); _mbMsg = $"Merkliste „{list}“ gelöscht – geladene Dateien bleiben."; }); Show(MB); }) } });
        var grid = new WrapPanel { Name = "mb-list-grid" };
        var i = 0;
        foreach (var e in cur.Entries)
        {
            var id = e.Hit.Id; var hit = e.Hit;
            Control pic = _mbThumbs.TryGetValue(id, out var tp) && tp is not null && BibBitmap(tp) is { } bmp
                ? new Image { Source = bmp, Stretch = Stretch.UniformToFill, Height = 110 }
                : new Border { Height = 110, Background = Brush.Parse("#14FFFFFF"), Child = new TextBlock { Text = hit.Kind == "klang" ? "♪" : "▧", FontSize = 30, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var del = new Button { Name = $"mb-list-del-{i}", Content = "✕", Padding = new Thickness(6, 0), Background = Brushes.Transparent, Foreground = Muted };
            del.Click += (_, _) => { Collections.Remove(list, id); Show(MB); };
            grid.Children.Add(new Border { Name = $"mb-list-item-{i++}", Width = 180, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new StackPanel { Children = { pic, new StackPanel { Margin = new Thickness(8, 6), Spacing = 2, Children = {
                    new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), new TextBlock { Text = hit.Title, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    new TextBlock { Text = hit.License + (hit.Free ? " · frei" : " · nennen"), Foreground = hit.Free ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), FontSize = 10.5 },
                    new TextBlock { Text = hit.Creator, Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } } } } });
        }
        sp.Children.Add(grid);
        return Card(sp);
    }

    // ───────────── Schriftvorschau ─────────────
    FreeFontsPreview FontPreviewer => new(MediaFetchOverride ?? new HttpFetch(BibShared), Path.Combine(Path.GetTempPath(), "trendo-schriftvorschau"));

    /// <summary>Vorschau-Schriften der ersten Treffer laden (im Hintergrund, eine Stärke je Schrift).</summary>
    async Task FontPreviewLoad(IEnumerable<FontHit> fonts)
    {
        foreach (var f in fonts.Take(12).Where(f => !_fontFiles.ContainsKey(f.Id)))
        {
            try { _fontFiles[f.Id] = await FontPreviewer.File400(f); } catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or TaskCanceledException or InvalidOperationException) { _fontFiles[f.Id] = null; }
            if (CurrentView == MB && MediaTab == "schriften") Show(MB);
        }
    }

    /// <summary>Probetext in einer Schrift als Bild (SkiaSharp – die Schrift muss dafür nicht installiert sein).</summary>
    public static byte[]? FontSamplePng(string ttf, string text, float size, int width = 560)
    {
        using var tf = SKTypeface.FromFile(ttf); if (tf is null) return null;
        using var font = new SKFont(tf, size); using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var words = (text.Length == 0 ? " " : text).Split(' '); var lines = new List<string>(); var cur = "";
        foreach (var w in words) { var t = cur.Length == 0 ? w : cur + " " + w; if (font.MeasureText(t) > width - 8 && cur.Length > 0) { lines.Add(cur); cur = w; } else cur = t; }
        lines.Add(cur); lines = [.. lines.Take(3)];
        var h = (int)Math.Ceiling(size * 1.3f * lines.Count + size * 0.4f);
        using var bmp = new SKBitmap(width, Math.Max(10, h), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var c = new SKCanvas(bmp)) { c.Clear(SKColors.Transparent); for (var i = 0; i < lines.Count; i++) c.DrawText(lines[i], 4, size * 1.05f + i * size * 1.3f, font, paint); }
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }

    Control FontSampleBar()
    {
        var t = new TextBox { Name = "mb-font-sample", Text = _fontSample, PlaceholderText = "Eigener Text für die Vorschau", MinWidth = 320 };
        t.LostFocus += (_, _) => { if ((t.Text ?? "") != _fontSample) { _fontSample = t.Text ?? ""; Show(MB); } };
        t.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _fontSample = t.Text ?? ""; Show(MB); e.Handled = true; } };
        var size = new Slider { Name = "mb-font-size", Minimum = 12, Maximum = 72, Value = _fontSize, Width = 140 };
        size.PointerCaptureLost += (_, _) => { _fontSize = Math.Round(size.Value); Show(MB); };
        return new WrapPanel { Children = { new TextBlock { Text = "Probetext", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, t, new Border { Width = 8 },
            new TextBlock { Text = "Größe", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, size } };
    }

    /// <summary>Vorschau einer Schrift (oder Platzhalter, solange sie lädt).</summary>
    Control FontSample(FontHit f, int i)
    {
        if (_fontFiles.TryGetValue(f.Id, out var file) && file is not null && FontSamplePng(file, _fontSample, (float)_fontSize) is { } png)
            return new Image { Name = $"mb-font-preview-{i}", Source = new Bitmap(new MemoryStream(png)), Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        return new TextBlock { Text = _fontFiles.ContainsKey(f.Id) ? "(Vorschau nicht verfügbar)" : i < 12 ? "Vorschau lädt …" : "", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) };
    }
}
