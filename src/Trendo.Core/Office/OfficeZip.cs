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
    /// <summary>Echte Excel-Teile sind kaum 30 Ebenen tief. Der XML-Baum läuft beim Aufbau je Knoten alle Eltern ab, und sein Text wird rekursiv gelesen: Sehr tiefes XML kostet sonst quadratische Zeit und kann das ganze Programm beenden.</summary>
    public const int MaxXmlDepth = 256;
    /// <summary>Höchstzahl der Knoten (Elemente, Attribute, Texte) aller XML-Bäume einer Datei zusammen: Ein Knoten belegt ein Vielfaches seiner Bytes,
    /// stark gepackte oder mehrfach verwiesene Teile füllen sonst den Speicher oder werden endlos neu gelesen.
    /// Große echte ODS-Tabellen brauchen etwa 6 Knoten je gefüllter Zelle; 40 Mio. reichen für rund 6,6 Mio. Zellen.</summary>
    public const int MaxXmlNodes = 40_000_000;

    /// <summary>Gezählte Knoten je geöffneter Datei.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZipArchive, System.Runtime.CompilerServices.StrongBox<long>> NodeCount = new();
    internal static System.Runtime.CompilerServices.StrongBox<long> NodesOf(ZipArchive z) => NodeCount.GetValue(z, _ => new(0));

    /// <summary>Zeichen, die aus Absatztexten einer Datei zusammen entstehen dürfen: das Achtfache ihres entpackten Inhalts.
    /// Verschachtelte Absätze werden wie bisher einzeln gelesen; nur hundertfache Verschachtelung überschreitet das.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZipArchive, System.Runtime.CompilerServices.StrongBox<long>> TextLeft = new();
    static System.Runtime.CompilerServices.StrongBox<long> TextOf(ZipArchive z) => TextLeft.GetValue(z, a => new(8L * a.Entries.Sum(e => e.Length) + 1_000_000));

    /// <summary>Bucht einen gelesenen Text auf das Textbudget der Datei.</summary>
    internal static string Charge(this ZipArchive z, string text)
    {
        var left = TextOf(z);
        left.Value -= text.Length;
        if (left.Value < 0) throw Bad("verschachtelte Absätze würden den Text vervielfachen.");
        return text;
    }

    internal static InvalidDataException Bad(string why) => new($"Die Datei wurde aus Sicherheitsgründen nicht geöffnet: {why}");

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
        XDocument? doc = null;
        e.ReadXml(r => doc = XDocument.Load(new ShallowReader(r, NodesOf(e.Archive)), options), !options.HasFlag(LoadOptions.PreserveWhitespace));
        return doc!;
    }

    /// <summary>Unterelemente, auf die <paramref name="match"/> zutrifft, in Dokumentreihenfolge, aber nicht die darin nochmals verschachtelten: So wird jeder Text nur einmal gelesen.</summary>
    internal static IEnumerable<XElement> Outermost(this XElement root, Func<XElement, bool> match)
    {
        var node = root.FirstNode;
        while (node is not null)
        {
            if (node is XElement e)
            {
                if (match(e)) yield return e;
                else if (e.FirstNode is { } first) { node = first; continue; }
            }
            while (node.NextNode is null)
            {
                node = node.Parent!;
                if (ReferenceEquals(node, root)) yield break;
            }
            node = node.NextNode;
        }
    }

    internal static IEnumerable<XElement> Outermost(this XElement root, XName name) => root.Outermost(e => e.Name == name);

    /// <summary>Absatztexte wie bisher, auch verschachtelte (Textfelder) einzeln – gebucht auf das gemeinsame Textbudget der Datei.</summary>
    internal static IEnumerable<string> WithinTextBudget(this IEnumerable<string> texts, ZipArchive z)
    {
        foreach (var t in texts) yield return z.Charge(t);
    }

    /// <summary>Begrenzt einen XML-Baum auf <see cref="MaxXmlDepth"/> Ebenen unter der Startstelle und <see cref="MaxXmlNodes"/> Knoten.</summary>
    internal sealed class ShallowReader(XmlReader inner, System.Runtime.CompilerServices.StrongBox<long> nodes) : XmlReader
    {
        readonly int _max = inner.Depth + MaxXmlDepth;
        public override bool Read()
        {
            var more = inner.Read();
            if (!more) return false;
            if (inner.Depth > _max) throw Bad($"das XML ist tiefer als {MaxXmlDepth} Ebenen verschachtelt.");
            if (inner.NodeType != XmlNodeType.EndElement) // schließende Marken legen keinen Knoten an
            {
                nodes.Value += 1 + (inner.NodeType == XmlNodeType.Element ? inner.AttributeCount : 0);
                if (nodes.Value > MaxXmlNodes) throw Bad($"die Datei hat zusammen mehr als {MaxXmlNodes:N0} XML-Knoten.");
            }
            return true;
        }
        public override int AttributeCount => inner.AttributeCount;
        public override string BaseURI => inner.BaseURI;
        public override int Depth => inner.Depth;
        public override bool EOF => inner.EOF;
        public override bool IsEmptyElement => inner.IsEmptyElement;
        public override string LocalName => inner.LocalName;
        public override string NamespaceURI => inner.NamespaceURI;
        public override XmlNameTable NameTable => inner.NameTable;
        public override XmlNodeType NodeType => inner.NodeType;
        public override string Prefix => inner.Prefix;
        public override ReadState ReadState => inner.ReadState;
        public override string Value => inner.Value;
        public override string GetAttribute(int i) => inner.GetAttribute(i);
        public override string? GetAttribute(string name) => inner.GetAttribute(name);
        public override string? GetAttribute(string name, string? ns) => inner.GetAttribute(name, ns);
        public override string? LookupNamespace(string prefix) => inner.LookupNamespace(prefix);
        public override bool MoveToAttribute(string name) => inner.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => inner.MoveToAttribute(name, ns);
        public override bool MoveToElement() => inner.MoveToElement();
        public override bool MoveToFirstAttribute() => inner.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => inner.MoveToNextAttribute();
        public override bool ReadAttributeValue() => inner.ReadAttributeValue();
        public override void ResolveEntity() => inner.ResolveEntity();
    }

    /// <summary>Eintrag als Datenstrom lesen, mit denselben Schutzregeln wie <see cref="LoadXml"/> – für Tabellenblätter, die als XDocument ein Vielfaches an Speicher belegen würden.</summary>
    public static void ReadXml(this ZipArchiveEntry e, Action<XmlReader> read, bool ignoreWhitespace = true)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0, MaxCharactersInDocument = MaxEntryBytes,
            IgnoreWhitespace = ignoreWhitespace,
        };
        using var s = e.OpenLimited();
        try
        {
            using var r = XmlReader.Create(s, settings);
            read(r);
            while (r.Read()) { } // bis zum Dateiende prüfen, wie XDocument: Nachgeschobenes (zweite Wurzel, DTD) bleibt ein Fehler
        }
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
