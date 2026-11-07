using System.Security.Cryptography;
using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin vor dem Laden statisch prüfen – nur Bytes lesen, nichts ausführen.
/// Liest den PE-Kopf: Architektur, Einstiegspunkt des Formats (VST3 „GetPluginFactory“, CLAP „clap_entry“), eingebundene Systembibliotheken
/// und heikle Funktionen (Netz, Programme starten, in andere Prozesse schreiben, Dienste, Registrierung), Signatur vorhanden ja/nein, SHA-256.
/// Ergebnis ist ein Hinweis für die Freigabe – kein Urteil über Schadcode. Geladen wird nur in der abgeschotteten Laufumgebung.</summary>
public static class PluginInspect
{
    public sealed record Risk(string Code, string Text, bool Serious);
    public sealed record Result(string Path, string Binary, string Format, string Arch, string Sha256, long Bytes, bool Signed, bool EntryFound,
        IReadOnlyList<string> Dlls, IReadOnlyList<Risk> Risks, string? Problem)
    {
        public bool Loadable => Problem is null && Arch == "x64" && EntryFound;
    }

    /// <summary>Programmdatei eines Plugins: VST3-Bündel → Contents/x86_64-win/&lt;Name&gt;.vst3, sonst die Datei selbst.</summary>
    public static string? BinaryOf(string plugin)
    {
        if (File.Exists(plugin)) return plugin;
        if (!Directory.Exists(plugin)) return null;
        var dir = Path.Combine(plugin, "Contents", "x86_64-win");
        if (!Directory.Exists(dir)) return null;
        var want = Path.Combine(dir, Path.GetFileName(plugin.TrimEnd('\\', '/')));
        return File.Exists(want) ? want : Directory.EnumerateFiles(dir, "*.vst3").FirstOrDefault();
    }

    static readonly (string Dll, string Code, string Text)[] DllRisks =
    [
        ("ws2_32.dll", "netz", "kann ins Netz (Sockets)"), ("winhttp.dll", "netz", "kann ins Netz (WinHTTP)"), ("wininet.dll", "netz", "kann ins Netz (WinINet)"),
        ("urlmon.dll", "netz", "kann Dateien aus dem Netz laden"), ("dnsapi.dll", "netz", "fragt Namen im Netz ab"),
    ];

    static readonly (string Fn, string Code, string Text, bool Serious)[] FnRisks =
    [
        ("CreateProcessA", "start", "kann andere Programme starten", false), ("CreateProcessW", "start", "kann andere Programme starten", false),
        ("WinExec", "start", "kann andere Programme starten", false), ("ShellExecuteA", "start", "kann Programme/Links öffnen", false), ("ShellExecuteW", "start", "kann Programme/Links öffnen", false),
        ("ShellExecuteExW", "start", "kann Programme/Links öffnen", false),
        ("WriteProcessMemory", "fremdprozess", "kann in andere Programme schreiben", true), ("CreateRemoteThread", "fremdprozess", "kann Code in anderen Programmen starten", true),
        ("VirtualAllocEx", "fremdprozess", "kann Speicher in anderen Programmen anlegen", true),
        ("OpenSCManagerW", "dienst", "kann Windows-Dienste verwalten", true), ("CreateServiceW", "dienst", "kann Windows-Dienste anlegen", true),
        ("RegSetValueExW", "registrierung", "schreibt in die Registrierung", false), ("RegCreateKeyExW", "registrierung", "legt Registrierungsschlüssel an", false),
        ("URLDownloadToFileW", "netz", "lädt Dateien aus dem Netz", true), ("InternetOpenW", "netz", "kann ins Netz (WinINet)", false),
        // Plugin-Oberflächen (z. B. JUCE) haken sich in ihre eigenen Fenster-Ereignisse – üblich; abgeschottet (niedrige Integritätsstufe) erreicht ein Haken keine anderen Programme
        ("SetWindowsHookExW", "eingaben", "fängt Fenster-Ereignisse ab – bei Plugin-Oberflächen üblich, abgeschottet nicht für andere Programme", false), ("GetAsyncKeyState", "eingaben", "liest Tastenzustände", false),
    ];

    public static Result Inspect(string plugin)
    {
        var format = plugin.EndsWith(".clap", StringComparison.OrdinalIgnoreCase) ? "CLAP" : "VST3";
        var bin = BinaryOf(plugin);
        if (bin is null) return new(plugin, "", format, "unbekannt", "", 0, false, false, [], [], "Keine Programmdatei gefunden (VST3: Contents/x86_64-win fehlt).");
        byte[] b;
        try { b = File.ReadAllBytes(bin); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(plugin, bin, format, "unbekannt", "", 0, false, false, [], [], "Programmdatei nicht lesbar: " + e.Message); }
        var sha = Convert.ToHexStringLower(SHA256.HashData(b));
        try
        {
            var pe = new Pe(b);
            var arch = pe.Machine switch { 0x8664 => "x64", 0xAA64 => "ARM64", 0x14C => "x86 (32 Bit)", _ => "unbekannt" };
            var entry = format == "CLAP" ? "clap_entry" : "GetPluginFactory";
            var exports = pe.Exports();
            var imports = pe.Imports();
            var dlls = imports.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var risks = new List<Risk>();
            foreach (var (dll, code, text) in DllRisks)
                if (imports.Keys.Any(k => k.Equals(dll, StringComparison.OrdinalIgnoreCase))) risks.Add(new(code, $"{text} ({dll})", false));
            var fns = imports.Values.SelectMany(x => x).ToHashSet(StringComparer.Ordinal);
            foreach (var (fn, code, text, serious) in FnRisks)
                if (fns.Contains(fn) && !risks.Any(r => r.Text.StartsWith(text))) risks.Add(new(code, $"{text} ({fn})", serious));
            if (imports.Keys.Any(k => k.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase)) && fns.Contains("LoadLibraryW") && fns.Contains("GetProcAddress"))
                risks.Add(new("nachladen", "lädt weitere Programmteile erst zur Laufzeit (LoadLibrary/GetProcAddress) – deren Inhalt ist hier nicht sichtbar", false));
            string? problem = arch != "x64" ? $"Architektur {arch} – Trendo lädt nur 64-Bit-Plugins (x64)." : !exports.Contains(entry) ? $"Kein {format}-Einstiegspunkt „{entry}“ – keine gültige {format}-Programmdatei." : null;
            return new(plugin, bin, format, arch, sha, b.LongLength, pe.HasSignature, exports.Contains(entry), dlls, risks, problem);
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException or ArgumentException)
        {
            return new(plugin, bin, format, "unbekannt", sha, b.LongLength, false, false, [], [], "Keine gültige Windows-Programmdatei (PE-Kopf defekt).");
        }
    }

    public static string Report(Result r)
    {
        var sb = new StringBuilder($"# Plugin-Prüfung vor dem Laden\n\nPlugin: `{Path.GetFileName(r.Path.TrimEnd('\\', '/'))}` ({r.Format})  \nProgrammdatei: `{Path.GetFileName(r.Binary)}`, {r.Bytes:N0} Bytes  \nSHA-256: `{r.Sha256}`  \n");
        sb.Append($"Architektur: {r.Arch} · Einstiegspunkt gefunden: {(r.EntryFound ? "ja" : "nein")} · digitale Signatur vorhanden: {(r.Signed ? "ja (nicht auf Gültigkeit geprüft)" : "nein")}\n\n");
        if (r.Problem is not null) sb.Append($"**Nicht ladbar:** {r.Problem}\n\n");
        sb.Append(r.Risks.Count == 0 ? "Keine heiklen Funktionen in der Importtabelle gefunden.\n" : "## Hinweise aus der Importtabelle\n" + string.Concat(r.Risks.Select(x => $"- {(x.Serious ? "⚠ " : "")}{x.Text}\n")));
        sb.Append("\n## Eingebundene Systembibliotheken\n").Append(string.Join(", ", r.Dlls)).Append('\n');
        sb.Append("\nDiese Prüfung liest nur die Datei und ist kein Virenscan. Geladen wird das Plugin nur abgeschottet (eigener Prozess, niedrige Integritätsstufe: keine Schreibrechte auf deine Dateien, Speicher- und Zeitgrenze, keine weiteren Programme).\n");
        return sb.ToString();
    }

    /// <summary>Minimaler PE-Leser (PE32 und PE32+): Sektionen, Import- und Exporttabelle, Sicherheitsverzeichnis.</summary>
    sealed class Pe
    {
        readonly byte[] _b; readonly int _pe; readonly bool _plus; readonly int _opt; readonly List<(uint Va, uint Size, uint Raw, uint RawSize)> _sec = [];
        public ushort Machine { get; }
        public Pe(byte[] b)
        {
            _b = b;
            if (b.Length < 0x40 || b[0] != 'M' || b[1] != 'Z') throw new InvalidDataException("MZ");
            _pe = BitConverter.ToInt32(b, 0x3C);
            if (_pe <= 0 || _pe + 24 > b.Length || b[_pe] != 'P' || b[_pe + 1] != 'E') throw new InvalidDataException("PE");
            Machine = BitConverter.ToUInt16(b, _pe + 4);
            var nsec = BitConverter.ToUInt16(b, _pe + 6); var optSize = BitConverter.ToUInt16(b, _pe + 20);
            _opt = _pe + 24; _plus = BitConverter.ToUInt16(b, _opt) == 0x20B;
            var sec = _opt + optSize;
            for (var i = 0; i < nsec; i++)
            {
                var s = sec + i * 40;
                _sec.Add((BitConverter.ToUInt32(b, s + 12), BitConverter.ToUInt32(b, s + 8), BitConverter.ToUInt32(b, s + 20), BitConverter.ToUInt32(b, s + 16)));
            }
        }
        (uint Rva, uint Size) Dir(int i) { var o = _opt + (_plus ? 112 : 96) + i * 8; return o + 8 > _b.Length ? (0, 0) : (BitConverter.ToUInt32(_b, o), BitConverter.ToUInt32(_b, o + 4)); }
        int Off(uint rva)
        {
            foreach (var (va, size, raw, rawSize) in _sec)
                if (rva >= va && rva < va + Math.Max(size, rawSize)) { var o = (long)rva - va + raw; if (o < 0 || o >= _b.Length) throw new InvalidDataException("RVA"); return (int)o; }
            throw new InvalidDataException("RVA außerhalb");
        }
        string Str(uint rva) { var o = Off(rva); var e = o; while (e < _b.Length && _b[e] != 0 && e - o < 512) e++; return Encoding.ASCII.GetString(_b, o, e - o); }
        public bool HasSignature => Dir(4).Size > 0; // Sicherheitsverzeichnis (Authenticode) – Dateioffset, nicht RVA
        public HashSet<string> Exports()
        {
            var r = new HashSet<string>(StringComparer.Ordinal); var (rva, size) = Dir(0); if (rva == 0 || size == 0) return r;
            var o = Off(rva); var n = BitConverter.ToUInt32(_b, o + 24); var names = Off(BitConverter.ToUInt32(_b, o + 32));
            for (var i = 0; i < Math.Min(n, 100_000u); i++) r.Add(Str(BitConverter.ToUInt32(_b, names + (int)i * 4)));
            return r;
        }
        public Dictionary<string, List<string>> Imports()
        {
            var r = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); var (rva, size) = Dir(1); if (rva == 0 || size == 0) return r;
            var o = Off(rva);
            for (var d = 0; d < 4096; d++, o += 20)
            {
                var nameRva = BitConverter.ToUInt32(_b, o + 12); if (nameRva == 0) break;
                var dll = Str(nameRva); var list = r.TryGetValue(dll, out var l) ? l : r[dll] = [];
                var thunk = BitConverter.ToUInt32(_b, o); if (thunk == 0) thunk = BitConverter.ToUInt32(_b, o + 16);
                if (thunk == 0) continue;
                var t = Off(thunk); var w = _plus ? 8 : 4;
                for (var k = 0; k < 20_000; k++, t += w)
                {
                    var v = _plus ? BitConverter.ToUInt64(_b, t) : BitConverter.ToUInt32(_b, t);
                    if (v == 0) break;
                    var byOrdinal = _plus ? (v & 0x8000000000000000UL) != 0 : (v & 0x80000000UL) != 0;
                    if (!byOrdinal) list.Add(Str((uint)(v & 0x7FFFFFFF) + 2));
                }
            }
            return r;
        }
    }
}
