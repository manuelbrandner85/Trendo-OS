using System.Runtime.InteropServices;
using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Shell;
using Trendo.Core.Sys;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0757: niedrige Restbefunde aus den Prüfungen von 0.9.108 (T-0752 Konsolenspiele, T-0754 Dateien und Schutz, T-0755 Gefahren prüfen).
/// Diese Datei nutzt nur Schnittstellen, die es schon auf main (24bb4f70e) gibt – sie läuft dort rot. Neue Schnittstellen: <see cref="T0757NeuTests"/>.</summary>
public class T0757RestpunkteTests(ITestOutputHelper output)
{
    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0757-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static FreePort.Title Own => FreePort.Find(FreePort.OwnId)!;

    /// <summary>Job mit allen Feldern; fehlt auf main die Bestätigung, gibt es den Parameter dort nicht (Reflexion, damit die Datei auf main übersetzt).</summary>
    internal static FreePort.Job MakeJob(FreePort.Title t, string elf, string lic, string recomp, string work)
    {
        var c = typeof(FreePort.Job).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
        object?[] given = [t, elf, lic, recomp, (IReadOnlyList<string>)[], work];
        return (FreePort.Job)c.Invoke(c.GetParameters().Select((p, i) => i < given.Length ? given[i] : p.ParameterType == typeof(bool) ? true : p.DefaultValue).ToArray());
    }

    // ---------- (A) Konsolenspiele ----------

    [Fact(DisplayName = "T-0757 R1: angehängter MIT-Satz in einer sonst unfreien Datei (Weitergabe-/Kopierverbot, vertraulich) gilt nicht als MIT")]
    public void R1_mit_in_unfreier_datei()
    {
        var r = Root(); var elf = FreePortTests.Elf(r);
        foreach (var head in new[]
        {
            "Diese Software darf nicht weitergegeben werden. Kopieren verboten.",
            "Diese Software wird nicht weitergegeben.",
            "CONFIDENTIAL – internal use only.",
            "Vertraulich. Nur für den Dienstgebrauch.",
            "All copying prohibited.",
        })
        {
            var lic = Path.Combine(r, "LICENSE"); File.WriteAllText(lic, head + "\n\n" + FreePortTests.Mit);
            var res = FreePort.CheckLicense(lic, Own, elf);
            Assert.False(res.Ok, head);
            Assert.Contains("keine freie Lizenz", res.Text);
        }
    }

    /// <summary>Abschnitte „# name … # END: name“ aus THIRD-PARTY-NOTICES der SkiaSharp-/HarfBuzzSharp-Pakete.</summary>
    static string? Section(string notices, string name)
    {
        var lines = File.ReadAllLines(notices);
        var a = Array.FindIndex(lines, l => l.Trim() == "# " + name); var b = Array.FindIndex(lines, l => l.Trim() == "# END: " + name);
        return a >= 0 && b > a ? string.Join("\n", lines[(a + 2)..(b - 1)]) : null;
    }

    [Fact(DisplayName = "T-0757 R1 (echte Dateien): echte MIT-, BSD-, Apache-2.0- und zlib-Texte aus dem NuGet-Cache gelten weiter als frei")]
    public void R1_echte_lizenzen_aus_nuget()
    {
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } n ? n : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        if (!Directory.Exists(cache)) { Ueberspringen.Weil("kein NuGet-Cache auf diesem PC."); return; }
        var r = Root(); var elf = FreePortTests.Elf(r);
        var texts = new List<(string Want, string From, string Text)>();
        void Add(string want, string from, string? text) { if (text is not null) texts.Add((want, from, text)); }
        // nur lesen: Text in den eigenen Temp-Ordner kopieren
        foreach (var f in new[] { "newtonsoft.json/13.0.3/LICENSE.md", "communitytoolkit.mvvm/8.4.2/License.md", "skiasharp/3.119.4/LICENSE.txt", "microsoft.win32.registry/5.0.0/LICENSE.TXT", "mqttnet/5.2.0.1603/LICENSE", "unicode.net/2.0.0/LICENSE" })
            if (File.Exists(Path.Combine(cache, f))) Add("MIT", f, File.ReadAllText(Path.Combine(cache, f)));
        foreach (var f in Directory.EnumerateDirectories(cache, "avalonia.angle.windows.natives").SelectMany(d => Directory.EnumerateFiles(d, "LICENSE", SearchOption.AllDirectories)).Take(1))
            Add("BSD", "avalonia.angle.windows.natives/…/LICENSE", File.ReadAllText(f));
        var notices = Directory.EnumerateDirectories(cache, "harfbuzzsharp.nativeassets.win32").Concat(Directory.EnumerateDirectories(cache, "skiasharp.nativeassets.win32"))
            .SelectMany(d => Directory.EnumerateFiles(d, "THIRD-PARTY-NOTICES.txt", SearchOption.AllDirectories)).FirstOrDefault();
        if (notices is not null)
        {
            Add("Apache-2.0", "THIRD-PARTY-NOTICES.txt # piex", Section(notices, "piex"));
            Add("Apache-2.0", "THIRD-PARTY-NOTICES.txt # sfntly", Section(notices, "sfntly"));
            Add("zlib", "THIRD-PARTY-NOTICES.txt # sdl", Section(notices, "sdl"));
        }
        Assert.Contains(texts, t => t.Want == "MIT");
        foreach (var (want, from, text) in texts)
        {
            var dir = Path.Combine(r, Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
            var lic = Path.Combine(dir, "LICENSE"); File.WriteAllText(lic, text);
            var res = FreePort.CheckLicense(lic, Own, Path.Combine(dir, "x.elf"));
            output.WriteLine($"{from}: {(res.Ok ? "frei " + res.Text : "abgewiesen: " + res.Text)}");
            Assert.True(res.Ok && res.Text == want, $"{from}: erwartet {want}, war {res.Text}");
        }
        output.WriteLine($"{texts.Count} echte Lizenztexte geprüft ({string.Join(", ", texts.GroupBy(t => t.Want).Select(g => g.Key + " " + g.Count()))}).");
    }

    [Fact(DisplayName = "T-0757 R2: eigene SYSTEM.CNF eines Homebrew-Projekts (BOOT2 = cdrom0:\\MEINSPIEL.ELF;1) ist erlaubt, Disc-Kennungen nicht")]
    public void R2_eigene_system_cnf()
    {
        var r = Root();
        var proj = Path.Combine(r, "meinspiel"); var elf = FreePortTests.Elf(proj, "MEINSPIEL.ELF");
        File.WriteAllText(Path.Combine(proj, "SYSTEM.CNF"), "BOOT2 = cdrom0:\\MEINSPIEL.ELF;1\nVER = 1.00\nVMODE = PAL\n");
        Assert.Null(FreePort.CheckElf(elf));
        File.WriteAllText(Path.Combine(proj, "SYSTEM.CNF"), "BOOT2=cdrom0:MEINSPIEL.ELF;1\n");
        Assert.Null(FreePort.CheckElf(elf));
        foreach (var id in new[] { "SLUS_203.12", "SCES-501.23", "slpm_652.34", "PBPX_952.01", "TCES_123.45" })
        {
            File.WriteAllText(Path.Combine(proj, "SYSTEM.CNF"), $"BOOT2 = cdrom0:\\{id};1\nVER = 1.00\n");
            var m = FreePort.CheckElf(elf);
            Assert.NotNull(m); Assert.Contains("SYSTEM.CNF", m); Assert.Contains("Kennung einer Spiel-Disc", m);
        }
    }

    [Fact(DisplayName = "T-0757 R3: eine untergeschobene trendo-einsortiert.txt im PS2Recomp-Ordner löscht keine PS2Recomp-Datei, auch mit richtiger Prüfsumme")]
    public async Task R3_untergeschobene_liste_loescht_nichts()
    {
        var r = Root(); var recomp = FreePortTests.Recomp(r);
        var runner = Path.Combine(recomp, "ps2xRuntime", "src", "runner"); Directory.CreateDirectory(runner);
        var victim = Path.Combine(runner, "ps2_runtime_teil.cpp"); File.WriteAllText(victim, "// gehört PS2Recomp");
        File.WriteAllText(Path.Combine(recomp, "ps2xRuntime", FreePort.Manifest), $"src/runner/ps2_runtime_teil.cpp\t{FreePort.Sha256(victim)}\n");
        var proj = Path.Combine(r, "projekt");
        var lic = Path.Combine(proj, "LICENSE"); Directory.CreateDirectory(proj); File.WriteAllText(lic, FreePortTests.Mit);
        var o = await FreePort.RunAsync(MakeJob(Own, FreePortTests.Elf(proj), lic, recomp, Path.Combine(r, "arbeit")), FreePortTests.AllThere, FreePortTests.FakeTools([]), null, null, CancellationToken.None);
        output.WriteLine($"{o.Status}: {o.Message}");
        Assert.True(File.Exists(victim), "PS2Recomp-Datei wurde gelöscht");
        Assert.Equal("// gehört PS2Recomp", File.ReadAllText(victim));
    }

    // ---------- (B) Dateien und Schutz – echte Windows-Einstellungen, nur lesend ----------

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle h, uint code, IntPtr inBuf, uint inSize, IntPtr outBuf, uint outSize, out uint returned, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumeNameForVolumeMountPointW")]
    static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, char[] volumeName, uint size);

    /// <summary>Datei mit großer Länge, die keinen Platz belegt (NTFS, sparse).</summary>
    internal static string Sparse(string path, long length)
    {
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        if (!DeviceIoControl(fs.SafeFileHandle, 0x000900C4 /* FSCTL_SET_SPARSE */, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero)) throw new IOException("sparse geht nicht: " + Marshal.GetLastWin32Error());
        fs.SetLength(length);
        return path;
    }

    static string? Junction(string link, string target)
    {
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit(10_000);
        return Directory.Exists(link) ? link : null;
    }

    /// <summary>Ist auf diesem PC für das Laufwerk der Temp-Ordner MaxCapacity oder „sofort löschen“ gesetzt? Dann gilt die Voreinstellung nicht.</summary>
    static string? NotDefault(string dir)
    {
        if (FileBrowser.WindowsTrashProblem(Path.Combine(dir, "nichts.txt")) is { } p) return p;
        var buf = new char[64];
        if (!GetVolumeNameForVolumeMountPoint(Path.GetPathRoot(Path.GetFullPath(dir))!, buf, (uint)buf.Length)) return "Kennung des Laufwerks nicht lesbar";
        var name = new string(buf).TrimEnd('\0'); var guid = name[name.IndexOf('{')..(name.IndexOf('}') + 1)];
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\" + guid);
        return k?.GetValue("MaxCapacity") is int ? "MaxCapacity für dieses Laufwerk gesetzt" : null;
    }

    static long Formula(long total) => Math.Min(40L << 30, total) / 10 + Math.Max(0, total - (40L << 30)) / 20;

    [Fact(DisplayName = "T-0757 B (echt, nur lesend): ohne MaxCapacity gilt die Windows-Formel 10 % der ersten 40 GB + 5 % vom Rest, nicht 5 %")]
    public void B_windows_formel_echt()
    {
        if (!OperatingSystem.IsWindows()) return;
        var r = Root();
        try
        {
            if (NotDefault(r) is { } why) { Ueberspringen.Weil("Papierkorb hier nicht auf Voreinstellung: " + why); return; }
            var total = new DriveInfo(Path.GetPathRoot(r)!).TotalSize;
            long lo = total / 20, hi = Formula(total);
            output.WriteLine($"Laufwerk {total >> 30} GB: 5 % = {lo >> 20} MB, Formel = {hi >> 20} MB");
            Assert.True(hi > lo, "Laufwerk kleiner als 40 GB – die Formel ergibt dort mehr als 5 %, nicht weniger");
            var f = Sparse(Path.Combine(r, "zwischen.bin"), (lo + hi) / 2);
            Assert.Null(FileBrowser.WindowsTrashProblem(f)); // passt in den echten Papierkorb
            var g = Sparse(Path.Combine(r, "zu-gross.bin"), hi + (1L << 30));
            Assert.Contains("größer als der Papierkorb", FileBrowser.WindowsTrashProblem(g));
            Assert.True(File.Exists(f) && File.Exists(g)); // nichts gelöscht
        }
        finally { Directory.Delete(r, true); }
    }

    [Fact(DisplayName = "T-0757 B (echt): eine Junction als Element zählt mit Größe 0 – der Inhalt des Ziels wird nicht mitgezählt")]
    public void B_junction_groesse_null()
    {
        if (!OperatingSystem.IsWindows()) return;
        var r = Root();
        try
        {
            var ziel = Directory.CreateDirectory(Path.Combine(r, "ziel")).FullName;
            File.WriteAllBytes(Path.Combine(ziel, "a.bin"), new byte[1000]);
            var link = Junction(Path.Combine(r, "verknuepfung"), ziel);
            Assert.NotNull(link);
            var fb = new FileBrowser { Root = r, RecycleBinPolicy = _ => new FileBrowser.BinPolicy(false, 100) };
            Assert.Contains("größer", fb.TrashProblem(ziel)); // Ziel selbst: 1000 Bytes > 100
            Assert.Null(fb.TrashProblem(link!)); // nur die Verknüpfung wandert in den Papierkorb
            // mit den echten Einstellungen dieses PCs: Ziel größer als der Papierkorb, Verknüpfung nicht
            if (NotDefault(r) is null)
            {
                var big = Directory.CreateDirectory(Path.Combine(r, "gross")).FullName;
                Sparse(Path.Combine(big, "riesig.bin"), Formula(new DriveInfo(Path.GetPathRoot(r)!).TotalSize) + (1L << 30));
                var bigLink = Junction(Path.Combine(r, "gross-verknuepfung"), big)!;
                Assert.Contains("größer als der Papierkorb", FileBrowser.WindowsTrashProblem(big));
                Assert.Null(FileBrowser.WindowsTrashProblem(bigLink));
            }
            Assert.True(File.Exists(Path.Combine(ziel, "a.bin")));
        }
        finally
        {
            foreach (var l in new[] { "verknuepfung", "gross-verknuepfung" }) if (Directory.Exists(Path.Combine(r, l))) Directory.Delete(Path.Combine(r, l)); // nur die Verknüpfung
            Directory.Delete(r, true);
        }
    }

    // ---------- (C) Gefahren prüfen ----------

    static readonly SignatureInfo Ms = new(SigState.Gueltig, "Microsoft Corporation", "Gültig signiert.");
    const string B64 = "SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkA";
    static string Sys(string f) => Path.Combine(Environment.SystemDirectory, f);
    static StartEntry Run(string cmd) => new(SystemScan.KindRun, "Test", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", cmd, SystemScan.FileFromCommand(cmd));

    [Fact(DisplayName = "T-0757 C: cmd mit ^ – powershell^.exe, p^owershell, -e^nc und Base64 mit ^ sind rot „Versteckter Befehl“")]
    public void C_caret_rot()
    {
        if (!OperatingSystem.IsWindows()) return;
        var b64c = B64[..10] + "^" + B64[10..20] + "^^" + B64[20..];
        foreach (var cmd in new[]
        {
            $"{Sys("cmd.exe")} /c powershell^.exe -enc {B64}",
            $"{Sys("cmd.exe")} /c p^owershell -enc {B64}",
            $"{Sys("cmd.exe")} /c powershell -e^nc {B64}",
            $"{Sys("cmd.exe")} /c powershell -enc {b64c}",
            $"{Sys("cmd.exe")} /c ^p^o^w^e^r^s^h^e^l^l -^e^n^c {b64c}",
            $"{Sys("cmd.exe")} /c start \"\" /min p^wsh -e {B64}",
        })
        {
            var f = SystemScan.Assess(Run(cmd), Ms);
            Assert.True(f.Light == Ampel.Rot && f.Reason == "Versteckter Befehl", $"{cmd} → {f.Light} {f.Reason}");
        }
    }

    [Fact(DisplayName = "T-0757 C: dotnet tool --shell powershell -e … ist kein „Versteckter Befehl“; cmd /c echo powershell -e … seit Fix3 bewusst rot")]
    public void C_kein_fehlalarm()
    {
        if (!OperatingSystem.IsWindows()) return;
        // T-0757 Fix3: echo ist keine Ausnahme mehr – bewusster Fehlalarm, sichere Seite
        Assert.Equal("Versteckter Befehl", SystemScan.Assess(Run($"{Sys("cmd.exe")} /c echo powershell -e {B64}"), Ms).Reason);
        Assert.Equal("Versteckter Befehl", SystemScan.Assess(Run($"{Sys("cmd.exe")} /c echo powershell -enc {B64} > notiz.txt"), Ms).Reason);
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        var file = File.Exists(dotnet) ? dotnet : Sys("kernel32.dll");
        var e = new StartEntry(SystemScan.KindRun, "Test", "HKCU", $"\"{dotnet}\" tool --shell powershell -e {B64}", file);
        Assert.NotEqual("Versteckter Befehl", SystemScan.Assess(e, Ms).Reason);
        // echte Starts bleiben rot
        foreach (var cmd in new[] { $"{Sys("cmd.exe")} /c echo x & powershell -enc {B64}", $"{Sys("cmd.exe")} /k powershell -enc {B64}", $"{Sys("cmd.exe")} /c echo x | powershell -enc {B64}" })
            Assert.Equal("Versteckter Befehl", SystemScan.Assess(Run(cmd), Ms).Reason);
    }
}
