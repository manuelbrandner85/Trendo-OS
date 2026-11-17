using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0729: Eigene Temp-Ordner von Trendo – anlegen, sicher wieder löschen, verwaiste Reste wegräumen.
/// Befund 17.11.2026: Über 100 GB in %TEMP% (585 Chrome-Profile, 150 000 leere Testordner …), weil ein einziges <c>Directory.Delete</c>
/// scheitert, solange Chrome oder LibreOffice noch eine Datei halten, und weil Tests ihre Ordner gar nicht löschten.
/// <list type="bullet">
/// <item><see cref="Create"/> + <c>using</c>: Ordner verschwindet am Ende – auch bei Fehler und Abbruch.</item>
/// <item><see cref="Delete"/>: wiederholt bei gesperrten Dateien, nimmt Schreibschutz weg, folgt keinen Verknüpfungen.</item>
/// <item><see cref="SweepStale"/>: beim Start nur EIGENE Präfixe (<see cref="OwnPrefixes"/>), älter als 24 h, nie <see cref="Protected"/>, nie fremde Ordner.</item>
/// <item><see cref="BeginTestRun"/>: Testläufe schreiben in einen eigenen Laufordner, der am Ende als Ganzes verschwindet.</item>
/// </list>
/// Abschalten der Aufräumung beim Start: Umgebungsvariable <c>TRENDO_TEMP_AUFRAEUMEN=0</c>.</summary>
public sealed class TempDir : IDisposable
{
    /// <summary>Ursprünglicher Temp-Ordner, falls ein Testlauf <c>TMP</c>/<c>TEMP</c> auf seinen Laufordner umgelenkt hat.</summary>
    public const string OriginalVar = "TRENDO_TEMP_ORIGINAL";
    /// <summary><c>0</c> schaltet die Aufräumung verwaister Ordner ab (App-Start und Testlauf-Start).</summary>
    public const string SweepVar = "TRENDO_TEMP_AUFRAEUMEN";
    public const string TestRunPrefix = "trendo-testlauf-";
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

    /// <summary>Feste Liste der Präfixe, die Trendo (Produkt und Tests) selbst für Wegwerf-Ordner und -Dateien in %TEMP% benutzt.</summary>
    public static readonly string[] OwnPrefixes =
    [
        // Produkt (src/)
        "trendo-chrome-", "trendo-lo-", "trendo-ocr-", "trendo-pdfocr-", "trendo-scan-ocr-", "trendo-ext-", "trendo-cat-", "trendo-mod-",
        "trendo-web-", "trendo-mappe-", "trendo-freihaendig-", "trendo-vorlesen-", "trendo-diktat-", "trendo-comic-", "trendo-antrag-",
        "trendo-whisper-", "trendo-animation-", "trendo-videoki-", "trendo-video-", "trendo-bib-", "trendo-cover-", "trendo-abschnitte-",
        "trendo-cwspuren-", "trendo-melodie-", "trendo-stimme-", "trendo-analyse-", "trendo-pdf-", "trendo-podcast-", "trendo-pod-",
        "trendo-kamera-", "trendo-mixamo-", "trendo-gh-", "trendo-druck-", "trendo-vorlage-", "trendo-untertitel-", "trendo-3d-",
        "trendo-seite-", "trendo-dl-", "trendo-glb-", "trendo-standbild-", "trendo-skizze-", "trendo-md-", "trendo-comp-", "trendo-foto-",
        "trendo-convert-", "trendo-startprobe-", "trendo-sig-", "trendo-deck-", "trendo-engine-export-", "trendo-schwaerzen-",
        "trendo-masse-", "trendo-xr-",
        // Tests (tests/, tools/)
        TestRunPrefix, "trendo-ui-", "trendo-s02-", "trendo-s03-", "trendo-s10-", "trendo-web2", "trendo-web3", "trendo-webki-",
        "trendo-webdesign-", "trendo-webregel-", "trendo-w7-", "trendo-mui-", "trendo-abbild-", "trendo-t0705-roh-", "trendo-t0725-roh-",
        "trendo-design-", "trendo-echtlauf-audit-", "trendo-needs-",
    ];

    /// <summary>Nie automatisch löschen: Arbeitskopien laufender Aufträge, Werkzeug-Prüfordner, große Echtlauf-Arbeitsordner, feste Caches.</summary>
    public static readonly string[] Protected =
    [
        "trendo-auftrag-", "trendo-tools-b", "trendo-axe", "trendo-ue-e2e", "trendo-ps2", "trendo-abbild-latest", "trendo-abbild-0.",
        "trendo-ace-live", "trendo-z3-quellen", "trendo-render", "trendo-claude-frage", "trendo-schriftvorschau", "trendo-gh-teile",
        "trendo-live-pegel", "trendo-t0705-beleg", "trendo-t0725-beleg", "trendo-pdf-profil",
        // Audio-Editor: ältere gespeicherte Projekte verweisen auf kopierte Ausschnitte in %TEMP%\trendo-audio-<pid>\clip-….wav –
        // die Aufräumung darf sie nie löschen (neue Ausschnitte liegen im Arbeitsordner „Medien\Ausschnitte“).
        "trendo-audio-",
    ];

    /// <summary>„trendo-…“ mit angehängter Guid (32 Hex-Zeichen, z. B. <c>Guid.NewGuid().ToString("N")</c>) – so benennt nur Trendo-Code seine Wegwerf-Ordner.</summary>
    static readonly Regex GuidName = new("^trendo-[a-z0-9-]*[0-9a-f]{32}(\\.[a-z0-9]{1,5}){0,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Path { get; }
    TempDir(string path) => Path = path;

    /// <summary>Neuen, eindeutigen Ordner <c>&lt;prefix&gt;&lt;guid&gt;</c> anlegen (Standard: %TEMP%). Nur eigene Präfixe.</summary>
    public static TempDir Create(string prefix, string? parent = null)
    {
        if (!OwnPrefixes.Any(p => prefix.StartsWith(p, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Kein eigener Temp-Präfix: " + prefix, nameof(prefix));
        var path = System.IO.Path.Combine(parent ?? System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    public void Dispose() => Delete(Path);

    /// <summary>Ursprünglicher Temp-Ordner des Rechners (auch während eines Testlaufs mit umgelenktem TMP) – für feste Prüf- und Werkzeugordner wie <c>trendo-tools-b</c>.</summary>
    public static string SystemTemp => Environment.GetEnvironmentVariable(OriginalVar) is { Length: > 0 } t ? t : System.IO.Path.GetTempPath();

    /// <summary>Ordner oder Datei löschen. Gesperrte Dateien (Chrome, LibreOffice, Virenscanner) geben oft erst nach Sekunden frei – deshalb mehrere Versuche.
    /// Folgt keinen Verknüpfungen (Junctions/Symlinks werden nur als Verweis entfernt). True, wenn danach nichts mehr da ist.</summary>
    public static bool Delete(string path, int attempts = 10, int delayMs = 200)
    {
        for (var i = 1; ; i++)
        {
            if (TryDeleteOnce(path, out var gone)) return gone;
            if (i >= attempts) return !Directory.Exists(path) && !File.Exists(path);
            Thread.Sleep(delayMs);
        }
    }

    /// <summary>Wie <see cref="Delete"/>, blockiert aber keinen Thread: Löschen läuft im Thread-Pool, Wartezeiten mit <c>Task.Delay</c>.
    /// Für Aufrufer auf dem Oberflächen-Thread (Chrome-Vorschau, LibreOffice-Export) – sonst friert das Fenster sekundenlang ein.</summary>
    public static Task<bool> DeleteAsync(string path, int attempts = 10, int delayMs = 200) => Task.Run(async () =>
    {
        for (var i = 1; ; i++)
        {
            if (TryDeleteOnce(path, out var gone)) return gone;
            if (i >= attempts) return !Directory.Exists(path) && !File.Exists(path);
            await Task.Delay(delayMs).ConfigureAwait(false);
        }
    });

    /// <summary>Ein Löschversuch. True: fertig (<paramref name="gone"/> = nichts mehr da); false: gesperrt, später erneut versuchen.</summary>
    static bool TryDeleteOnce(string path, out bool gone)
    {
        gone = true;
        try
        {
            if (Directory.Exists(path))
            {
                if (!new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint)) ClearReadOnly(path);
                Directory.Delete(path, true);
            }
            else if (File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
            return true;
        }
        catch (DirectoryNotFoundException) { return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { gone = false; return false; }
    }

    static void ClearReadOnly(string dir)
    {
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var f in Directory.EnumerateFiles(dir, "*", opt))
            try { if (File.GetAttributes(f).HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(f, FileAttributes.Normal); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static bool IsProtected(string name) => Protected.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Gehört dieser Name in %TEMP% zu Trendo und darf (wenn alt genug) weg? Fremde Namen, geschützte Ordner und %TEMP%\claude nie.</summary>
    public static bool IsOwnName(string name)
    {
        if (!name.StartsWith("trendo-", StringComparison.OrdinalIgnoreCase) || IsProtected(name)) return false;
        return OwnPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || GuidName.IsMatch(name);
    }

    public static bool SweepDisabled => Environment.GetEnvironmentVariable(SweepVar) == "0";

    /// <summary>Verwaiste eigene Ordner/Dateien in <paramref name="root"/> (Standard: <see cref="SystemTemp"/>) löschen, die älter als <paramref name="maxAge"/> sind.
    /// „Alt“ heißt: kein Eintrag darin wurde in dieser Zeit geändert oder angelegt (laufende Arbeit bleibt also stehen). Laufordner von Tests
    /// (<c>trendo-testlauf-&lt;pid&gt;-…</c>) gehen sofort weg, sobald ihr Testprozess nicht mehr läuft. Gibt die Zahl gelöschter Einträge zurück.</summary>
    public static int SweepStale(string? root = null, TimeSpan? maxAge = null, DateTime? nowUtc = null, bool ignoreSwitch = false)
    {
        if (SweepDisabled && !ignoreSwitch) return 0;
        root ??= SystemTemp;
        if (!Directory.Exists(root)) return 0;
        var cutoff = (nowUtc ?? DateTime.UtcNow) - (maxAge ?? DefaultMaxAge);
        var deleted = 0;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(root, "trendo-*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
        foreach (var entry in entries)
        {
            var name = System.IO.Path.GetFileName(entry);
            if (!IsOwnName(name)) continue;
            try
            {
                if (name.StartsWith(TestRunPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Laufordner: nur wenn der eigene Testprozess beendet ist (oder der Name unlesbar und der Ordner alt ist)
                    var alive = OwnerAlive(name);
                    if (alive == true || (alive is null && ChangedSince(entry, cutoff))) continue;
                }
                else if (ChangedSince(entry, cutoff)) continue;
                if (Delete(entry, attempts: 2, delayMs: 100)) deleted++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* gerade in Benutzung – beim nächsten Start */ }
        }
        return deleted;
    }

    /// <summary>App-Start: Aufräumen im Hintergrund, kurz verzögert, damit der Start nicht wartet. Fehler bleiben folgenlos.</summary>
    public static Task SweepInBackground(TimeSpan? delay = null) => Task.Run(async () =>
    {
        try { await Task.Delay(delay ?? TimeSpan.FromSeconds(20)); SweepStale(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    });

    /// <summary>Wurde in <paramref name="path"/> (rekursiv, ohne Verknüpfungen zu folgen) seit <paramref name="cutoffUtc"/> etwas angelegt oder geändert?
    /// Bei sehr großen Bäumen (über 50 000 Einträge) gilt der Ordner vorsichtshalber als benutzt.</summary>
    public static bool ChangedSince(string path, DateTime cutoffUtc)
    {
        FileSystemInfo top = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (!top.Exists) return false;
        if (top.LastWriteTimeUtc > cutoffUtc || top.CreationTimeUtc > cutoffUtc) return true;
        if (top is not DirectoryInfo d || d.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
        var n = 0;
        foreach (var i in d.EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            if (++n > 50_000) return true;
            if (i.LastWriteTimeUtc > cutoffUtc || i.CreationTimeUtc > cutoffUtc) return true;
        }
        return false;
    }

    /// <summary>Laufordnername für diesen Prozess: <c>trendo-testlauf-&lt;pid&gt;-&lt;Startzeit&gt;</c> – damit erkennt die Aufräumung, ob der Lauf noch lebt.</summary>
    public static string TestRunName(Process p) => $"{TestRunPrefix}{p.Id}-{StartStamp(p):x}";

    /// <summary>Startzeit in Sekunden (kurz gehalten: lange Temp-Pfade lassen z. B. LibreOffice still scheitern).</summary>
    static long StartStamp(Process p) => new DateTimeOffset(p.StartTime.ToUniversalTime()).ToUnixTimeSeconds();

    /// <summary>True: Testprozess läuft noch; false: beendet; null: Name nicht lesbar.</summary>
    public static bool? OwnerAlive(string runName)
    {
        var m = Regex.Match(runName, "^" + TestRunPrefix + "(\\d+)-([0-9a-f]+)$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        try
        {
            using var p = Process.GetProcessById(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            try { return Math.Abs(StartStamp(p) - Convert.ToInt64(m.Groups[2].Value, 16)) <= 1; }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return true; } // fremder Prozess, Startzeit nicht lesbar – vorsichtshalber „lebt“
        }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    static string? _run;

    /// <summary>Für Testprojekte (per ModuleInitializer): Temp dieses Testprozesses auf einen eigenen Laufordner
    /// <c>%TEMP%\trendo-testlauf-&lt;pid&gt;-…</c> umlenken (TMP/TEMP/TMPDIR, gilt auch für gestartete Kindprozesse wie Chrome und LibreOffice).
    /// Am Prozessende wird der Laufordner als Ganzes gelöscht; vorher räumt der Start verwaiste Reste früherer Läufe weg.
    /// Feste Prüfordner (z. B. <c>trendo-tools-b</c>) erreichen Tests über <see cref="SystemTemp"/>.</summary>
    public static string BeginTestRun()
    {
        if (_run is not null) return _run;
        var original = SystemTemp;
        using var self = Process.GetCurrentProcess();
        var run = System.IO.Path.Combine(original, TestRunName(self));
        Directory.CreateDirectory(run);
        Environment.SetEnvironmentVariable(OriginalVar, original);
        foreach (var v in new[] { "TMP", "TEMP", "TMPDIR" }) Environment.SetEnvironmentVariable(v, run);
        _run = run;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Delete(run, attempts: 15, delayMs: 200);
        try { SweepStale(original); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return run;
    }

    /// <summary>Laufordner dieses Testprozesses (null außerhalb von Tests).</summary>
    public static string? TestRun => _run;
}
