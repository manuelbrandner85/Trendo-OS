using System.IO.Compression;
using System.Text;
using Trendo.Core.Office;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0255 (AP-100-2, OFF100-SEC-01): präparierte Office-Dateien werden abgelehnt, normale öffnen weiter.</summary>
public class T0255_OfficeZip
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-zip-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string Zip(string dir, string name, params (string Entry, byte[] Data)[] entries)
    {
        var p = Path.Combine(dir, name);
        using var z = ZipFile.Open(p, ZipArchiveMode.Create);
        foreach (var (n, data) in entries) { using var s = z.CreateEntry(n, CompressionLevel.SmallestSize).Open(); s.Write(data); }
        return p;
    }

    static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    [Fact(DisplayName = "T-0255: DOCX mit DTD/externer Entität (XXE) wird abgelehnt")]
    public void Xxe_docx()
    {
        var d = Tmp();
        try
        {
            var f = Zip(d, "xxe.docx", ("word/document.xml", U("<?xml version=\"1.0\"?><!DOCTYPE w [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>&x;</w:t></w:r></w:p></w:body></w:document>")));
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => TextFiles.Open(f)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0255: XLSX mit Entitäten-Kaskade (Billion Laughs) wird abgelehnt")]
    public void Billion_laughs_xlsx()
    {
        var d = Tmp();
        try
        {
            var sb = new StringBuilder("<?xml version=\"1.0\"?><!DOCTYPE l [<!ENTITY a \"hahahahaha\">");
            for (var i = 1; i < 10; i++) sb.Append($"<!ENTITY a{i} \"{string.Concat(Enumerable.Repeat(i == 1 ? "&a;" : $"&a{i - 1};", 10))}\">");
            sb.Append("]><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheets><sheet name=\"&a9;\" sheetId=\"1\"/></sheets></workbook>");
            var f = Zip(d, "lol.xlsx", ("xl/workbook.xml", U(sb.ToString())));
            Assert.Contains("Sicherheitsgründen", Assert.Throws<InvalidDataException>(() => SheetFiles.Open(f)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0255: ZIP-Bombe (stark gepackter Eintrag) und zu viele Einträge werden vor dem Entpacken abgelehnt")]
    public void Bomb_and_entries()
    {
        var d = Tmp();
        try
        {
            var bomb = Zip(d, "bombe.pptx", ("ppt/presentation.xml", new byte[20 * 1024 * 1024]));
            Assert.Contains("gepackt", Assert.Throws<InvalidDataException>(() => SlideFiles.Open(bomb)).Message);
            var many = Zip(d, "viele.docx", Enumerable.Range(0, OfficeZip.MaxEntries + 1).Select(i => ($"x/{i}.xml", Array.Empty<byte>())).ToArray());
            Assert.Contains("zu viele Einträge", Assert.Throws<InvalidDataException>(() => TextFiles.Open(many)).Message);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0255: normale DOCX/XLSX/PPTX öffnen weiter (Rundreise)")]
    public void Normal_files_still_open()
    {
        var d = Tmp();
        try
        {
            var t = Path.Combine(d, "a.docx"); TextFiles.Save(new TextDoc { Blocks = [new Para("Hallo Erika")] }, t);
            Assert.Equal("Hallo Erika", TextFiles.Open(t).Paras.Single().Text);
            var x = Path.Combine(d, "a.xlsx"); var sd = new SheetDoc(); sd.Sheets[0].Name = "Blatt"; SheetFiles.Save(sd, x);
            Assert.Equal("Blatt", SheetFiles.Open(x).Sheets[0].Name);
            var p = Path.Combine(d, "a.pptx"); SlideFiles.Save(new SlideDoc(), p);
            Assert.NotNull(SlideFiles.Open(p));
        }
        finally { Directory.Delete(d, true); }
    }
}
