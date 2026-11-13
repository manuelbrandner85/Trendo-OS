using System.Runtime.InteropServices;

namespace Trendo.Core.Compat;

/// <summary>Leistungsprofile laut Auftrag „Commercial Desktop Edition“ (13.11.2026). „Unter“ = unter dem Basis-Profil.</summary>
public enum PerfProfile { Unter = 0, Basic = 1, Standard = 2, Professional = 3 }

public enum CompatState { Ok, Warn, Fail, Unknown }

/// <summary>Gerätedaten für den Systemcheck. Nur technische Werte, keine Namen, keine Seriennummern, nichts verlässt den PC.</summary>
public sealed record HardwareReport(
    string Os, int OsBuild, bool IsWindows11, string Arch, string? Cpu, int Cores, long RamBytes,
    long DiskFreeBytes, long DiskTotalBytes, string? Gpu, long VramBytes, string? GpuDriver, int ScreenWidth, int ScreenHeight);

public sealed record ProfileSpec(PerfProfile Profile, string Name, string ForWhat, int MinCores, double MinRamGb, double MinDiskFreeGb, double MinVramGb);

public sealed record CheckItem(string Id, string Title, string Value, CompatState State, string Hint);

/// <summary>Leistungsprofile. Die Grenzwerte sind <b>vorläufig</b>: Sie stammen nicht aus Messreihen auf verschiedener Hardware
/// und dürfen nicht als offizielle Systemanforderungen veröffentlicht werden (siehe docs/commercial/PERFORMANCE_PROFILES.md).</summary>
public static class PerformanceProfiles
{
    public const bool Provisional = true;
    const double G = 1024d * 1024 * 1024;

    public static readonly IReadOnlyList<ProfileSpec> Specs =
    [
        new(PerfProfile.Basic, "Basic", "Desktop, Büro, einfache kreative Aufgaben", 4, 8, 20, 0),
        new(PerfProfile.Standard, "Standard", "normaler Betrieb der meisten Studios", 6, 16, 50, 4),
        new(PerfProfile.Professional, "Professional", "3D, Video, Spiele und lokale KI", 8, 32, 200, 8),
    ];

    public static ProfileSpec Spec(PerfProfile p) => Specs.First(s => s.Profile == p);

    /// <summary>Höchstes Profil, dessen Mindestwerte für Prozessor, Arbeitsspeicher und Grafikspeicher erfüllt sind. Unbekannter Grafikspeicher zählt als 0.
    /// Freier Speicherplatz zählt nicht zum Profil (ein voller Datenträger macht den PC nicht schwächer); er wird im Systemcheck eigens gewarnt.</summary>
    public static PerfProfile Classify(HardwareReport r)
    {
        var best = PerfProfile.Unter;
        foreach (var s in Specs)
            if (r.Cores >= s.MinCores && r.RamBytes / G >= s.MinRamGb * 0.94 && r.VramBytes / G >= s.MinVramGb * 0.94) best = s.Profile;
            else break;
        return best;
    }

    public static string Label(PerfProfile p) => p == PerfProfile.Unter ? "unter Basic" : Spec(p).Name;
}

/// <summary>Was ein Studio braucht. Mindestprofil: läuft, ggf. langsam. Empfohlen: läuft flüssig. Zusatzprogramme: Kennungen aus <see cref="DependencyCatalog"/>.</summary>
public sealed record StudioNeed(PerfProfile Min, PerfProfile Recommended, string[] Programs, string Alternative);

public enum StudioFit { Laeuft, Eingeschraenkt, BrauchtProgramm, ZuSchwach }

public sealed record StudioVerdict(string StudioId, StudioFit Fit, string Text);

public static class StudioNeeds
{
    static StudioNeed N(PerfProfile min, PerfProfile rec, string alt = "", params string[] programs) => new(min, rec, programs, alt);

    /// <summary>Studios mit höheren Anforderungen. Alle anderen: Basic. Einstufung nach Art der Arbeit, vorläufig wie die Profile.</summary>
    public static readonly IReadOnlyDictionary<string, StudioNeed> ById = new Dictionary<string, StudioNeed>(StringComparer.OrdinalIgnoreCase)
    {
        ["CORE-05"] = N(PerfProfile.Standard, PerfProfile.Professional, "Vorschau in niedriger Qualität, Rendern über Nacht", "blender"),
        ["CORE-06"] = N(PerfProfile.Standard, PerfProfile.Professional, "kleine 2D- und Retro-Spiele laufen auch auf Standard; Unreal nur mit starker Grafikkarte", "unreal"),
        ["SPEC-04"] = N(PerfProfile.Professional, PerfProfile.Professional, "Räume planen geht ohne Brille und ohne starke Grafikkarte"),
        ["SPEC-09"] = N(PerfProfile.Standard, PerfProfile.Professional, "weniger Fotos je Scan, Ergebnis dauert länger", "meshroom"),
        ["CORE-08"] = N(PerfProfile.Basic, PerfProfile.Standard, "Vorschau in halber Auflösung, Export dauert länger", "ffmpeg"),
        ["SPEC-15"] = N(PerfProfile.Standard, PerfProfile.Standard, "niedrigere Streamauflösung", "obs"),
        ["STUDIO-MUSIK-01"] = N(PerfProfile.Standard, PerfProfile.Professional, "kürzere Lieder, Erzeugung dauert länger"),
        ["STUDIO-FILM-01"] = N(PerfProfile.Standard, PerfProfile.Professional, "Vorschau statt Endrender, Szenen einzeln rendern", "blender"),
        ["STUDIO-CONSOLE-01"] = N(PerfProfile.Standard, PerfProfile.Standard, "Bau dauert länger"),
        ["STUDIO-PODCAST-01"] = N(PerfProfile.Basic, PerfProfile.Standard, "", "ffmpeg"),
        ["CORE-02"] = N(PerfProfile.Basic, PerfProfile.Standard, "kleines KI-Modell (Qwen2.5 0.5B) statt des großen", "ollama"),
        ["CORE-03"] = N(PerfProfile.Standard, PerfProfile.Professional, "weniger KI-Rollen gleichzeitig", "ollama"),
        ["CORE-09"] = N(PerfProfile.Standard, PerfProfile.Standard, "niedrigere Bildrate der Kamera"),
        ["SPEC-14"] = N(PerfProfile.Basic, PerfProfile.Standard, "einfachere 3D-Vorschau"),
    };

    public static StudioNeed Get(string id) => ById.TryGetValue(id, out var n) ? n : N(PerfProfile.Basic, PerfProfile.Basic);

    public static StudioVerdict Verdict(string id, PerfProfile have, Func<string, bool> hasProgram, Func<string, string>? programName = null)
    {
        var n = Get(id);
        if (have < n.Min)
            return new(id, StudioFit.ZuSchwach, $"Dieser PC liegt unter dem Profil {PerformanceProfiles.Label(n.Min)}. Das Studio startet, wird aber sehr langsam sein.{Alt(n)}");
        var missing = n.Programs.Where(p => !hasProgram(p)).ToList();
        if (missing.Count > 0)
            return new(id, StudioFit.BrauchtProgramm, $"Braucht noch: {string.Join(", ", missing.Select(p => programName?.Invoke(p) ?? p))} (kostenlos, Installation nach deiner Zustimmung).");
        if (have < n.Recommended)
            return new(id, StudioFit.Eingeschraenkt, $"Läuft, für flüssiges Arbeiten wird {PerformanceProfiles.Label(n.Recommended)} empfohlen.{Alt(n)}");
        return new(id, StudioFit.Laeuft, "Läuft auf diesem PC.");
    }

    static string Alt(StudioNeed n) => n.Alternative.Length > 0 ? $" Tipp: {n.Alternative}." : "";
}

public enum AdviceAction { None, SmallKiModel, ReducedPreview, LimitParallel, CpuOnly }

/// <summary>Vorschlag zur Leistungsanpassung. Wird nur nach Zustimmung angewendet und entfernt keine Funktion.</summary>
public sealed record PerformanceAdvice(string Id, string Title, string Why, AdviceAction Action);

public static class PerformanceAdvisor
{
    const double G = 1024d * 1024 * 1024;

    public static List<PerformanceAdvice> Suggest(HardwareReport r, PerfProfile p)
    {
        var list = new List<PerformanceAdvice>();
        if (r.RamBytes / G < 15)
            list.Add(new("ki-klein", "Kleines KI-Modell verwenden", $"{r.RamBytes / G:0} GB Arbeitsspeicher: Qwen2.5 0.5B antwortet hier schneller als das große Modell.", AdviceAction.SmallKiModel));
        if (p <= PerfProfile.Basic)
            list.Add(new("vorschau", "Vorschau in geringerer Qualität", "3D- und Videovorschau in halber Auflösung, Export bleibt in voller Qualität.", AdviceAction.ReducedPreview));
        if (r.Cores < 6)
            list.Add(new("parallel", "Weniger Aufgaben gleichzeitig", $"{r.Cores} Prozessorkerne: höchstens zwei große Aufgaben gleichzeitig, damit der PC bedienbar bleibt.", AdviceAction.LimitParallel));
        if (r.Gpu is null || r.VramBytes == 0)
            list.Add(new("cpu", "Grafikbeschleunigung nicht nachgewiesen", "Keine Grafikkarte mit eigenem Speicher erkannt. Trendo rechnet dann mit dem Prozessor, das ist langsamer.", AdviceAction.CpuOnly));
        return list;
    }

    /// <summary>Wie viele große Aufgaben gleichzeitig laufen sollten.</summary>
    public static int ParallelLimit(HardwareReport r) => r.Cores < 6 ? 2 : r.Cores < 12 ? 3 : 4;
}

public static class CompatibilityCheck
{
    const double G = 1024d * 1024 * 1024;

    public static List<CheckItem> Items(HardwareReport r, Func<string, bool> hasProgram, bool? internet)
    {
        var basic = PerformanceProfiles.Spec(PerfProfile.Basic);
        var std = PerformanceProfiles.Spec(PerfProfile.Standard);
        CompatState Level(double v, double min, double rec) => v >= rec * 0.94 ? CompatState.Ok : v >= min * 0.94 ? CompatState.Warn : CompatState.Fail;
        var list = new List<CheckItem>
        {
            new("os", "Windows-Version", $"{r.Os} (Build {r.OsBuild})", r.IsWindows11 ? CompatState.Ok : CompatState.Warn,
                r.IsWindows11 ? "Windows 11 – unterstützt." : "Trendo ist für Windows 11 gebaut. Unter älteren Versionen ist es nicht geprüft."),
            new("arch", "Architektur", r.Arch, r.Arch == "X64" ? CompatState.Ok : CompatState.Warn, r.Arch == "X64" ? "64-Bit – unterstützt." : "Nur x64 ist geprüft. ARM-Geräte sind nicht getestet."),
            new("cpu", "Prozessor", $"{r.Cpu ?? "unbekannt"} · {r.Cores} Kerne", Level(r.Cores, basic.MinCores, std.MinCores), $"Basic ab {basic.MinCores}, Standard ab {std.MinCores} Kernen."),
            new("ram", "Arbeitsspeicher", $"{r.RamBytes / G:0} GB", Level(r.RamBytes / G, basic.MinRamGb, std.MinRamGb), $"Basic ab {basic.MinRamGb:0} GB, Standard ab {std.MinRamGb:0} GB."),
            new("disk", "Freier Speicherplatz", $"{r.DiskFreeBytes / G:0} von {r.DiskTotalBytes / G:0} GB", Level(r.DiskFreeBytes / G, basic.MinDiskFreeGb, std.MinDiskFreeGb), "Ob SSD oder Festplatte, ermittelt Trendo nicht. Eine SSD wird empfohlen."),
            new("gpu", "Grafikkarte", r.Gpu ?? "nicht erkannt", r.Gpu is null ? CompatState.Unknown : CompatState.Ok, r.Gpu is null ? "Keine Angaben gefunden. Trendo läuft trotzdem, 3D und KI werden langsamer." : "Erkannt."),
            new("vram", "Grafikspeicher", r.VramBytes > 0 ? $"{r.VramBytes / G:0.#} GB" : "unbekannt", r.VramBytes == 0 ? CompatState.Unknown : Level(r.VramBytes / G, 2, std.MinVramGb), $"Standard ab {std.MinVramGb:0} GB, Professional ab {PerformanceProfiles.Spec(PerfProfile.Professional).MinVramGb:0} GB."),
            new("driver", "Grafiktreiber", r.GpuDriver ?? "unbekannt", r.GpuDriver is null ? CompatState.Unknown : CompatState.Ok, "Aktuelle Treiber holst du beim Hersteller deiner Grafikkarte."),
            new("screen", "Bildschirm", r.ScreenWidth > 0 ? $"{r.ScreenWidth} × {r.ScreenHeight}" : "unbekannt", r.ScreenWidth == 0 ? CompatState.Unknown : r.ScreenWidth >= 1366 && r.ScreenHeight >= 768 ? CompatState.Ok : CompatState.Warn, "Empfohlen ab 1366 × 768, besser 1920 × 1080."),
            new("runtime", "Laufzeit (.NET)", Environment.Version.ToString(), CompatState.Ok, "Im Setup enthalten, nichts nachzuinstallieren."),
        };
        foreach (var (id, name) in new[] { ("ollama", "Lokale KI (Ollama)"), ("blender", "Blender"), ("ffmpeg", "FFmpeg"), ("unreal", "Unreal Engine") })
            list.Add(new("prog-" + id, name, hasProgram(id) ? "installiert" : "nicht installiert", hasProgram(id) ? CompatState.Ok : CompatState.Unknown, "Optional. Nur für bestimmte Studios nötig, Installation nach deiner Zustimmung."));
        list.Add(new("net", "Internet", internet switch { true => "erreichbar", false => "nicht erreichbar", null => "nicht geprüft" },
            internet switch { true => CompatState.Ok, false => CompatState.Warn, null => CompatState.Unknown }, "Nur für Onlinefunktionen, Updates und den Kauf nötig. Geprüft wird erst auf Knopfdruck."));
        return list;
    }
}

/// <summary>Liest die Gerätedaten nur lesend (Windows: Registrierung lesen, kein Schreiben; Linux: /proc). Nichts wird gesendet.</summary>
public static class HardwareScan
{
    public static HardwareReport Read(string dataRoot, int screenWidth, int screenHeight)
    {
        var os = RuntimeInformation.OSDescription.Trim();
        var build = Environment.OSVersion.Version.Build;
        var win11 = OperatingSystem.IsWindows() && Environment.OSVersion.Version.Major >= 10 && build >= 22000;
        string? cpu = null, gpu = null, driver = null; long vram = 0;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var c = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpu = (c?.GetValue("ProcessorNameString") as string)?.Trim();
                using var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                foreach (var n in cls?.GetSubKeyNames().Where(n => n.All(char.IsDigit)) ?? [])
                {
                    using var k = cls!.OpenSubKey(n);
                    var mem = k?.GetValue("HardwareInformation.qwMemorySize") is long l ? l : k?.GetValue("HardwareInformation.MemorySize") is int i ? (uint)i : 0;
                    if (mem > vram || gpu is null) { if (mem > vram) vram = mem; gpu = k?.GetValue("DriverDesc") as string ?? gpu; driver = k?.GetValue("DriverVersion") as string ?? driver; }
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { /* ohne Angaben weiter */ }
        }
        else if (File.Exists("/proc/cpuinfo"))
        {
            cpu = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim();
        }
        long free = 0, total = 0;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dataRoot));
            if (!string.IsNullOrEmpty(root)) { var d = new DriveInfo(root); free = d.AvailableFreeSpace; total = d.TotalSize; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        if (SystemMetrics.GpuMemory() is { } gm && gm.TotalGb > 0) vram = Math.Max(vram, (long)(gm.TotalGb * 1024 * 1024 * 1024));
        return new HardwareReport(os, build, win11, RuntimeInformation.OSArchitecture.ToString(), cpu, Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, free, total, gpu, vram, driver, screenWidth, screenHeight);
    }
}
