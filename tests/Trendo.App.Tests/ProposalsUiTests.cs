using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Vorschläge A, B, C (27.10.2026): Hinzufügen-Zentrale, Profil-Einrichtung und Sperrbildschirm, kostenlose Internet-KIs.</summary>
public class ProposalsUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    sealed class Remote(string answer) : IRemoteKiBackend
    {
        public int Calls;
        public string Name => "OpenRouter"; public string BaseUrl => "https://openrouter.ai/api/v1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["a/text:free"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult(answer); }
    }

    [AvaloniaFact(DisplayName = "A: Hinzufügen-Zentrale mit Reitern und Suche über alles; Studio-Knopf; Trendo-Suche findet Installierbares; Meine Installationen")]
    public void Add_hub()
    {
        var (w, _, _) = Ui.Open();
        w.OpenAddHub();
        Assert.Equal("hinzufuegen", w.CurrentView);
        foreach (var (id, _) in MainWindow.HubTabs) Assert.NotNull(w.TryFind<Button>("hub-tab-" + id));
        var items = w.AddItems();
        Assert.Contains(items, i => i.Tab == "programme"); Assert.Contains(items, i => i.Tab == "ki"); Assert.Contains(items, i => i.Id == "page:ki-internet");
        w.Find<TextBox>("hub-search").Text = "Blender"; Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Blender"));
        Assert.NotNull(w.TryFind<Button>("hub-github-search"));
        if (w.TryFind<Button>("hub-install-") is { } install)
        {
            var id = install.Name!["hub-install-".Length..];
            install.Press();
            Assert.NotNull(w.TryFind<Button>("hub-yes-" + id));
            w.Find<Button>("hub-no-" + id).Press();
        }
        w.Find<Button>("hub-tab-meine").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Alles, was du in Trendo installiert hast"));
        // A2: Studio mit benötigten Programmen hat „Hinzufügen …“
        var studio = StudioTools.Required.Keys.First(k => w.PageForStudio(k) is not null);
        w.Show(w.PageForStudio(studio)!); Pump();
        if (w.TryFind<Button>("auftrag-hinzufuegen") is { } add) { add.Press(); Assert.Equal("hinzufuegen", w.CurrentView); }
        // A3: Suche findet Installierbares
        var missing = items.First(i => i.Tab == "programme" && !i.Installed);
        Assert.Contains(w.BuildSearch().Find(missing.Name), h => h.Target == "add:" + missing.Name);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "B: Einrichtung mit Profilbild, PIN und Wiederherstellungs-Code, Interessen → Desktop-Symbole, Personen; Sperrbildschirm mit PIN vergessen")]
    public async Task Profile_setup_and_lock()
    {
        var (w, _, root) = Ui.Open();
        w.Show("einrichtung");
        w.Find<TextBox>("wiz-name").Text = "Erika Muster";
        w.Find<Button>("wiz-avatar-1").Press();
        Assert.Equal("emoji:🦊", w.Settings.Avatar);
        w.Find<Button>("wiz-next").Press();
        w.Find<RadioButton>("wiz-lock-pin").IsChecked = true; Pump();
        w.Find<TextBox>("wiz-secret").Text = "4711"; w.Find<TextBox>("wiz-secret2").Text = "4711";
        w.Find<Button>("wiz-next").Press();
        Assert.True(w.TryFind<SelectableTextBlock>("wiz-recovery-code") is not null, string.Join(" | ", w.GetLogicalTexts().Where(t => t.Contains("PIN") || t.Contains("Eingab") || t.Contains("Schritt") || t.Contains("Code"))));
        var code = w.Find<SelectableTextBlock>("wiz-recovery-code").Text!;
        Assert.False(w.Find<Button>("wiz-next").IsEnabled); // erst Code bestätigen
        w.Find<CheckBox>("wiz-recovery-noted").IsChecked = true; Pump();
        w.Find<Button>("wiz-next").Press();
        Assert.NotNull(w.TryFind<TextBox>("wiz-folder"));
        w.Find<Button>("wiz-next").Press();
        w.Find<Button>("wiz-accent-D4A84B").Press(); Assert.Equal("#D4A84B", w.Settings.Accent);
        w.Find<Button>("wiz-next").Press();
        w.Find<Button>("wiz-interest-spiele").Press();
        Assert.Contains("spiele", w.Settings.Interests);
        foreach (var cb in w.GetLogicalDescendants().OfType<CheckBox>().Where(c => c.Name!.StartsWith("wiz-pack-")).ToList()) cb.IsChecked = false;
        w.Find<Button>("wiz-install").Press();
        for (var i = 0; i < 100 && w.TryFind<CheckBox>("wiz-priv-updates") is null; i++) { await Task.Delay(20); Pump(); }
        w.Find<Button>("wiz-next").Press(); // Datenschutz
        w.Find<Button>("wiz-next").Press(); // Handy überspringen
        w.Find<TextBox>("wiz-person-name").Text = "Max Muster"; w.Find<CheckBox>("wiz-person-child").IsChecked = true;
        w.Find<Button>("wiz-person-add").Press();
        Assert.Single(new PersonStore(root).All());
        w.Find<Button>("wiz-next").Press();
        Assert.NotNull(w.TryFind<Button>("wiz-done"));
        Assert.Contains("CORE-05", w.Settings.DesktopIcons);

        // Sperrbildschirm: falsche PIN, PIN vergessen mit Code, neue PIN
        w.LockNow(); Pump();
        Assert.True(w.IsLocked);
        Assert.False(w.TryUnlock("0000"));
        Assert.Contains(w.GetLogicalTexts(), t => t == "Die PIN stimmt nicht.");
        Assert.True(w.RecoverWithCode(code));
        Assert.NotNull(w.SetNewSecret("2580", "2580"));
        w.Find<Button>("lock-done").Press();
        Assert.False(w.IsLocked);
        Assert.True(new ProfileLock(root).Verify("2580"));
        // PIN vergessen mit Windows-Anmeldung; Personenwechsel
        w.LockNow(); w.WindowsConfirmOverride = () => true;
        Assert.True(w.RecoverWithWindows());
        string? switched = null; w.SwitchPersonOverride = r => switched = r;
        var kid = new PersonStore(root).All()[0].Id;
        w.Find<Button>("lock-person-" + kid).Press();
        Assert.EndsWith(kid, switched);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "C: kostenlose Internet-KIs – Liste laden, Schlüssel geführt einrichten, in allen Bereichen einspringen, Hinweis und Tageszähler, Bild verstehen")]
    public async Task OpenRouter_flow()
    {
        var (w, _, root) = Ui.Open();
        w.OpenRouterModelsOverride = () => Task.FromResult("""{"data":[{"id":"a/text:free","name":"Text","pricing":{"prompt":"0","completion":"0"},"architecture":{"input_modalities":["text"],"output_modalities":["text"]}},{"id":"b/vision:free","name":"Vision","pricing":{"prompt":"0","completion":"0"},"architecture":{"input_modalities":["text","image"],"output_modalities":["text"]}}]}""");
        Assert.Equal(2, await w.RefreshOpenRouterAsync(force: true));
        w.Show("ki-internet"); Pump();
        Assert.NotNull(w.TryFind<Control>("or-privacy"));
        Assert.NotNull(w.TryFind<Button>("or-signup"));
        w.OpenRouterValidateOverride = k => Task.FromResult(k.StartsWith("sk-or-"));
        Assert.False(await w.OpenRouterSaveKey("falsch-aber-lang-genug-12345"));
        Assert.True(await w.OpenRouterSaveKey("sk-or-v1-0123456789abcdef0123456789"));
        Assert.True(w.OpenRouterReady);
        Assert.Contains("openrouter", KiSourceSettings.Load(root).Cloud);

        // Nur im Chat: ein Studio-Auftrag (ohne AllowRemote) bleibt lokal; „ersatz“: Internet springt ein, Zähler steigt
        var remote = new Remote("Antwort aus dem Netz");
        w.KiBackendsOverride = () => [remote];
        var a = await w.Intel.AskAsync(new KiRequest("Hallo"));
        Assert.False(a.Ok); Assert.Equal(0, remote.Calls);
        w.Show("ki-internet"); Pump();
        w.Find<RadioButton>("or-mode-ersatz").IsChecked = true; Pump();
        a = await w.Intel.AskAsync(new KiRequest("Hallo"));
        Assert.True(a.Ok); Assert.Equal(1, remote.Calls); Pump();
        Assert.Equal(1, w.RemoteCounter.Today());

        // Bild verstehen
        var img = Path.Combine(root, "foto.png"); File.WriteAllBytes(img, [0x89, 0x50, 0x4E, 0x47]);
        string? usedModel = null;
        w.OpenRouterFileOverride = (m, q, f) => { usedModel = m; return Task.FromResult("Ein Hund im Garten."); };
        Assert.Equal("Ein Hund im Garten.", await w.OpenRouterAskFile(img, "Was ist zu sehen?"));
        Assert.Equal("b/vision:free", usedModel);
        w.Show("ki-internet"); Pump();
        w.Find<Button>("or-key-remove").Press();
        Assert.False(w.OpenRouterReady);
        w.Close();
    }
}
