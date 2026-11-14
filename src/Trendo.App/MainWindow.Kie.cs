using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Kie;
using Trendo.Core.Kreativ;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>kie.ai in jedem Studio (Freigabe 13.11.2026): Knopf „kie.ai“ neben „Claude“, Schlüssel je Person, Kosten vor jedem Auftrag,
/// Ergebnis sofort auf den Rechner (kie.ai löscht nach 14 Tagen), Herkunftsnachweis je Datei. Ohne Schlüssel passiert nichts im Netz.</summary>
public partial class MainWindow
{
    const string KieSecret = "kie.ai";
    const long KieMaxImage = 10L * 1024 * 1024, KieMaxAudio = 100L * 1024 * 1024, KieMaxResult = 600L * 1024 * 1024;

    /// <summary>Tests: Netz und Wartezeit nachbilden.</summary>
    public IWebSend? KieWebOverride { get; set; }
    public Func<TimeSpan, CancellationToken, Task>? KieDelayOverride { get; set; }
    IWebSend KieWeb => KieWebOverride ?? PodHttp;
    string? KieKey => PodSecret(KieSecret);
    public bool KieOn => KieKey is not null;
    double? _kieCredits; string _kieMsg = "";

    public sealed class KieJob(string studio, KieModel model, KieInput input, string label)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
        public string Studio { get; } = studio;
        public KieModel Model { get; } = model;
        public KieInput Input { get; set; } = input;
        public string Label { get; } = label;
        public DateTime Started { get; } = DateTime.Now;
        public string? TaskId { get; set; }
        public string State { get; set; } = "wird gesendet";
        public List<string> Files { get; } = [];
        public string? Error { get; set; }
        public double? Credits { get; set; }
        public bool Done { get; set; }
        /// <summary>Auftrag liegt bei kie.ai, das Ergebnis kam aber nicht an: „Ergebnis holen“ versucht es ohne neue Kosten.</summary>
        public bool CanResume => Done && TaskId is not null && Files.Count == 0 && Error is not null;
    }
    readonly List<KieJob> _kieJobs = [];
    public IReadOnlyList<KieJob> KieJobs => _kieJobs;
    Action? _kieRefresh;
    void KieRefresh() => Dispatcher.UIThread.Post(() => _kieRefresh?.Invoke());

    /// <summary>Schlüssel prüfen (Kontostand abfragen) und erst dann für diese Person speichern.</summary>
    public async Task<bool> KieConnect(string key)
    {
        key = (key ?? "").Trim();
        if (key.Length < 10) { _kieMsg = "Bitte den vollständigen Schlüssel von kie.ai einfügen."; KieRefresh(); return false; }
        try
        {
            _kieCredits = await new KieClient(KieWeb, key).CreditsAsync();
            _core.Secrets.Set(KieSecret, key);
            _core.Audit.Append(Principal.LocalOwner.Name, "kie.verbunden", "Schlüssel gespeichert");
            _kieMsg = "Verbunden.";
            return true;
        }
        catch (Exception e) when (e is KieException or HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or UnauthorizedAccessException)
        { _kieMsg = e is KieException ? e.Message : e is InvalidOperationException or UnauthorizedAccessException ? "Der Schlüssel ließ sich auf diesem Rechner nicht sicher speichern." : "kie.ai ist nicht erreichbar."; return false; }
        finally { KieRefresh(); }
    }

    public void KieDisconnect()
    {
        try { _core.Secrets.Delete(KieSecret); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        _kieCredits = null; _kieMsg = "Getrennt. Der Schlüssel ist von diesem Rechner gelöscht.";
        _core.Audit.Append(Principal.LocalOwner.Name, "kie.getrennt", "");
        KieRefresh();
    }

    public async Task<double?> KieBalance()
    {
        if (KieKey is not { } k) return null;
        try { _kieCredits = await new KieClient(KieWeb, k).CreditsAsync(); }
        catch (Exception e) when (e is KieException or HttpRequestException or TaskCanceledException or IOException) { _kieMsg = e is KieException ? e.Message : "kie.ai ist nicht erreichbar."; }
        KieRefresh();
        return _kieCredits;
    }

    IReadOnlyList<string>? _kieMissing;
    public IReadOnlyList<string>? KieMissing => _kieMissing;

    /// <summary>K4: Modellliste mit der offiziellen Übersicht von kie.ai abgleichen (ohne Schlüssel). Nicht mehr genannte Modelle werden gekennzeichnet.</summary>
    public async Task<bool> KieCheckModels()
    {
        try
        {
            var r = await KieWeb.SendAsync("GET", "https://docs.kie.ai/llms.txt", new Dictionary<string, string>());
            if (!r.Ok) { _kieMsg = "Die Modellübersicht von kie.ai ist gerade nicht abrufbar. Die eingebaute Liste bleibt."; return false; }
            _kieMissing = KieModels.Missing(r.Text);
            _kieMsg = _kieMissing.Count == 0 ? "Alle Modelle stehen noch in der Übersicht von kie.ai." : $"{_kieMissing.Count} Modell(e) nennt kie.ai nicht mehr – sie sind markiert.";
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { _kieMsg = "Die Modellübersicht von kie.ai ist gerade nicht abrufbar."; return false; }
        finally { KieRefresh(); }
    }

    string KieDir(string studio) => Path.Combine(_core.DataRoot, "kie", string.Concat(studio.Where(c => char.IsLetterOrDigit(c) || c == '-')));

    static bool Ext(string f, IReadOnlyList<string> allowed) => allowed.Contains(Path.GetExtension(f).ToLowerInvariant());

    /// <summary>Einen Auftrag ausführen: Prüfen, eigene Dateien hochladen, Auftrag anlegen, warten, Ergebnis herunterladen, Nachweis schreiben.
    /// Die Bestätigung der Kosten holt der Aufrufer vorher ein (Knopf in zwei Schritten oder Rückfrage im Claude-Chat).</summary>
    public async Task<KieJob> KieRun(string studio, KieModel m, KieInput input, IReadOnlyList<string> localImages, string? localAudio, bool consent, CancellationToken ct = default)
    {
        var job = new KieJob(studio, m, input, KreativSafe.Cut(string.IsNullOrWhiteSpace(input.Title) ? (string.IsNullOrWhiteSpace(input.Prompt) ? input.Lyrics : input.Prompt) : input.Title, 60));
        _kieJobs.Insert(0, job); KieRefresh();
        try
        {
            if (KieKey is not { } key) throw new KieException(401, "Es ist kein kie.ai-Schlüssel eingetragen.");
            // Dateien nur, wenn das Modell sie nutzt (Video: Startbild optional)
            var usesImage = m.NeedsImage || m.Kind == KieKind.Video;
            var imgs = usesImage ? localImages.ToList() : [];
            var aud = m.NeedsAudio ? localAudio : null;
            var check = input with { ImageUrls = imgs.Count > 0 ? imgs : null, AudioUrl = aud };
            if (KieModels.Blocked(m, check, consent) is { } why) throw new InvalidOperationException(why);
            foreach (var f in imgs)
                if (!Ext(f, KieModels.ImageExt) || !KreativSafe.LocalFile(f, KieMaxImage)) throw new InvalidOperationException($"„{Path.GetFileName(f)}“ ist kein Bild (PNG, JPG, WEBP) auf diesem Rechner oder größer als 10 MB.");
            if (aud is not null && (!Ext(aud, KieModels.AudioExt) || !KreativSafe.LocalFile(aud, KieMaxAudio))) throw new InvalidOperationException("Die Aufnahme muss MP3, WAV, M4A oder OGG auf diesem Rechner sein, höchstens 100 MB.");
            var client = new KieClient(KieWeb, key);
            job.State = "lädt hoch"; KieRefresh();
            var urls = new List<string>();
            foreach (var f in imgs) urls.Add(await client.UploadAsync(await File.ReadAllBytesAsync(f, ct), Path.GetFileName(f), ct));
            var audioUrl = aud is null ? null : await client.UploadAsync(await File.ReadAllBytesAsync(aud, ct), Path.GetFileName(aud), ct);
            job.Input = input with { ImageUrls = urls, AudioUrl = audioUrl };
            job.TaskId = await client.CreateAsync(m.Id, m.Input(job.Input), ct);
            _core.Audit.Append(Principal.LocalOwner.Name, "kie.auftrag", $"{m.Id} {job.TaskId}");
            await KieFinish(job, client, ct);
        }
        catch (Exception e) when (e is KieException or InvalidOperationException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or ArgumentException)
        { KieFail(job, e, ct); }
        finally { job.Done = true; KieRefresh(); }
        return job;
    }

    /// <summary>Ergebnis eines schon angelegten Auftrags erneut abholen – ohne neuen Auftrag und ohne neue Kosten.</summary>
    public async Task<KieJob> KieResume(KieJob job, CancellationToken ct = default)
    {
        if (job.TaskId is null || KieKey is not { } key) return job;
        job.Error = null; job.Done = false; job.State = "fragt erneut nach"; KieRefresh();
        try { await KieFinish(job, new KieClient(KieWeb, key), ct); }
        catch (Exception e) when (e is KieException or InvalidOperationException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or ArgumentException) { KieFail(job, e, ct); }
        finally { job.Done = true; KieRefresh(); }
        return job;
    }

    void KieFail(KieJob job, Exception e, CancellationToken ct)
    {
        job.Error = e switch
        {
            TaskCanceledException when ct.IsCancellationRequested => "Abgebrochen.",
            TaskCanceledException or HttpRequestException => "Die Verbindung zu kie.ai ist abgerissen.",
            _ => e.Message,
        } + (job.TaskId is not null && job.Files.Count == 0 && e is not InvalidOperationException ? " Der Auftrag liegt bei kie.ai – „Ergebnis holen“ versucht es ohne neue Kosten." : "");
        job.State = "fehlgeschlagen";
    }

    async Task KieFinish(KieJob job, KieClient client, CancellationToken ct)
    {
        var m = job.Model;
        job.State = "kie.ai arbeitet"; KieRefresh();
        var max = m.Kind is KieKind.Video or KieKind.Avatar or KieKind.Musik ? TimeSpan.FromMinutes(25) : TimeSpan.FromMinutes(10);
        var t = await client.WaitAsync(job.TaskId!, KieDelayOverride ?? ((d, c) => Task.Delay(d, c)), max, x => { job.State = x.State switch { "queuing" or "waiting" => "wartet bei kie.ai", "generating" => "wird erzeugt", _ => x.State }; KieRefresh(); }, ct);
        job.Credits = t.Credits;
        if (!t.Ok) { job.TaskId = null; throw new KieException(501, KieClient.Explain(501, t.FailMessage)); } // fehlgeschlagen bei kie.ai: nichts mehr abzuholen
        if (t.Urls.Count == 0) throw new KieException(500, "kie.ai hat kein Ergebnis geliefert.");
        var dir = KieDir(job.Studio); Directory.CreateDirectory(dir);
        job.State = "wird gespeichert"; KieRefresh();
        job.Files.Clear();
        for (var i = 0; i < t.Urls.Count; i++)
        {
            var url = t.Urls[i];
            if (!KieModels.PublicHttps(url)) throw new KieException(500, "kie.ai nennt eine ungewöhnliche Adresse für das Ergebnis. Aus Sicherheit nicht geladen.");
            var r = await KieWeb.SendAsync("GET", url, new Dictionary<string, string>(), null, null, ct); // ohne Schlüssel: der geht nur an kie.ai selbst
            if (!r.Ok || r.Body.Length == 0) throw new KieException(r.Status, "Das Ergebnis ließ sich nicht herunterladen. Es liegt noch 14 Tage bei kie.ai.");
            if (r.Body.Length > KieMaxResult) throw new KieException(500, "Das Ergebnis ist ungewöhnlich groß und wurde nicht gespeichert.");
            if (!KieModels.LooksLike(r.Body, m.Kind)) throw new KieException(500, "Das Heruntergeladene ist keine passende Bild-, Video- oder Tondatei.");
            var file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{m.Kind.ToString().ToLowerInvariant()}-{job.Id}-{i + 1}{KieModels.Extension(url, m.Kind)}");
            await File.WriteAllBytesAsync(file, r.Body, ct);
            var inp = job.Input;
            KieProvenance.Write(file, new("kie.ai", m.Id, m.Kind.ToString(), inp.Prompt.Length > 0 ? inp.Prompt : inp.Lyrics, inp.Title, inp.Style, job.TaskId!, t.Credits, DateTime.UtcNow, url, ""));
            job.Files.Add(file);
        }
        job.State = "fertig";
        _core.Audit.Append(Principal.LocalOwner.Name, "kie.ergebnis", $"{m.Id} {job.Files.Count} Datei(en)" + (t.Credits is { } c ? $", {c} Credits" : ""));
        _ = KieBalance();
    }

    /// <summary>Knopf in der Studio-Leiste, öffnet das kie.ai-Feld für dieses Studio.</summary>
    Button KieBarButton(string studio)
    {
        var b = new Button { Name = "auftrag-kie", Content = "kie.ai", Height = 32 };
        ToolTip.SetTip(b, "Bilder, Videos, Musik und Sprache über dein eigenes kie.ai-Konto – kostenpflichtig, optional. Ohne Schlüssel passiert nichts.");
        var fl = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        fl.Opening += (_, _) => fl.Content = new ScrollViewer { MaxHeight = 680, Width = 540, Content = KiePanel(studio) };
        fl.Closed += (_, _) => _kieRefresh = null;
        b.Flyout = fl;
        return b;
    }

    /// <summary>Für Tests und die Sichtprüfung: das Feld eines Studios ohne Klappfenster.</summary>
    public Control KiePanelForTest(string studio) => KiePanel(studio);

    Control KiePanel(string studio)
    {
        var host = new ContentControl { Name = "kie-panel" };
        var kinds = KieModels.ForStudio(studio);
        var kind = kinds[0]; KieModel model = KieModels.Of(kind).First();
        var askAt = DateTime.MinValue; var consent = false; var unknownOk = false; var offAsk = false;
        var images = new List<string>(); string? audio = null;
        var f = new Dictionary<string, string> { ["aspect"] = "1:1", ["seconds"] = "5", ["upscale"] = "2" };
        var jobsBox = new StackPanel { Name = "kie-jobs", Spacing = 2 };
        var balance = new TextBlock { Name = "kie-balance", Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
        var msg = new TextBlock { Name = "kie-msg", Foreground = Brush.Parse("#E0A458"), FontSize = 12, TextWrapping = TextWrapping.Wrap };

        void ResetAsk() => askAt = DateTime.MinValue;
        TextBox Box(string key, string name, string ph, bool multi = false)
        {
            var t = new TextBox { Name = name, Text = f.GetValueOrDefault(key, ""), PlaceholderText = ph, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 70 : 0 };
            t.TextChanged += (_, _) => { f[key] = t.Text ?? ""; ResetAsk(); };
            return t;
        }
        ComboBox Choice(string key, string name, IReadOnlyList<string> items)
        {
            var c = new ComboBox { Name = name, ItemsSource = items, SelectedItem = items.Contains(f.GetValueOrDefault(key, "")) ? f[key] : items[0], MinWidth = 120 };
            f[key] = (string)c.SelectedItem!;
            c.SelectionChanged += (_, _) => { if (c.SelectedItem is string s) f[key] = s; ResetAsk(); };
            return c;
        }
        TextBlock Label(string t) => new() { Text = t, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 6, 0, 2) };

        // Nur Kontostand, Meldung und Auftragsliste werden laufend erneuert – das Formular bleibt stehen, damit beim Tippen nichts verloren geht
        void Live()
        {
            balance.Text = _kieCredits is { } cr ? $"Guthaben: {cr.ToString("N0", De)} Credits (≈ {(cr * KieClient.UsdPerCredit).ToString("0.00", De)} $)" : "Guthaben: wird abgefragt …";
            msg.Text = _kieMsg; msg.IsVisible = _kieMsg.Length > 0;
            jobsBox.Children.Clear();
            var jobs = _kieJobs.Where(j => j.Studio == studio).Take(8).ToList();
            if (jobs.Count > 0) jobsBox.Children.Add(H("Aufträge", 13));
            foreach (var j in jobs)
            {
                var row = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 4) };
                row.Children.Add(P($"{j.Started:HH:mm} · {j.Model.Label} · {j.Label}", Text, 12));
                row.Children.Add(P(j.Error ?? (j.State + (j.Credits is { } c ? $" · {c.ToString("0.#", De)} Credits" : "")), j.Error is null ? Muted : Brush.Parse("#E0A458"), 11));
                var actions = new WrapPanel();
                foreach (var file in j.Files)
                {
                    var open = new Button { Content = "Öffnen: " + Path.GetFileName(file), Padding = new Thickness(8, 2), FontSize = 11, Margin = new Thickness(0, 0, 4, 4) };
                    var path = file; open.Click += (_, _) => Launch(path);
                    actions.Children.Add(open);
                }
                if (j.Files.Count > 0)
                {
                    var dir = new Button { Content = "Ordner öffnen", Padding = new Thickness(8, 2), FontSize = 11, Margin = new Thickness(0, 0, 4, 4) };
                    var d = Path.GetDirectoryName(j.Files[0])!; dir.Click += (_, _) => Launch(d);
                    actions.Children.Add(dir);
                }
                if (j.CanResume)
                {
                    var again = new Button { Name = "kie-resume-" + j.Id, Content = "Ergebnis holen", Padding = new Thickness(8, 2), FontSize = 11 };
                    var job = j; again.Click += (_, _) => _ = KieResume(job);
                    actions.Children.Add(again);
                }
                row.Children.Add(actions);
                jobsBox.Children.Add(row);
            }
        }

        void Render()
        {
            var sp = new StackPanel { Spacing = 4, Margin = new Thickness(14) };
            sp.Children.Add(H($"kie.ai – {StudioCatalog.NameOf(studio, studio)}", 15));
            sp.Children.Add(P("Eigenes kie.ai-Konto, Abrechnung nach Verbrauch. Die Kosten stehen vor dem Start; erzeugt wird erst nach Bestätigung. Ergebnisse werden sofort gespeichert.", Muted, 11));
            if (!KieOn)
            {
                var key = new TextBox { Name = "kie-key", PasswordChar = '•', PlaceholderText = "kie.ai-Schlüssel einfügen" };
                var get = new Button { Name = "kie-get", Content = "Schlüssel bei kie.ai holen", Padding = new Thickness(10, 4) };
                get.Click += (_, _) => Launch(KieClient.KeyPage);
                var go = new Button { Name = "kie-connect", Content = "Verbinden", Padding = new Thickness(12, 4) }; go.Classes.Add("accent");
                go.Click += async (_, _) => { if (await KieConnect(key.Text ?? "")) Render(); };
                sp.Children.Add(new StackPanel { Spacing = 6, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Children = { get, key, go } });
                sp.Children.Add(P("Der Schlüssel wird verschlüsselt auf diesem Rechner gespeichert, nur für dein Konto. Nicht enthalten: Midjourney und Chat-Modelle.", Muted, 11));
                sp.Children.Add(msg); Live();
                host.Content = sp; return;
            }
            var refresh = new Button { Name = "kie-refresh", Content = "Aktualisieren", Padding = new Thickness(8, 2) }; refresh.Click += (_, _) => _ = KieBalance();
            var logs = new Button { Name = "kie-logs", Content = "Verbrauch", Padding = new Thickness(8, 2) }; logs.Click += (_, _) => Launch(KieClient.LogsPage);
            var check = new Button { Name = "kie-check", Content = "Modelle prüfen", Padding = new Thickness(8, 2) }; check.Click += (_, _) => _ = KieCheckModels();
            var off = new Button { Name = "kie-disconnect", Content = offAsk ? "Ja, Schlüssel löschen" : "Trennen", Padding = new Thickness(8, 2) };
            off.Click += (_, _) => { if (!offAsk) { offAsk = true; Render(); } else { offAsk = false; KieDisconnect(); Render(); } };
            sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 6), Children = { balance, new Border { Width = 10 }, refresh, new Border { Width = 4 }, logs, new Border { Width = 4 }, check, new Border { Width = 4 }, off } });
            if (_kieCredits is null && _kieMsg.Length == 0) _ = KieBalance();

            var kindBox = new ComboBox { Name = "kie-kind", ItemsSource = kinds.Select(KieModels.KindLabel).ToList(), SelectedIndex = kinds.ToList().IndexOf(kind), HorizontalAlignment = HorizontalAlignment.Stretch };
            kindBox.SelectionChanged += (_, _) => { if (kindBox.SelectedIndex >= 0 && kinds[kindBox.SelectedIndex] != kind) { kind = kinds[kindBox.SelectedIndex]; model = KieModels.Of(kind).First(); ResetAsk(); images.Clear(); audio = null; Render(); } };
            var models = KieModels.Of(kind).ToList();
            var modelBox = new ComboBox { Name = "kie-model", ItemsSource = models.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, models.IndexOf(model)), HorizontalAlignment = HorizontalAlignment.Stretch };
            modelBox.SelectionChanged += (_, _) => { if (modelBox.SelectedIndex >= 0) { model = models[modelBox.SelectedIndex]; ResetAsk(); Render(); } };
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { new StackPanel { Children = { Label("Art"), kindBox } }, Pos(new StackPanel { Children = { Label("Modell"), modelBox } }, 2) } });
            if (_kieMissing?.Contains(model.Id) == true) sp.Children.Add(P("Dieses Modell steht nicht mehr in der Übersicht von kie.ai und ist möglicherweise abgeschaltet.", Brush.Parse("#E0A458"), 11));
            sp.Children.Add(P(model.Hint + (model.Verified ? "" : " · Eingabeformat bei kie.ai nicht vollständig dokumentiert."), Muted, 11));

            Button Pick(string name, string label, string[] patterns, Action<string> set)
            {
                var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 0, 0) };
                b.Click += async (_, _) => { if (await PickMediaFile(name, label, patterns) is { } file) { set(file); ResetAsk(); Render(); } };
                return b;
            }
            string[] imgPat = ["*.png", "*.jpg", "*.jpeg", "*.webp"];
            switch (kind)
            {
                case KieKind.Bild or KieKind.BildBearbeiten or KieKind.Video:
                    sp.Children.Add(Label("Beschreibung")); sp.Children.Add(Box("prompt", "kie-prompt", kind == KieKind.BildBearbeiten ? "Was soll sich am Bild ändern?" : "Motiv, Umgebung, Licht, Stimmung", true));
                    sp.Children.Add(Label("Seitenverhältnis")); sp.Children.Add(Choice("aspect", "kie-aspect", kind == KieKind.Video ? ["16:9", "9:16", "1:1"] : ["1:1", "16:9", "9:16", "4:3", "3:4", "3:2", "2:3"]));
                    if (kind == KieKind.Video) { sp.Children.Add(Label("Länge in Sekunden")); var sec = Choice("seconds", "kie-seconds", ["5", "10", "15"]); sec.SelectionChanged += (_, _) => Render(); sp.Children.Add(sec); }
                    if (kind != KieKind.Bild) sp.Children.Add(Pick("kie-image", images.Count > 0 ? "Bild: " + Path.GetFileName(images[0]) : kind == KieKind.Video ? "Startbild (optional) …" : "Bild wählen …", imgPat, x => { images.Clear(); images.Add(x); }));
                    break;
                case KieKind.Freistellen or KieKind.Hochskalieren:
                    sp.Children.Add(Pick("kie-image", images.Count > 0 ? "Bild: " + Path.GetFileName(images[0]) : "Bild wählen …", imgPat, x => { images.Clear(); images.Add(x); }));
                    if (kind == KieKind.Hochskalieren) { sp.Children.Add(Label("Vergrößern um")); sp.Children.Add(Choice("upscale", "kie-upscale", ["2", "4", "8"])); }
                    break;
                case KieKind.Avatar:
                    sp.Children.Add(Pick("kie-image", images.Count > 0 ? "Porträt: " + Path.GetFileName(images[0]) : "Porträt wählen …", ["*.png", "*.jpg", "*.jpeg"], x => { images.Clear(); images.Add(x); }));
                    sp.Children.Add(Pick("kie-audio", audio is not null ? "Aufnahme: " + Path.GetFileName(audio) : "Sprachaufnahme wählen …", ["*.mp3", "*.wav", "*.m4a", "*.ogg"], x => audio = x));
                    sp.Children.Add(Label("Ausdruck (optional)")); sp.Children.Add(Box("prompt", "kie-prompt", "z. B. freundlich, ruhig"));
                    var cb = new CheckBox { Name = "kie-consent", IsChecked = consent, Content = new TextBlock { Text = "Das Bild zeigt mich selbst oder eine erfundene Figur, und die Stimme gehört mir oder ich habe die Einwilligung.", TextWrapping = TextWrapping.Wrap, MaxWidth = 460 } };
                    cb.IsCheckedChanged += (_, _) => { consent = cb.IsChecked == true; ResetAsk(); };
                    sp.Children.Add(cb);
                    break;
                case KieKind.Musik:
                    sp.Children.Add(Label("Titel")); sp.Children.Add(Box("title", "kie-title", "Titel des Lieds"));
                    sp.Children.Add(Label("Stil")); sp.Children.Add(Box("style", "kie-style", "Genre, Stimmung, Instrumente, z. B. „ruhiger Indie-Pop, Akustikgitarre, warm“"));
                    var inst = new CheckBox { Name = "kie-instrumental", Content = "Instrumental (ohne Gesang)", IsChecked = f.GetValueOrDefault("instrumental") == "ja" };
                    inst.IsCheckedChanged += (_, _) => { f["instrumental"] = inst.IsChecked == true ? "ja" : "nein"; ResetAsk(); };
                    sp.Children.Add(inst);
                    sp.Children.Add(Label("Liedtext (nur eigener Text)")); sp.Children.Add(Box("lyrics", "kie-lyrics", "[Strophe]\n…", true));
                    sp.Children.Add(Label("Länge in Sekunden")); sp.Children.Add(Choice("seconds", "kie-seconds", ["60", "120", "180", "240"]));
                    break;
                case KieKind.Sprache:
                    sp.Children.Add(Label("Text")); sp.Children.Add(Box("prompt", "kie-prompt", "Text zum Vorlesen, bis 5000 Zeichen", true));
                    sp.Children.Add(Label("Stimm-ID (leer = Standardstimme)")); sp.Children.Add(Box("voice", "kie-voice", KieModels.DefaultVoice));
                    break;
            }
            var est = KieInputFrom(f);
            sp.Children.Add(P("Kosten: " + KieModels.CostText(model, est), Text, 12));
            if (!KieModels.PriceKnown(model, est))
            {
                var ok = new CheckBox { Name = "kie-unknown-price", IsChecked = unknownOk, Content = "Ich starte trotz unbekanntem Preis und sehe die Kosten danach unter „Verbrauch“." };
                ok.IsCheckedChanged += (_, _) => { unknownOk = ok.IsChecked == true; ResetAsk(); };
                sp.Children.Add(ok);
            }
            var asking = askAt != DateTime.MinValue;
            var start = new Button { Name = "kie-start", Content = asking ? "Ja, kostenpflichtig erzeugen" : "Erzeugen …", Padding = new Thickness(16, 6), Margin = new Thickness(0, 6, 0, 0) };
            start.Classes.Add("accent");
            start.Click += (_, _) =>
            {
                var input = KieInputFrom(f);
                if (KieModels.Blocked(model, input with { ImageUrls = images.Count > 0 ? images : null, AudioUrl = audio }, consent) is { } why) { _kieMsg = why; Live(); return; }
                if (!KieModels.PriceKnown(model, input) && !unknownOk) { _kieMsg = "Der Preis ist vorab unbekannt. Bitte das Häkchen setzen, wenn du trotzdem starten willst."; Live(); return; }
                if (askAt == DateTime.MinValue) { askAt = DateTime.UtcNow; _kieMsg = $"Das kostet {KieModels.CostText(model, input)} von deinem kie.ai-Guthaben. Zum Starten bestätigen."; Render(); return; }
                if (DateTime.UtcNow - askAt < TimeSpan.FromMilliseconds(600)) return; // Doppelklick zählt nicht als Bestätigung
                ResetAsk(); _kieMsg = "";
                _ = KieRun(studio, model, input, images.ToList(), audio, consent);
                Render();
            };
            sp.Children.Add(start);
            sp.Children.Add(msg);
            sp.Children.Add(jobsBox);
            Live();
            host.Content = sp;
        }
        _kieRefresh = Live;
        Render();
        return host;
    }

    static KieInput KieInputFrom(IReadOnlyDictionary<string, string> f)
    {
        int.TryParse(f.GetValueOrDefault("seconds", "5"), out var sec);
        return new KieInput(Prompt: f.GetValueOrDefault("prompt", "").Trim(), Aspect: f.GetValueOrDefault("aspect", "1:1"), Seconds: sec <= 0 ? 5 : sec,
            Title: f.GetValueOrDefault("title", "").Trim(), Style: f.GetValueOrDefault("style", "").Trim(), Lyrics: f.GetValueOrDefault("lyrics", "").Trim(),
            Instrumental: f.GetValueOrDefault("instrumental") == "ja", Voice: f.GetValueOrDefault("voice", "").Trim(), Upscale: f.GetValueOrDefault("upscale", "2"));
    }
}
