using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 (C1–C4): kostenlose KIs von OpenRouter in Trendo. Trendo hält die Liste aktuell (C1), führt durch Konto und
/// Schlüssel (C2), lässt sie auf Wunsch in allen Bereichen einspringen – die KI auf dem PC bleibt Standard (C3) – und sagt klar, wenn etwas
/// ins Internet geht, mit Tageszähler (C4). Eigene Dokumente („Wissen“) gehen nie hinaus.</summary>
public partial class MainWindow
{
    string _orMsg = "", _orKeyDraft = "", _orQuestion = "Was ist auf diesem Bild zu sehen? Antworte auf Deutsch.", _orFile = "", _orAnswer = "";
    bool _orBusy;
    /// <summary>Für Tests: Inhalt von /api/v1/models statt OpenRouter.</summary>
    public Func<Task<string>>? OpenRouterModelsOverride { get; set; }
    /// <summary>Für Tests: Schlüsselprüfung ohne Netz.</summary>
    public Func<string, Task<bool>>? OpenRouterValidateOverride { get; set; }
    /// <summary>Für Tests: Frage mit Datei ohne Netz (Modell, Frage, Datei).</summary>
    public Func<string, string, string, Task<string>>? OpenRouterFileOverride { get; set; }
    static readonly HttpClient OpenRouterHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    public RemoteKiCounter RemoteCounter => new(_core.DataRoot);
    static KiCloudService OpenRouterService => KiCatalog.Cloud.First(c => c.Id == "openrouter");
    public bool OpenRouterReady => _core.Secrets.Get(OpenRouterService.SecretName) is { Length: > 0 } && KiSourceSettings.Load(_core.DataRoot).Cloud.Contains("openrouter");

    void InitOpenRouter()
    {
        _showMap["ki-internet"] = OpenRouterView; if (!KiTabs.Contains(("ki-internet", "Kostenlose Internet-KIs"))) KiTabs.Add(("ki-internet", "Kostenlose Internet-KIs"));
        if (!ExtraPages.Any(p => p.Key == "ki-internet")) ExtraPages.Add(("Kostenlose Internet-KIs", "ki-internet", ["openrouter", "internet", "kostenlos", "cloud", "bild verstehen", "audio verstehen", "online ki"]));
    }

    /// <summary>C1: öffentliche Liste laden (ohne Schlüssel), nur kostenlose Modelle behalten, ablegen. Höchstens einmal am Tag von selbst.</summary>
    public async Task<int> RefreshOpenRouterAsync(bool force = false)
    {
        if (_core.Offline || (!force && !OpenRouterFree.Stale(_core.DataRoot))) return OpenRouterFree.Load(_core.DataRoot).Models.Count;
        try
        {
            string json;
            if (OpenRouterModelsOverride is { } o) json = await o();
            else
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, OpenRouterFree.ModelsUrl);
                req.Headers.UserAgent.ParseAdd("TrendoOS/1.0");
                using var res = await OpenRouterHttp.SendAsync(req);
                res.EnsureSuccessStatusCode();
                json = await res.Content.ReadAsStringAsync();
            }
            var list = OpenRouterFree.Parse(json);
            OpenRouterFree.Save(_core.DataRoot, list);
            _core.Audit.Append("app", "ki.openrouter.list", $"{list.Count} kostenlose Modelle");
            return list.Count;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        {
            _orMsg = "Die Liste ließ sich gerade nicht laden: " + e.Message;
            return OpenRouterFree.Load(_core.DataRoot).Models.Count;
        }
    }

    /// <summary>C4: Antwort kam aus dem Internet – zählen und sichtbar sagen (beim ersten Mal am Tag und dann alle 10).</summary>
    void RemoteKiUsed(string name)
    {
        var n = RemoteCounter.Add();
        if (n == 1 || n % 10 == 0 || n >= OpenRouterFree.DailyFreeLimit - 5)
            Toast($"🌐 Antwort kam über das Internet ({name}) · heute {n} von ca. {OpenRouterFree.DailyFreeLimit} kostenlosen Anfragen", ("Einstellen", new Action(() => Show("ki-internet"))));
    }

    /// <summary>C2: Schlüssel prüfen, sicher ablegen (Windows-Schlüsselspeicher) und OpenRouter einschalten.</summary>
    public async Task<bool> OpenRouterSaveKey(string key)
    {
        key = key.Trim();
        if (key.Length < 20) { _orMsg = "Das sieht nicht wie ein OpenRouter-Schlüssel aus (beginnt meist mit „sk-or-“)."; return false; }
        _orBusy = true; _orMsg = "Schlüssel wird geprüft …"; Show("ki-internet");
        try
        {
            var ok = OpenRouterValidateOverride is { } v ? await v(key) : await CloudKiBackend.ValidateAsync(OpenRouterService, key);
            if (!ok) { _orMsg = "OpenRouter hat den Schlüssel nicht angenommen. Bitte noch einmal kopieren (ganz, ohne Leerzeichen)."; return false; }
            _core.Secrets.Set(OpenRouterService.SecretName, key);
            KiSourceSettings.Update(_core.DataRoot, s => { if (!s.Cloud.Contains("openrouter")) s.Cloud.Insert(0, "openrouter"); });
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.cloud.key", "openrouter");
            _orKeyDraft = ""; _orMsg = "✓ Verbunden. Die kostenlosen Internet-KIs stehen bereit.";
            return true;
        }
        finally { _orBusy = false; Show("ki-internet"); }
    }

    public void OpenRouterRemoveKey()
    {
        _core.Secrets.Delete(OpenRouterService.SecretName);
        KiSourceSettings.Update(_core.DataRoot, s => s.Cloud.Remove("openrouter"));
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.cloud.key.removed", "openrouter");
        _orMsg = "Schlüssel entfernt – es geht nichts mehr an OpenRouter.";
    }

    /// <summary>C3: Bild, Audio oder Video verstehen lassen (mit dem passenden kostenlosen Modell).</summary>
    public async Task<string> OpenRouterAskFile(string file, string question)
    {
        var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
        var area = ext is "wav" or "mp3" or "ogg" or "flac" or "m4a" ? OpenRouterFree.Audio : ext is "mp4" or "webm" or "mov" ? OpenRouterFree.Video : OpenRouterFree.Images;
        var models = OpenRouterFree.Load(_core.DataRoot).Models;
        var set = KiSourceSettings.Load(_core.DataRoot);
        if (OpenRouterFree.Best(models, area, set.OpenRouterModels.GetValueOrDefault(area)) is not { } m) return $"Gerade gibt es kein kostenloses Modell für „{area}“.";
        if (!OpenRouterReady && OpenRouterFileOverride is null) return "Bitte zuerst oben OpenRouter einrichten (kostenloses Konto und Schlüssel).";
        using var label = KiActivityHub.Label("🌐 " + area);
        var act = KiActivityHub.Begin();
        try
        {
            KiActivityHub.Update(act, phase: $"🌐 Geht ins Internet zu OpenRouter ({m.Name}) …", backend: "🌐 OpenRouter", model: m.Id);
            var text = OpenRouterFileOverride is { } o ? await o(m.Id, question, file)
                : await CloudKiBackend.For(OpenRouterService, _core.Secrets.Get(OpenRouterService.SecretName)!).AskWithFileAsync(m.Id, question, file, act.Token);
            _core.Audit.Append(Principal.LocalOwner.Name, "ki.ask", $"quelle=OpenRouter modell={m.Id} datei={Path.GetExtension(file)} extern");
            RemoteKiUsed("OpenRouter");
            KiActivityHub.End(act, true, text.Length);
            return text;
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or IOException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            KiActivityHub.End(act, false, 0);
            return "Hat nicht geklappt: " + e.Message;
        }
    }

    Control OpenRouterView()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(P("Zusätzlich zur KI auf deinem PC kannst du kostenlose KIs aus dem Internet nutzen – über OpenRouter. Trendo hält die Liste der kostenlosen Modelle selbst aktuell.", Text, 13));
        sp.Children.Add(new Border { Name = "or-privacy", Background = Brush.Parse("#1AFACC15"), BorderBrush = Brush.Parse("#55FACC15"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
            Child = P("🌐 Wichtig: Was du diese KIs fragst, geht ins Internet zu OpenRouter und dem jeweiligen Anbieter. Deine eigenen Dokumente („Wissen“) gibt Trendo nie mit. Für Privates nimm die KI auf dem PC.", Text, 12.5) });
        if (_orMsg.Length > 0) sp.Children.Add(P(_orMsg, _orMsg.StartsWith('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), 12.5));

        // C2: geführte Einrichtung
        var setup = new StackPanel { Name = "or-setup", Spacing = 6 };
        if (OpenRouterReady)
        {
            setup.Children.Add(new TextBlock { Text = "✓ OpenRouter ist verbunden", Foreground = Brush.Parse("#4ADE80"), FontSize = 14, FontWeight = FontWeight.SemiBold });
            var rm = new Button { Name = "or-key-remove", Content = "Schlüssel entfernen" };
            rm.Click += (_, _) => { OpenRouterRemoveKey(); Show("ki-internet"); };
            setup.Children.Add(rm);
        }
        else
        {
            setup.Children.Add(H("In 3 Schritten einrichten (kostenlos)", 15));
            var s1 = new Button { Name = "or-signup", Content = "1. Kostenloses Konto anlegen", IsEnabled = !_core.Offline };
            s1.Click += (_, _) => Launch(OpenRouterFree.SignupUrl);
            var s2 = new Button { Name = "or-keys", Content = "2. Schlüssel erstellen und kopieren", IsEnabled = !_core.Offline };
            s2.Click += (_, _) => Launch(OpenRouterFree.KeysUrl);
            setup.Children.Add(new WrapPanel { Children = { Sp(s1), Sp(s2) } });
            setup.Children.Add(P("Auf der OpenRouter-Seite „Create Key“ wählen, einen Namen wie „Trendo“ eintragen, den Schlüssel kopieren. Ein Guthaben ist nicht nötig.", null, 12));
            var key = new TextBox { Name = "or-key", PasswordChar = '•', PlaceholderText = "3. Schlüssel hier einfügen (sk-or-…)", Text = _orKeyDraft, MinWidth = 260 };
            key.TextChanged += (_, _) => _orKeyDraft = key.Text ?? "";
            var paste = new Button { Name = "or-key-paste", Content = "Aus Zwischenablage einfügen" };
            paste.Click += async (_, _) => { if (Clipboard is { } cb && await cb.TryGetTextAsync() is { Length: > 0 } t) { _orKeyDraft = t.Trim(); key.Text = _orKeyDraft; } };
            var save = new Button { Name = "or-key-save", Content = "Prüfen & verbinden", Background = Accent, IsEnabled = !_orBusy && !_core.Offline };
            save.Click += async (_, _) => await OpenRouterSaveKey(key.Text ?? "");
            setup.Children.Add(new WrapPanel { Children = { Sp(key), Sp(paste), Sp(save) } });
        }
        sp.Children.Add(WCard(setup));

        // C3: wo sie helfen darf
        var set = KiSourceSettings.Load(_core.DataRoot);
        var where = new StackPanel { Name = "or-where", Spacing = 4 };
        where.Children.Add(H("Wo darf die Internet-KI helfen?", 15));
        foreach (var (id, label, hint) in new[] {
                     ("chat", "Nur im KI-Chat", "Alles andere bleibt auf deinem PC."),
                     ("ersatz", "In allen Bereichen – nur wenn die KI auf dem PC nicht antwortet (empfohlen)", "Briefe, Texte, Recherche, Agenten … zuerst lokal, sonst Internet."),
                     ("zuerst", "In allen Bereichen – Internet-KI zuerst", "Schneller auf schwachen PCs, aber mehr geht ins Internet.") })
        {
            var rb = new RadioButton { Name = "or-mode-" + id, GroupName = "or-mode", IsChecked = set.RemoteMode == id, Foreground = Text,
                Content = new StackPanel { Children = { new TextBlock { Text = label, Foreground = Text, TextWrapping = TextWrapping.Wrap }, P(hint, null, 11) } } };
            var mode = id;
            rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true && KiSourceSettings.Load(_core.DataRoot).RemoteMode != mode) { KiSourceSettings.Update(_core.DataRoot, s => s.RemoteMode = mode); _core.Audit.Append(Principal.LocalOwner.Name, "ki.remote.mode", mode); } };
            where.Children.Add(rb);
        }
        var today = RemoteCounter.Today();
        where.Children.Add(new TextBlock { Name = "or-today", Text = $"Heute über das Internet: {today} von ca. {OpenRouterFree.DailyFreeLimit} kostenlosen Anfragen", Foreground = today >= OpenRouterFree.DailyFreeLimit ? Brush.Parse("#FACC15") : Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        sp.Children.Add(WCard(where));

        // C1: kostenlose Modelle nach Bereichen
        var (fetched, models) = OpenRouterFree.Load(_core.DataRoot);
        var list = new StackPanel { Name = "or-models", Spacing = 6 };
        var refresh = new Button { Name = "or-refresh", Content = "Jetzt aktualisieren", IsEnabled = !_core.Offline };
        refresh.Click += async (_, _) => { await RefreshOpenRouterAsync(force: true); Show("ki-internet"); };
        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
            new StackPanel { Children = { H($"Kostenlose Modelle ({models.Count})", 15), P(fetched == default ? "Noch nicht geladen." : $"Stand: {fetched.ToLocalTime():dd.MM.yyyy HH:mm} · aktualisiert sich täglich von selbst", null, 11) } },
            Pos(refresh, 1) } });
        foreach (var area in OpenRouterFree.AllAreas)
        {
            var inArea = models.Where(m => m.Areas.Contains(area)).ToList();
            if (inArea.Count == 0) continue;
            var chosen = OpenRouterFree.Best(models, area, set.OpenRouterModels.GetValueOrDefault(area));
            list.Children.Add(H($"{area} ({inArea.Count})", 13.5));
            foreach (var m in inArea.Take(8))
            {
                var on = chosen?.Id == m.Id;
                var use = new Button { Name = "or-use-" + area.GetHashCode().ToString("x") + "-" + m.Id.Replace('/', '-').Replace(':', '-').Replace('.', '-'), Content = on ? "✓ genutzt" : "Nutzen", Background = on ? Accent : Brushes.Transparent, Padding = new Thickness(8, 2) };
                var ar = area; var id = m.Id;
                use.Click += (_, _) => { KiSourceSettings.Update(_core.DataRoot, s => s.OpenRouterModels[ar] = id); Show("ki-internet"); };
                list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new StackPanel { Children = { new TextBlock { Text = m.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                        P($"{(m.Context > 0 ? $"{m.Context / 1000:N0}k Kontext · " : "")}{m.Description}", null, 11) } },
                    Pos(use, 1) } });
            }
        }
        if (models.Count == 0) list.Children.Add(P(_core.Offline ? "Trendo ist offline." : "Mit „Jetzt aktualisieren“ lädt Trendo die Liste (ohne Schlüssel, ohne Konto).", null, 12));
        sp.Children.Add(WCard(list));

        // C3: Bild/Audio/Video verstehen
        var tryIt = new StackPanel { Name = "or-try", Spacing = 6 };
        tryIt.Children.Add(H("Bild, Audio oder Video verstehen lassen", 15));
        var pick = new Button { Name = "or-file-pick", Content = _orFile.Length == 0 ? "Datei wählen …" : "📎 " + Path.GetFileName(_orFile) };
        pick.Click += async (_, _) =>
        {
            var req = new PickRequest("or-file", false, false, ["png", "jpg", "jpeg", "webp", "gif", "wav", "mp3", "ogg", "flac", "m4a", "mp4", "webm", "mov"]);
            string[]? got = PickOverride is { } f ? await f(req)
                : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Bild, Audio oder Video wählen", FileTypeFilter = [new FilePickerFileType("Bild, Audio, Video") { Patterns = req.Extensions.Select(e => "*." + e).ToList() }] }))
                    .Select(x => x.TryGetLocalPath()).OfType<string>().ToArray();
            if (got is { Length: > 0 }) { _orFile = got[0]; Show("ki-internet"); }
        };
        var q = new TextBox { Name = "or-question", Text = _orQuestion, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44 };
        q.TextChanged += (_, _) => _orQuestion = q.Text ?? "";
        var ask = new Button { Name = "or-ask", Content = "🌐 Fragen", Background = Accent, IsEnabled = _orFile.Length > 0 && !_core.Offline };
        ask.Click += async (_, _) => { _orAnswer = "Wird gefragt …"; Show("ki-internet"); _orAnswer = await OpenRouterAskFile(_orFile, _orQuestion); Show("ki-internet"); };
        tryIt.Children.Add(new WrapPanel { Children = { Sp(pick), Sp(ask) } });
        tryIt.Children.Add(q);
        if (_orAnswer.Length > 0) tryIt.Children.Add(new SelectableTextBlock { Name = "or-answer", Text = _orAnswer, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(WCard(tryIt));
        return KiPage("Kostenlose Internet-KIs", sp);
    }
}
