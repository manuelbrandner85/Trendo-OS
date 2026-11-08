using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Automation;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>n8n in Trendo (N1–N7): Einrichten nur lokal, Trendo KI, Vorlagen, Umwandlung der Trendo-Abläufe, Prüfung (auch für Claude), Ereignisse nur auf diesem PC.</summary>
public class N8nTests
{
    [Fact(DisplayName = "Abläufe n8n N1/N7: Einrichten in eigenen Ordner ohne globale Installation, Start nur auf 127.0.0.1 ohne Telemetrie; Node-RED ebenfalls nur lokal (Apache-2.0)")]
    public void Setup_local_only()
    {
        var args = N8n.InstallArgs("C:/werkzeuge/n8n");
        Assert.Equal("install", args[0]); Assert.Contains("--prefix", args); Assert.DoesNotContain("-g", args); Assert.DoesNotContain("--global", args);
        var env = N8n.StartEnv("C:/daten");
        Assert.Equal(("127.0.0.1", "false", "C:/daten"), (env["N8N_LISTEN_ADDRESS"], env["N8N_DIAGNOSTICS_ENABLED"], env["N8N_USER_FOLDER"]));
        Assert.Contains("uiHost: \"127.0.0.1\"", N8n.NodeRedSettings); Assert.Equal("Apache-2.0", N8n.NodeRedLicense);
        Assert.EndsWith(OperatingSystem.IsWindows() ? "n8n.cmd" : "n8n", N8n.Exe("x"));
    }

    [Fact(DisplayName = "Node-RED Sicherheit (08.11.2026): Einrichten ohne Installationsskripte; Herkunftsprüfung für Verwaltung und Ablauf-Adressen; n8n unverändert")]
    public void NodeRed_hardened()
    {
        Assert.Contains("--ignore-scripts", N8n.InstallArgs("C:/werkzeuge/node-red", "node-red"));
        Assert.DoesNotContain("--ignore-scripts", N8n.InstallArgs("C:/werkzeuge/n8n"));
        var s = N8n.NodeRedSettings;
        Assert.Contains("httpAdminMiddleware: nurVonDiesemPc", s); Assert.Contains("httpNodeMiddleware: nurVonDiesemPc", s);
        Assert.Contains(@"/^(127\.0\.0\.1|localhost)(:\d+)?$/i", s); Assert.Contains("res.status(403)", s);
        Assert.EndsWith("};", s.TrimEnd()); Assert.StartsWith("const lokal", s);
    }

    [Fact(DisplayName = "Abläufe n8n N2: Trendo KI wählt den Anbieter über den Vorsatz („lmstudio/…“) oder den ersten erreichbaren; Zugang für n8n zeigt auf 127.0.0.1")]
    public void Ki_route()
    {
        Assert.Equal(("lmstudio", "qwen"), N8n.Route("lmstudio/qwen", ["ollama", "lmstudio"]) is { } r ? (r.Backend.Id, r.Model) : default);
        Assert.Equal(("ollama", "llama3.2"), N8n.Route("llama3.2", ["ollama"]) is { } r2 ? (r2.Backend.Id, r2.Model) : default);
        Assert.Null(N8n.Route("lmstudio/qwen", ["ollama"])); Assert.Null(N8n.Route("x", []));
        var cred = N8n.KiCredential("geheim");
        Assert.Equal(("openAiApi", "http://127.0.0.1:47800/v1"), (cred["type"]!.GetValue<string>(), cred["data"]!["url"]!.GetValue<string>()));
    }

    [Fact(DisplayName = "Abläufe n8n N4: alle Vorlagen sind gültige n8n-Abläufe (verkettete Bausteine, eindeutige Namen); nur die Handy-Vorlage wirkt nach außen und ist so markiert; Schlüssel nur als Kopfzeile")]
    public void Templates_valid()
    {
        Assert.True(N8n.Templates.Length >= 6);
        foreach (var t in N8n.Templates)
        {
            var wf = t.Build("TOKEN-123"); var json = wf.ToJsonString();
            var rv = N8n.Check(json);
            Assert.Empty(rv.Problems);
            Assert.Equal(wf["nodes"]!.AsArray().Count - 1, wf["connections"]!.AsObject().Count);
            Assert.Equal(t.External, rv.External.Count > 0);
            if (json.Contains("TOKEN-123")) Assert.Contains("Bearer TOKEN-123", json);
        }
        Assert.Contains(N8n.Templates, t => t.Studio == "Büro"); Assert.Contains(N8n.Templates, t => t.Studio == "Technik");
    }

    [Fact(DisplayName = "Abläufe n8n N6: Trendo-Ablauf wird n8n-Ablauf mit gleichem Auslöser (täglich, Intervall, neue Datei, von Hand), der den Trendo-Ablauf über die lokale Schnittstelle startet")]
    public void From_trendo()
    {
        var w = new Workflow { Id = "abc123", Name = "Fotos ordnen", Trigger = TriggerKind.NeueDatei, WatchFolder = "C:/Bilder" };
        var wf = N8n.FromTrendo(w, "T");
        var nodes = wf["nodes"]!.AsArray();
        Assert.Equal("n8n-nodes-base.localFileTrigger", nodes[0]!["type"]!.GetValue<string>());
        Assert.Equal("http://127.0.0.1:47800/api/workflows/abc123/run", nodes[1]!["parameters"]!["url"]!.GetValue<string>());
        Assert.Equal("n8n-nodes-base.scheduleTrigger", N8n.FromTrendo(new Workflow { Name = "x", Trigger = TriggerKind.Taeglich, At = "07:30" }, "T")["nodes"]![0]!["type"]!.GetValue<string>());
        Assert.Equal(7, N8n.FromTrendo(new Workflow { Name = "x", Trigger = TriggerKind.Taeglich, At = "07:30" }, "T")["nodes"]![0]!["parameters"]!["rule"]!["interval"]![0]!["triggerAtHour"]!.GetValue<int>());
        Assert.Empty(N8n.Check(wf.ToJsonString()).External);
    }

    [Fact(DisplayName = "Abläufe n8n N5: Prüfung eines (von Claude gebauten) Ablaufs – fremde Dienste und Adressen außerhalb des PCs als „wirkt nach außen“, Befehls-Bausteine und doppelte Namen als Problem, kaputtes JSON verständlich")]
    public void Check_review()
    {
        var json = """
            {"name":"Test","nodes":[
              {"name":"Start","type":"n8n-nodes-base.manualTrigger","parameters":{}},
              {"name":"Mail","type":"n8n-nodes-base.emailSend","parameters":{}},
              {"name":"Web","type":"n8n-nodes-base.httpRequest","parameters":{"url":"https://example.org/x"}},
              {"name":"Lokal","type":"n8n-nodes-base.httpRequest","parameters":{"url":"http://127.0.0.1:47800/api/actions/notify"}},
              {"name":"Befehl","type":"n8n-nodes-base.executeCommand","parameters":{}},
              {"name":"Befehl","type":"n8n-nodes-base.noOp","parameters":{}}],"connections":{}}
            """;
        var rv = N8n.Check(json);
        Assert.Equal(6, rv.Nodes.Count);
        Assert.Equal(2, rv.External.Count); Assert.Contains(rv.External, x => x.Contains("example.org"));
        Assert.Contains(rv.Problems, p => p.Contains("Befehle")); Assert.Contains(rv.Problems, p => p.Contains("heißen gleich"));
        Assert.StartsWith("Kein gültiger Ablauf", N8n.Check("{kaputt").Problems.Single());
        Assert.Contains("Datei", N8nRules.Text); Assert.Contains("TRENDO_SCHLUESSEL", N8nRules.Text);
    }

    [Fact(DisplayName = "Abläufe n8n N3: Ereignisse nur an Webhooks auf diesem PC, unbekannte Ereignisse abgelehnt, Abo entfernen")]
    public void Hooks_local()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-hooks-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var h = new N8n.Hooks(dir);
            Assert.Throws<ArgumentException>(() => h.Add("druck-fertig", "https://example.org/webhook"));
            Assert.Throws<ArgumentException>(() => h.Add("gibts-nicht", N8n.WebhookUrl("x")));
            h.Add("druck-fertig", N8n.WebhookUrl("trendo-druck-fertig")); h.Add("druck-fertig", N8n.WebhookUrl("trendo-druck-fertig"));
            Assert.Single(h.For("druck-fertig")); Assert.Equal("http://127.0.0.1:5678/webhook/trendo-druck-fertig", h.For("druck-fertig").Single());
            h.Remove("druck-fertig", N8n.WebhookUrl("trendo-druck-fertig")); Assert.Empty(h.List());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
