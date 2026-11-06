using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Automation;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>n8n N1–N7 (Freigabe 06.11.2026): Profi-Ebene in „Abläufe“ als Agenten-Arbeitsplatz – Einrichten nur nach Ja, Vorlage als Fluss ansehen, nach Ja anlegen, Claudes Datei erkennen, Ereignis an Webhook auf diesem PC.</summary>
public class N8nWorkspace_Ui
{
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); Settle(); }

    [AvaloniaFact(DisplayName = "Abläufe Profi: n8n einrichten nur nach Ja, starten, Vorlage als Fluss mit Nach-außen-Hinweis, Nein verwirft, Ja speichert .n8n.json; Claude-Datei erscheint; Probe-Ereignis geht an den lokalen Webhook")]
    public void Pro_mode_flow()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var installs = new List<string>(); var posts = new List<(string Url, string? Body)>();
        w.AutomationInstallOverride = (pkg, dir) => { installs.Add(pkg); var exe = N8n.Exe(dir, pkg == "node-red" ? "node-red" : "n8n"); Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, ""); return Task.FromResult(true); };
        w.AutomationHttpOverride = (m, url, body) => { if (m == "POST") posts.Add((url, body)); return Task.FromResult<string?>(url.Contains("/webhook/") ? "{}" : null); };
        w.Show("ablaeufe"); Settle();
        Click(w, "wf-mode-pro"); Assert.True(w.WfPro); Assert.NotNull(V<Button>(w, "n8n-install"));
        // N1: Einrichten fragt erst – Nein lädt nichts
        Click(w, "n8n-install"); Click(w, "n8n-ask-no"); Assert.Empty(installs);
        Click(w, "n8n-install"); Click(w, "n8n-ask-yes"); Assert.Equal(["n8n"], installs); Assert.True(w.N8nInstalled);
        Click(w, "n8n-start"); Assert.True(w.N8nRunning); Assert.NotNull(V<Button>(w, "n8n-stop"));
        // N4: Vorlage nach außen → Hinweis, Nein verwirft
        var ext = N8n.Templates.First(t => t.External);
        Click(w, "n8n-tpl-" + ext.Id); Assert.NotNull(V<Control>(w, "n8n-flow")); Assert.Contains("nach außen", (string)V<Button>(w, "n8n-import").Content!);
        Click(w, "n8n-import-no"); Assert.Null(w.TryFind<Button>("n8n-import"));
        // Ja ohne n8n-Schlüssel: als Datei gespeichert
        var local = N8n.Templates.First(t => !t.External);
        Click(w, "n8n-tpl-" + local.Id); Click(w, "n8n-import");
        var saved = Directory.EnumerateFiles(root, "*.n8n.json", SearchOption.AllDirectories).ToList();
        Assert.Single(saved); Assert.Empty(N8n.Check(File.ReadAllText(saved[0])).Problems);
        Assert.Contains("Gespeichert", w.Find<TextBlock>("n8n-msg").Text);
        // N5: Claudes Datei wird erkannt, Platzhalter ersetzt
        Assert.Contains(w.N8nClaudeFiles(), f => f == saved[0]);
        // N3: Ereignis nur an abonnierten Webhook auf diesem PC
        new N8n.Hooks(root).Add("test", N8n.WebhookUrl("trendo-test"));
        Click(w, "n8n-event-test");
        Assert.Contains(posts, p => p.Url == N8n.WebhookUrl("trendo-test") && p.Body!.Contains("Probe aus Trendo"));
        Click(w, "n8n-stop"); Assert.False(w.N8nRunning);
        Click(w, "wf-mode-simple"); Assert.False(w.WfPro);
    }
}
