using System.IO.Compression;
using System.Text;
using Trendo.Core.Cad;
using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0776 Gegenprüfung: Datei-Strukturen ohne Fremdprogramm prüfen – STEP-Zeichenketten und -Zahlen (ISO 10303-21), PSD-Ebenen
/// (Unicode-Name „luni“, genau eine Ebene „Hintergrund“) und der ASCII-Vermerk der Sicherungs-ZIP.</summary>
public class T0776_Korrektur
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0776k-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0776 STEP: Namen nach ISO 10303-21 (Apostroph und Backslash verdoppelt, Umlaute als \\X2\\…\\X0\\, Zeichen außerhalb der Grundebene als \\X4\\), Zahlen immer mit Dezimalpunkt")]
    public void Step_strings_and_reals()
    {
        Assert.Equal("Quader", CadExport.StepString("Quader"));
        Assert.Equal("Peter''s Teil", CadExport.StepString("Peter's Teil"));
        Assert.Equal("C:\\\\Teile\\\\a", CadExport.StepString("C:\\Teile\\a"));
        Assert.Equal("Halter f\\X2\\00FC\\X0\\r T\\X2\\00FC\\X0\\r", CadExport.StepString("Halter für Tür"));
        Assert.Equal("Ma\\X2\\00DF00E4\\X0\\", CadExport.StepString("Maßä"));
        Assert.Equal("Teil \\X4\\0001F527\\X0\\", CadExport.StepString("Teil 🔧"));
        Assert.Equal("a\\X2\\000A\\X0\\b", CadExport.StepString("a\nb")); // Steuerzeichen ebenfalls kodiert
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "teil'ä.step");
            CadExport.WriteStep([("Peter's Würfel", CadShapes.Box(new Vec3(0, 0, 0), 30, 20, 10))], f);
            var text = File.ReadAllText(f);
            Assert.Contains("PRODUCT('Peter''s W\\X2\\00FC\\X0\\rfel','Peter''s W\\X2\\00FC\\X0\\rfel'", text);
            Assert.Contains("FILE_NAME('teil''\\X2\\00E4\\X0\\.step'", text);
            Assert.All(text, c => Assert.True(c < 128, "Nicht-ASCII in der STEP-Datei: " + c));
            var points = System.Text.RegularExpressions.Regex.Matches(text, @"CARTESIAN_POINT\('',\(([^)]*)\)\)");
            Assert.NotEmpty(points);
            foreach (System.Text.RegularExpressions.Match m in points)
                foreach (var num in m.Groups[1].Value.Split(','))
                    Assert.True(num.Contains('.') || num.Contains('E'), $"Zahl ohne Dezimalpunkt: {num} in {m.Value}");
            Assert.Contains("CARTESIAN_POINT('',(30.,20.,10.))", text);
            // Trendos eigener Leser kommt mit verdoppelten Apostrophen und Dezimalpunkten zurecht
            Assert.Equal(6000, CadReference.FromMesh("Teil", f, CadStepRead.ReadFacetted(f)).ToSolid().Volume(), 3);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>Ebenen einer PSD (Abschnitt „Layer and Mask Information“) nach der öffentlichen Formatbeschreibung lesen: Pascal-Name und, falls vorhanden, „luni“.</summary>
    static List<(string Pascal, string? Luni, int Flags, byte Opacity)> PsdLayers(byte[] psd)
    {
        int p = 0;
        uint U32() { var v = (uint)(psd[p] << 24 | psd[p + 1] << 16 | psd[p + 2] << 8 | psd[p + 3]); p += 4; return v; }
        void Skip() { var n = (int)U32(); p += n; } // nicht „p += U32()“: dort würde p vor dem Lesen ausgewertet
        int U16() { var v = psd[p] << 8 | psd[p + 1]; p += 2; return v; }
        Assert.Equal("8BPS", Encoding.ASCII.GetString(psd, 0, 4));
        p = 26; Skip(); Skip(); // Farbmodus-Daten, Bildressourcen
        var lmEnd = (int)U32() + p; var liLen = (int)U32(); var liEnd = p + liLen;
        var count = (short)U16(); count = Math.Abs(count);
        var res = new List<(string, string?, int, byte)>();
        for (var i = 0; i < count; i++)
        {
            p += 16; var ch = U16(); p += ch * 6;
            Assert.Equal("8BIM", Encoding.ASCII.GetString(psd, p, 4)); p += 8;
            var opacity = psd[p]; var flags = psd[p + 2]; p += 4;
            var extraLen = (int)U32(); var extraEnd = p + extraLen;
            Skip(); Skip(); // Maske, Mischbereiche
            var nameLen = psd[p]; var pascal = Encoding.ASCII.GetString(psd, p + 1, nameLen); p += (1 + nameLen + 3) / 4 * 4;
            string? luni = null;
            while (p + 12 <= extraEnd)
            {
                Assert.Equal("8BIM", Encoding.ASCII.GetString(psd, p, 4)); var key = Encoding.ASCII.GetString(psd, p + 4, 4); p += 8;
                var len = (int)U32(); var start = p;
                if (key == "luni") { var n = (int)U32(); luni = Encoding.BigEndianUnicode.GetString(psd, p, n * 2); Assert.Equal(0, len % 4); }
                p = start + len;
            }
            Assert.Equal(extraEnd, p);
            res.Add((pascal, luni, flags, opacity));
        }
        Assert.True(liEnd <= lmEnd);
        return res;
    }

    [Fact(DisplayName = "T-0776 PSD: jede Ebene hat Pascal-Namen ohne Füllbytes und den Unicode-Namen „luni“ (Umlaute erhalten); genau eine Ebene „Hintergrund“ ganz unten in der Hintergrundfarbe")]
    public void Psd_structure()
    {
        var d = new PaintDoc(8, 6, "#F0E0D0");
        var a = d.Add("Skizze"); var c = d.Add("Farbe rötlich"); var h = d.Add("Ö"); h.Visible = false; c.Opacity = 0.5;
        a.Px.Fill(1, 2, 3, 255);
        var psd = PaintFiles.Psd(d);
        var layers = PsdLayers(psd);
        Assert.Equal(["Hintergrund", "Skizze", "Farbe r_tlich", "_"], layers.Select(l => l.Pascal));
        Assert.Equal(["Hintergrund", "Skizze", "Farbe rötlich", "Ö"], layers.Select(l => l.Luni));
        Assert.Single(layers, l => l.Luni == "Hintergrund");
        Assert.All(layers, l => Assert.DoesNotContain('\0', l.Pascal));
        Assert.Equal(2, layers[3].Flags & 2); // „Ö“ verborgen
        Assert.Equal(128, layers[2].Opacity); // 0,5 → 128
        // Hintergrund-Ebene: ganz unten, deckend in der Hintergrundfarbe (Kanäle A, R, G, B ohne Kompression nach den Ebenendatensätzen)
        var idx = IndexOfChannelData(psd);
        var n = 8 * 6;
        Assert.Equal(0, psd[idx] << 8 | psd[idx + 1]); // Rohdaten
        Assert.All(psd.Skip(idx + 2).Take(n), x => Assert.Equal(255, x));                                   // Alpha
        Assert.All(psd.Skip(idx + 2 + n + 2).Take(n), x => Assert.Equal(0xF0, x));                          // Rot
        Assert.All(psd.Skip(idx + 2 + 2 * (n + 2)).Take(n), x => Assert.Equal(0xE0, x));                    // Grün
        Assert.All(psd.Skip(idx + 2 + 3 * (n + 2)).Take(n), x => Assert.Equal(0xD0, x));                    // Blau
        // auch bei nur einer eigenen Ebene: genau eine Hintergrund-Ebene ganz unten
        var e = new PaintDoc(4, 4, "#FFFFFF"); e.Add("Text");
        Assert.Equal(["Hintergrund", "Text"], PsdLayers(PaintFiles.Psd(e)).Select(l => l.Luni));
    }

    /// <summary>Beginn der Kanaldaten der ersten Ebene: direkt nach dem letzten Ebenendatensatz.</summary>
    static int IndexOfChannelData(byte[] psd)
    {
        int p = 26;
        uint U32() { var v = (uint)(psd[p] << 24 | psd[p + 1] << 16 | psd[p + 2] << 8 | psd[p + 3]); p += 4; return v; }
        void Skip() { var n = (int)U32(); p += n; } // nicht „p += U32()“: dort würde p vor dem Lesen ausgewertet
        Skip(); Skip(); p += 8; var count = Math.Abs((short)(psd[p] << 8 | psd[p + 1])); p += 2;
        for (var i = 0; i < count; i++) { p += 16; var ch = psd[p] << 8 | psd[p + 1]; p += 2 + ch * 6 + 12; var extra = (int)U32(); p += extra; }
        return p;
    }

    [Fact(DisplayName = "T-0776 Sicherung: Vermerk der ZIP nur in ASCII („nicht verschluesselt“); nicht gesicherte Dateinamen mit Umlaut kommen über \\u-Folgen unverändert zurück")]
    public void Backup_note_ascii()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        File.WriteAllText(Path.Combine(root, "a.txt"), "Inhalt A");
        var locked = Path.Combine(root, "Bücher-Ü.dat"); File.WriteAllText(locked, "x");
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        Backup.Result r;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) r = b.CreateChecked(new DateTime(2026, 11, 26, 12, 0, 0));
        using (var z = ZipFile.OpenRead(r.Zip))
        {
            Assert.All(z.Comment, c => Assert.True(c < 128, $"Nicht-ASCII im Vermerk: {c}"));
            Assert.Contains("Diese Sicherungen sind nicht verschluesselt. Es sind gewoehnliche ZIP-Dateien", z.Comment);
            Assert.Contains("alles darin lesen - Projekte", z.Comment);
            Assert.Contains("B\\u00fccher-\\u00dc.dat", z.Comment);
        }
        var note = b.Describe(r.Zip)!;
        Assert.Equal(["Bücher-Ü.dat"], note.NotSaved);
        Assert.Contains("nicht verschluesselt", note.Hinweis);
        Assert.Equal(Backup.Ascii(Backup.UnencryptedNotice + " " + Backup.EncryptAdvice), note.Hinweis);
        Assert.Equal("Tuer \"Schutz\" - aeoeuess", Backup.Ascii("Tür „Schutz“ – äöüß"));
    }
}
