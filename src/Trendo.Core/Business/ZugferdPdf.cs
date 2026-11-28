using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Trendo.Core.Business;

/// <summary>T-0694: E-Rechnung als ZUGFeRD/Factur-X-PDF – das lesbare PDF trägt dieselbe Rechnung als eingebettete Datei „factur-x.xml“ (Profil EN 16931).
/// Aufbau nach der öffentlichen Factur-X-Beschreibung: PDF/A-3-Kennung und Factur-X-Angaben in den XMP-Metadaten, Anhang mit Beziehung „Alternative“
/// und Verweis im Katalog (/AF). PDFsharp (MIT) schreibt Schriften, Farbprofil und Anhang; die XMP-Metadaten ergänzt Trendo als angehängte Fassung
/// (inkrementelle Aktualisierung), weil PDFsharp sie beim Speichern selbst neu schreibt.
/// Ehrlich: Die PDF/A-3-Konformität ist NICHT mit einem externen Prüfer (veraPDF, Mustang, KoSIT) bestätigt.</summary>
public static partial class ZugferdPdf
{
    public const string XmlName = "factur-x.xml";

    /// <summary>ZUGFeRD-PDF aus einer festgeschriebenen Rechnung. Wirft <see cref="ArgumentException"/> mit den fehlenden Pflichtangaben.</summary>
    public static byte[] Build(RechnungsBuch b, Vorgang v, string? logo = null)
    {
        var xml = Rechnungen.ERechnungXml(b, v, xrechnung: false);
        var (first, _) = RechnungPdf.Build(b, v, logo, doc =>
        {
            doc.Info.Subject = "E-Rechnung (ZUGFeRD / Factur-X, Profil EN 16931)"; doc.Info.Creator = ""; // T-0784: kein Trendo als Ersteller; PDF/A verlangt nur, dass Info und XMP übereinstimmen
            doc.SetPdfA();
            doc.AddEmbeddedFile(XmlName, new MemoryStream(xml));
        });
        return Finish(first, $"{Rechnungen.Name(v.Art)} {v.Nummer}", b.Firma.Name);
    }

    /// <summary>Zweiter Schritt: Anhang als „Alternative“ kennzeichnen, /AF setzen, dann die XMP-Metadaten (PDF/A-3B + Factur-X) als neue Fassung anhängen.</summary>
    static byte[] Finish(byte[] pdf, string title, string author)
    {
        byte[] second; int metaNo; string created, producer, creator;
        using (var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify))
        {
            var cat = doc.Internals.Catalog;
            foreach (var o in doc.Internals.GetAllObjects().OfType<PdfDictionary>().ToList())
            {
                var type = o.Elements.GetName("/Type");
                if (type == "/Filespec")
                {
                    o.Elements.SetName("/AFRelationship", "/Alternative"); o.Elements.SetString("/Desc", "Factur-X/ZUGFeRD-Rechnung");
                    if (o.Reference is { } r) cat.Elements["/AF"] = new PdfArray(doc, r);
                }
                else if (type == "/EmbeddedFile") o.Elements.SetName("/Subtype", "/text/xml"); // PDFsharp schreibt den Schrägstrich im Namen selbst als #2F
            }
            doc.Info.Title = title; doc.Info.Author = author;
            created = doc.Info.CreationDate.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
            using var ms = new MemoryStream(); doc.Save(ms, false); second = ms.ToArray();
        }
        using (var probe = PdfReader.Open(new MemoryStream(second), PdfDocumentOpenMode.Import))
        {
            producer = probe.Info.Producer;
            creator = probe.Info.Creator ?? ""; // T-0784: xmp:CreatorTool nur, wenn das PDF selbst einen Ersteller nennt (gleicher Wert)
            metaNo = probe.Internals.Catalog.Elements.GetReference("/Metadata")?.ObjectNumber ?? throw new InvalidDataException("Das PDF hat keine Metadaten.");
        }
        return AppendObject(second, metaNo, "<</Type/Metadata/Subtype/XML/Length {0}>>", Encoding.UTF8.GetBytes(Xmp(title, author, created, producer, creator)));
    }

    [GeneratedRegex(@"startxref\s+(\d+)\s+%%EOF\s*$")] private static partial Regex StartXref();
    [GeneratedRegex(@"/Root\s+(\d+)\s+(\d+)\s+R")] private static partial Regex RootRx();
    [GeneratedRegex(@"/Info\s+(\d+)\s+(\d+)\s+R")] private static partial Regex InfoRx();
    [GeneratedRegex(@"/Size\s+(\d+)")] private static partial Regex SizeRx();
    [GeneratedRegex(@"/ID\s*\[\s*<[0-9A-Fa-f]+>\s*<[0-9A-Fa-f]+>\s*\]")] private static partial Regex IdRx();

    /// <summary>Inkrementelle Aktualisierung nach PDF 1.7 (ISO 32000-1, 7.5.6): neue Fassung eines Stream-Objekts, eigener Querverweis-Abschnitt, Trailer mit /Prev.</summary>
    static byte[] AppendObject(byte[] pdf, int objectNumber, string dictFormat, byte[] stream)
    {
        var latin = Encoding.Latin1; var tail = latin.GetString(pdf, Math.Max(0, pdf.Length - 2048), Math.Min(2048, pdf.Length));
        var sx = StartXref().Match(tail); var ti = tail.LastIndexOf("trailer", StringComparison.Ordinal);
        if (!sx.Success || ti < 0) throw new InvalidDataException("Das PDF hat keinen klassischen Querverweis – die E-Rechnung kann nicht fertiggestellt werden.");
        var trailer = tail[ti..];
        var root = RootRx().Match(trailer); var size = SizeRx().Match(trailer); var info = InfoRx().Match(trailer); var id = IdRx().Match(trailer);
        if (!root.Success || !size.Success) throw new InvalidDataException("Der Trailer des PDFs ist unvollständig.");
        using var ms = new MemoryStream();
        ms.Write(pdf); if (pdf[^1] != (byte)'\n') ms.WriteByte((byte)'\n');
        var at = ms.Position;
        void W(string s) => ms.Write(latin.GetBytes(s));
        W($"{objectNumber} 0 obj\n{string.Format(CultureInfo.InvariantCulture, dictFormat, stream.Length)}\nstream\n"); ms.Write(stream); W("\nendstream\nendobj\n");
        var xref = ms.Position;
        W($"xref\n{objectNumber} 1\n{at:0000000000} 00000 n \ntrailer\n<</Size {size.Groups[1].Value}/Root {root.Groups[1].Value} {root.Groups[2].Value} R" +
          (info.Success ? $"/Info {info.Groups[1].Value} {info.Groups[2].Value} R" : "") + (id.Success ? id.Value : "") + $"/Prev {sx.Groups[1].Value}>>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    static string Xmp(string title, string author, string created, string producer, string creator = "")
    {
        static string E(string s) => SecurityElement.Escape(s) ?? "";
        const string fx = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
        static string Prop(string name, string desc) => $"<rdf:li rdf:parseType=\"Resource\"><pdfaProperty:name>{name}</pdfaProperty:name><pdfaProperty:valueType>Text</pdfaProperty:valueType><pdfaProperty:category>external</pdfaProperty:category><pdfaProperty:description>{desc}</pdfaProperty:description></rdf:li>";
        return "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n" +
            "<rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"><pdfaid:part>3</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description>\n" +
            $"<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{E(title)}</rdf:li></rdf:Alt></dc:title><dc:creator><rdf:Seq><rdf:li>{E(author)}</rdf:li></rdf:Seq></dc:creator><dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">E-Rechnung (ZUGFeRD / Factur-X, Profil EN 16931)</rdf:li></rdf:Alt></dc:description></rdf:Description>\n" +
            $"<rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\"><pdf:Producer>{E(producer)}</pdf:Producer></rdf:Description>\n" +
            $"<rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">{(creator.Length > 0 ? $"<xmp:CreatorTool>{E(creator)}</xmp:CreatorTool>" : "")}<xmp:CreateDate>{created}</xmp:CreateDate><xmp:ModifyDate>{created}</xmp:ModifyDate></rdf:Description>\n" +
            $"<rdf:Description rdf:about=\"\" xmlns:fx=\"{fx}\"><fx:DocumentType>INVOICE</fx:DocumentType><fx:DocumentFileName>{XmlName}</fx:DocumentFileName><fx:Version>1.0</fx:Version><fx:ConformanceLevel>EN 16931</fx:ConformanceLevel></rdf:Description>\n" +
            "<rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\" xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\">" +
            $"<pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\"><pdfaSchema:schema>Factur-X PDFA Extension Schema</pdfaSchema:schema><pdfaSchema:namespaceURI>{fx}</pdfaSchema:namespaceURI><pdfaSchema:prefix>fx</pdfaSchema:prefix><pdfaSchema:property><rdf:Seq>" +
            Prop("DocumentFileName", "The name of the embedded XML document") + Prop("DocumentType", "The type of the hybrid document in capital letters, e.g. INVOICE or ORDER") +
            Prop("Version", "The actual version of the standard applying to the embedded XML document") + Prop("ConformanceLevel", "The conformance level of the embedded XML document") +
            "</rdf:Seq></pdfaSchema:property></rdf:li></rdf:Bag></pdfaExtension:schemas></rdf:Description>\n</rdf:RDF>\n</x:xmpmeta>\n<?xpacket end=\"w\"?>";
    }

    /// <summary>Eingebettete Rechnungsdaten aus einem ZUGFeRD-/Factur-X-PDF holen (auch aus fremden PDFs: factur-x.xml, zugferd-invoice.xml, xrechnung.xml). null = kein Anhang.</summary>
    public static byte[]? Extract(string pdfPath)
    {
        using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        PdfDictionary? best = null; var rank = 99;
        foreach (var spec in doc.Internals.GetAllObjects().OfType<PdfDictionary>().Where(o => o.Elements.ContainsKey("/EF")))
        {
            var name = (spec.Elements.GetString("/UF") is { Length: > 0 } u ? u : spec.Elements.GetString("/F")).ToLowerInvariant();
            var r = name switch { "factur-x.xml" => 0, "zugferd-invoice.xml" => 1, "xrechnung.xml" => 2, _ => name.EndsWith(".xml", StringComparison.Ordinal) ? 3 : 99 };
            var ef = spec.Elements.GetDictionary("/EF"); var file = ef?.Elements.GetDictionary("/F") ?? ef?.Elements.GetDictionary("/UF");
            if (file?.Stream is not null && r < rank) { best = file; rank = r; }
        }
        return best?.Stream.UnfilteredValue;
    }

    /// <summary>XMP-Metadaten der zuletzt gültigen Fassung als Text (für die Kontrolle nach dem Schreiben).</summary>
    public static string Metadata(string pdfPath)
    {
        using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        var md = doc.Internals.Catalog.Elements.GetDictionary("/Metadata");
        return md?.Stream is null ? "" : Encoding.UTF8.GetString(md.Stream.UnfilteredValue);
    }
}
