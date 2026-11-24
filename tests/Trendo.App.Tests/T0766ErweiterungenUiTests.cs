using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>T-0766 (X-04): Funktions-Erweiterung vom Klick bis zur echten Datei – mit der echten Wasmtime-Sandbox, nichts ersetzt.</summary>
public class T0766ErweiterungenUi
{
    static void Until(Func<bool> ok, int ms = 20000)
    {
        var end = Environment.TickCount64 + ms;
        while (!ok() && Environment.TickCount64 < end) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact(DisplayName = "T-0766 (UI): Werkstatt → bauen → ZIP → installieren (leerer Datenordner) → Rechte im eigenen Schritt → Knopf im Studio „Projekte“ → Tabelle im Projekt → Rückgängig → Rechte entziehen")]
    public void Werkstatt_bis_Projektdatei()
    {
        var (w, _, _) = Ui.Open();
        Assert.Empty(w.Exts.All());
        w.Show("erweiterungen");
        // Werkstatt: neue Erweiterung aus Vorlage
        w.Find<TextBox>("ext-ws-name").Text = "Zentrierte Tabelle";
        w.Find<Button>("ext-ws-new").Press();
        Assert.NotNull(w.ExtWorkshopDir);
        w.Find<TextBox>("ext-ws-f-cmd").Text = "Als zentrierte Tabelle";
        var src = w.Find<TextBox>("ext-ws-source");
        src.Text = src.Text!.Replace("(data (i32.const 40) \" --- |\")", "(data (i32.const 40) \" :-: |\")");
        w.Find<Button>("ext-ws-export").Press(); Until(() => w.ExtWorkshopZip is { } z0 && File.Exists(z0)); // Befund M2: baut außerhalb des Oberflächen-Threads
        Assert.True(w.ExtWorkshopZip is { } z && File.Exists(z), w.Find<TextBlock>("ext-ws-msg").Text);
        // Installieren nur über die Rückfrage
        w.Find<Button>("ext-ws-install").Press();
        Assert.Empty(w.Exts.All());
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Möchte diese Rechte") && t.Contains("eigenen Schritt"));
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.All().Count == 1 && w.TryFind<Button>("ext-ask-yes") is null);
        var e = Assert.Single(w.Exts.All());
        Assert.Equal(Extensions.ModuleKind, e.Manifest.Kind);
        Assert.Empty(w.Exts.GrantedRights(e)); // installiert ≠ freigegeben

        // Projekt mit echter CSV-Datei – ohne Rechte kein Knopf
        var pid = w.Core.Projects.CreateNamed("Kunden").Id;
        w.Core.Projects.WriteFile(pid, "kunden.csv", "Name,Ort\nErika Muster,Köln\n");
        w.ProjectSelected = pid; w.Show("projects");
        Assert.Null(w.TryFind<Button>("ext-cmd-run-0"));

        // Rechte in eigenem Schritt: Knopf → Rückfrage mit verständlichen Rechten → Ja
        w.Show("erweiterungen");
        w.Find<Button>("ext-use-0").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Rechte für „"));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Ergebnis als neue Datei") && t.Contains("Nicht erlaubt") && t.Contains("Internet"));
        Assert.Empty(w.Exts.GrantedRights(e));
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.GrantedRights(w.Exts.All()[0]).Count > 0);
        Assert.Equal(3, w.Exts.GrantedRights(w.Exts.All()[0]).Count);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "ext.grant");

        // Klick im Studio „Projekte“ → echte Datei
        w.ProjectSelected = pid; w.Show("projects");
        Assert.Equal("kunden.csv", w.Find<ComboBox>("ext-cmd-file-0").SelectedItem);
        w.Find<Button>("ext-cmd-run-0").Press();
        var md = Path.Combine(w.Core.Projects.ProjectFilesRoot(pid), "kunden.md");
        Until(() => File.Exists(md));
        Assert.Equal("| Name | Ort |\n| :-: | :-: |\n| Erika Muster | Köln |\n", File.ReadAllText(md));
        Assert.Contains("kunden.md", w.ProjectMessage);
        // Rückgängig
        Until(() => w.TryFind<Button>("ext-cmd-undo") is not null, 3000);
        w.Find<Button>("ext-cmd-undo").Press();
        Assert.False(File.Exists(md));

        // Rechte entziehen → Knopf verschwindet
        w.Show("erweiterungen");
        w.Find<Button>("ext-revoke-0").Press();
        Assert.Empty(w.Exts.GrantedRights(w.Exts.All()[0]));
        w.ProjectSelected = pid; w.Show("projects");
        Assert.Null(w.TryFind<Button>("ext-cmd-run-0"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0766 (UI): Katalog-Modul – Rechte werden vor der Installation angezeigt; Not-Stopp sperrt die Rechte-Freigabe")]
    public void Katalog_Rechte_und_NotStopp()
    {
        var (w, _, _) = Ui.Open();
        w.Show("erweiterungen");
        w.Find<Button>("ext-cat-local").Press();
        var idx = w.ExtCatalog.FindIndex(x => x.Kind == Extensions.ModuleKind);
        w.Find<Button>($"ext-cat-install-{idx}").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Möchte diese Rechte") && t.Contains("Einen Knopf im Studio"));
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.All().Count == 1 && w.TryFind<Button>("ext-ask-yes") is null);
        w.Core.Approvals.Halt(Principal.LocalOwner, "Test");
        w.Find<Button>("ext-use-0").Press();
        w.Find<Button>("ext-ask-yes").Press(); Dispatcher.UIThread.RunJobs();
        Assert.Empty(w.Exts.GrantedRights(w.Exts.All()[0]));
        w.Core.Approvals.Resume(Principal.LocalOwner);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0766 Gegenprüfung (UI): Netz-Katalog erst laden, dann Rechte zeigen, dann fragen; Update nennt ersetzte Fassung und fragt nach dem Datenordner; Freigabe nur für die angezeigte Prüfsumme")]
    public void Netzkatalog_Update_Pruefsumme()
    {
        var (w, _, _) = Ui.Open();
        var kat = Path.Combine(w.Core.DataRoot, "kat-quelle"); ExtensionCatalog.BuildLocal(kat);
        var local = ExtensionCatalog.Parse(File.ReadAllText(Path.Combine(kat, ExtensionCatalog.FileName))).Entries.Single(e => e.Kind == Extensions.ModuleKind);
        var zip = File.ReadAllBytes(ExtensionCatalog.LocalFile(kat, local)!);
        var remote = local with { Url = "https://raw.githubusercontent.com/beispiel/katalog/main/csv.zip" };
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { remote });
        var fetched = new List<string>();
        w.ExtFetchOverride = u => { fetched.Add(u); return Task.FromResult(u.EndsWith(".zip") ? zip : System.Text.Encoding.UTF8.GetBytes(json)); };
        w.Show("erweiterungen");
        var load = w.ExtCatalogLoadRemote("https://raw.githubusercontent.com/beispiel/katalog/main/katalog.json"); Until(() => load.IsCompleted);
        w.Find<Button>("ext-cat-install-0").Press(); Until(() => w.TryFind<Button>("ext-ask-yes") is not null);
        // geladen und geprüft, bevor gefragt wird – die Rückfrage zeigt die Rechte aus der geladenen Datei
        Assert.Contains(fetched, u => u.EndsWith(".zip"));
        Assert.Empty(w.Exts.All());
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Möchte diese Rechte") && t.Contains("Einen Knopf im Studio"));
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.All().Count == 1 && w.TryFind<Button>("ext-ask-yes") is null);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Noch ohne Rechte"));
        var e = w.Exts.All()[0];
        // N4: Freigabe für eine andere (z. B. inzwischen veränderte) Fassung greift nicht
        Assert.Contains("geändert", w.ExtGrantApproved(e.Manifest.Id, new string('0', 64)));
        Assert.Empty(w.Exts.GrantedRights(e));

        // N5: Update mit Daten der alten Fassung – Rückfrage nennt die Fassung und bietet „behalten“ an (Vorgabe: leeren)
        Directory.CreateDirectory(w.Exts.DataDir(e.Manifest.Id)); File.WriteAllText(Path.Combine(w.Exts.DataDir(e.Manifest.Id), "notiz.txt"), "x");
        var src = ExtensionModuleSample.Write(Path.Combine(w.Core.DataRoot, "neu"));
        var mf = Path.Combine(src, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("\"1.0.0\"", "\"1.1.0\""));
        w.ExtInstall(src);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Ersetzt die installierte Fassung 1.0.0"));
        var keep = w.Find<CheckBox>("ext-ask-keepdata"); Assert.False(keep.IsChecked);
        keep.IsChecked = true;
        w.Find<Button>("ext-ask-yes").Press(); Until(() => w.Exts.All()[0].Manifest.Version == "1.1.0" && w.TryFind<Button>("ext-ask-yes") is null);
        Assert.True(File.Exists(Path.Combine(w.Exts.DataDir(e.Manifest.Id), "notiz.txt")));
        w.Close();
    }
}
