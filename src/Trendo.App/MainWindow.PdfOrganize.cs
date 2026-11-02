using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Trendo PDF als echter Betrachter (Masterprompt 131): Miniaturleiste beim Lesen und „Seiten ordnen“ – Seiten ziehen, drehen, löschen,
/// leere Seite oder andere PDF einfügen, Rückgängig; gespeichert wird in der Arbeitskopie „Name (Trendo).pdf“, das Original bleibt.</summary>
public partial class MainWindow
{
    List<PageOp>? _pdfOrg; readonly Stack<List<PageOp>> _pdfOrgUndo = new(); readonly HashSet<int> _pdfOrgSel = [];
    readonly Dictionary<string, Bitmap?> _pdfThumbs = [];
    public bool PdfOrganizing => _pdfOrg is not null;
    public IReadOnlyList<PageOp> PdfOrgPages => _pdfOrg ?? [];
    public IReadOnlyCollection<int> PdfOrgSelection => _pdfOrgSel;

    void PdfThumb(Image img, string file, int page, int rot, int dpi)
    {
        var key = $"{file}|{page}|{rot}|{dpi}|{File.GetLastWriteTimeUtc(file).Ticks}";
        if (_pdfThumbs.TryGetValue(key, out var b)) { img.Source = b; return; }
        var pw = string.Equals(file, PdfPath, StringComparison.OrdinalIgnoreCase) ? _pdfPassword : null;
        _ = Task.Run(() =>
        {
            Bitmap? bmp = null;
            try { bmp = PdfView.Render(file, page, dpi, rot, pw); } catch (Exception) { /* Miniatur fehlt → Platzhalter */ }
            _ui.Post(() => { _pdfThumbs[key] = bmp; img.Source = bmp; });
        });
    }

    /// <summary>Miniaturleiste neben der Seite: Klick springt hin, die aktuelle Seite ist hervorgehoben.</summary>
    Control PdfThumbStrip()
    {
        var sp = new StackPanel { Name = "pdf-thumbs", Spacing = 8, Margin = new Thickness(0, 0, 8, 0) };
        for (var i = 0; i < Math.Min(PdfPages, 300); i++)
        {
            var on = i == PdfPage || _pdfDouble && i == PdfPage + 1; var idx = i;
            var img = new Image { Width = 84, Height = 112, Stretch = Stretch.Uniform };
            PdfThumb(img, PdfPath!, i, 0, 14);
            var b = new Button { Name = $"pdf-thumb-{i}", Padding = new Thickness(4), CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Center,
                Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Spacing = 2, Children = { new Border { Background = Brushes.White, Child = img, BoxShadow = BoxShadows.Parse("0 2 6 0 #55000000") },
                    new TextBlock { Text = (i + 1).ToString(), Foreground = on ? GoldText() : Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            b.Click += (_, _) => { PdfGo(idx); Show("pdf"); };
            sp.Children.Add(b);
        }
        var sv = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        // aktuelle Seite in die Leiste holen
        sv.AttachedToVisualTree += (_, _) => _ui.Post(() => { if (sp.Children.Count > PdfPage) sp.Children[PdfPage].BringIntoView(); }, DispatcherPriority.Background);
        return sv;
    }

    public void PdfOrgStart() { if (PdfPath is null || PdfPages == 0) return; _pdfOrg = Enumerable.Range(0, PdfPages).Select(i => new PageOp(PdfPath, i)).ToList(); _pdfOrgUndo.Clear(); _pdfOrgSel.Clear(); }
    void PdfOrgChange(Func<List<PageOp>, List<PageOp>> f)
    {
        var before = _pdfOrg!.ToList(); var after = f(before.ToList());
        if (after.Count == 0) { PdfSay("Es muss mindestens eine Seite übrig bleiben."); return; }
        _pdfOrgUndo.Push(before); _pdfOrg = after;
    }

    /// <summary>Ausgewählte Seiten vor die Seite <paramref name="target"/> (Index in der jetzigen Reihenfolge; = Anzahl für „ans Ende“) verschieben.</summary>
    public void PdfOrgMove(IEnumerable<int> pages, int target)
    {
        var sel = pages.Where(i => i >= 0 && i < _pdfOrg!.Count).Distinct().Order().ToList(); if (sel.Count == 0) return;
        PdfOrgChange(l =>
        {
            var moving = sel.Select(i => l[i]).ToList(); var pos = target - sel.Count(i => i < target);
            var rest = l.Where((_, i) => !sel.Contains(i)).ToList(); rest.InsertRange(Math.Clamp(pos, 0, rest.Count), moving);
            _pdfOrgSel.Clear(); for (var k = 0; k < moving.Count; k++) _pdfOrgSel.Add(Math.Clamp(pos, 0, rest.Count - moving.Count) + k);
            return rest;
        });
    }
    public void PdfOrgSelect(params int[] pages) { _pdfOrgSel.Clear(); foreach (var p in pages) _pdfOrgSel.Add(p); }
    void PdfOrgRotate(int deg) => PdfOrgChange(l => l.Select((o, i) => _pdfOrgSel.Contains(i) ? o with { Rotate = (((o.Rotate + deg) % 360) + 360) % 360 } : o).ToList());
    void PdfOrgDelete() { PdfOrgChange(l => l.Where((_, i) => !_pdfOrgSel.Contains(i)).ToList()); _pdfOrgSel.Clear(); }
    void PdfOrgBlank() { var at = _pdfOrgSel.Count > 0 ? _pdfOrgSel.Max() + 1 : _pdfOrg!.Count; PdfOrgChange(l => { l.Insert(at, new PageOp("", -1)); return l; }); _pdfOrgSel.Clear(); _pdfOrgSel.Add(at); }
    public void PdfOrgInsertFile(string file)
    {
        var n = PdfView.PageCount(file, null); var at = _pdfOrgSel.Count > 0 ? _pdfOrgSel.Max() + 1 : _pdfOrg!.Count;
        PdfOrgChange(l => { l.InsertRange(at, Enumerable.Range(0, n).Select(i => new PageOp(file, i))); return l; });
        _pdfOrgSel.Clear(); for (var k = 0; k < n; k++) _pdfOrgSel.Add(at + k);
        PdfSay($"{n} Seiten aus {Path.GetFileName(file)} eingefügt.");
    }

    /// <summary>Neue Reihenfolge speichern: Arbeitskopie „Name (Trendo).pdf“, danach wird sie geöffnet. Gibt den Pfad zurück.</summary>
    public string? PdfOrgSave()
    {
        if (_pdfOrg is null || PdfPath is null) return null;
        var target = WorkingCopy(); var tmp = Path.Combine(Path.GetTempPath(), $"trendo-pdf-{Guid.NewGuid():N}.pdf");
        try
        {
            var n = PdfTools.Rearrange(_pdfOrg, tmp, _pdfPassword, PdfPath, _core.Audit);
            File.Copy(tmp, target, true); _pdfOrg = null; _pdfOrgUndo.Clear(); _pdfOrgSel.Clear();
            OpenPdf(target, null); PdfSay($"Neue Seitenfolge ({n} Seiten) gespeichert in {Path.GetFileName(target)} – das Original ist unverändert.");
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or PdfSharp.Pdf.IO.PdfReaderException) { PdfSay("Nicht gespeichert: " + ex.Message); return null; }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    Control PdfOrganizeView()
    {
        var ops = _pdfOrg!;
        Button B(string name, string label, Action go, bool on = true, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(10, 4), IsEnabled = on };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += (_, _) => { go(); Show("pdf"); }; return b;
        }
        var info = new TextBlock { Name = "pdf-org-info", Foreground = GoldText(), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 5) };
        var selDep = new List<Button>();
        void Info() { info.Text = $"{ops.Count} Seiten" + (_pdfOrgSel.Count > 0 ? $" · {_pdfOrgSel.Count} ausgewählt" : " · Seiten anklicken (Strg: mehrere), ziehen zum Verschieben"); foreach (var b in selDep) b.IsEnabled = _pdfOrgSel.Count > 0; }
        var left = B("pdf-org-left", "⟲ Drehen", () => PdfOrgRotate(-90)); var right = B("pdf-org-right", "⟳ Drehen", () => PdfOrgRotate(90)); var del = B("pdf-org-delete", "🗑 Löschen", PdfOrgDelete);
        var back = B("pdf-org-back", "◀ Nach vorn", () => PdfOrgMove(_pdfOrgSel, Math.Max(0, _pdfOrgSel.Min() - 1)));
        var fwd = B("pdf-org-fwd", "Nach hinten ▶", () => PdfOrgMove(_pdfOrgSel, Math.Min(ops.Count, _pdfOrgSel.Max() + 2)));
        selDep.AddRange([left, right, del, back, fwd]);
        var insert = new Button { Name = "pdf-org-insert", Content = "PDF einfügen …", Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(10, 4) };
        insert.Click += async (_, _) => { if (await PickPdf("pdf-org-insert", false) is [var f, ..]) { try { PdfOrgInsertFile(f); } catch (Exception ex) when (ex is IOException or InvalidOperationException) { PdfSay("Nicht lesbar: " + ex.Message); } } Show("pdf"); };
        var undo = B("pdf-org-undo", "↶ Rückgängig", () => { if (_pdfOrgUndo.TryPop(out var b)) { _pdfOrg = b; _pdfOrgSel.Clear(); } }, _pdfOrgUndo.Count > 0);
        var bar = new WrapPanel { Children = { info, left, right, del, back, fwd, B("pdf-org-blank", "+ Leere Seite", PdfOrgBlank), insert, undo,
            B("pdf-org-save", "Als Kopie speichern", () => PdfOrgSave(), _pdfOrgUndo.Count > 0, true), B("pdf-org-cancel", "Abbrechen", () => { _pdfOrg = null; _pdfOrgSel.Clear(); }) } };

        var wrap = new WrapPanel { Name = "pdf-org-pages" };
        var indicator = new Border { Width = 4, Background = Brush.Parse("#D4A84B"), CornerRadius = new CornerRadius(2), IsVisible = false, IsHitTestVisible = false, BoxShadow = BoxShadows.Parse("0 0 8 0 #AAD4A84B") };
        var overlay = new Canvas { IsHitTestVisible = false, Children = { indicator } };
        var tiles = new List<Border>();
        void Mark() { for (var k = 0; k < tiles.Count; k++) { var on = _pdfOrgSel.Contains(k); tiles[k].BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"); tiles[k].BorderThickness = new Thickness(on ? 3 : 1); tiles[k].Background = Brush.Parse(on ? "#33C9A227" : "#10FFFFFF"); } Info(); }
        int DropIndex(Point p)
        {
            for (var k = 0; k < tiles.Count; k++) { var r = tiles[k].Bounds; if (p.Y >= r.Top - 4 && p.Y <= r.Bottom + 4 && p.X <= r.Right + 4) return p.X < r.Center.X ? k : k + 1; }
            return tiles.Count;
        }
        for (var i = 0; i < ops.Count; i++)
        {
            var o = ops[i]; var idx = i;
            var img = new Image { Width = 120, Height = 160, Stretch = Stretch.Uniform };
            Control face = o.Page < 0 ? new Border { Width = 120, Height = 160, Background = Brushes.White, Child = new TextBlock { Text = "leer", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }
                : new Border { Background = Brushes.White, Child = img };
            if (o.Page >= 0) PdfThumb(img, o.File, o.Page, o.Rotate, 18);
            var label = (i + 1).ToString() + (o.Rotate != 0 ? $"  ⟳{o.Rotate}°" : "") + (o.Page >= 0 && !string.Equals(o.File, PdfPath, StringComparison.OrdinalIgnoreCase) ? "  · " + Path.GetFileNameWithoutExtension(o.File) : o.Page >= 0 && o.Page != i ? $"  (war {o.Page + 1})" : "");
            var tile = new Border { Name = $"pdf-org-{i}", Padding = new Thickness(6), Margin = new Thickness(0, 0, 12, 12), CornerRadius = new CornerRadius(8), Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Spacing = 4, Children = { new Border { BoxShadow = BoxShadows.Parse("0 3 10 0 #66000000"), Child = face },
                    new TextBlock { Text = label, Foreground = Text, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 120, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            Point? down = null; var dragging = false;
            tile.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed) return; e.Handled = true;
                down = e.GetPosition(wrap); dragging = false; e.Pointer.Capture(tile);
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) { if (!_pdfOrgSel.Remove(idx)) _pdfOrgSel.Add(idx); }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _pdfOrgSel.Count > 0) { var a = _pdfOrgSel.Min(); for (var k = Math.Min(a, idx); k <= Math.Max(a, idx); k++) _pdfOrgSel.Add(k); }
                else if (!_pdfOrgSel.Contains(idx)) { _pdfOrgSel.Clear(); _pdfOrgSel.Add(idx); }
                Mark();
            };
            tile.PointerMoved += (_, e) =>
            {
                if (down is not { } d0) return; var p = e.GetPosition(wrap);
                if (!dragging && Math.Abs(p.X - d0.X) + Math.Abs(p.Y - d0.Y) < 10) return;
                dragging = true; var t = DropIndex(p);
                var r = t < tiles.Count ? tiles[t].Bounds : tiles[^1].Bounds;
                Canvas.SetLeft(indicator, t < tiles.Count ? r.Left - 8 : r.Right - 8); Canvas.SetTop(indicator, r.Top); indicator.Height = r.Height; indicator.IsVisible = true;
            };
            tile.PointerReleased += (_, e) =>
            {
                if (down is null) return; down = null; e.Pointer.Capture(null); indicator.IsVisible = false;
                if (dragging) { PdfOrgMove(_pdfOrgSel, DropIndex(e.GetPosition(wrap))); Show("pdf"); }
                else if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _pdfOrgSel.Count > 1) { _pdfOrgSel.Clear(); _pdfOrgSel.Add(idx); Mark(); }
            };
            tiles.Add(tile); wrap.Children.Add(tile);
        }
        Mark();
        var area = new Grid { Children = { wrap, overlay } };
        return new StackPanel { Name = "pdf-organize", Spacing = 6, Children = { bar, new ScrollViewer { Content = area, MaxHeight = 640 },
            P("Gespeichert wird als „Name (Trendo).pdf“ neben dem Original – das Original bleibt unverändert.", Muted, 11.5) } };
    }
}
