using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0229 P6: Trendo Präsentation speichert PPTX (PowerPoint/Impress) und liest PPTX und ODP.
/// Texte, Aufzählungen, Formen, Bilder, Tabellen, Diagramme, Notizen, Übergänge und Einblend-Animationen bleiben erhalten.
/// In der Datei steht weder ein Autorname noch der Programmname (Vorgabe 2026-10-27).</summary>
public static partial class SlideFiles
{
    public static readonly string[] Extensions = [".pptx", ".odp"];
    const long Emu = 360000; // 1 cm
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main", A = "http://schemas.openxmlformats.org/drawingml/2006/main",
        R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", Pr = "http://schemas.openxmlformats.org/package/2006/relationships",
        C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    const string NsDecl = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
    const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
    const string RelT = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

    /// <summary>Öffnet die Datei und hängt den Importbericht an (T-0682): was die Datei enthält, das Trendo Präsentation nicht hält.</summary>
    public static SlideDoc Open(string path) { var d = Read(path); d.Import = ImportCheck.For(path); return d; }

    static SlideDoc Read(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pptx" => ReadPptx(path),
        ".odp" => ReadOdp(path),
        _ => throw new InvalidDataException("Dieses Format kann Trendo Präsentation nicht öffnen."),
    };

    public static void Save(SlideDoc doc, string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".pptx": WritePptx(doc, path); break;
            case ".pdf": SlidePdf.Export(doc, path); break;
            default: throw new InvalidDataException("Speichern geht als PPTX oder PDF.");
        }
    }

    static string X(string s) => XmlSafe.Escape(s); // T-0780: Steuerzeichen raus (M2)
    static long E(double cm) => (long)Math.Round(cm * Emu);
    static string Hex(string c) => c.TrimStart('#').ToUpperInvariant() is var h && h.Length == 8 ? h[2..] : h;
    static string RoleName(string? role, ShapeKind k) => role switch { "title" => "Titel", "sub" => "Untertitel", "body" => "Inhalt", "body2" => "Inhalt 2", "pic" => "Bildplatzhalter", _ => k switch { ShapeKind.Bild => "Bild", ShapeKind.Tabelle => "Tabelle", ShapeKind.Diagramm => "Diagramm", ShapeKind.Medien => "Medien", ShapeKind.Text => "Textfeld", _ => "Form" } };
    static string? RoleOf(string? name) => name?.Trim() switch { "Titel" => "title", "Untertitel" => "sub", "Inhalt" => "body", "Inhalt 2" => "body2", "Bildplatzhalter" => "pic", _ => null };

    // ================= PPTX schreiben =================
    /// <summary>PPTX schreiben. T-0701: Stammt die Präsentation aus einer fremden PPTX und hat in Trendo kein anderes Design bekommen
    /// (<see cref="SlideDoc.KeepsDesign"/>), kommen Folienmaster, Layouts, Theme und deren Bilder unverändert aus der Datei; Platzhalter, Übergänge,
    /// Animationen und Hintergründe, die in Trendo nicht geändert wurden, werden ebenfalls unverändert zurückgeschrieben.</summary>
    public static void WritePptx(SlideDoc doc, string path)
    {
        var th = doc.Theme;
        var design = doc.KeepsDesign ? doc.Design : null;
        double kx = design is null ? 1 : design.Cx / (SlideDoc.WidthCm * Emu), ky = design is null ? 1 : design.Cy / (SlideDoc.HeightCm * Emu);
        long Ex(double cm) => (long)Math.Round(cm * Emu * kx);
        long Ey(double cm) => (long)Math.Round(cm * Emu * ky);
        var taken = new HashSet<string>(design?.Parts.Keys ?? [], StringComparer.OrdinalIgnoreCase);
        string Free(string dir, string stem, string ext, ref int n) { string name; do name = $"{dir}{stem}{++n}.{ext}"; while (taken.Contains(name)); taken.Add(name); return name; }

        using var fs = File.Create(path);
        using var z = new ZipArchive(fs, ZipArchiveMode.Create);
        void Add(string name, string content) { using var w = new StreamWriter(z.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); w.Write(content); }
        void AddBytes(string name, byte[] b) { using var s = z.CreateEntry(name, CompressionLevel.NoCompression).Open(); s.Write(b); }

        var overrides = new StringBuilder();
        void Ov(string part, string type) => overrides.Append($"<Override PartName=\"/{part}\" ContentType=\"{type}\"/>");
        const string Pml = "application/vnd.openxmlformats-officedocument.presentationml.";
        Ov("ppt/presentation.xml", Pml + "presentation.main+xml");
        var themeNo = 0; var notesTheme = "ppt/theme/theme2.xml";
        if (design is null)
        {
            Ov("ppt/slideMasters/slideMaster1.xml", Pml + "slideMaster+xml");
            Ov("ppt/slideLayouts/slideLayout1.xml", Pml + "slideLayout+xml");
            Ov("ppt/theme/theme1.xml", "application/vnd.openxmlformats-officedocument.theme+xml");
        }
        else
        {
            foreach (var (part, bytes) in design.Parts)
            {
                AddBytes(part, bytes);
                if (design.ContentTypes.TryGetValue(part, out var ct)) Ov(part, ct);
            }
            notesTheme = Free("ppt/theme/", "theme", "xml", ref themeNo);
        }
        Ov("ppt/notesMasters/notesMaster1.xml", Pml + "notesMaster+xml");
        Ov(notesTheme, "application/vnd.openxmlformats-officedocument.theme+xml");
        Ov("docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");

        string LayoutFor(Slide s)
        {
            if (design is null) return "../slideLayouts/slideLayout1.xml";
            if (s.SourceLayout is { } sl && design.Parts.ContainsKey(sl)) return "/" + sl;
            var type = s.Layout switch { SlideLayout.Titel => "title", SlideLayout.TitelInhalt => "obj", SlideLayout.ZweiSpalten => "twoObj", SlideLayout.Bild => "picTx", SlideLayout.NurTitel => "titleOnly", _ => "blank" };
            return "/" + (design.LayoutByType.GetValueOrDefault(type) ?? design.LayoutByType.GetValueOrDefault("blank") ?? design.FirstLayout ?? design.LayoutByType.Values.First());
        }

        var media = 0; var charts = 0;
        for (var i = 0; i < doc.Slides.Count; i++)
        {
            var s = doc.Slides[i]; var n = i + 1;
            var rels = new StringBuilder($"<Relationship Id=\"rId1\" Type=\"{RelT}slideLayout\" Target=\"{LayoutFor(s)}\"/><Relationship Id=\"rId2\" Type=\"{RelT}notesSlide\" Target=\"../notesSlides/notesSlide{n}.xml\"/>");
            var rid = 3; var sp = new StringBuilder(); var anims = new List<(int Id, Animation A)>();
            // T-0701: Objekte behalten ihre Nummer aus der Datei (Animationen zeigen darauf); doppelte (z. B. nach Duplizieren) bekommen eine neue
            var ids = new HashSet<int> { 1 }; var keptIds = new HashSet<int>(); var nextId = Math.Max(2, s.Shapes.Select(x => x.SourceId ?? 0).DefaultIfEmpty(0).Max() + 1);
            var phDone = new HashSet<string>();
            foreach (var shp in s.Shapes)
            {
                int sid;
                if (shp.SourceId is int src && ids.Add(src)) { sid = src; keptIds.Add(src); }
                else { while (!ids.Add(nextId)) nextId++; sid = nextId++; }
                if (shp.Animation != Animation.Keine) anims.Add((sid, shp.Animation));
                var xfrm = $"<a:xfrm><a:off x=\"{Ex(shp.X)}\" y=\"{Ey(shp.Y)}\"/><a:ext cx=\"{Ex(Math.Max(0.1, shp.W))}\" cy=\"{Ey(Math.Max(0.1, shp.H))}\"/></a:xfrm>";
                var name = X(RoleName(shp.Role, shp.Kind));
                // Platzhalter der fremden Vorlage – jeder nur einmal je Folie
                var ph = design is not null && shp.PhType is { } pt && phDone.Add(pt + "|" + shp.PhIdx)
                    ? $"<p:ph{(pt.Length > 0 ? $" type=\"{X(pt)}\"" : "")}{(shp.PhIdx is { } pi ? $" idx=\"{X(pi)}\"" : "")}/>" : null;
                switch (shp.Kind)
                {
                    case ShapeKind.Bild when shp.Image is { Length: > 0 } img:
                    {
                        var png = img.Length > 4 && img[0] == 0x89 && img[1] == 0x50; var ext = png ? "png" : "jpeg";
                        var mediaName = Free("ppt/media/", "image", ext, ref media); AddBytes(mediaName, img);
                        rels.Append($"<Relationship Id=\"rId{rid}\" Type=\"{RelT}image\" Target=\"../media/{mediaName[10..]}\"/>");
                        sp.Append($"<p:pic><p:nvPicPr><p:cNvPr id=\"{sid}\" name=\"{name}\"/><p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr>{ph}</p:nvPr></p:nvPicPr><p:blipFill><a:blip r:embed=\"rId{rid++}\"/><a:stretch><a:fillRect/></a:stretch></p:blipFill><p:spPr>{xfrm}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>");
                        break;
                    }
                    case ShapeKind.Tabelle when shp.Table is { Count: > 0 } tbl:
                    {
                        var cols = tbl.Max(r => r.Count); var cw = Ex(shp.W) / Math.Max(1, cols); var rh = Ey(shp.H) / tbl.Count;
                        sp.Append($"<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"{sid}\" name=\"{name}\"/><p:cNvGraphicFramePr><a:graphicFrameLocks noGrp=\"1\"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>");
                        sp.Append($"<p:xfrm><a:off x=\"{Ex(shp.X)}\" y=\"{Ey(shp.Y)}\"/><a:ext cx=\"{Ex(shp.W)}\" cy=\"{Ey(shp.H)}\"/></p:xfrm><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\"><a:tbl><a:tblPr firstRow=\"1\" bandRow=\"1\"/><a:tblGrid>");
                        for (var c = 0; c < cols; c++) sp.Append($"<a:gridCol w=\"{cw}\"/>");
                        sp.Append("</a:tblGrid>");
                        for (var r = 0; r < tbl.Count; r++)
                        {
                            sp.Append($"<a:tr h=\"{rh}\">");
                            for (var c = 0; c < cols; c++)
                            {
                                var txt = c < tbl[r].Count ? tbl[r][c] : "";
                                var fill = r == 0 ? Hex(th.Accent) : r % 2 == 0 ? "EEF2F7" : "FFFFFF"; var color = r == 0 ? "FFFFFF" : "1F2937";
                                sp.Append($"<a:tc><a:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"de-DE\" sz=\"{(int)(shp.FontSize * 100)}\"{(r == 0 ? " b=\"1\"" : "")} dirty=\"0\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill></a:rPr><a:t>{X(txt)}</a:t></a:r></a:p></a:txBody><a:tcPr><a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill></a:tcPr></a:tc>");
                            }
                            sp.Append("</a:tr>");
                        }
                        sp.Append("</a:tbl></a:graphicData></a:graphic></p:graphicFrame>");
                        break;
                    }
                    case ShapeKind.Diagramm when shp.ChartValues is { Count: > 0 }:
                    {
                        var chartName = Free("ppt/charts/", "chart", "xml", ref charts);
                        rels.Append($"<Relationship Id=\"rId{rid}\" Type=\"{RelT}chart\" Target=\"../charts/{chartName[11..]}\"/>");
                        Add(chartName, ChartXml(shp, th));
                        Ov(chartName, "application/vnd.openxmlformats-officedocument.drawingml.chart+xml");
                        sp.Append($"<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"{sid}\" name=\"{name}\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr><p:xfrm><a:off x=\"{Ex(shp.X)}\" y=\"{Ey(shp.Y)}\"/><a:ext cx=\"{Ex(shp.W)}\" cy=\"{Ey(shp.H)}\"/></p:xfrm>");
                        sp.Append($"<a:graphic><a:graphicData uri=\"{C}\"><c:chart xmlns:c=\"{C}\" r:id=\"rId{rid++}\"/></a:graphicData></a:graphic></p:graphicFrame>");
                        break;
                    }
                    default:
                    {
                        var geom = shp.Kind switch { ShapeKind.Ellipse => "ellipse", ShapeKind.Pfeil => "rightArrow", _ => "rect" };
                        var text = shp.Kind == ShapeKind.Medien ? "▶ " + Path.GetFileName(shp.MediaPath ?? "Medien") : shp.Text;
                        var descr = shp.Kind == ShapeKind.Medien && shp.MediaPath is { } mp ? $" descr=\"{X("media:" + mp)}\"" : "";
                        var fill = shp.Fill is { } f ? $"<a:solidFill><a:srgbClr val=\"{Hex(f)}\"/></a:solidFill>" : ph is not null ? "" : "<a:noFill/>";
                        var isText = shp.Kind == ShapeKind.Text;
                        var cnv = ph is not null ? "<p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr>" : $"<p:cNvSpPr{(isText ? " txBox=\"1\"" : "")}/>";
                        var geomXml = ph is not null && shp.Kind == ShapeKind.Text ? "" : $"<a:prstGeom prst=\"{geom}\"><a:avLst/></a:prstGeom>";
                        sp.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{sid}\" name=\"{name}\"{descr}/>{cnv}<p:nvPr>{ph}</p:nvPr></p:nvSpPr><p:spPr>{xfrm}{geomXml}{fill}{(isText || ph is not null ? "" : "<a:ln><a:noFill/></a:ln>")}</p:spPr>");
                        sp.Append(ph is not null ? "<p:txBody><a:bodyPr/><a:lstStyle/>" : $"<p:txBody><a:bodyPr wrap=\"square\" rtlCol=\"0\" anchor=\"{(isText ? "t" : "ctr")}\"><a:normAutofit/></a:bodyPr><a:lstStyle/>");
                        // Mit fremdem Design: Farbe, Schrift und geerbte Größe nur schreiben, wenn sie in Trendo festgelegt wurden – sonst greift die Vorlage
                        var color = shp.Color is { } sc ? Hex(sc) : design is null ? Hex(shp.Role == "title" ? th.Title : th.Text) : null;
                        var font = design is null ? th.Font : null;
                        var algn = shp.Align switch { Align.Center => "ctr", Align.Right => "r", Align.Justify => "just", _ => "l" };
                        var runs = shp.Kind == ShapeKind.Medien ? null : shp.Runs;
                        var lines = text.Split('\n'); var pos = 0;
                        foreach (var raw in lines)
                        {
                            var lvl = raw.StartsWith('\t') ? 1 : 0; var line = raw.TrimStart('\t'); var lineStart = pos + (raw.Length - line.Length);
                            string ppr;
                            if (ph is not null)
                            {
                                var attrs = (lvl > 0 ? " lvl=\"1\"" : "") + (shp.Align != Align.Left ? $" algn=\"{algn}\"" : "");
                                ppr = !shp.Bullets && shp.PhType is "" or "body" or "obj" ? $"<a:pPr{attrs}><a:buNone/></a:pPr>" : attrs.Length > 0 ? $"<a:pPr{attrs}/>" : "";
                            }
                            else ppr = shp.Bullets ? $"<a:pPr marL=\"{342900 + lvl * 457200}\" lvl=\"{lvl}\" indent=\"-342900\" algn=\"{algn}\"><a:buFont typeface=\"Arial\"/><a:buChar char=\"{(lvl == 0 ? "•" : "–")}\"/></a:pPr>" : $"<a:pPr algn=\"{algn}\"><a:buNone/></a:pPr>";
                            string Rpr(TextRun? run)
                            {
                                var size = run?.Size is { } rs ? rs : shp.SizeInherited && design is not null ? (double?)null : shp.FontSize * (lvl == 1 ? 0.85 : 1);
                                bool? bold = run?.Bold ?? (shp.BoldInherited && design is not null ? null : shp.Bold);
                                var c = run?.Color is { } rc ? Hex(rc) : color; var fnt = run?.Font ?? font;
                                return $"<a:rPr lang=\"de-DE\"{(size is { } z ? $" sz=\"{(int)Math.Round(z * 100)}\"" : "")}{(bold == true ? " b=\"1\"" : bold == false && design is not null && !shp.BoldInherited ? " b=\"0\"" : "")}{(run?.Italic == true ? " i=\"1\"" : "")}{(run?.Underline is { } u ? $" u=\"{X(u)}\"" : "")} dirty=\"0\">"
                                    + (c is not null ? $"<a:solidFill><a:srgbClr val=\"{c}\"/></a:solidFill>" : "") + (fnt is not null ? $"<a:latin typeface=\"{X(fnt)}\"/>" : "") + "</a:rPr>";
                            }
                            var endSize = shp.SizeInherited && design is not null ? "" : $" sz=\"{(int)(shp.FontSize * 100)}\"";
                            if (line.Length == 0) sp.Append($"<a:p>{ppr}<a:endParaRPr lang=\"de-DE\"{endSize} dirty=\"0\"/></a:p>");
                            else if (runs is null) sp.Append($"<a:p>{ppr}<a:r>{Rpr(null)}<a:t>{X(line)}</a:t></a:r></a:p>");
                            else
                            {
                                // T-0701: mehrere Textformate – je Abschnitt ein eigener Lauf
                                sp.Append("<a:p>").Append(ppr);
                                var chars = TextRuns.Expand(runs, text);
                                for (var k = 0; k < line.Length;)
                                {
                                    var look = chars[lineStart + k]; var j = k; while (j < line.Length && chars[lineStart + j] == look) j++;
                                    sp.Append($"<a:r>{Rpr(look)}<a:t>{X(line[k..j])}</a:t></a:r>"); k = j;
                                }
                                sp.Append("</a:p>");
                            }
                            pos += raw.Length + 1;
                        }
                        sp.Append("</p:txBody></p:sp>");
                        break;
                    }
                }
            }
            var xml = new StringBuilder($"{Xml}<p:sld {NsDecl}><p:cSld>");
            // Hintergrund: unverändert aus der Datei, eigene Farbe oder – ohne fremdes Design – die Farbe von Trendos Design
            if (s.SourceBg is { } srcBg && s.Background == s.SourceBgColor) xml.Append(srcBg);
            else if (s.Background is not null || design is null) xml.Append($"<p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"{Hex(s.Background ?? th.Background)}\"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>");
            xml.Append("<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>");
            xml.Append(sp).Append("</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>");
            xml.Append(s.SourceTransition is { } srcTr && s.Transition == s.SourceTransitionKind ? srcTr : s.Transition switch
            {
                Transition.Ueberblenden => "<p:transition spd=\"med\"><p:fade/></p:transition>",
                Transition.Schieben => "<p:transition spd=\"med\"><p:push dir=\"u\"/></p:transition>",
                Transition.Wischen => "<p:transition spd=\"med\"><p:wipe dir=\"r\"/></p:transition>",
                _ => "",
            });
            // T-0701: Animationen der Datei unverändert, solange in Trendo daran nichts geändert wurde und jedes Ziel-Objekt noch da ist
            if (s.SourceTiming is { } timing && s.AnimSignature() == s.SourceAnimSig && TimingTargets(timing).All(keptIds.Contains)) xml.Append(timing);
            else if (anims.Count > 0) xml.Append(TimingXml(anims));
            xml.Append("</p:sld>");
            Add($"ppt/slides/slide{n}.xml", xml.ToString());
            Add($"ppt/slides/_rels/slide{n}.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\">{rels}</Relationships>");
            Ov($"ppt/slides/slide{n}.xml", Pml + "slide+xml");
            // Notizen
            Add($"ppt/notesSlides/notesSlide{n}.xml", $"{Xml}<p:notes {NsDecl}><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
                "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Folienbild\"/><p:cNvSpPr><a:spLocks noGrp=\"1\" noRot=\"1\" noChangeAspect=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"sldImg\"/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"381000\" y=\"685800\"/><a:ext cx=\"6096000\" cy=\"3429000\"/></a:xfrm></p:spPr></p:sp>" +
                "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notizen\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"685800\" y=\"4343400\"/><a:ext cx=\"5486400\" cy=\"4114800\"/></a:xfrm></p:spPr><p:txBody><a:bodyPr/><a:lstStyle/>" +
                string.Concat((s.Notes.Length == 0 ? [""] : s.Notes.Split('\n')).Select(l => l.Length == 0 ? "<a:p><a:endParaRPr lang=\"de-DE\" dirty=\"0\"/></a:p>" : $"<a:p><a:r><a:rPr lang=\"de-DE\" dirty=\"0\"/><a:t>{X(l)}</a:t></a:r></a:p>")) +
                "</p:txBody></p:sp></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:notes>");
            Add($"ppt/notesSlides/_rels/notesSlide{n}.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}notesMaster\" Target=\"../notesMasters/notesMaster1.xml\"/><Relationship Id=\"rId2\" Type=\"{RelT}slide\" Target=\"../slides/slide{n}.xml\"/></Relationships>");
            Ov($"ppt/notesSlides/notesSlide{n}.xml", Pml + "notesSlide+xml");
        }

        var defaults = new StringBuilder("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>");
        foreach (var (ext, type) in design?.Defaults ?? new Dictionary<string, string>())
            if (ext is not ("rels" or "xml" or "png" or "jpeg") && ext.All(char.IsLetterOrDigit)) defaults.Append($"<Default Extension=\"{X(ext)}\" ContentType=\"{X(type)}\"/>");
        Add("[Content_Types].xml", $"{Xml}<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">{defaults}{overrides}</Types>");
        Add("_rels/.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}officeDocument\" Target=\"ppt/presentation.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
        Add("docProps/core.xml", $"{Xml}<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:title>{X(doc.Slides.FirstOrDefault()?.Title ?? "")}</dc:title><dc:creator></dc:creator><dcterms:created xsi:type=\"dcterms:W3CDTF\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created></cp:coreProperties>");
        var sldIds = string.Concat(doc.Slides.Select((_, i) => $"<p:sldId id=\"{256 + i}\" r:id=\"rId{10 + i}\"/>"));
        const string grp = "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>";
        const string clrMap = "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>";
        var slideRels = string.Concat(doc.Slides.Select((_, i) => $"<Relationship Id=\"rId{10 + i}\" Type=\"{RelT}slide\" Target=\"slides/slide{i + 1}.xml\"/>"));
        var notesRel = $"<Relationship Id=\"rIdNm\" Type=\"{RelT}notesMaster\" Target=\"notesMasters/notesMaster1.xml\"/>";
        if (design is null)
        {
            Add("ppt/presentation.xml", $"{Xml}<p:presentation {NsDecl} saveSubsetFonts=\"1\"><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst><p:notesMasterIdLst><p:notesMasterId r:id=\"rIdNm\"/></p:notesMasterIdLst><p:sldIdLst>{sldIds}</p:sldIdLst><p:sldSz cx=\"12192000\" cy=\"6858000\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/><p:defaultTextStyle/></p:presentation>");
            Add("ppt/_rels/presentation.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/><Relationship Id=\"rId2\" Type=\"{RelT}theme\" Target=\"theme/theme1.xml\"/>{notesRel}{slideRels}</Relationships>");
            Add("ppt/slideMasters/slideMaster1.xml", $"{Xml}<p:sldMaster {NsDecl}><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg><p:spTree>{grp}</p:spTree></p:cSld>{clrMap}<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst><p:txStyles><p:titleStyle><a:lvl1pPr><a:defRPr sz=\"4400\"/></a:lvl1pPr></p:titleStyle><p:bodyStyle><a:lvl1pPr><a:defRPr sz=\"2400\"/></a:lvl1pPr></p:bodyStyle><p:otherStyle><a:lvl1pPr><a:defRPr sz=\"1800\"/></a:lvl1pPr></p:otherStyle></p:txStyles></p:sldMaster>");
            Add("ppt/slideMasters/_rels/slideMaster1.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/><Relationship Id=\"rId2\" Type=\"{RelT}theme\" Target=\"../theme/theme1.xml\"/></Relationships>");
            Add("ppt/slideLayouts/slideLayout1.xml", $"{Xml}<p:sldLayout {NsDecl} type=\"blank\" preserve=\"1\"><p:cSld name=\"Leer\"><p:spTree>{grp}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>");
            Add("ppt/slideLayouts/_rels/slideLayout1.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}slideMaster\" Target=\"../slideMasters/slideMaster1.xml\"/></Relationships>");
            Add("ppt/theme/theme1.xml", ThemeXml(th));
        }
        else
        {
            var masterIds = string.Concat(design.Masters.Select((m, i) => $"<p:sldMasterId id=\"{X(m.Id)}\" r:id=\"rIdM{i + 1}\"/>"));
            var masterRels = string.Concat(design.Masters.Select((m, i) => $"<Relationship Id=\"rIdM{i + 1}\" Type=\"{RelT}slideMaster\" Target=\"/{X(m.Part)}\"/>"));
            var themeRel = design.ThemePart is { } tp ? $"<Relationship Id=\"rIdTh\" Type=\"{RelT}theme\" Target=\"/{X(tp)}\"/>" : "";
            Add("ppt/presentation.xml", $"{Xml}<p:presentation {NsDecl} saveSubsetFonts=\"1\"><p:sldMasterIdLst>{masterIds}</p:sldMasterIdLst><p:notesMasterIdLst><p:notesMasterId r:id=\"rIdNm\"/></p:notesMasterIdLst><p:sldIdLst>{sldIds}</p:sldIdLst>"
                + $"<p:sldSz cx=\"{design.Cx}\" cy=\"{design.Cy}\"{(design.SizeType is { } st ? $" type=\"{X(st)}\"" : "")}/><p:notesSz cx=\"6858000\" cy=\"9144000\"/>{design.DefaultTextStyle ?? "<p:defaultTextStyle/>"}</p:presentation>");
            Add("ppt/_rels/presentation.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\">{masterRels}{themeRel}{notesRel}{slideRels}</Relationships>");
        }
        Add("ppt/notesMasters/notesMaster1.xml", $"{Xml}<p:notesMaster {NsDecl}><p:cSld><p:spTree>{grp}<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Notizen\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"685800\" y=\"4343400\"/><a:ext cx=\"5486400\" cy=\"4114800\"/></a:xfrm></p:spPr><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:endParaRPr lang=\"de-DE\"/></a:p></p:txBody></p:sp></p:spTree></p:cSld>{clrMap}</p:notesMaster>");
        Add("ppt/notesMasters/_rels/notesMaster1.xml.rels", $"{Xml}<Relationships xmlns=\"{Pr}\"><Relationship Id=\"rId1\" Type=\"{RelT}theme\" Target=\"/{notesTheme}\"/></Relationships>");
        Add(notesTheme, ThemeXml(SlideThemes.All[0]));
    }

    /// <summary>Objekt-Nummern, auf die Animationen zeigen.</summary>
    static IEnumerable<int> TimingTargets(string timing)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(timing, "spid=\"(\\d+)\""))
            yield return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    static string ThemeXml(SlideTheme th)
    {
        string S(string v) => $"<a:srgbClr val=\"{Hex(v)}\"/>";
        var bgIsDark = Hex(th.Background) is var b && Convert.ToInt32(b[..2], 16) + Convert.ToInt32(b[2..4], 16) + Convert.ToInt32(b[4..6], 16) < 384;
        var (dk1, lt1) = bgIsDark ? (th.Background, th.Title) : (th.Title, th.Background);
        return $"{Xml}<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"{X(th.Name)}\"><a:themeElements><a:clrScheme name=\"{X(th.Name)}\">" +
            $"<a:dk1>{S(dk1)}</a:dk1><a:lt1>{S(lt1)}</a:lt1><a:dk2>{S(th.Text)}</a:dk2><a:lt2>{S(th.Background2 ?? th.Background)}</a:lt2>" +
            $"<a:accent1>{S(th.Accent)}</a:accent1><a:accent2>{S("#F59E0B")}</a:accent2><a:accent3>{S("#10B981")}</a:accent3><a:accent4>{S("#EF4444")}</a:accent4><a:accent5>{S("#8B5CF6")}</a:accent5><a:accent6>{S("#06B6D4")}</a:accent6>" +
            $"<a:hlink>{S("#2F86F6")}</a:hlink><a:folHlink>{S("#7C3AED")}</a:folHlink></a:clrScheme>" +
            $"<a:fontScheme name=\"{X(th.Font)}\"><a:majorFont><a:latin typeface=\"{X(th.Font)}\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont><a:minorFont><a:latin typeface=\"{X(th.Font)}\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>" +
            "<a:fmtScheme name=\"Standard\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst>" +
            "<a:lnStyleLst><a:ln w=\"6350\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln></a:lnStyleLst>" +
            "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>" +
            "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>" +
            "</a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>";
    }

    internal static string ChartXml(SlideShape s, SlideTheme th)
    {
        var labels = s.ChartLabels ?? []; var vals = s.ChartValues ?? [];
        var inv = CultureInfo.InvariantCulture;
        var cat = $"<c:cat><c:strLit><c:ptCount val=\"{labels.Count}\"/>{string.Concat(labels.Select((l, i) => $"<c:pt idx=\"{i}\"><c:v>{X(l)}</c:v></c:pt>"))}</c:strLit></c:cat>";
        var val = $"<c:val><c:numLit><c:formatCode>General</c:formatCode><c:ptCount val=\"{vals.Count}\"/>{string.Concat(vals.Select((v, i) => $"<c:pt idx=\"{i}\"><c:v>{v.ToString("R", inv)}</c:v></c:pt>"))}</c:numLit></c:val>";
        var ser = $"<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:tx><c:v>{X(s.Text.Length > 0 ? s.Text : "Werte")}</c:v></c:tx>{(s.ChartKind == ChartKind.Kreis ? "" : $"<c:spPr><a:solidFill><a:srgbClr val=\"{Hex(th.Accent)}\"/></a:solidFill></c:spPr>")}{(s.ChartKind == ChartKind.Linie ? "<c:marker><c:symbol val=\"circle\"/></c:marker>" : "")}{cat}{val}{(s.ChartKind == ChartKind.Linie ? "<c:smooth val=\"0\"/>" : "")}</c:ser>";
        var plot = s.ChartKind switch
        {
            ChartKind.Kreis => $"<c:pieChart><c:varyColors val=\"1\"/>{ser}<c:firstSliceAng val=\"0\"/></c:pieChart>",
            ChartKind.Linie => $"<c:lineChart><c:grouping val=\"standard\"/><c:varyColors val=\"0\"/>{ser}<c:marker val=\"1\"/><c:axId val=\"1001\"/><c:axId val=\"1002\"/></c:lineChart>",
            _ => $"<c:barChart><c:barDir val=\"col\"/><c:grouping val=\"clustered\"/><c:varyColors val=\"0\"/>{ser}<c:axId val=\"1001\"/><c:axId val=\"1002\"/></c:barChart>",
        };
        var axes = s.ChartKind == ChartKind.Kreis ? "" : "<c:catAx><c:axId val=\"1001\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"b\"/><c:crossAx val=\"1002\"/></c:catAx><c:valAx><c:axId val=\"1002\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"l\"/><c:majorGridlines/><c:crossAx val=\"1001\"/></c:valAx>";
        var title = s.Text.Length > 0 ? $"<c:title><c:tx><c:rich><a:bodyPr/><a:p><a:r><a:t>{X(s.Text)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>" : "<c:autoTitleDeleted val=\"1\"/>";
        return $"{Xml}<c:chartSpace xmlns:c=\"{C}\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><c:chart>{title}<c:plotArea><c:layout/>{plot}{axes}</c:plotArea><c:legend><c:legendPos val=\"b\"/><c:overlay val=\"0\"/></c:legend><c:plotVisOnly val=\"1\"/></c:chart></c:chartSpace>";
    }

    /// <summary>P4: Animationen als Klickfolge (Einblenden = presetID 10, Hereinfliegen = 2 von unten, Zoomen = 23).</summary>
    static string TimingXml(List<(int Id, Animation A)> anims)
    {
        var sb = new StringBuilder("<p:timing><p:tnLst><p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst><p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"2\" dur=\"indefinite\" nodeType=\"mainSeq\"><p:childTnLst>");
        var id = 3;
        foreach (var (spid, a) in anims)
        {
            var (preset, sub) = a switch { Animation.Hereinfliegen => (2, 4), Animation.Zoomen => (23, 16), _ => (10, 0) };
            var tgt = $"<p:tgtEl><p:spTgt spid=\"{spid}\"/></p:tgtEl>";
            sb.Append($"<p:par><p:cTn id=\"{id++}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"indefinite\"/></p:stCondLst><p:childTnLst><p:par><p:cTn id=\"{id++}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>");
            sb.Append($"<p:par><p:cTn id=\"{id++}\" presetID=\"{preset}\" presetClass=\"entr\" presetSubtype=\"{sub}\" fill=\"hold\" grpId=\"0\" nodeType=\"clickEffect\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>");
            sb.Append($"<p:set><p:cBhvr><p:cTn id=\"{id++}\" dur=\"1\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst></p:cTn>{tgt}<p:attrNameLst><p:attrName>style.visibility</p:attrName></p:attrNameLst></p:cBhvr><p:to><p:strVal val=\"visible\"/></p:to></p:set>");
            sb.Append(a switch
            {
                Animation.Hereinfliegen => $"<p:anim calcmode=\"lin\" valueType=\"num\"><p:cBhvr additive=\"base\"><p:cTn id=\"{id++}\" dur=\"500\" fill=\"hold\"/>{tgt}<p:attrNameLst><p:attrName>ppt_y</p:attrName></p:attrNameLst></p:cBhvr><p:tavLst><p:tav tm=\"0\"><p:val><p:strVal val=\"1+#ppt_h/2\"/></p:val></p:tav><p:tav tm=\"100000\"><p:val><p:strVal val=\"#ppt_y\"/></p:val></p:tav></p:tavLst></p:anim>",
                Animation.Zoomen => $"<p:animScale><p:cBhvr><p:cTn id=\"{id++}\" dur=\"500\" fill=\"hold\"/>{tgt}</p:cBhvr><p:from x=\"0\" y=\"0\"/><p:to x=\"100000\" y=\"100000\"/></p:animScale>",
                _ => $"<p:animEffect transition=\"in\" filter=\"fade\"><p:cBhvr><p:cTn id=\"{id++}\" dur=\"500\"/>{tgt}</p:cBhvr></p:animEffect>",
            });
            sb.Append("</p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par>");
        }
        sb.Append("</p:childTnLst></p:cTn><p:prevCondLst><p:cond evt=\"onPrev\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst><p:nextCondLst><p:cond evt=\"onNext\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst></p:seq></p:childTnLst></p:cTn></p:par></p:tnLst>");
        sb.Append("<p:bldLst>").Append(string.Concat(anims.Select(x => $"<p:bldP spid=\"{x.Id}\" grpId=\"0\" animBg=\"1\"/>"))).Append("</p:bldLst></p:timing>");
        return sb.ToString();
    }
}
