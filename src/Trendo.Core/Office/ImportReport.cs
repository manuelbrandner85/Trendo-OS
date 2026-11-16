using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0682: Ein Punkt, den Trendo beim Öffnen einer fremden Datei nicht übernimmt.
/// <paramref name="Id"/> ist stabil (für Tests und Protokoll), <paramref name="Was"/> der Text für den Nutzer,
/// <paramref name="Anzahl"/> wie oft es vorkommt, <paramref name="Wo"/> die Fundstelle, soweit bekannt.</summary>
public sealed record ImportLoss(string Id, string Was, int Anzahl, string Wo = "");

/// <summary>T-0682: Importbericht – was eine geöffnete Datei enthält, das Trendos Dokumentmodell nicht hält.
/// Der Bericht nennt nur, was die Prüfung in der Datei wirklich gefunden hat. Er ist eine Untergrenze, kein Beweis der Vollständigkeit.</summary>
public sealed class ImportReport(string file, IReadOnlyList<ImportLoss> losses)
{
    public static readonly ImportReport None = new("", []);
    public string Datei { get; } = file;
    public IReadOnlyList<ImportLoss> Verluste { get; } = losses;
    public bool HasLoss => Verluste.Count > 0;
    public bool Has(string id) => Verluste.Any(v => v.Id == id);

    /// <summary>Kurzfassung für die Hinweisleiste: die ersten Punkte mit Anzahl.</summary>
    public string Kurz(int max = 4)
    {
        var first = string.Join(", ", Verluste.Take(max).Select(v => v.Anzahl > 1 ? $"{v.Was} ({v.Anzahl}×)" : v.Was));
        return Verluste.Count > max ? $"{first} und {Verluste.Count - max} weitere Punkte" : first;
    }

    /// <summary>Alle Punkte, je Zeile einer, mit Fundstelle.</summary>
    public string Lang() => string.Join("\n", Verluste.Select(v => $"• {v.Was}: {v.Anzahl}×{(v.Wo.Length > 0 ? " – " + v.Wo : "")}"));
}

/// <summary>T-0682: Prüft eine Office-Datei darauf, was die Leser von Trendo Text, Tabelle und Präsentation nicht übernehmen.
/// Die Prüfung folgt den Lesern: Sie zählt die Bausteine, die der jeweilige Leser überspringt oder vereinfacht, und Paketteile, die er nicht kennt.
/// Dateien aus Trendos eigenen Schreibern ergeben einen leeren Bericht (Test „eigene Dateien bleiben ohne Hinweis“).</summary>
public static partial class ImportCheck
{
    /// <summary>Bericht zur Datei; für Formate ohne Paket (TXT, CSV) leer. Wirft nicht: Lässt sich die Datei nicht prüfen, steht genau das im Bericht.</summary>
    public static ImportReport For(string path)
    {
        var l = new Losses();
        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".docx": using (var z = OfficeZip.OpenRead(path)) Docx(z, l); break;
                case ".odt": using (var z = OfficeZip.OpenRead(path)) Odt(z, l); break;
                case ".xlsx": using (var z = OfficeZip.OpenRead(path)) Xlsx(z, l); break;
                case ".ods": using (var z = OfficeZip.OpenRead(path)) Ods(z, l); break;
                case ".pptx": using (var z = OfficeZip.OpenRead(path)) Pptx(z, l); break;
                case ".odp": using (var z = OfficeZip.OpenRead(path)) Odp(z, l); break;
                default: return ImportReport.None;
            }
        }
        catch (Exception e) when (e is XmlException or InvalidDataException or IOException or FormatException or ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException)
        {
            l.Add("pruefung", "Die Datei ließ sich nicht vollständig prüfen – es kann mehr fehlen, als hier steht");
        }
        return new ImportReport(Path.GetFileName(path), l.ToList());
    }

    // ---------- gemeinsame Helfer ----------
    sealed class Losses
    {
        readonly List<(string Id, string Was)> _order = [];
        readonly Dictionary<string, (int N, List<string> Wo)> _items = [];

        public void Add(string id, string was, int count = 1, string? wo = null)
        {
            if (count <= 0) return;
            if (!_items.TryGetValue(id, out var it)) { it = (0, []); _order.Add((id, was)); }
            it.N += count;
            if (!string.IsNullOrWhiteSpace(wo) && it.Wo.Count < 4 && !it.Wo.Contains(wo)) it.Wo.Add(wo);
            _items[id] = it;
        }

        public bool Has(string id) => _items.ContainsKey(id);
        public List<ImportLoss> ToList() => _order.Select(o => new ImportLoss(o.Id, o.Was, _items[o.Id].N, string.Join("; ", _items[o.Id].Wo))).ToList();
    }

    static XDocument? X(ZipArchive z, string name) => z.GetEntry(name)?.LoadXml();

    static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    static string DirOf(string file) { var i = file.LastIndexOf('/'); return i < 0 ? "" : file[..i]; }

    static string Resolve(string baseFile, string target)
    {
        if (target.StartsWith('/')) return target[1..];
        var parts = DirOf(baseFile).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var seg in target.Split('/')) { if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); } else if (seg != ".") parts.Add(seg); }
        return string.Join('/', parts);
    }

    /// <summary>Beziehungen eines Teils: Id → (Art, aufgelöstes Ziel, extern).</summary>
    static Dictionary<string, (string Type, string Target, bool External)> RelsOf(ZipArchive z, string part)
    {
        var map = new Dictionary<string, (string, string, bool)>();
        var d = X(z, $"{DirOf(part)}/_rels/{part[(part.LastIndexOf('/') + 1)..]}.rels".TrimStart('/'));
        foreach (var r in d?.Root?.Elements(PkgRel + "Relationship") ?? [])
        {
            var ext = (string?)r.Attribute("TargetMode") == "External"; var t = (string?)r.Attribute("Target") ?? "";
            map.TryAdd((string?)r.Attribute("Id") ?? "", ((string?)r.Attribute("Type") ?? "", ext ? t : Resolve(part, t), ext));
        }
        return map;
    }

    static string Short(string text, int max = 40)
    {
        var t = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    static bool Truthy(string? v) => v is not (null or "0" or "false" or "none" or "off");

    /// <summary>Paketteile, die kein Leser kennt: nach Vorsilbe benannt (z. B. Makros), der Rest gesammelt unter „Weitere Bestandteile“.</summary>
    static void Parts(ZipArchive z, Losses l, string id, Func<string, bool> known, params (string Prefix, string Id, string Was)[] named)
    {
        foreach (var e in z.Entries)
        {
            var n = e.FullName;
            if (n.EndsWith('/') || n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) || n == "[Content_Types].xml") continue;
            var hit = named.FirstOrDefault(p => n.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase));
            if (hit.Prefix is not null) { if (hit.Id.Length > 0) l.Add(hit.Id, hit.Was, 1, n[(n.LastIndexOf('/') + 1)..]); continue; }
            if (!known(n)) l.Add(id + ".teile", "Weitere Bestandteile der Datei, die Trendo nicht kennt", 1, n);
        }
    }
}
