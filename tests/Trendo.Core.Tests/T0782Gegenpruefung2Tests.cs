using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0782, 2. Gegenprüfung (Kern): M4-Rest (ganzer AC-Ordner, Registry-Speicher, freier Platz), M6-Rest (Anzeige-Filter), M7-Erweiterung,
/// Umlaut-Richtung, „1.234“, ProfileExists über die Registry. Alle echten Läufe in einem eigenen, kurzlebigen Test-Profil.</summary>
[Collection("T0782-Sandbox")]
public class T0782_Gegenpruefung2 : T0782ProfilTest
{
    public sealed class PyFactAttribute : FactAttribute { public PyFactAttribute() { if (!OperatingSystem.IsWindows() || LernSandbox.BundledPython() is null) Skip = "Übersprungen – mitgeliefertes Python fehlt auf diesem PC."; } }
    string Py(out string root) { root = Path.Combine(Path.GetTempPath(), "trendo-t0782-gp2-" + Guid.NewGuid().ToString("N")[..6]); return LernSandbox.PrepareRuntime(root).Exe!; }
    static string Work(string root) { var w = Path.Combine(root, "lauf-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(w); return w; }
    static void Clean(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    [PyFact(DisplayName = "M4-Rest ECHT: ganzer AC-Ordner (INetCache, Microsoft, neue Unterordner) nach dem Lauf leer, Standardordner bleiben; Größe im ganzen AC-Ordner begrenzt")]
    public void M4_whole_ac_folder()
    {
        var py = Py(out var root);
        try
        {
            Assert.NotEqual(LernSandbox.ContainerName, LernSandbox.Current); // nie das echte Profil
            var ac = LernSandbox.ContainerFolder(LernSandbox.Sid())!;
            Directory.CreateDirectory(Path.Combine(ac, "INetCache")); File.WriteAllText(Path.Combine(ac, "INetCache", "alt.bin"), "alt");
            var w = Work(root);
            File.WriteAllText(Path.Combine(w, "p.py"), "import os, sys\nac = sys.argv[1]\nfor d in ('Microsoft', 'Neu', 'INetCache'):\n    os.makedirs(os.path.join(ac, d), exist_ok=True)\n    open(os.path.join(ac, d, 'x.bin'), 'wb').write(b'q' * 1000)\nprint('ok')\n");
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py", ac], w, 15_000);
            Assert.Contains("ok", r.Out + r.Err);
            Assert.False(Directory.Exists(Path.Combine(ac, "Neu")));
            Assert.Empty(Directory.EnumerateFiles(ac, "*", SearchOption.AllDirectories));
            var w2 = Work(root);
            File.WriteAllText(Path.Combine(w2, "p.py"), "import os, sys\np = os.path.join(sys.argv[1], 'Microsoft')\nos.makedirs(p, exist_ok=True)\nwith open(os.path.join(p, 'gross.bin'), 'wb') as f:\n    for i in range(200):\n        f.write(b'z' * (1 << 20))\n");
            Assert.True(LernSandbox.Run(py, ["-I", "-B", "p.py", ac], w2, new LernSandbox.Limits(TimeoutMs: 30_000, DiskBytes: 20L << 20)).OutputLimit);
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "M4-Rest ECHT: Registry-Speicher des Containers – Schreiben über GetAppContainerRegistryLocation wird begrenzt, danach ist der Speicher leer")]
    public void M4_registry_storage()
    {
        var py = Py(out var root);
        try
        {
            const string probe = "import ctypes, ctypes.wintypes as wt\nh = wt.HKEY()\nr = ctypes.windll.userenv.GetAppContainerRegistryLocation(0xF003F, ctypes.byref(h))\nprint('REG', r, flush=True)\nadv = ctypes.windll.advapi32\n"
                + "sub = wt.HKEY()\nadv.RegCreateKeyExW(h, 'Probe', 0, None, 0, 0xF003F, None, ctypes.byref(sub), None)\ndata = b'r' * 65536\nimport time\nfor i in range(200):\n    adv.RegSetValueExW(sub, 'v%d' % i, 0, 3, data, len(data))\nprint('GESCHRIEBEN', flush=True)\ntime.sleep(5)\n";
            var w = Work(root); File.WriteAllText(Path.Combine(w, "p.py"), probe);
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, new LernSandbox.Limits(TimeoutMs: 20_000, RegistryBytes: 1L << 20));
            Assert.Contains("REG 0", r.Out + r.Err);
            Assert.True(r.OutputLimit, r.Text + " | " + r.Out + r.Err);
            Assert.Equal(0, LernSandbox.RegistryBytes());
        }
        finally { Clean(root); }
    }

    [PyFact(DisplayName = "M4-Rest ECHT: sinkender freier Platz des Laufwerks bricht ab (Dateigrenze hier absichtlich hoch)")]
    public void M4_drive_drop()
    {
        var py = Py(out var root);
        try
        {
            var w = Work(root);
            File.WriteAllText(Path.Combine(w, "p.py"), "import os\nwith open('wachsend.bin', 'wb') as f:\n    for i in range(400):\n        f.write(os.urandom(1 << 20))\n        f.flush()\n");
            var r = LernSandbox.Run(py, ["-I", "-B", "p.py"], w, new LernSandbox.Limits(TimeoutMs: 60_000, DiskBytes: 4L << 30, DriveDropBytes: 50L << 20));
            Assert.True(r.OutputLimit, r.Text);
        }
        finally { Clean(root); }
    }

    [Fact(DisplayName = "M6-Rest: Anzeige-Filter verdeckt Antwort/Lösungsweg/Hinweis in Plan-Texten und hält Texte mit der Lösung der offenen Übung zurück")]
    public void M6_display_filter()
    {
        var d = new ProgramDocument("STUDIO-TUTOR-01", "T", Nachhilfe.Neu("mathe", 6)); Nachhilfe.ThemaWaehlen(d, "m-kuerzen");
        var u = Nachhilfe.NaechsteUebung(d, new Random(3)); var soll = u["antworten"]![0]!.GetValue<string>();
        var step = "Übung erzeugen – Frage: Was ist 6 · 7?, Antwort: 42, Lösungsweg: sechs mal sieben, Hinweis: Zähle in Siebenern";
        var shown = Nachhilfe.FuerAnzeige(d, step);
        Assert.Contains("Frage: Was ist 6 · 7?", shown); Assert.DoesNotContain("42", shown); Assert.DoesNotContain("Siebenern", shown);
        Assert.Equal("(ausgeblendet – darin stünde eine Lösung)", Nachhilfe.FuerAnzeige(d, "Die Lösung ist " + soll));
        Assert.Equal("Denk an den ggT.", Nachhilfe.FuerAnzeige(d, "Denk an den ggT."));
        Assert.DoesNotContain(soll, Nachhilfe.FuerAnzeige(d, u["frage"]!.GetValue<string>() + " – sieh dir die Aufgabe an")); // Zahlen der Aufgabe gelten nicht als Lösung
        Assert.Equal(u["frage"]!.GetValue<string>(), Nachhilfe.FuerAnzeige(d, u["frage"]!.GetValue<string>()));
    }

    [Fact(DisplayName = "M7 erweitert: „drei Viertel“, 0,75 und 6/8 bei Brüchen, „zweieinhalb“; Bruch aus der Aufgabe zählt nicht")]
    public void M7_more()
    {
        Assert.True(Nachhilfe.VerraetLoesung("Das sind drei Viertel.", ["3/4"]));
        Assert.True(Nachhilfe.VerraetLoesung("Das ist 0,75.", ["3/4"]));
        Assert.True(Nachhilfe.VerraetLoesung("Also 6/8.", ["3/4"]));
        Assert.True(Nachhilfe.VerraetLoesung("Es sind zweieinhalb.", ["2,5"]));
        Assert.False(Nachhilfe.VerraetLoesung("Kürze 12/18 Schritt für Schritt.", ["2/3"], "Kürze so weit wie möglich: 12/18 = ?"));
        Assert.True(Nachhilfe.VerraetLoesung("Kürze 12/18 zu 4/6.", ["2/3"], "Kürze so weit wie möglich: 12/18 = ?"));
    }

    [Fact(DisplayName = "Niedrig: ue → ü nur in Richtung Eingabe („Baür“ ist nicht „Bauer“), „1.234“ zählt nicht als Versuch")]
    public void Umlaut_and_thousands()
    {
        Assert.False(Nachhilfe.Vergleiche(new("x", ["Bauer"], "", []), "Baür").Richtig);
        Assert.True(Nachhilfe.Vergleiche(new("x", ["die Bäume"], "", []), "die Baeume").Richtig);
        var t = Nachhilfe.Vergleiche(new("x", ["1234"], "", [], "zahl"), "1.234"); Assert.False(t.Richtig); Assert.False(t.Zaehlt);
    }

    [PyFact(DisplayName = "ProfileExists: auch über die Registry-Zuordnung; eigenes Test-Profil angelegt und wieder entfernt")]
    public void Profile_exists_registry()
    {
        var name = Profil + "x";
        LernSandbox.Sid(name);
        try
        {
            Assert.True(LernSandbox.ProfileExists(name));
            var sid = LernSandbox.Sid(name);
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Mappings\" + sid);
            Assert.NotNull(k);
        }
        finally { LernSandbox.RemoveProfile(name); }
        Assert.False(LernSandbox.ProfileExists(name));
    }
}
