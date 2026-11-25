using System.Runtime.InteropServices;

namespace Trendo.Core.Shell;

/// <summary>T-0754 (Gegenprüfung): Löschen würde hier endgültig löschen statt in den Papierkorb zu legen – kein Papierkorb auf dem Laufwerk
/// (Netz, SUBST, Wechseldatenträger), Papierkorb auf „sofort löschen“ gestellt oder Element größer als der Papierkorb.
/// Es wird nichts gelöscht; die Oberfläche fragt ausdrücklich nach.</summary>
public sealed class NoRecycleBinException(string path, string reason = FileBrowser.NoBinReason) : IOException($"„{System.IO.Path.GetFileName(path.TrimEnd('\\', '/'))}“: {reason} – Löschen wäre dort endgültig.")
{
    public string ItemPath { get; } = path;
    public string Reason { get; } = reason;
}

/// <summary>Dateimanager-Kern (Explorer, Finder, GNOME-Dateien nachgebaut, 04.11.2026): Ordner auflisten, Laufwerke („Dieser PC“),
/// bekannte Ordner, neuer Ordner, umbenennen, kopieren/einfügen, in den Papierkorb legen. Löschen geht nie endgültig, sondern immer in den Papierkorb.
/// Mit <see cref="Root"/> (Tests) arbeitet alles nur innerhalb dieses Ordners – Laufwerke und Papierkorb inklusive.</summary>
public sealed class FileBrowser
{
    public sealed record Item(string Path, string Name, bool IsDir, long Size, DateTime Modified, string Kind);
    public sealed record Drive(string Path, string Label, long Total, long Free, string Kind);

    /// <summary>Nur innerhalb dieses Ordners arbeiten (Tests, Testmodus). Null = echtes Dateisystem des Nutzers.</summary>
    public string? Root { get; init; }
    public bool ShowHidden { get; set; }
    /// <summary>Bedienstil (windows, mac, linux, trendo): bestimmt Kopie- und Ordnernamen wie im jeweiligen System.</summary>
    public string Style { get; set; } = "windows";

    public string Home => Root ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Bekannte Ordner in der Reihenfolge der Seitenleiste (Desktop, Downloads, Dokumente, Bilder, Musik, Videos).</summary>
    public IReadOnlyList<(string Id, string Label, string Path)> KnownFolders()
    {
        string P(Environment.SpecialFolder f, string sub) => Root is not null ? System.IO.Path.Combine(Root, sub) : Environment.GetFolderPath(f) is { Length: > 0 } x ? x : System.IO.Path.Combine(Home, sub);
        var l = new List<(string, string, string)>
        {
            ("desktop", "Desktop", P(Environment.SpecialFolder.DesktopDirectory, "Desktop")),
            ("downloads", "Downloads", System.IO.Path.Combine(Home, "Downloads")),
            ("dokumente", "Dokumente", P(Environment.SpecialFolder.MyDocuments, "Dokumente")),
            ("bilder", "Bilder", P(Environment.SpecialFolder.MyPictures, "Bilder")),
            ("musik", "Musik", P(Environment.SpecialFolder.MyMusic, "Musik")),
            ("videos", "Videos", P(Environment.SpecialFolder.MyVideos, "Videos")),
        };
        return l.Where(x => Directory.Exists(x.Item3) || Root is not null).ToList();
    }

    /// <summary>Laufwerke für „Dieser PC“ / „Orte“ / „Andere Orte“ – nur bereite Laufwerke; im Testmodus ein erfundenes Laufwerk im Testordner.</summary>
    public IReadOnlyList<Drive> Drives()
    {
        if (Root is not null) return [new Drive(Root, "Lokaler Datenträger", 512L << 30, 200L << 30, "fest")];
        var l = new List<Drive>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                if (!OperatingSystem.IsWindows() && d.DriveType is DriveType.Ram or DriveType.Unknown) continue;
                if (!OperatingSystem.IsWindows() && d.RootDirectory.FullName != "/" && !d.RootDirectory.FullName.StartsWith("/media", StringComparison.Ordinal) && !d.RootDirectory.FullName.StartsWith("/run/media", StringComparison.Ordinal) && !d.RootDirectory.FullName.StartsWith("/Volumes", StringComparison.Ordinal)) continue;
                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.DriveType switch { DriveType.Removable => "USB-Laufwerk", DriveType.Network => "Netzlaufwerk", DriveType.CDRom => "DVD-Laufwerk", _ => "Lokaler Datenträger" } : d.VolumeLabel;
                l.Add(new Drive(d.RootDirectory.FullName, label, d.TotalSize, d.AvailableFreeSpace, d.DriveType switch { DriveType.Removable => "wechsel", DriveType.Network => "netz", DriveType.CDRom => "dvd", _ => "fest" }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return l;
    }

    /// <summary>Inhalt eines Ordners: Ordner zuerst, dann Dateien, jeweils nach Name (wie in allen drei Vorbildern).</summary>
    public IReadOnlyList<Item> List(string dir, string sort = "name", bool descending = false)
    {
        Guard(dir, name: false); // T-0764 (10./11. Prüfrunde): läuft bewusst über die Schreibprüfung – im Kinderprofil zeigt der Dateimanager nur Ordner der Freigabe (keine Lesesperre nur für Trendo-Ordner, sondern alles außerhalb der Freigabe), ohne Namensprüfung
        var di = new DirectoryInfo(dir);
        if (!di.Exists) throw new DirectoryNotFoundException("Den Ordner gibt es nicht mehr: " + dir);
        var items = new List<Item>();
        IEnumerable<FileSystemInfo> all;
        try { all = di.EnumerateFileSystemInfos(); } catch (UnauthorizedAccessException) { throw new InvalidOperationException("Auf diesen Ordner hast du keinen Zugriff."); }
        foreach (var f in all)
        {
            try
            {
                var hidden = f.Name.StartsWith('.') || f.Attributes.HasFlag(FileAttributes.Hidden) || f.Attributes.HasFlag(FileAttributes.System);
                if (hidden && !ShowHidden) continue;
                var isDir = f is DirectoryInfo;
                items.Add(new Item(f.FullName, f.Name, isDir, isDir ? 0 : ((FileInfo)f).Length, f.LastWriteTime, Kind(f.Name, isDir)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        IEnumerable<Item> o = sort switch
        {
            "datum" => items.OrderBy(i => i.Modified),
            "groesse" => items.OrderBy(i => i.Size),
            "typ" => items.OrderBy(i => i.Kind, StringComparer.CurrentCultureIgnoreCase),
            _ => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        if (descending) o = o.Reverse();
        return [.. o.OrderBy(i => i.IsDir ? 0 : 1)];
    }

    /// <summary>Art der Datei auf Deutsch („Textdokument“, „PNG-Datei“ …).</summary>
    public static string Kind(string name, bool isDir)
    {
        if (isDir) return "Dateiordner";
        var e = System.IO.Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        return e switch
        {
            "txt" or "md" => "Textdokument", "pdf" => "PDF-Dokument", "doc" or "docx" or "odt" => "Textdokument (Office)", "xls" or "xlsx" or "ods" or "csv" => "Tabelle",
            "ppt" or "pptx" or "odp" => "Präsentation", "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp" or "svg" => "Bild (" + e.ToUpperInvariant() + ")",
            "mp3" or "wav" or "flac" or "ogg" or "m4a" => "Audiodatei", "mp4" or "mkv" or "mov" or "avi" or "webm" => "Videodatei", "zip" or "7z" or "rar" or "tar" or "gz" => "Archiv",
            "exe" or "msi" => "Programm", "lnk" => "Verknüpfung", "tdoc" => "Trendo-Dokument", "" => "Datei", _ => e.ToUpperInvariant() + "-Datei",
        };
    }

    /// <summary>Größe wie im Explorer: „1,2 MB“.</summary>
    public static string SizeText(long b) => b < 1024 ? $"{b} Bytes" : b < 1 << 20 ? $"{b / 1024.0:0.#} KB" : b < 1L << 30 ? $"{b / 1048576.0:0.#} MB" : $"{b / 1073741824.0:0.#} GB";

    /// <summary>Pfad-Stücke für die Adresszeile („Dieser PC › C: › Benutzer …“).</summary>
    public IReadOnlyList<(string Label, string Path)> Crumbs(string dir)
    {
        var l = new List<(string, string)>();
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            l.Insert(0, (d.Parent is null ? d.FullName.TrimEnd('\\', '/') is { Length: > 0 } r ? r : "/" : d.Name, d.FullName));
            if (Root is not null && string.Equals(System.IO.Path.GetFullPath(d.FullName).TrimEnd('\\', '/'), System.IO.Path.GetFullPath(Root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) break;
        }
        return l;
    }

    public string NewFolder(string dir, string? name = null)
    {
        Guard(dir, name: false);
        bool Ex(string n) => Directory.Exists(System.IO.Path.Combine(dir, n)) || File.Exists(System.IO.Path.Combine(dir, n));
        var p = System.IO.Path.Combine(dir, name is null ? OsBehavior.NewFolderName(Style, Ex) : OsBehavior.CopyName(Style, name, true, Ex));
        Guard(p); // 11. Prüfrunde: auch der neue Name
        Directory.CreateDirectory(p); return p;
    }

    public string Rename(string path, string newName)
    {
        Guard(path, name: false);
        newName = newName.Trim();
        if (newName.Length == 0 || newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || newName.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            throw new InvalidOperationException("Der Name darf keines dieser Zeichen enthalten: \\ / : * ? \" < > |");
        var target = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, newName);
        if (string.Equals(target, path, StringComparison.Ordinal)) return path;
        if (File.Exists(target) || Directory.Exists(target)) throw new InvalidOperationException($"„{newName}“ gibt es in diesem Ordner schon.");
        Guard(target); // 11. Prüfrunde: „a.txt“ → „a.exe“ prüft den neuen Namen
        if (Directory.Exists(path)) Directory.Move(path, target); else File.Move(path, target);
        return target;
    }

    /// <summary>Kopieren oder Ausschneiden + Einfügen; bei gleichem Namen „ – Kopie“ wie im Explorer.</summary>
    public string Paste(string source, string targetDir, bool move)
    {
        Guard(source, write: move, name: false); Guard(targetDir, name: false); // Kopieren liest die Quelle nur
        var name = System.IO.Path.GetFileName(source.TrimEnd('\\', '/'));
        var target = System.IO.Path.Combine(targetDir, name);
        if (move && string.Equals(System.IO.Path.GetDirectoryName(source), targetDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return source;
        if (Directory.Exists(source) && (targetDir + System.IO.Path.DirectorySeparatorChar).StartsWith(source.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ein Ordner kann nicht in sich selbst eingefügt werden.");
        target = System.IO.Path.Combine(targetDir, OsBehavior.CopyName(Style, name, Directory.Exists(source), n => File.Exists(System.IO.Path.Combine(targetDir, n)) || Directory.Exists(System.IO.Path.Combine(targetDir, n))));
        Guard(target); // 11. Prüfrunde: Ziel samt Namen
        if (WriteGuard is not null && Directory.Exists(source)) // jede Datei im Ordner, mit ihrem Ziel – kein .exe/.lnk über einen Ordner hinein
            foreach (var f in Directory.EnumerateFileSystemEntries(source, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false }))
                Guard(System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, f)));
        if (Directory.Exists(source)) { if (move) Directory.Move(source, target); else CopyDir(source, target); }
        else if (move) File.Move(source, target); else File.Copy(source, target);
        return target;
    }

    internal static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, System.IO.Path.Combine(to, System.IO.Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, System.IO.Path.Combine(to, System.IO.Path.GetFileName(d)));
    }

    /// <summary>Für Tests: ob es für einen Pfad einen Papierkorb gibt (null = echte Prüfung).</summary>
    public Func<string, bool>? RecycleBinCheck { get; set; }

    /// <summary>Papierkorb-Einstellung eines Laufwerks: „Dateien sofort löschen“ und größte Papierkorbgröße in Bytes (null = unbekannt).</summary>
    public sealed record BinPolicy(bool NukeOnDelete, long? MaxBytes);
    /// <summary>Für Tests: Papierkorb-Einstellung nachbilden (null = echte, nur lesende Abfrage der Windows-Einstellungen).</summary>
    public Func<string, BinPolicy?>? RecycleBinPolicy { get; set; }

    public const string NoBinReason = "auf diesem Laufwerk gibt es keinen Papierkorb";

    /// <summary>T-0754 (Gegenprüfung): Gibt es für diesen Pfad einen Papierkorb, der das Element wirklich aufnimmt? Siehe <see cref="TrashProblem"/>.</summary>
    public bool HasTrash(string path) => TrashProblem(path) is null;

    /// <summary>T-0754 (Zweitprüfung): Warum Löschen hier endgültig wäre – oder null, wenn das Element in den Papierkorb geht.
    /// Testordner, Mac, Linux: Papierkorb vorhanden. Windows: kein Papierkorb für Netzpfade, Netz-/DVD-/RAM-/Wechsel- und SUBST-Laufwerke;
    /// außerdem endgültig, wenn der Papierkorb des Laufwerks auf „Dateien sofort löschen“ steht oder das Element größer ist als der Papierkorb
    /// (Einstellungen NukeOnDelete/MaxCapacity werden nur gelesen).</summary>
    public string? TrashProblem(string path)
    {
        if (RecycleBinCheck is { } c && !c(path)) return NoBinReason;
        if (RecycleBinPolicy is { } pol) return PolicyProblem(path, pol(path));
        if (RecycleBinCheck is not null || Root is not null || !OperatingSystem.IsWindows()) return null;
        return WindowsTrashProblem(path);
    }

    /// <summary>Wie <see cref="TrashProblem"/>, nur mit den echten Windows-Einstellungen (für <see cref="SystemRecycle"/>).</summary>
    public static string? WindowsTrashProblem(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!WindowsHasRecycleBin(path)) return NoBinReason;
        return PolicyProblem(path, ReadWindowsPolicy(path));
    }

    static string? PolicyProblem(string path, BinPolicy? p)
    {
        if (p is null) return null;
        if (p.NukeOnDelete) return "der Papierkorb ist auf „Dateien sofort löschen“ gestellt";
        if (p.MaxBytes is long max && ItemBytes(path, max) > max) return $"das Element ist größer als der Papierkorb ({SizeText(max)})";
        return null;
    }

    /// <summary>Größe einer Datei oder eines Ordners; hört auf zu zählen, sobald <paramref name="stopAbove"/> überschritten ist.
    /// T-0757: Ist das Element selbst ein Reparse-Point (Junction, symbolische Verknüpfung), wandert nur die Verknüpfung in den Papierkorb – Größe 0,
    /// der Inhalt des Ziels zählt nicht mit.</summary>
    public static long ItemBytes(string path, long stopAbove = long.MaxValue)
    {
        try { if ((File.Exists(path) || Directory.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) return 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (File.Exists(path)) return new FileInfo(path).Length;
        long sum = 0;
        if (!Directory.Exists(path)) return 0;
        foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            sum += f.Length; if (sum > stopAbove) break;
        }
        return sum;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumeNameForVolumeMountPointW")]
    static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, char[] volumeName, uint size);

    /// <summary>Liest (nur lesend) die Papierkorb-Einstellung des Laufwerks: HKCU\…\Explorer\BitBucket\Volume\{GUID} (NukeOnDelete, MaxCapacity in MB),
    /// sonst die allgemeine Einstellung unter BitBucket; Richtlinie „NoRecycleFiles“. Ohne MaxCapacity gilt die Windows-Voreinstellung
    /// <see cref="WindowsDefaultBinBytes"/>. T-0757: Das Laufwerk wird über <see cref="VolumePath"/> bestimmt (auch als Ordner eingehängte Laufwerke);
    /// lässt sich seine Kennung nicht lesen, gilt die allgemeine Einstellung NukeOnDelete. Ändert nie etwas.</summary>
    public static BinPolicy? ReadWindowsPolicy(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var root = VolumePath(path);
            if (string.IsNullOrEmpty(root)) return null;
            const string Bb = @"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket";
            int? Dword(string key, string name) { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key); return k?.GetValue(name) is int v ? v : null; }
            var nuke = (Dword(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRecycleFiles") ?? 0) != 0;
            int? max = null;
            var buf = new char[64];
            if (GetVolumeNameForVolumeMountPoint(root.EndsWith('\\') ? root : root + "\\", buf, (uint)buf.Length))
            {
                var vol = new string(buf).TrimEnd('\0'); var a = vol.IndexOf('{'); var b = vol.IndexOf('}');
                if (a >= 0 && b > a)
                {
                    var key = Bb + @"\Volume\" + vol[a..(b + 1)];
                    if (Dword(key, "NukeOnDelete") is int n) nuke |= n != 0; else nuke |= (Dword(Bb, "NukeOnDelete") ?? 0) != 0;
                    max = Dword(key, "MaxCapacity");
                }
                else nuke |= (Dword(Bb, "NukeOnDelete") ?? 0) != 0;
            }
            else nuke |= (Dword(Bb, "NukeOnDelete") ?? 0) != 0; // Kennung unbekannt: allgemeine Einstellung
            long? maxBytes = max is int mb ? mb * 1024L * 1024L : VolumeBytes(root) is long total ? WindowsDefaultBinBytes(total) : null;
            return new BinPolicy(nuke, maxBytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { return null; }
    }

    /// <summary>T-0757: Voreinstellung von Windows ohne MaxCapacity: 10 % der ersten 40 GB, 5 % vom Rest.</summary>
    public static long WindowsDefaultBinBytes(long volumeBytes)
    {
        const long G40 = 40L << 30;
        return Math.Min(G40, Math.Max(0, volumeBytes)) / 10 + Math.Max(0, volumeBytes - G40) / 20;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumePathNameW")]
    static extern bool GetVolumePathName(string fileName, char[] volumePathName, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDriveTypeW")]
    static extern uint GetDriveType(string rootPathName);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDiskFreeSpaceExW")]
    static extern bool GetDiskFreeSpaceEx(string directory, out ulong freeForCaller, out ulong total, out ulong totalFree);

    /// <summary>T-0757: Wurzel des Laufwerks, auf dem der Pfad liegt – mit abschließendem „\“. Bei einem als Ordner eingehängten Laufwerk
    /// (z. B. <c>C:\Daten\USB\</c>) ist das der Einhängeordner, nicht <c>C:\</c>. Nur Windows; null, wenn Windows es nicht sagen kann.</summary>
    public static string? VolumePath(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var buf = new char[1024];
        if (!GetVolumePathName(System.IO.Path.GetFullPath(path), buf, (uint)buf.Length)) return null;
        var v = new string(buf).TrimEnd('\0');
        return v.Length == 0 ? null : v.EndsWith('\\') ? v : v + "\\";
    }

    static long? VolumeBytes(string volume) => GetDiskFreeSpaceEx(volume, out _, out var total, out _) && total > 0 ? (long)total : null;

    static bool WindowsHasRecycleBin(string path)
    {
        try
        {
            var full = System.IO.Path.GetFullPath(path);
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false; // Netzpfad
            var root = System.IO.Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':') return false;
            if (IsSubst(root[..2])) return false;
            // T-0757: als Ordner eingehängte Laufwerke – die Art des eingehängten Laufwerks zählt, nicht die von C:
            if (VolumePath(full) is { } vol && !string.Equals(vol, root.EndsWith('\\') ? root : root + "\\", StringComparison.OrdinalIgnoreCase))
                return GetDriveType(vol) == 3; // DRIVE_FIXED
            var d = new DriveInfo(root);
            return d.DriveType == DriveType.Fixed; // Zweitprüfung: Wechseldatenträger legen oft nicht ab, auch mit $Recycle.Bin – dort immer nachfragen
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryDosDeviceW")]
    static extern uint QueryDosDevice(string lpDeviceName, char[] lpTargetPath, uint ucchMax);

    /// <summary>SUBST-Laufwerke zeigen auf einen Ordner („\??\C:\…“) und haben keinen eigenen Papierkorb.</summary>
    static bool IsSubst(string drive)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var buf = new char[1024];
        var n = QueryDosDevice(drive, buf, (uint)buf.Length);
        return n > 0 && new string(buf, 0, (int)n).StartsWith(@"\??\", StringComparison.Ordinal);
    }

    /// <summary>T-0754: Liegt dieser Pfad direkt in einem echten Papierkorb? Nur absolute Pfade, nie ein Wort oder ein relativer Pfad (Gegenprüfung),
    /// und nur an den Stellen, an denen die Systeme ihren Papierkorb führen (Zweitprüfung):
    /// <c>X:\$Recycle.Bin\&lt;SID&gt;\$R…</c> direkt unter der Laufwerkswurzel; <c>~/.Trash/…</c> bzw. <c>/Volumes/X/.Trashes/…</c> (Mac);
    /// <c>$XDG_DATA_HOME/Trash/files/…</c> bzw. <c>&lt;Wurzel&gt;/.Trash-&lt;uid&gt;/files/…</c> (Linux); Test-Papierkorb nur <c>&lt;Root&gt;/.Papierkorb/…</c>.</summary>
    public bool IsTrashLocation(string? p)
    {
        if (string.IsNullOrWhiteSpace(p) || !System.IO.Path.IsPathFullyQualified(p)) return false;
        var full = System.IO.Path.GetFullPath(p).TrimEnd('\\', '/');
        var parent = System.IO.Path.GetDirectoryName(full); if (parent is null) return false;
        bool Same(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a).TrimEnd('\\', '/'), System.IO.Path.GetFullPath(b).TrimEnd('\\', '/'), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (Root is not null) return Same(parent, TestTrash);
        var name = System.IO.Path.GetFileName(full);
        // Windows: X:\$Recycle.Bin\<SID>\$R…
        var sidDir = System.IO.Path.GetDirectoryName(parent);
        if (name.StartsWith("$R", StringComparison.OrdinalIgnoreCase) && sidDir is not null && System.IO.Path.GetFileName(sidDir).Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            && System.IO.Path.GetDirectoryName(sidDir) is { } drv && System.IO.Path.GetPathRoot(full) is { } r && (Same(drv, r) || VolumePath(drv) is { } vp && Same(drv, vp))) return true; // T-0757: auch eingehängte Laufwerke
        // Mac
        if (Same(parent, System.IO.Path.Combine(Home, ".Trash"))) return true;
        if (sidDir is not null && System.IO.Path.GetFileName(sidDir) == ".Trashes") return true;
        // Linux/freedesktop
        if (System.IO.Path.GetFileName(parent) == "files" && sidDir is not null)
        {
            var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : System.IO.Path.Combine(Home, ".local", "share");
            if (Same(sidDir, System.IO.Path.Combine(data, "Trash"))) return true;
            if (System.IO.Path.GetFileName(sidDir).StartsWith(".Trash-", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Endgültig löschen – nur nach ausdrücklichem „Ja“ der Person (Laufwerk ohne Papierkorb).</summary>
    public void DeletePermanently(string path)
    {
        Guard(path);
        if (Directory.Exists(path)) Directory.Delete(path, true); else File.Delete(path);
    }

    /// <summary>Papierkorb-Ordner im Testmodus (sonst der des Systems).</summary>
    public string TestTrash => System.IO.Path.Combine(Root ?? Home, ".Papierkorb");

    /// <summary>In den Papierkorb legen – nie endgültig löschen. Windows: Papierkorb des Systems; Linux: freedesktop-Papierkorb (~/.local/share/Trash);
    /// Mac: ~/.Trash; Testmodus: Ordner „.Papierkorb“ im Testordner.
    /// T-0754 (Gegenprüfung): Rückgabe ist die Lage im Papierkorb – oder null, wenn das Element dort nicht zu finden ist (dann ist es endgültig weg).
    /// Hat das Laufwerk keinen Papierkorb, wird nichts gelöscht: <see cref="NoRecycleBinException"/>.</summary>
    public string? Trash(string path, Action<string>? windowsRecycle = null)
    {
        Guard(path);
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Nicht gefunden: " + System.IO.Path.GetFileName(path));
        if (TrashProblem(path) is { } why) throw new NoRecycleBinException(path, why);
        string dir;
        if (Root is not null) dir = TestTrash;
        else if (OperatingSystem.IsWindows())
        {
            if (windowsRecycle is null) throw new InvalidOperationException("Papierkorb nicht verfügbar.");
            var full = System.IO.Path.GetFullPath(path); var since = DateTime.Now.AddSeconds(-2);
            windowsRecycle(path);
            return FindInWindowsRecycleBin(full, since); // T-0754: Lage im Papierkorb merken; null = nicht im Papierkorb (endgültig gelöscht)
        }
        else if (OperatingSystem.IsMacOS()) dir = System.IO.Path.Combine(Home, ".Trash");
        else
        {
            var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : System.IO.Path.Combine(Home, ".local", "share");
            var trash = System.IO.Path.Combine(data, "Trash"); dir = System.IO.Path.Combine(trash, "files"); Directory.CreateDirectory(System.IO.Path.Combine(trash, "info"));
            var name0 = Unique(dir, System.IO.Path.GetFileName(path));
            File.WriteAllText(System.IO.Path.Combine(trash, "info", System.IO.Path.GetFileName(name0) + ".trashinfo"),
                "[Trash Info]\nPath=" + Uri.EscapeDataString(path).Replace("%2F", "/") + "\nDeletionDate=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\n");
            MoveAny(path, name0); return name0;
        }
        Directory.CreateDirectory(dir);
        var t = Unique(dir, System.IO.Path.GetFileName(path)); MoveAny(path, t); return t;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOp
    {
        public IntPtr Hwnd; public uint Func; public string From; public string? To; public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool Aborted; public IntPtr NameMappings; public string? Title;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    static extern int SHFileOperation(ref ShFileOp op);

    /// <summary>T-0754: Windows-Papierkorb des Systems (wie „Löschen“ im Explorer). Nur Windows.
    /// Zweitprüfung: Windows fragt mit FOF_NOCONFIRMATION|FOF_SILENT NICHT nach, auch nicht mit FOF_WANTNUKEWARNING – es würde still endgültig löschen.
    /// Deshalb prüft Trendo vorher selbst (<see cref="WindowsTrashProblem"/>: kein Papierkorb, „sofort löschen“, zu groß) und löscht dann gar nicht,
    /// sondern wirft <see cref="NoRecycleBinException"/>; die Oberfläche fragt.</summary>
    public static void SystemRecycle(string p)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Der Windows-Papierkorb gibt es nur unter Windows.");
        if (WindowsTrashProblem(p) is { } why) throw new NoRecycleBinException(p, why);
        const uint FoDelete = 3; const ushort Silent = 0x4, NoConfirmation = 0x10, AllowUndo = 0x40, WantNukeWarning = 0x4000;
        var op = new ShFileOp { Func = FoDelete, From = System.IO.Path.GetFullPath(p) + "\0\0", Flags = (ushort)(AllowUndo | NoConfirmation | WantNukeWarning | Silent) };
        var rc = SHFileOperation(ref op);
        if (op.Aborted) throw new IOException("Löschen abgebrochen – es wurde nichts gelöscht.");
        if (rc != 0) throw new IOException($"Löschen ging nicht (Fehler 0x{rc:X}): {System.IO.Path.GetFileName(p)}");
    }

    /// <summary>T-0754: Wo liegt ein gerade gelöschtes Element im Windows-Papierkorb? Liest die $I-Vermerke im Papierkorb-Ordner der Person
    /// auf dem Laufwerk der Datei (<c>X:\$Recycle.Bin\&lt;SID&gt;</c>) und liefert die passende $R-Datei – null, wenn es dort nicht liegt
    /// (Laufwerk ohne Papierkorb, zu groß, Papierkorb geleert).</summary>
    public static string? FindInWindowsRecycleBin(string originalPath, DateTime deletedAfter)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value; var root = (System.IO.Path.GetDirectoryName(originalPath) is { } dir ? VolumePath(dir) : null) ?? System.IO.Path.GetPathRoot(originalPath); // T-0757: eingehängte Laufwerke
            if (sid is null || string.IsNullOrEmpty(root)) return null;
            var bin = System.IO.Path.Combine(root, "$Recycle.Bin", sid);
            return Trendo.Core.Sys.Trash.List(bin, null)
                .Where(t => string.Equals(StripLongPrefix(t.OriginalPath), StripLongPrefix(originalPath), StringComparison.OrdinalIgnoreCase) && t.Deleted >= deletedAfter && (File.Exists(t.DataFile) || Directory.Exists(t.DataFile)))
                .OrderByDescending(t => t.Deleted).Select(t => t.DataFile).FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }

    /// <summary>Zweitprüfung: Lange Pfade stehen im $I-Vermerk als <c>\\?\C:\…</c> bzw. <c>\\?\UNC\server\…</c> – vor dem Vergleich abschneiden.</summary>
    public static string StripLongPrefix(string p) =>
        p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + p[8..] : p.StartsWith(@"\\?\", StringComparison.Ordinal) ? p[4..] : p;

    /// <summary>T-0754: Ein Element aus dem Papierkorb an seinen alten Platz zurücklegen. Überschreibt nie: liegt dort schon etwas, passiert nichts (Fehler).
    /// Räumt den Vermerk mit auf (Windows <c>$I…</c>, freedesktop <c>info/*.trashinfo</c>).</summary>
    public void Restore(string trashPath, string original)
    {
        Guard(original);
        if (!File.Exists(trashPath) && !Directory.Exists(trashPath)) throw new FileNotFoundException($"„{System.IO.Path.GetFileName(original)}“ ist nicht mehr im Papierkorb (geleert oder schon zurückgeholt).");
        if (File.Exists(original) || Directory.Exists(original)) throw new InvalidOperationException($"Am alten Platz liegt inzwischen wieder „{System.IO.Path.GetFileName(original)}“ – Trendo überschreibt nichts.");
        var parent = System.IO.Path.GetDirectoryName(original);
        if (parent is null || !Directory.Exists(parent)) throw new DirectoryNotFoundException($"Den Ordner „{parent}“ gibt es nicht mehr – zurücklegen geht nicht.");
        MoveAny(trashPath, original);
        var name = System.IO.Path.GetFileName(trashPath); var dir = System.IO.Path.GetDirectoryName(trashPath)!;
        string? info = name.StartsWith("$R", StringComparison.Ordinal) ? System.IO.Path.Combine(dir, "$I" + name[2..])
            : System.IO.Path.GetFileName(dir) == "files" ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir)!, "info", name + ".trashinfo") : null;
        if (info is not null) try { File.Delete(info); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    static string Unique(string dir, string name)
    {
        var t = System.IO.Path.Combine(dir, name);
        for (var n = 2; File.Exists(t) || Directory.Exists(t); n++) t = System.IO.Path.Combine(dir, $"{System.IO.Path.GetFileNameWithoutExtension(name)} {n}{System.IO.Path.GetExtension(name)}");
        return t;
    }

    internal static void MoveAny(string from, string to) { if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to); }

    /// <summary>T-0764 (10. Prüfrunde): Prüfung vor jedem Schreiben (Anlegen, Kopieren-Ziel, Verschieben, Umbenennen, Löschen, Papierkorb,
    /// Zurücklegen) – liefert eine Meldung, wenn das Schreiben nicht erlaubt ist (Kinderprofil in Trendos eigenen Ordnern), sonst null.</summary>
    /// <remarks>Zweiter Wert: auch den Namen prüfen (11. Prüfrunde: keine ausführbaren Endungen im Kinderprofil).</remarks>
    public Func<string, bool, string?>? WriteGuard { get; set; }

    internal void Guard(string p, bool write = true, bool name = true)
    {
        if (write && WriteGuard?.Invoke(p, name) is { } why) throw new UnauthorizedAccessException(why);
        if (Root is null) return;
        var full = System.IO.Path.GetFullPath(p); var root = System.IO.Path.GetFullPath(Root).TrimEnd('\\', '/');
        if (!full.Equals(root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Im Testmodus nur im Testordner: " + p);
    }
}
