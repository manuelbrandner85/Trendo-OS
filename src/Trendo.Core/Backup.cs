using System.IO.Compression;

namespace Trendo.Core;

/// <summary>Sicherung der Benutzerdaten (Projekte, Einstellungen, Audit) als ZIP; Wiederherstellung legt vorher eine Sicherheitskopie an.
/// T-0754 (CORE-11): Die ZIP-Sicherungen sind NICHT verschlüsselt (Entscheidung des Nutzers 23.11.2026: keine Verschlüsselung, deutlicher Hinweis).
/// Das steht in der Oberfläche, im Ergebnis jeder Sicherung (<see cref="Result.Say"/>) und im Vermerk (Kommentar) jeder ZIP-Datei.</summary>
public sealed class Backup(string dataRoot, string backupDir, AuditLog audit)
{
    public string Folder => backupDir;

    /// <summary>Ehrlicher Hinweis: unverschlüsselt, für jeden mit Zugriff auf den Ordner lesbar; Empfehlung verschlüsseltes Laufwerk (nur Text, Trendo ändert nichts am System).</summary>
    public const string UnencryptedNotice = "Diese Sicherungen sind nicht verschlüsselt. Es sind gewöhnliche ZIP-Dateien: Jeder, der Zugriff auf den Sicherungsordner hat, kann sie öffnen und alles darin lesen – Projekte, Einstellungen und das Protokoll.";
    /// <summary>Empfehlung zum Hinweis (nur Text).</summary>
    public const string EncryptAdvice = "Empfehlung: Lege den Sicherungsordner auf ein verschlüsseltes Laufwerk, zum Beispiel ein Laufwerk mit BitLocker (Windows) oder einen verschlüsselten USB-Stick. Trendo ändert dafür nichts an deinem System. Verschlüsselt mit Passwort sichern kannst du unter „Schutz“ mit restic.";
    // T-0660: Anmeldesitzung bei Mixamo nicht mitsichern; T-0766 (H1): Zwischenspeicher übersetzter Erweiterungen weder sichern noch wiederherstellen
    private static readonly string[] Skip = ["backups", "settings.json.tmp", "web-mixamo", ExtensionStore.LegacyCacheName];

    public IReadOnlyList<FileInfo> List() => Directory.Exists(backupDir)
        ? new DirectoryInfo(backupDir).GetFiles("trendo-sicherung-*.zip").OrderByDescending(f => f.Name).ToList() : [];

    /// <summary>Die neueste Sicherung, die sich öffnen lässt. Eine beschädigte Datei (z. B. Rest aus einer älteren Version) zählt nicht als „gesichert“.</summary>
    public FileInfo? Latest() => List().FirstOrDefault(f => Readable(f.FullName));

    public bool Due(DateTime now, int everyHours) => everyHours > 0 && (Latest() is not { } last || (now - last.LastWriteTime).TotalHours >= everyHours);

    /// <summary>Ergebnis einer Sicherung: was gesichert ist und was nicht (gesperrt, nicht lesbar). Mit Lücken gilt sie als unvollständig.</summary>
    public sealed record Result(string Zip, int Saved, IReadOnlyList<string> NotSaved)
    {
        public bool Complete => NotSaved.Count == 0;
        /// <summary>Ein Satz für die Anzeige.</summary>
        public string Say() => Complete ? $"Gesichert: {Path.GetFileName(Zip)} ({Saved} Dateien, geprüft, nicht verschlüsselt)."
            : $"Unvollständig gesichert: {Path.GetFileName(Zip)} – {Saved} Dateien gesichert, {NotSaved.Count} nicht (gerade von einem anderen Programm belegt oder nicht lesbar): {string.Join(", ", NotSaved.Take(5))}{(NotSaved.Count > 5 ? " …" : "")}. Schließe die Programme und sichere noch einmal.";
    }

    /// <summary>Was in einer Sicherung über sie selbst steht (ab T-0681). Ältere Sicherungen haben keinen Vermerk: null.</summary>
    public sealed record Note(int Saved, int NotSavedCount, IReadOnlyList<string> NotSaved, string? Hinweis = null) { public bool Complete => NotSavedCount == 0; }

    /// <summary>Prüfergebnis einer Sicherungsdatei: lässt sie sich vollständig lesen, stimmen die Prüfsummen?</summary>
    public sealed record Check(bool Ok, int Files, string Problem);

    const string NoteHead = "trendo-sicherung-v1 ";
    const string Part = ".teil";

    public string Create(DateTime now, int keep = 10) => CreateChecked(now, keep).Zip;

    /// <summary>Sichert in eine vorläufige Datei, liest sie zur Prüfung zurück (Anzahl, Größe, Prüfsummen) und legt sie erst dann unter dem endgültigen Namen ab.
    /// Eine einzelne gesperrte oder nicht lesbare Datei bricht nichts ab – sie steht im Ergebnis unter „nicht gesichert“.</summary>
    public Result CreateChecked(DateTime now, int keep = 10)
    {
        Directory.CreateDirectory(backupDir);
        CleanLeftovers();
        var tmp = Path.Combine(backupDir, $"sicherung-{Guid.NewGuid():N}{Part}");
        var notSaved = new List<string>(); var sums = new Dictionary<string, (long Bytes, string Sha)>(StringComparer.Ordinal);
        try
        {
            // Bricht das Lesen mitten in einer Datei ab, steht ein halber Eintrag in der ZIP – dann wird ohne diese Datei neu geschrieben.
            for (var round = 0; ; round++)
            {
                sums.Clear();
                if (WriteZip(tmp, notSaved, sums) is not { } broken) break;
                notSaved.Add(broken);
                if (round >= 50) throw new IOException("Sichern hat nicht geklappt: zu viele Dateien ändern sich gerade oder sind nicht lesbar.");
            }
            VerifyWritten(tmp, sums); // Prüflesen: erst danach gilt die Sicherung
            var zip = Path.Combine(backupDir, $"trendo-sicherung-{now:yyyyMMdd-HHmmss}.zip");
            for (var n = 2; File.Exists(zip); n++) zip = Path.Combine(backupDir, $"trendo-sicherung-{now:yyyyMMdd-HHmmss}-{n}.zip"); // nie eine vorhandene Sicherung überschreiben
            File.Move(tmp, zip);
            foreach (var old in List().Skip(keep)) try { old.Delete(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            notSaved.Sort(StringComparer.OrdinalIgnoreCase);
            audit.Append("app", notSaved.Count == 0 ? "backup.created" : "backup.incomplete", notSaved.Count == 0 ? Path.GetFileName(zip) : $"{Path.GetFileName(zip)} nicht gesichert={notSaved.Count}");
            return new(zip, sums.Count, notSaved);
        }
        finally { try { File.Delete(tmp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }

    /// <summary>Schreibt die ZIP. Rückgabe: Datei, deren Lesen mittendrin abbrach (dann neu schreiben), sonst null.</summary>
    string? WriteZip(string tmp, List<string> notSaved, Dictionary<string, (long Bytes, string Sha)> sums)
    {
        var skipNow = new HashSet<string>(notSaved, StringComparer.Ordinal);
        using var z = new ZipArchive(new FileStream(tmp, FileMode.Create, FileAccess.ReadWrite, FileShare.None), ZipArchiveMode.Create);
        var buf = new byte[81920]; var full = Path.GetFullPath(backupDir);
        foreach (var f in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dataRoot, f);
            if (Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) || Path.GetFullPath(f).StartsWith(full)) continue;
            var name = rel.Replace('\\', '/');
            if (skipNow.Contains(name)) continue;
            FileStream src;
            try { src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
            catch (FileNotFoundException) { continue; } // zwischen Auflisten und Lesen gelöscht: gehört nicht mehr zum Stand
            catch (DirectoryNotFoundException) { continue; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { notSaved.Add(name); continue; }
            using (src)
            {
                using var dst = z.CreateEntry(name, CompressionLevel.Optimal).Open();
                using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                long bytes = 0;
                try { int n; while ((n = src.Read(buf, 0, buf.Length)) > 0) { dst.Write(buf, 0, n); sha.AppendData(buf, 0, n); bytes += n; } }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return name; } // z. B. ein gesperrter Bereich mitten in der Datei
                sums[name] = (bytes, Convert.ToHexString(sha.GetHashAndReset()));
            }
        }
        var list = notSaved.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(150).ToList();
        z.Comment = NoteHead + System.Text.Json.JsonSerializer.Serialize(new NoteData(sums.Count, notSaved.Count, list, UnencryptedNotice + " " + EncryptAdvice), NoteJson); // T-0754: Hinweis lesbar (Umlaute nicht als \u-Folgen), z. B. im Kommentar-Feld von 7-Zip
        return null;
    }

    sealed record NoteData(int Saved, int NotSavedCount, List<string> NotSaved, string? Hinweis = null);
    static readonly System.Text.Json.JsonSerializerOptions NoteJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Prüflesen direkt nach dem Schreiben: jede gesicherte Datei ist in der ZIP, mit derselben Größe und derselben SHA-256-Prüfsumme wie beim Lesen der Quelle.</summary>
    static void VerifyWritten(string tmp, Dictionary<string, (long Bytes, string Sha)> sums)
    {
        using var z = ZipFile.OpenRead(tmp);
        if (z.Entries.Count != sums.Count) throw new IOException($"Die Sicherung hat die Prüfung nicht bestanden: {z.Entries.Count} statt {sums.Count} Dateien. Es wurde nichts abgelegt.");
        var buf = new byte[81920];
        foreach (var e in z.Entries)
        {
            if (!sums.TryGetValue(e.FullName, out var want)) throw new IOException($"Die Sicherung hat die Prüfung nicht bestanden: unerwartete Datei „{e.FullName}“. Es wurde nichts abgelegt.");
            using var s = e.Open(); using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            long bytes = 0; int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) { sha.AppendData(buf, 0, n); bytes += n; }
            if (bytes != want.Bytes || Convert.ToHexString(sha.GetHashAndReset()) != want.Sha) throw new IOException($"Die Sicherung hat die Prüfung nicht bestanden: „{e.FullName}“ ist in der Sicherung anders als im Original. Es wurde nichts abgelegt.");
        }
    }

    /// <summary>Reste abgebrochener Sicherungen (vorläufige Dateien) entfernen. Was gerade ein zweites Trendo-Fenster schreibt, ist gesperrt und bleibt.</summary>
    void CleanLeftovers()
    {
        foreach (var f in new DirectoryInfo(backupDir).GetFiles("sicherung-*" + Part))
            try { f.Delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Lässt sich die Datei als ZIP öffnen (Inhaltsverzeichnis vorhanden)? Schnell, liest keine Inhalte.</summary>
    public static bool Readable(string zip)
    {
        try { using var z = ZipFile.OpenRead(zip); return true; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Vermerk der Sicherung über sich selbst (gesichert / nicht gesichert). null: älter als T-0681 oder nicht lesbar.</summary>
    public Note? Describe(string zip)
    {
        try
        {
            using var z = ZipFile.OpenRead(zip);
            if (!z.Comment.StartsWith(NoteHead, StringComparison.Ordinal)) return null;
            var d = System.Text.Json.JsonSerializer.Deserialize<NoteData>(z.Comment[NoteHead.Length..]);
            return d is null ? null : new(d.Saved, d.NotSavedCount, d.NotSaved ?? [], d.Hinweis);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return null; }
    }

    /// <summary>Liest die ganze Sicherung zur Prüfung: jede Datei muss sich entpacken lassen, Größe und CRC-32-Prüfsumme müssen stimmen. Ändert nichts.</summary>
    public Check Verify(string zip)
    {
        if (!File.Exists(zip)) return new(false, 0, "Die Sicherung gibt es nicht mehr.");
        try
        {
            using var z = ZipFile.OpenRead(zip);
            var buf = new byte[81920]; var files = 0;
            foreach (var e in z.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;
                using var s = e.Open(); uint crc = 0xFFFFFFFF; long bytes = 0; int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0) { bytes += n; for (var i = 0; i < n; i++) crc = Crc[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8); }
                if (bytes != e.Length || (crc ^ 0xFFFFFFFF) != e.Crc32) return new(false, files, $"„{e.FullName}“ ist in der Sicherung beschädigt.");
                files++;
            }
            return new(true, files, "");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return new(false, 0, "Die Sicherungsdatei ist unvollständig oder beschädigt."); }
    }

    static readonly uint[] Crc = MakeCrc();
    static uint[] MakeCrc()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { var c = i; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[i] = c; }
        return t;
    }

    public sealed record RestorePlan(int Replaced, int Added, IReadOnlyList<string> Examples);

    /// <summary>T-0766 (Befund N14): relativer Zielpfad, normalisiert („x/../a“ → „a“, „./a“ → „a“, „\“ → „/“).</summary>
    string RelOf(string dest) => Path.GetRelativePath(Path.GetFullPath(dataRoot), dest).Replace('\\', '/');
    /// <summary>Audit und Skip-Bereiche werden nie wiederhergestellt – geprüft am normalisierten Zielpfad, ohne Groß-/Kleinschreibung.</summary>
    bool Untouchable(string dest)
    {
        var rel = RelOf(dest);
        return rel.StartsWith("audit/", StringComparison.OrdinalIgnoreCase) || Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Was ein Wiederherstellen ändern würde: ersetzte und neue Dateien (ohne Audit, ohne Zip-Slip), nichts wird geschrieben.</summary>
    public RestorePlan Preview(string zip)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException(zip);
        using var z = ZipFile.OpenRead(zip);
        int replaced = 0, added = 0; var examples = new List<string>();
        foreach (var e in z.Entries)
        {
            if (e.FullName.EndsWith('/')) continue;
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar) || Untouchable(dest)) continue;
            if (File.Exists(dest)) { replaced++; if (examples.Count < 5) examples.Add(e.FullName); }
            else added++;
        }
        return new(replaced, added, examples);
    }

    /// <summary>Stellt eine Sicherung wieder her. Vorher wird sie geprüft (beschädigt: InvalidDataException, nichts geändert) und der aktuelle Stand als eigene Sicherung gespeichert.</summary>
    public int Restore(string zip, DateTime now)
    {
        if (!File.Exists(zip)) throw new FileNotFoundException(zip);
        // T-0681: erst prüfen, dann anfassen – eine beschädigte Sicherung wird übersprungen, am Rechner ändert sich nichts
        if (Verify(zip) is { Ok: false } bad)
        {
            audit.Append("owner", "backup.restore.skipped", $"{Path.GetFileName(zip)}: {bad.Problem}");
            throw new InvalidDataException($"Diese Sicherung ist beschädigt und wurde übersprungen – es wurde nichts geändert. {bad.Problem} Nimm eine andere Sicherung aus der Liste.");
        }
        var before = Create(now.AddSeconds(-1), keep: 20);
        using var z = ZipFile.OpenRead(zip);
        var count = 0;
        foreach (var e in z.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(dataRoot, e.FullName));
            if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) continue; // kein Zip-Slip
            if (Untouchable(dest)) continue; // Audit-Kette wird nie überschrieben; T-0766 (H1, N14): Skip-Bereiche nach dem echten Zielpfad
            if (e.FullName.EndsWith('/')) { Directory.CreateDirectory(dest); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (File.Exists(dest)) { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); } // z. B. schreibgeschützte .git-Objekte
            e.ExtractToFile(dest, true);
            count++;
        }
        audit.Append("owner", "backup.restored", $"{Path.GetFileName(zip)} (vorher gesichert: {Path.GetFileName(before)})");
        return count;
    }

    // ---------- Zeitleiste: Inhalt, Vergleich, Einzeldatei (Masterprompt 131) ----------
    public sealed record Entry(string Path, long Bytes, DateTimeOffset Changed);
    public enum Change { Gleich, Geaendert, Geloescht, Neu }
    public sealed record Diff(string Path, Change Kind, long Bytes);

    /// <summary>Dateien einer Sicherung (ohne Protokoll), alphabetisch.</summary>
    public IReadOnlyList<Entry> Entries(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        return z.Entries.Where(e => !e.FullName.EndsWith('/') && !e.FullName.StartsWith("audit/")).Select(e => new Entry(e.FullName, e.Length, e.LastWriteTime)).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Vergleich Sicherung ↔ heute: geändert (Inhalt anders), seither gelöscht, seither neu (ohne Protokoll und Sicherungen).</summary>
    public IReadOnlyList<Diff> CompareWithNow(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        var res = new List<Diff>(); var inZip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in z.Entries.Where(e => !e.FullName.EndsWith('/') && !e.FullName.StartsWith("audit/")))
        {
            inZip.Add(e.FullName);
            var cur = Path.Combine(dataRoot, e.FullName);
            if (!File.Exists(cur)) { res.Add(new(e.FullName, Change.Geloescht, e.Length)); continue; }
            res.Add(new(e.FullName, SameContent(e, cur) ? Change.Gleich : Change.Geaendert, e.Length));
        }
        foreach (var f in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dataRoot, f).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.StartsWith("audit/") || Skip.Any(s => rel.StartsWith(s, StringComparison.OrdinalIgnoreCase)) || Path.GetFullPath(f).StartsWith(Path.GetFullPath(backupDir))) continue;
            if (!inZip.Contains(rel)) res.Add(new(rel, Change.Neu, new FileInfo(f).Length));
        }
        return res.OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static bool SameContent(ZipArchiveEntry e, string file)
    {
        var fi = new FileInfo(file); if (fi.Length != e.Length) return false;
        using var a = e.Open(); using var b = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var ba = new byte[81920]; var bb = new byte[81920];
        while (true)
        {
            var na = a.ReadAtLeast(ba, ba.Length, false); var nb = b.ReadAtLeast(bb, bb.Length, false);
            if (na != nb || !ba.AsSpan(0, na).SequenceEqual(bb.AsSpan(0, nb))) return false;
            if (na == 0) return true;
        }
    }

    /// <summary>Inhalt einer Datei aus der Sicherung (für die Vorschau), höchstens <paramref name="max"/> Bytes.</summary>
    public byte[] Read(string zip, string path, int max = 2_000_000)
    {
        using var z = ZipFile.OpenRead(zip);
        var e = z.GetEntry(path) ?? throw new FileNotFoundException("Nicht in der Sicherung: " + path);
        using var s = e.Open(); using var ms = new MemoryStream(); var buf = new byte[81920]; int n;
        while ((n = s.Read(buf)) > 0 && ms.Length < max) ms.Write(buf, 0, n);
        return ms.ToArray();
    }

    /// <summary>Eine einzelne Datei zurückholen. Die heutige Fassung wird vorher unter backups/einzeln/&lt;Zeit&gt;/ aufgehoben. Gibt den Ablageort der alten Fassung zurück (oder null).</summary>
    public string? RestoreOne(string zip, string path, DateTime now)
    {
        var dest = Path.GetFullPath(Path.Combine(dataRoot, path));
        if (!dest.StartsWith(Path.GetFullPath(dataRoot) + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Ungültiger Pfad.");
        if (RelOf(dest).StartsWith("audit/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Das Protokoll wird nie überschrieben.");
        if (Untouchable(dest)) throw new InvalidOperationException("Dieser Bereich wird nicht wiederhergestellt.");
        using var z = ZipFile.OpenRead(zip);
        var e = z.GetEntry(path) ?? throw new FileNotFoundException("Nicht in der Sicherung: " + path);
        string? kept = null;
        if (File.Exists(dest))
        {
            kept = Path.Combine(backupDir, "einzeln", now.ToString("yyyyMMdd-HHmmss"), path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!); File.Copy(dest, kept, true);
            File.SetAttributes(dest, FileAttributes.Normal);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        e.ExtractToFile(dest, true);
        audit.Append("owner", "backup.restore.one", $"{Path.GetFileName(zip)}: {path}");
        return kept;
    }
}
