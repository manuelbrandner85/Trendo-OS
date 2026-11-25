using System.Runtime.InteropServices;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0763 Fix 3 (H1): Selbstheilung ersetzt Dateien statt sie zu überschreiben – ein Hardlink auf eine fremde Datei bleibt unberührt.
public class T0763Fix3Tests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0763h-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CreateHardLinkW(string newName, string existing, IntPtr security);

    [Fact(DisplayName = "T-0763 H1: tools.json und selbstheilung-letzte.json als Hardlink auf eine fremde Datei – Selbstheilung ersetzt, die fremde Datei bleibt unverändert")]
    public void H1_hardlinked_files_are_replaced_not_overwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "audit", "audit.jsonl"));
        foreach (var f in SelfHealer.Folders) Directory.CreateDirectory(Path.Combine(root, f));
        var outside = Path.Combine(Tmp(), "fremd-tools.json");
        File.WriteAllText(outside, "{\"blender\":\"/gibt/es/nicht/blender.exe\"}");
        Assert.True(CreateHardLinkW(Path.Combine(root, "tools.json"), outside, IntPtr.Zero), "Hardlink ließ sich nicht anlegen");
        var outside2 = Path.Combine(Tmp(), "fremd-letzte.json"); File.WriteAllText(outside2, "{\"fremd\":true}");
        Assert.True(CreateHardLinkW(Path.Combine(root, "logs", "selbstheilung-letzte.json"), outside2, IntPtr.Zero));
        var r = new SelfHealer(root, audit) { FreeSpace = _ => 100L << 30 }.Run(true, DateTime.Now);
        Assert.Equal(HealState.Repaired, r.Single(x => x.Id == "tools").State);
        Assert.Equal("{\"blender\":\"/gibt/es/nicht/blender.exe\"}", File.ReadAllText(outside)); // fremde Datei unverändert
        Assert.Equal("{\"fremd\":true}", File.ReadAllText(outside2));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(root, "tools.json"))); // eigene Datei ersetzt
        Assert.NotNull(SelfHealer.Last(root));
    }
}
