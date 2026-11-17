using System.IO.Compression;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0696: Gemeinsames für den Vektor-Editor in „Logos &amp; Marken“ und „Animation &amp; Motion“ – Zustand je Dokument, Werkzeugleiste,
/// Bilder/SVG einsetzen, Logo-Ausgaben (PNG in Größen, PDF, Favicon, Paket).</summary>
public partial class MainWindow
{
    readonly Dictionary<string, VectorEditorState> _vecStates = [];

    /// <summary>Editor-Zustand (Werkzeug, Auswahl, Raster) des Dokuments – bleibt beim Neuzeichnen erhalten.</summary>
    public VectorEditorState VecState(ProgramDocument d) => _vecStates.TryGetValue(d.Id, out var s) ? s : _vecStates[d.Id] = new VectorEditorState();

    /// <summary>Der gerade gezeigte Editor je Programm (für Tests und Claude).</summary>
    public readonly Dictionary<string, VectorEditor> VecEditors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Werkzeuge links (Hülle): gleiche Liste in beiden Programmen.</summary>
    List<ProgramTool> VecTools(string key) =>
        [.. VectorEditorState.Tools.Select(t => new ProgramTool("vec-tool-" + t.Id, t.Icon, t.Label, t.Tip, d => VecState(d).Tool = t.Id, () => ProgramActive(key) is { } d && VecState(d).Tool == t.Id))];

    /// <summary>Dateien einsetzen: SVG wird zur Gruppe (bearbeitbar), PNG/JPEG zum Bild – eingepasst und mittig bei (cx, cy).</summary>
    public List<string> VecImport(VDoc doc, IReadOnlyList<string> files, double cx, double cy)
    {
        var ids = new List<string>();
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (new FileInfo(f).Length > Trendo.Core.Kreativ.KreativSafe.MaxImageBytes) throw new InvalidDataException("Die Datei ist zu groß.");
            if (ext == ".svg")
            {
                var (src, _) = VectorKit.FromSvg(File.ReadAllText(f));
                ids.AddRange(VectorKit.Place(doc, src, cx, cy, doc.W * 0.6, doc.H * 0.6));
            }
            else if (ext is ".png" or ".jpg" or ".jpeg")
            {
                var bytes = File.ReadAllBytes(f);
                double w, h;
                using (var data = SkiaSharp.SKData.CreateCopy(bytes)) using (var codec = SkiaSharp.SKCodec.Create(data) ?? throw new InvalidDataException("Das Bild ließ sich nicht lesen.")) { w = codec.Info.Width; h = codec.Info.Height; }
                var k = Math.Min(1, Math.Min(doc.W * 0.6 / w, doc.H * 0.6 / h));
                var s = new VShape { Id = VectorKit.NextId(doc, "bild"), Kind = "image", Name = Path.GetFileNameWithoutExtension(f), Fill = "", W = w * k, H = h * k, Image = VectorKit.ToDataUri(bytes, f) };
                s.X = cx - s.W / 2; s.Y = cy - s.H / 2;
                doc.Shapes.Add(s); ids.Add(s.Id);
            }
            else throw new InvalidDataException("Einsetzen geht mit SVG, PNG und JPEG.");
        }
        return ids;
    }

    /// <summary>Editor mit den gemeinsamen Anschlüssen der Hülle (Neuzeichnen, Einsetzen per Auswahl und Ziehen).</summary>
    VectorEditor VecEditor(string key, ProgramDocument d, VDoc doc, Action<VDoc, string> save, bool scaleMode = false)
    {
        var st = VecState(d);
        VectorEditor? ed = null;
        ed = new VectorEditor(doc, st, (v, label) => ProgramRun(key, () => save(v, label)), key == LogoKey ? "logo-vec" : "anim-vec") { ScaleMode = scaleMode };
        ed.Rerender = () => Show(key);
        void Insert(IReadOnlyList<string> files, double x, double y) => ProgramRun(key, () =>
        {
            var v = ed!.Doc.Clone();
            var ids = VecImport(v, files, x, y);
            st.Selected.Clear(); st.Selected.UnionWith(ids); st.Tool = "select";
            save(v, files.Count == 1 ? Path.GetFileName(files[0]) + " eingesetzt" : $"{files.Count} Dateien eingesetzt");
        });
        ed.DropFiles = Insert;
        ed.PickImport = () => _ = ProgramPickThen(key, key + "-import", "Bild oder SVG einsetzen", ["svg", "png", "jpg", "jpeg"], true, files =>
        {
            var v = ed.Doc.Clone(); var ids = VecImport(v, files, v.W / 2, v.H / 2);
            st.Selected.Clear(); st.Selected.UnionWith(ids); st.Tool = "select";
            save(v, files.Length == 1 ? Path.GetFileName(files[0]) + " eingesetzt" : $"{files.Length} Dateien eingesetzt");
        });
        VecEditors[key] = ed;
        return ed;
    }

    // ---------------- Logo-Ausgaben ----------------

    /// <summary>PNG in mehreren Größen, PDF, Favicon und Markenpaket in einem ZIP (Ordnerstruktur wie ein Marken-Kit).</summary>
    public static void LogoPackage(VDoc v, string name, string zipFile)
    {
        if (v.Shapes.Count == 0) throw new InvalidOperationException("Das Logo ist noch leer.");
        var tmp = zipFile + ".tmp";
        using (var fs = File.Create(tmp))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            void Put(string entry, byte[] bytes) { using var s = zip.CreateEntry(entry, CompressionLevel.Optimal).Open(); s.Write(bytes); }
            void Txt(string entry, string text) => Put(entry, System.Text.Encoding.UTF8.GetBytes(text));
            Txt("logo.svg", VectorKit.ToSvg(v));
            Txt("logo-hell.svg", VectorKit.ToSvg(VectorKit.ForBackground(v, "#FFFFFF"), false));
            Txt("logo-dunkel.svg", VectorKit.ToSvg(VectorKit.ForBackground(v, "#111827"), false));
            Txt("logo-einfarbig.svg", VectorKit.ToSvg(VectorKit.Mono(v, "#000000"), false));
            Txt("logo-negativ.svg", VectorKit.ToSvg(VectorKit.Mono(v, "#FFFFFF"), false));
            foreach (var px in new[] { 512, 1024, 2048 }) Put($"logo-{px}.png", VectorRender.Png(v, px));
            Put("logo.pdf", VectorKit.Pdf(v));
            Put("favicon.ico", LogoIco(v));
            Txt("markenpaket.md", VectorKit.BrandMarkdown(v, name));
        }
        File.Move(tmp, zipFile, true);
    }

    /// <summary>Favicon: das Zeichen (Gruppe „zeichen“, sonst alles) quadratisch in 16, 32, 48, 64, 128, 256 Pixel.</summary>
    public static byte[] LogoIco(VDoc v)
    {
        var mark = v.Shapes.Where(s => s.Group == "zeichen" || s.Id.StartsWith("zeichen", StringComparison.Ordinal)).ToList();
        if (mark.Count == 0) mark = v.Shapes;
        var b = VectorKit.Bounds(mark); var side = Math.Max(b.W, b.H) * 1.06;
        var sq = new VDoc { W = side, H = side, Background = "transparent", Shapes = mark.Select(s => { var c = s.Clone(); c.X += side / 2 - (b.X + b.W / 2); c.Y += side / 2 - (b.Y + b.H / 2); return c; }).ToList() };
        return VectorKit.Ico(new[] { 16, 32, 48, 64, 128, 256 }.Select(px => (px, VectorRender.Png(sq, px, null, px))).ToList());
    }
}
