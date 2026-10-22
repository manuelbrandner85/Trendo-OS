using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-15: echte Vorschau, Aufnahme und Livestream. Der Stream startet nur nach Freigabe.</summary>
public partial class MainWindow
{
    private enum LiveAskState { None, Pending, Approved }

    private LiveSession? _live;
    private ICaptureProcessFactory _captureFactory = new FfmpegCaptureFactory();
    private DispatcherTimer? _liveTimer;
    private Bitmap? _liveBmp;
    private DateTime _liveStamp;
    private string _liveMsg = "Bereit. Es geht nichts von allein auf Sendung.";
    private string? _ffmpegResolved;
    private bool _liveAskInstall;
    private bool _liveBusy;
    private VideoKind _liveVideo = VideoKind.Screen;
    private LivePlatform _livePlatform = LivePlatform.YouTube;
    private string _liveQuality = "720p30";
    private string _liveCustom = "";
    private string _liveKeyDraft = "";
    private bool _liveMic;
    private bool _liveCopy;
    private List<WindowGrab> _liveWindows = [];
    private List<DeviceItem> _liveCams = [];
    private List<DeviceItem> _liveMics = [];
    private int _liveWindowIndex, _liveCamIndex, _liveMicIndex;
    private string? _liveApprovalId;
    private LiveAskState _liveAsk;
    private CaptureChoice? _frozen;
    private LivePlatform _frozenPlatform;
    private string? _frozenCustom;
    private string _frozenLabel = "";

    /// <summary>null = echtes FFmpeg suchen. Leer = fehlt (nur Tests). Sonst dieser Pfad.</summary>
    public string? LiveFfmpegPath { get; set; }
    public LiveSession Live => _live ??= new LiveSession(_captureFactory);
    public ICaptureProcessFactory CaptureFactory { get => _captureFactory; set { _captureFactory = value; _live = new LiveSession(value); } }
    public Func<IReadOnlyList<WindowGrab>> FindWindows { get; set; } = DesktopCapture.Windows;
    public Func<string, CaptureDevices> FindDevices { get; set; } = DesktopCapture.Devices;

    private void StartLiveTimer()
    {
        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _liveTimer.Tick += (_, _) => RefreshLivePreview();
        _liveTimer.Start();
    }

    private static CaptureOs ThisOs() => OperatingSystem.IsWindows() ? CaptureOs.Windows : OperatingSystem.IsMacOS() ? CaptureOs.Mac : CaptureOs.Linux;

    private T? LiveCtrl<T>(string name) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<T>().FirstOrDefault(c => c.Name == name);

    private void ReadLiveForm()
    {
        if (LiveCtrl<ComboBox>("live-platform") is { } pf && pf.SelectedIndex >= 0) _livePlatform = (LivePlatform)Math.Clamp(pf.SelectedIndex, 0, 2);
        if (LiveCtrl<ComboBox>("live-quality") is { } q && q.SelectedItem is string qs) _liveQuality = qs;
        if (LiveCtrl<TextBox>("live-rtmp") is { } r) _liveCustom = r.Text ?? "";
        if (LiveCtrl<TextBox>("live-key") is { } k) _liveKeyDraft = k.Text ?? "";
        if (LiveCtrl<CheckBox>("live-mic") is { } m) _liveMic = m.IsChecked == true;
        if (LiveCtrl<CheckBox>("live-copy") is { } c) _liveCopy = c.IsChecked == true;
        if (LiveCtrl<ComboBox>("live-windows") is { } w && w.SelectedIndex >= 0) _liveWindowIndex = w.SelectedIndex;
        if (LiveCtrl<ComboBox>("live-cameras") is { } cam && cam.SelectedIndex >= 0) _liveCamIndex = cam.SelectedIndex;
        if (LiveCtrl<ComboBox>("live-mic-list") is { } mic && mic.SelectedIndex >= 0) _liveMicIndex = mic.SelectedIndex;
    }

    private string PreviewPath => Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), "vorschau.jpg");
    private string NewRecording() => Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), "aufnahme-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".mp4");

    private CaptureChoice CurrentChoice()
    {
        var rates = LiveFfmpeg.Rates(_livePlatform == LivePlatform.Twitch ? LivePlatform.Twitch : LivePlatform.YouTube, _liveQuality);
        var choice = new CaptureChoice
        {
            Os = ThisOs(), Video = _liveVideo, Fps = rates.Fps, Width = rates.W, Height = rates.H,
            VideoKbps = rates.VideoKbps, AudioKbps = rates.AudioKbps, Microphone = _liveMic,
            Display = Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 } d ? d : ":0"
        };
        if (_liveVideo == VideoKind.Window)
        {
            if (_liveWindows.Count == 0) throw new InvalidOperationException("Bitte zuerst auf „Fenster suchen“ tippen und ein Fenster wählen.");
            var win = _liveWindows[Math.Clamp(_liveWindowIndex, 0, _liveWindows.Count - 1)];
            choice = choice with { Device = win.Title, ByTitle = win.ByTitle, WinX = win.X, WinY = win.Y, WinW = win.Width, WinH = win.Height };
        }
        else if (_liveVideo == VideoKind.Camera)
        {
            if (_liveCams.Count == 0) throw new InvalidOperationException("Bitte zuerst auf „Kameras suchen“ tippen und eine Kamera wählen.");
            choice = choice with { Device = _liveCams[Math.Clamp(_liveCamIndex, 0, _liveCams.Count - 1)].Id };
        }
        if (_liveMic)
        {
            if (_liveMics.Count == 0)
            {
                if (ThisOs() != CaptureOs.Linux) throw new InvalidOperationException("Bitte zuerst auf „Mikrofone suchen“ tippen und ein Mikrofon wählen.");
                choice = choice with { Mic = "default" };
            }
            else choice = choice with { Mic = _liveMics[Math.Clamp(_liveMicIndex, 0, _liveMics.Count - 1)].Id };
        }
        return choice;
    }

    private static string SourceLabel(CaptureChoice c)
    {
        var mic = c.Microphone ? " mit Mikrofon" : "";
        return c.Video switch
        {
            VideoKind.Screen => "Bildschirm" + mic,
            VideoKind.Window => "Fenster „" + c.Device + "“" + mic,
            _ => "Kamera „" + c.Device + "“" + mic
        };
    }

    private async Task<string?> EnsureFfmpeg()
    {
        if (LiveFfmpegPath is not null)
        {
            if (LiveFfmpegPath.Length == 0 || !File.Exists(LiveFfmpegPath))
            {
                _liveAskInstall = true;
                _liveMsg = "FFmpeg fehlt. Trendo installiert es nur, wenn du zustimmst.";
                return null;
            }
            _liveAskInstall = false;
            return LiveFfmpegPath;
        }
        if (_ffmpegResolved is { } cached && File.Exists(cached)) { _liveAskInstall = false; return cached; }
        var check = await FfmpegReady.Resolve(_deps);
        if (check.Ok && check.Path is { } p && File.Exists(p))
        {
            _ffmpegResolved = p;
            _liveAskInstall = false;
            return p;
        }
        _liveAskInstall = true;
        _liveMsg = check.State == ToolState.Broken
            ? "FFmpeg ist da, startet aber nicht (" + check.Detail + "). Trendo installiert es nur, wenn du zustimmst."
            : "FFmpeg fehlt. Trendo installiert es nur, wenn du zustimmst.";
        return null;
    }

    private Button LiveBtn(string name, string label, Func<Task> click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Thickness(10, 3), Background = primary ? Accent : Brushes.Transparent };
        b.Click += async (_, _) =>
        {
            try { await click(); }
            catch (Exception ex) when (ex is ApprovalException or OfflineException or InvalidOperationException or ArgumentException or IOException or FileNotFoundException or FormatException)
            { _liveMsg = ex.Message; Show("sendung"); }
        };
        return b;
    }

    private async Task CheckFfmpeg()
    {
        _ffmpegResolved = null;
        _liveMsg = "FFmpeg wird geprüft …";
        Show("sendung");
        var p = await EnsureFfmpeg();
        if (p is not null) _liveMsg = "FFmpeg ist da (" + Path.GetFileName(p) + ").";
        Show("sendung");
    }

    private async Task InstallFfmpeg()
    {
        var comp = DependencyCatalog.Get("ffmpeg");
        var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", "FFmpeg für Live streamen installieren", comp.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        _liveMsg = "FFmpeg wird installiert …";
        Show("sendung");
        var ok = await _deps.InstallAsync(Principal.LocalOwner, comp);
        _liveAskInstall = !ok;
        _ffmpegResolved = null;
        _liveMsg = ok
            ? "FFmpeg ist installiert. Du kannst jetzt die Vorschau starten."
            : "FFmpeg konnte nicht installiert werden. Unter Zusatzprogramme kannst du es erneut versuchen.";
        Show("sendung");
    }

    private void LaterFfmpeg()
    {
        _liveAskInstall = false;
        _liveMsg = "FFmpeg bleibt ungeinstalliert. Vorschau, Aufnahme und Stream brauchen es.";
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", "ffmpeg");
        Show("sendung");
    }

    private async Task RunCapture(bool record)
    {
        var ff = await EnsureFfmpeg();
        if (ff is null) { Show("sendung"); return; }
        ReadLiveForm();
        var choice = CurrentChoice();
        if (record)
        {
            var file = NewRecording();
            Live.StartRecording(ff, choice, PreviewPath, file);
            _core.Audit.Append(Principal.LocalOwner.Name, "live.record", Path.GetFileName(file));
        }
        else
        {
            Live.StartPreview(ff, choice, PreviewPath);
            _core.Audit.Append(Principal.LocalOwner.Name, "live.preview", choice.Video.ToString());
        }
        _liveMsg = "";
        Show("sendung");
    }

    private void SearchWindows()
    {
        ReadLiveForm();
        try
        {
            _liveWindows = FindWindows().ToList();
            _liveWindowIndex = 0;
            _liveVideo = VideoKind.Window;
            _liveMsg = _liveWindows.Count == 0 ? "Kein Fenster gefunden." : _liveWindows.Count + " Fenster gefunden. Such dir eins aus.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { _liveMsg = ex.Message; }
        Show("sendung");
    }

    private async Task SearchDevices(bool cameras)
    {
        if (_liveBusy) { _liveMsg = "Einen Moment, die Suche läuft schon."; Show("sendung"); return; }
        ReadLiveForm();
        _liveBusy = true;
        try
        {
            var ff = await EnsureFfmpeg();
            if (ff is null) { Show("sendung"); return; }
            _liveMsg = cameras ? "Suche Kameras …" : "Suche Mikrofone …";
            Show("sendung");
            var found = await Task.Run(() => FindDevices(ff));
            if (cameras)
            {
                _liveCams = found.Cameras.ToList();
                _liveCamIndex = 0;
                _liveVideo = VideoKind.Camera;
                if (_liveMics.Count == 0 && found.Microphones.Count > 0) _liveMics = found.Microphones.ToList();
                _liveMsg = _liveCams.Count == 0 ? "Keine Kamera gefunden." : _liveCams.Count + " Kameras gefunden. Such dir eine aus.";
            }
            else
            {
                _liveMics = found.Microphones.ToList();
                _liveMicIndex = 0;
                _liveMsg = _liveMics.Count == 0 ? "Kein Mikrofon gefunden." : _liveMics.Count + " Mikrofone gefunden. Such dir eins aus und setze das Häkchen, wenn es mit soll.";
            }
        }
        finally { _liveBusy = false; }
        Show("sendung");
    }

    private void SaveKey()
    {
        ReadLiveForm();
        if (string.IsNullOrWhiteSpace(_liveKeyDraft))
        {
            _liveMsg = StreamKeys.Saved(_core.Secrets, _livePlatform) ? "Der gespeicherte Schlüssel bleibt." : "Bitte den Stream-Schlüssel eintragen.";
            Show("sendung");
            return;
        }
        StreamKeys.Save(_core.Secrets, _livePlatform, _liveKeyDraft);
        _core.Audit.Append(Principal.LocalOwner.Name, "stream.key.saved", _livePlatform.ToString());
        _liveKeyDraft = "";
        _liveMsg = "Schlüssel gespeichert. Er steht nicht in der Anzeige und nicht im Protokoll.";
        Show("sendung");
    }

    private void DeleteKey()
    {
        ReadLiveForm();
        _core.Secrets.Delete(StreamKeys.Slot(_livePlatform));
        _liveKeyDraft = "";
        _core.Audit.Append(Principal.LocalOwner.Name, "stream.key.deleted", _livePlatform.ToString());
        _liveMsg = "Schlüssel gelöscht.";
        Show("sendung");
    }

    private void AskLive()
    {
        ReadLiveForm();
        if (_core.Offline) throw new OfflineException("Offline-Modus: Ein Livestream braucht eine Verbindung. Aufnehmen auf diesem PC geht weiter.");
        if (!string.IsNullOrWhiteSpace(_liveKeyDraft))
        {
            StreamKeys.Save(_core.Secrets, _livePlatform, _liveKeyDraft);
            _core.Audit.Append(Principal.LocalOwner.Name, "stream.key.saved", _livePlatform.ToString());
            _liveKeyDraft = "";
        }
        if (!StreamKeys.Saved(_core.Secrets, _livePlatform)) throw new InvalidOperationException("Bitte den Stream-Schlüssel eintragen und auf „Schlüssel speichern“ tippen.");
        var choice = CurrentChoice();
        var label = SourceLabel(choice);
        var req = LiveBroadcast.Ask(_core.Approvals, Principal.LocalOwner, _livePlatform, _liveCustom, label);
        _liveApprovalId = req.Id;
        _liveAsk = LiveAskState.Pending;
        _frozen = choice;
        _frozenPlatform = _livePlatform;
        _frozenCustom = _liveCustom;
        _frozenLabel = label;
        _liveMsg = "Wartet auf deine Freigabe. Der Stream startet nicht von allein.";
        Show("sendung");
    }

    private async Task GoLive()
    {
        if (_liveApprovalId is null || _frozen is null) throw new InvalidOperationException("Bitte zuerst auf „Live anfragen“ tippen.");
        var ff = await EnsureFfmpeg();
        if (ff is null) { Show("sendung"); return; }
        ReadLiveForm();
        if (_liveAsk == LiveAskState.Pending) _core.Approvals.Approve(_liveApprovalId, Principal.LocalOwner);
        var copy = _liveCopy ? NewRecording() : null;
        LiveBroadcast.Go(_core.Approvals, _liveApprovalId, Live, _frozenPlatform, _frozenCustom, _core.Secrets, ff, _frozen, PreviewPath, copy, _core.Offline);
        _liveApprovalId = null;
        _liveAsk = LiveAskState.None;
        _liveMsg = "";
        Show("sendung");
    }

    private void DenyLive()
    {
        if (_liveApprovalId is { } id && _liveAsk == LiveAskState.Pending)
            _core.Approvals.Deny(id, Principal.LocalOwner);
        _liveApprovalId = null;
        _liveAsk = LiveAskState.None;
        _frozen = null;
        _liveMsg = "Livestream abgelehnt. Es wurde nichts gesendet.";
        Show("sendung");
    }

    private void StopLive()
    {
        ReadLiveForm();
        var was = Live.Mode;
        Live.Stop();
        _core.Audit.Append(Principal.LocalOwner.Name, "live.stop", was.ToString());
        _liveMsg = "";
        Show("sendung");
    }

    private void OpenRecording()
    {
        if (Live.OutputFile is { } f && File.Exists(f)) { Launch(f); _liveMsg = "Aufnahme geöffnet."; }
        else _liveMsg = "Es gibt noch keine Aufnahme.";
        Show("sendung");
    }

    private void OpenRecordingFolder()
    {
        var d = Fach.StoreDir(_core.DataRoot, "sendung");
        Launch(d);
        _liveMsg = "Ordner geöffnet.";
        Show("sendung");
    }

    private string StatusText()
    {
        if (_liveMsg.Length > 0) return _liveMsg;
        if (Live.Mode == LiveMode.Broadcasting) return "Live auf " + LiveBroadcast.Where(_frozenPlatform, _frozenCustom) + ". Mit Stopp beenden.";
        return Live.StatusLine();
    }

    private void RefreshLivePreview()
    {
        try
        {
            if (_live is null) return;
            if (_live.Exited) _live.NoteExit();
            var img = LiveCtrl<Image>("live-preview");
            var hint = LiveCtrl<TextBlock>("live-preview-hint");
            var status = LiveCtrl<TextBlock>("live-status");
            if (status is not null && _liveMsg.Length == 0) status.Text = StatusText();
            if (img is null || _live.PreviewFile is null || !File.Exists(_live.PreviewFile)) return;
            var write = File.GetLastWriteTimeUtc(_live.PreviewFile);
            if (write == _liveStamp && img.Source is not null) return;
            using var fs = new FileStream(_live.PreviewFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bmp = new Bitmap(fs);
            var prev = _liveBmp;
            _liveBmp = bmp;
            img.Source = bmp;
            _liveStamp = write;
            if (hint is not null) hint.IsVisible = false;
            prev?.Dispose();
        }
        catch { /* Datei gerade im Schreiben oder Seite schon zu */ }
    }

    private Control LiveSection()
    {
        var box = new StackPanel { Spacing = 8 };
        var status = new TextBlock { Name = "live-status", Text = StatusText(), Foreground = Text, FontSize = 14, TextWrapping = TextWrapping.Wrap };
        box.Children.Add(status);
        var stop = new Button
        {
            Name = "live-stop", Content = "Stopp", Background = Brush.Parse("#E11D48"), Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = 16, Padding = new Thickness(12, 8), FontWeight = FontWeight.Bold
        };
        stop.Click += (_, _) => StopLive();
        box.Children.Add(stop);

        var sources = new StackPanel { Spacing = 6 };
        sources.Children.Add(H("Quellen", 14));
        sources.Children.Add(P("Was soll ins Bild? Mikrofon kannst du dazunehmen.", null, 11));
        sources.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children =
            {
                LiveBtn("live-src-screen", "Bildschirm", () => { ReadLiveForm(); _liveVideo = VideoKind.Screen; Show("sendung"); return Task.CompletedTask; }, _liveVideo == VideoKind.Screen),
                LiveBtn("live-src-window", "Fenster", () => { ReadLiveForm(); _liveVideo = VideoKind.Window; Show("sendung"); return Task.CompletedTask; }, _liveVideo == VideoKind.Window),
                LiveBtn("live-src-camera", "Kamera", () => { ReadLiveForm(); _liveVideo = VideoKind.Camera; Show("sendung"); return Task.CompletedTask; }, _liveVideo == VideoKind.Camera)
            }
        });
        if (_liveVideo == VideoKind.Window)
        {
            sources.Children.Add(LiveBtn("live-windows-search", "Fenster suchen", () => { SearchWindows(); return Task.CompletedTask; }));
            if (_liveWindows.Count > 0)
            {
                var win = new ComboBox { Name = "live-windows", ItemsSource = _liveWindows, SelectedIndex = Math.Clamp(_liveWindowIndex, 0, _liveWindows.Count - 1), MinWidth = 280 };
                sources.Children.Add(win);
            }
        }
        else if (_liveVideo == VideoKind.Camera)
        {
            sources.Children.Add(LiveBtn("live-cam-search", "Kameras suchen", () => SearchDevices(true)));
            if (_liveCams.Count > 0)
                sources.Children.Add(new ComboBox { Name = "live-cameras", ItemsSource = _liveCams, SelectedIndex = Math.Clamp(_liveCamIndex, 0, _liveCams.Count - 1), MinWidth = 280 });
        }
        else sources.Children.Add(P("Der ganze Bildschirm.", null, 11));
        var micRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var mic = new CheckBox { Name = "live-mic", Content = "Mikrofon mit aufnehmen", IsChecked = _liveMic, Foreground = Text };
        micRow.Children.Add(mic);
        micRow.Children.Add(LiveBtn("live-mic-search", "Mikrofone suchen", () => SearchDevices(false)));
        sources.Children.Add(micRow);
        if (_liveMics.Count > 0)
            sources.Children.Add(new ComboBox { Name = "live-mic-list", ItemsSource = _liveMics, SelectedIndex = Math.Clamp(_liveMicIndex, 0, _liveMics.Count - 1), MinWidth = 280 });
        box.Children.Add(WCard(sources));

        var picture = new StackPanel { Spacing = 6 };
        picture.Children.Add(H("Bild", 14));
        var qualities = LiveFfmpeg.Qualities(_livePlatform).ToList();
        if (!qualities.Contains(_liveQuality)) _liveQuality = qualities[0];
        var quality = new ComboBox { Name = "live-quality", ItemsSource = qualities, SelectedItem = _liveQuality, MinWidth = 140 };
        picture.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { P("Qualität", null, 11), quality } });
        picture.Children.Add(new TextBlock { Name = "live-preview-hint", Text = "Noch kein Bild. Mit „Vorschau starten“ siehst du die Quelle hier.", Foreground = Muted, TextWrapping = TextWrapping.Wrap, IsVisible = _liveBmp is null || Live.Mode == LiveMode.Idle && !File.Exists(PreviewPath) });
        picture.Children.Add(new Image { Name = "live-preview", Height = 220, Stretch = Stretch.Uniform });
        picture.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children =
            {
                LiveBtn("live-preview-start", "Vorschau starten", () => RunCapture(false), true),
                LiveBtn("live-record", "Aufnahme starten", () => RunCapture(true), true),
                LiveBtn("live-folder", "Aufnahmeordner öffnen", () => { OpenRecordingFolder(); return Task.CompletedTask; }),
                LiveBtn("live-open-file", "Letzte Aufnahme öffnen", () => { OpenRecording(); return Task.CompletedTask; })
            }
        });
        box.Children.Add(WCard(picture));

        var live = new StackPanel { Spacing = 6 };
        live.Children.Add(H("Live senden", 14));
        live.Children.Add(P("YouTube, Twitch oder ein eigener RTMP-Server. Der Schlüssel bleibt im Schlüsselspeicher. Es geht nichts von allein auf Sendung.", null, 11));
        var platform = new ComboBox { Name = "live-platform", ItemsSource = new[] { "YouTube", "Twitch", "Eigenes RTMP" }, SelectedIndex = (int)_livePlatform, MinWidth = 180 };
        live.Children.Add(platform);
        var rtmp = new TextBox { Name = "live-rtmp", Text = _liveCustom, PlaceholderText = "rtmp://server/anwendung", MinWidth = 320, IsVisible = _livePlatform == LivePlatform.Custom };
        platform.SelectionChanged += (_, _) =>
        {
            if (platform.SelectedIndex < 0) return;
            _livePlatform = (LivePlatform)Math.Clamp(platform.SelectedIndex, 0, 2);
            rtmp.IsVisible = _livePlatform == LivePlatform.Custom;
            var q = LiveFfmpeg.Qualities(_livePlatform).ToList();
            if (!q.Contains(_liveQuality)) _liveQuality = q[0];
            quality.ItemsSource = q;
            quality.SelectedItem = _liveQuality;
        };
        live.Children.Add(rtmp);
        var key = new TextBox { Name = "live-key", PasswordChar = '•', PlaceholderText = "Stream-Schlüssel", MinWidth = 240, Text = _liveKeyDraft };
        live.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children =
            {
                key,
                LiveBtn("live-key-save", "Schlüssel speichern", () => { SaveKey(); return Task.CompletedTask; }),
                LiveBtn("live-key-delete", "Schlüssel löschen", () => { DeleteKey(); return Task.CompletedTask; })
            }
        });
        live.Children.Add(new TextBlock
        {
            Name = "live-key-state",
            Text = StreamKeys.Saved(_core.Secrets, _livePlatform) ? "Für diese Plattform ist ein Schlüssel gespeichert." : "Noch kein Schlüssel für diese Plattform.",
            Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap
        });
        live.Children.Add(new CheckBox { Name = "live-copy", Content = "Kopie auf diesem PC speichern", IsChecked = _liveCopy, Foreground = Text });
        if (_liveAsk == LiveAskState.None)
            live.Children.Add(LiveBtn("live-ask", "Live anfragen …", () => { AskLive(); return Task.CompletedTask; }, true));
        else
        {
            live.Children.Add(P(_liveAsk == LiveAskState.Pending
                ? "Freigabe für " + LiveBroadcast.Where(_frozenPlatform, _frozenCustom) + ". Quelle: " + _frozenLabel + ". Erst „Ja, jetzt senden“ startet den Stream."
                : "Freigegeben für " + LiveBroadcast.Where(_frozenPlatform, _frozenCustom) + ". Quelle: " + _frozenLabel + ". „Jetzt senden“ startet den Stream.", Text, 12));
            live.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children =
                {
                    LiveBtn(_liveAsk == LiveAskState.Pending ? "live-yes" : "live-send", _liveAsk == LiveAskState.Pending ? "Ja, jetzt senden" : "Jetzt senden", GoLive, true),
                    LiveBtn("live-no", "Nein", () => { DenyLive(); return Task.CompletedTask; })
                }
            });
        }
        box.Children.Add(WCard(live));

        var tools = new StackPanel { Spacing = 6 };
        tools.Children.Add(H("FFmpeg", 14));
        tools.Children.Add(P("Vorschau, Aufnahme und Stream laufen über FFmpeg. Trendo prüft es und installiert es nur nach deiner Zustimmung.", null, 11));
        tools.Children.Add(LiveBtn("live-ffmpeg-check", "FFmpeg prüfen", CheckFfmpeg));
        if (_liveAskInstall)
        {
            var comp = DependencyCatalog.Get("ffmpeg");
            tools.Children.Add(P(comp.Name + " – Lizenz " + comp.License + ". " + comp.Reason + (comp.ApproxMb > 0 ? " Etwa " + comp.ApproxMb + " MB." : ""), null, 11));
            tools.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children =
                {
                    LiveBtn("live-ffmpeg-yes", "Ja, installieren", InstallFfmpeg, true),
                    LiveBtn("live-ffmpeg-later", "Später", () => { LaterFfmpeg(); return Task.CompletedTask; })
                }
            });
        }
        box.Children.Add(WCard(tools));
        return box;
    }

    /// <summary>Freigabe aus der Freigaben-Seite: nur merken, nicht senden.</summary>
    private void NoteLiveApproved(string id)
    {
        if (_liveApprovalId == id) _liveAsk = LiveAskState.Approved;
        _liveMsg = "Freigegeben. Im Studio auf „Jetzt senden“ tippen. Es startet nicht von allein.";
    }

    private void NoteLiveDenied(string id)
    {
        if (_liveApprovalId == id) { _liveApprovalId = null; _liveAsk = LiveAskState.None; _frozen = null; }
        _liveMsg = "Livestream abgelehnt. Es wurde nichts gesendet.";
    }
}
