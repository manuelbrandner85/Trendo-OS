using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0255 (AP-100-2): Schutz beim Öffnen von Office-Dateien (DOCX, ODT, XLSX, ODS, PPTX, ODP).
/// Eine präparierte Datei („ZIP-Bombe“, XML mit DTD/Entitäten) darf Trendo weder lahmlegen noch Dateien von außen nachladen:
/// begrenzte Eintragszahl und entpackte Größe, Prüfung des Packverhältnisses, gelesen wird nie mehr als angegeben, DTDs sind verboten.</summary>
public static class OfficeZip
{
    public const int MaxEntries = 10_000;
    public const long MaxEntryBytes = 256L * 1024 * 1024;
    public const long MaxTotalBytes = 512L * 1024 * 1024;
    /// <summary>Normale Office-XML packt sich etwa 5- bis 50-fach; ab 200-fach und mehr als 10 MB ist es fast sicher eine Bombe.</summary>
    public const int MaxRatio = 200;
    const long RatioMinBytes = 10L * 1024 * 1024;

    static InvalidDataException Bad(string why) => new($"Die Datei wurde aus Sicherheitsgründen nicht geöffnet: {why}");

    /// <summary>ZIP öffnen und vor dem Lesen die Grenzen prüfen.</summary>
    public static ZipArchive OpenRead(string path)
    {
        var z = ZipFile.OpenRead(path);
        try { Check(z); return z; }
        catch { z.Dispose(); throw; }
    }

    public static void Check(ZipArchive z)
    {
        if (z.Entries.Count > MaxEntries) throw Bad($"zu viele Einträge ({z.Entries.Count}, erlaubt {MaxEntries}).");
        long total = 0;
        foreach (var e in z.Entries)
        {
            if (e.Length > MaxEntryBytes) throw Bad($"„{e.FullName}“ wäre entpackt {e.Length / 1048576} MB groß.");
            if (e.Length > RatioMinBytes && e.CompressedLength > 0 && e.Length / e.CompressedLength > MaxRatio) throw Bad($"„{e.FullName}“ ist ungewöhnlich stark gepackt (ZIP-Bombe?).");
            total += e.Length;
            if (total > MaxTotalBytes) throw Bad($"entpackt zusammen über {MaxTotalBytes / 1048576} MB.");
        }
    }

    /// <summary>Eintrag lesen – höchstens so viel, wie im Verzeichnis angegeben (falsche Angaben fallen auf).</summary>
    public static Stream OpenLimited(this ZipArchiveEntry e) => new Limited(e.Open(), Math.Min(e.Length, MaxEntryBytes), e.FullName);

    public static byte[] ReadBytes(this ZipArchiveEntry e)
    {
        using var s = e.OpenLimited(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray();
    }

    /// <summary>XML eines Eintrags laden: ohne DTD, ohne Nachladen externer Dateien, mit Größengrenze.</summary>
    public static XDocument LoadXml(this ZipArchiveEntry e, LoadOptions options = LoadOptions.None)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0, MaxCharactersInDocument = MaxEntryBytes,
            IgnoreWhitespace = !options.HasFlag(LoadOptions.PreserveWhitespace),
        };
        using var s = e.OpenLimited();
        try { using var r = XmlReader.Create(s, settings); return XDocument.Load(r, options); }
        catch (XmlException x) when (x.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)) { throw Bad($"„{e.FullName}“ enthält eine DTD (nicht erlaubt)."); }
    }

    sealed class Limited(Stream inner, long max, string name) : Stream
    {
        long _read;
        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            _read += n;
            if (_read > max) throw Bad($"„{name}“ ist entpackt größer als angegeben.");
            return n;
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
