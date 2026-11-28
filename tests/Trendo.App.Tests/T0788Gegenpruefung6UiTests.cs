using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788, 5. Prüfrunde: Junction auf Claudes Ordner umgeht die Erkennung nicht; beim Verlegen bleibt eine fremde „….tdoc.notiz.tmp“ stehen. Claude ersetzt.</summary>
[Collection("Claude")]
public class T0788Gegenpruefung6_Ui
{
    const string Fam = MainWindow.FamKey, Studio = MainWindow.FamStudio;

    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static bool Inside(string folder, string? file) => file is not null && Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    static bool Junction(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "/d", "/c", "mklink", "/J", link, target }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!; p.WaitForExit(30000);
        return p.ExitCode == 0 && Directory.Exists(link);
    }

    [AvaloniaFact(DisplayName = "T-0788 R5 MITTEL: „Speichern unter“ über eine Junction auf Claudes Ordner legt den geschützten Stammbaum nicht dort ab")]
    public void Junction_save_as()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Junctions nur unter Windows."); return; }
        var (w, _, _) = Ui.Open();
        var link = Path.Combine(Path.GetTempPath(), "trendo-t0788-junction-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
            Assert.True(w.FamProtected(d));
            var claudeDir = w.ClaudeChatRoot(Studio); Directory.CreateDirectory(claudeDir);
            Assert.True(Junction(link, claudeDir), "Junction ließ sich nicht anlegen");
            var viaLink = Path.Combine(link, "x.tdoc");
            w.ProgramSave(Fam, viaLink);
            Assert.False(File.Exists(viaLink));
            Assert.False(File.Exists(Path.Combine(claudeDir, "x.tdoc")));
            Assert.False(Inside(Path.GetDirectoryName(claudeDir)!, d.Path), d.Path);
            Assert.False(Inside(link, d.Path), d.Path);
            Assert.Contains("liegt jetzt in", w.ProgramMessage(Fam));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link); // nur die selbst angelegte Junction (nicht rekursiv – das Ziel bleibt unberührt)
            w.Close();
        }
    }

    [AvaloniaFact(DisplayName = "T-0788 R5 NIEDRIG/MITTEL: Verlegen entfernt nur eigene Zwischendateien – eine fremde „….tdoc.notiz.tmp“ bleibt unverändert")]
    public async Task Foreign_tmp_kept()
    {
        var folders = new List<string>();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, cwd, line, _) => { folders.Add(cwd); line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1 })); return Task.FromResult(0); };
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            await Run(w.ClaudeCheck(true));
            var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze Erikas Geschwister"));
            for (var i = 0; i < 10; i++) Settle();
            var p = d.Path!; Assert.True(Inside(folders[0], p));
            var foreign = p + ".notiz.tmp"; var own = p + ".0a1b2c3d.tmp";
            File.WriteAllText(foreign, "fremde Notiz"); File.WriteAllText(own, "Rest");
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.Equal("fremde Notiz", File.ReadAllText(foreign));
            Assert.False(File.Exists(own));
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStepDelay = 350; w.Close(); }
    }
}
