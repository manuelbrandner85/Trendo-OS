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

    static string ZipWithComment(string dir, string comment)
    {
        var zip = Path.Combine(dir, "trendo-sicherung-20261120-100000.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var s = new StreamWriter(z.CreateEntry("a.txt").Open())) s.Write("Inhalt A");
            z.Comment = comment;
        }
        return zip;
    }

    [Fact(DisplayName = "T-0776 Sicherung: ein wörtliches „\\u“ im Dateinamen bleibt im Vermerk wörtlich (wird nicht zum Zeichen) – neben einem echten Umlaut")]
    public void Backup_note_literal_backslash_u()
    {
        var names = new[] { "ordner/a\\u00fcb.dat", "Bücher\\u.dat", "x\\\\u0041.dat" };
        var comment = Backup.NoteComment(1, names.Length, names);
        Assert.All(comment, c => Assert.True(c < 128, $"Nicht-ASCII im Vermerk: {c}"));
        Assert.Contains("ordner/a\\\\u00fcb.dat", comment); // JSON: Backslash verdoppelt, „u00fc“ bleibt Text
        Assert.Contains("B\\u00fccher\\\\u.dat", comment);   // echter Umlaut als \u-Folge, wörtliches \u verdoppelt
        var dir = Tmp();
        try
        {
            var zip = ZipWithComment(dir, comment);
            var b = new Backup(dir, dir, new AuditLog(Path.Combine(dir, "audit.jsonl")));
            var note = b.Describe(zip)!;
            Assert.Equal(names, note.NotSaved);
            Assert.False(note.Complete);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0776 Sicherung: eine alte Sicherung mit Umlaut-Vermerk (UTF-8, vor T-0776) wird weiter gelesen – Hinweis und nicht gesicherte Namen mit Umlauten bleiben, die Sicherung zählt")]
    public void Backup_old_umlaut_note_still_read()
    {
        var dir = Tmp();
        try
        {
            // so schrieb Trendo bis T-0776 den Vermerk: Umlaute direkt (UTF-8), nicht als \u-Folgen
            var old = "trendo-sicherung-v1 {\"Saved\":1,\"NotSavedCount\":1,\"NotSaved\":[\"Bücher-Ü.dat\"],\"Hinweis\":\"" + Backup.UnencryptedNotice + " " + Backup.EncryptAdvice + "\"}";
            var zip = ZipWithComment(dir, old);
            var b = new Backup(dir, dir, new AuditLog(Path.Combine(dir, "audit.jsonl")));
            var note = b.Describe(zip)!;
            Assert.NotNull(note);
            Assert.Equal(["Bücher-Ü.dat"], note.NotSaved);
            Assert.Equal(Backup.UnencryptedNotice + " " + Backup.EncryptAdvice, note.Hinweis);
            Assert.Contains("nicht verschlüsselt", note.Hinweis);
            Assert.True(b.Verify(zip).Ok);
            Assert.Equal(zip, b.Latest()?.FullName);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0776 LibreOffice-Kopie: alte eigene Kopien weg, fremde bleiben; Start nur aus der Kopie (Prozesspfade, Kindprozess außerhalb erkannt); fremder Zugriff aufs Original = „nicht prüfbar“, Änderung ohne fremden Zugriff = Hinweis; Aufräumen löscht die Kopie")]
    public void LoKopie_watch()
    {
        if (!OperatingSystem.IsWindows()) { Ueberspringen.Weil("nur unter Windows (Prozessliste über Toolhelp)."); return; }
        var root = Environment.GetEnvironmentVariable("T0776_WORK") is { Length: > 0 } w ? w : Path.Combine(TempDir.SystemTemp, "trendo-t0776-work");
        Directory.CreateDirectory(root);
        var alt = Path.Combine(root, "lo-kopie-alt" + Guid.NewGuid().ToString("N")[..4]); Directory.CreateDirectory(alt); File.WriteAllText(Path.Combine(alt, T0776_LoKopie.Marker), "alt");
        Directory.SetCreationTimeUtc(alt, DateTime.UtcNow.AddDays(-1));
        var fremd = Path.Combine(root, "lo-kopie-fremd" + Guid.NewGuid().ToString("N")[..4]); Directory.CreateDirectory(fremd); // ohne Kennung: nicht von diesem Test
        Directory.SetCreationTimeUtc(fremd, DateTime.UtcNow.AddDays(-1));
        var dir = Tmp();
        try
        {
            // Ersatz-„Installation“: cmd.exe als program\soffice.exe – nur, um Start, Kindprozesse und fremden Zugriff zu beobachten
            var inst = Path.Combine(dir, "libreoffice"); Directory.CreateDirectory(Path.Combine(inst, "program"));
            var fake = Path.Combine(inst, "program", "soffice.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), fake);
            var fx = new T0776_LoKopie();
            Assert.False(Directory.Exists(alt)); Assert.True(Directory.Exists(fremd));
            Assert.Contains("alte eigene Kopie entfernt", fx.CleanupNote);
            var exe = fx.Exe(fake);
            Assert.StartsWith(fx.CopyDir!, exe, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(fx.CopyDir!)!, T0776_LoKopie.Marker)));
            Assert.Same(exe, fx.Exe(fake)); // nur einmal kopiert
            Assert.Throws<InvalidOperationException>(() => fx.Run(fake, ["/c", "exit", "0"])); // nie aus dem Original
            // eigener Start aus der Kopie: der Kindprozess ping.exe liegt außerhalb der Kopie und wird gemeldet
            fx.BeginCase();
            var r = fx.Run(exe, ["/c", "ping -n 3 127.0.0.1 >nul"]);
            Assert.Contains(r.Own, x => x.Contains("soffice.exe", StringComparison.OrdinalIgnoreCase) && x.Contains(fx.CopyDir!, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(r.OwnOutside, x => x.Contains("ping.exe", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(r.Foreign);
            Assert.Equal("unverändert", fx.EndCase().Status);
            // fremder Zugriff: ein soffice aus dem Original läuft während des Falls
            fx.BeginCase();
            using (var foreign = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fake, "/c ping -n 8 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!)
            {
                try
                {
                    var r2 = fx.Run(exe, ["/c", "ping -n 2 127.0.0.1 >nul"]);
                    Assert.Contains(r2.Foreign, x => x.Contains($"PID {foreign.Id} "));
                    var (st, rep) = fx.EndCase();
                    Assert.Equal("nicht prüfbar (fremder Zugriff)", st); Assert.Contains("fremder Zugriff", rep);
                }
                finally { foreign.Kill(true); foreign.WaitForExit(10000); }
            }
            // Änderung am Original ohne fremden Prozess: deutlicher Hinweis, kein Fehler
            fx.BeginCase(); File.WriteAllText(Path.Combine(inst, "program", "neu.txt"), "x");
            var (st3, rep3) = fx.EndCase();
            Assert.Equal("geändert", st3); Assert.StartsWith("HINWEIS", rep3); Assert.Contains("neu.txt", rep3);
            var copyRoot = Path.GetDirectoryName(fx.CopyDir!)!;
            fx.Dispose();
            Assert.False(Directory.Exists(copyRoot));
        }
        finally { Directory.Delete(dir, true); try { Directory.Delete(fremd, true); } catch (IOException) { } }
    }
}
