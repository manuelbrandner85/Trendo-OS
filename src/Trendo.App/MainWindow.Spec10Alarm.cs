using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0750: Anlagen-Monitor – weitere Quellen (Leistungsindikatoren dieses PCs, wachsende Datei, HTTP im eigenen Netz nach Freigabe) und Alarmregeln
/// (über/unter, Dauer, Hysterese) mit Benachrichtigung und Protokoll. Regeln und Quellen stehen im Modell des Zwillings (Rückgängig über die Hülle, T-0716),
/// lassen sich als Datei aus- und einlesen. Trendo schaltet nichts: nur Anzeige und Benachrichtigung.</summary>
public partial class MainWindow
{
    PcCounters? _pcc;
    HttpClient? _twHttp;
    string _twSrcMsg = "";
    DateTime _twLastCheck;
    readonly HashSet<string> _twHttpBusy = [];
    bool _pccBusy, _twFileBusy, _twCloseHooked, _twClosed;

    /// <summary>Für Tests und Belege: zuletzt gemeldete Alarme (Text der Benachrichtigung).</summary>
    public List<string> TwinAlarmNotes { get; } = [];
    public string TwinSourceMessage => _twSrcMsg;
    /// <summary>Gegenprüfung T-0750: läuft gerade eine Messung oder Abfrage im Hintergrund?</summary>
    public bool TwinBusy => _pccBusy || _twFileBusy || _twHttpBusy.Count > 0;
    /// <summary>Für Tests: wird im Hintergrund unmittelbar vor dem Lesen aufgerufen („pc“ = Leistungsindikatoren, „datei“ = wachsende Datei).</summary>
    public Action<string>? TwinBackgroundHook { get; set; }
    /// <summary>Für Tests: die offenen Leistungsindikatoren (null, wenn freigegeben).</summary>
    public PcCounters? TwinCounters => _pcc;

    void HookTwinClose() { if (_twCloseHooked) return; _twCloseHooked = true; Closed += (_, _) => TwinRelease(true); }

    /// <summary>Gegenprüfung T-0750: Leistungsindikatoren freigeben, wenn die Seite verlassen oder das Fenster geschlossen wird (HTTP-Verbindung nur beim Schließen).</summary>
    void TwinRelease(bool closing)
    {
        _pcc?.Dispose(); _pcc = null;
        if (closing) { _twClosed = true; _twHttp?.Dispose(); _twHttp = null; }
    }

    /// <summary>Echte Leistungsindikatoren dieses PCs (CPU, RAM, Datenträger, Netz) als Sensoren von „Dieser PC“ aufzeichnen.
    /// Gegenprüfung T-0750: die Abfrage (die erste dauert spürbar) läuft im Hintergrund, das Ergebnis kommt auf dem UI-Faden an; kein Doppelstart.</summary>
    void MeasureCounters()
    {
        if (_pccBusy) return;
        HookTwinClose();
        var pcc = _pcc ??= new PcCounters();
        var hook = TwinBackgroundHook;
        _pccBusy = true;
        _ = Task.Run(() =>
        {
            Dictionary<string, double> vals = []; var at = DateTime.Now;
            try { hook?.Invoke("pc"); at = DateTime.Now; vals = pcc.Read(at); }
            catch (Exception e) when (e is not OutOfMemoryException) { vals = []; }
            Dispatcher.UIThread.Post(() =>
            {
                _pccBusy = false;
                if (_twClosed || vals.Count == 0) return; // Fenster inzwischen zu: nichts mehr anfassen
                foreach (var (name, unit, warn, alarm) in PcCounters.Sensors)
                {
                    if (!vals.TryGetValue(name, out var v)) continue;
                    if (!Twin.Model.Assets.Any(a => a.Name == "Dieser PC" && a.Sensors.Any(s => s.Name == name))) Twin.Define("Dieser PC", name, unit, warn, alarm, kind: "Computer");
                    Twin.Record(at, "Dieser PC", name, v, false);
                }
                Twin.Save(); TwinCheckRules();
                if (CurrentView == "zwilling") Show("zwilling");
            });
        });
    }

    /// <summary>Datei- und HTTP-Quellen abfragen, danach die Alarmregeln prüfen. Gegenprüfung T-0750: Dateien werden im Hintergrund gelesen
    /// (bis 1 MB, auch langsame Laufwerke), übernommen wird auf dem UI-Faden; <paramref name="done"/> bekommt dort die Zahl neuer Werte.
    /// false = eine Abfrage läuft noch (kein Doppelstart).</summary>
    public bool TwinPollSources(Action<int>? done = null)
    {
        foreach (var s in Twin.Model.Sources.Where(s => s.Kind == "http" && s.Approved).ToList())
        {
            if (!_twHttpBusy.Add(s.Id)) continue;
            HookTwinClose();
            var http = _twHttp ??= TwinHttp.Create();
            var src = s;
            _ = Task.Run(async () =>
            {
                double? v = null; string err = "";
                try { v = await TwinHttp.FetchAsync(http, src); }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    // Sammelstand 0.9.106 (zweite Prüfung T-0750, N2): die eigene Sperr-Begründung zeigen statt „keine Verbindung zum Internet“,
                    // und bei lokalen Geräten keine Hinweise auf Zugangsschlüssel oder Guthaben
                    err = e.Message.Contains("eigenen Netz", StringComparison.Ordinal) || e.InnerException?.Message.Contains("eigenen Netz", StringComparison.Ordinal) == true
                        ? (e.InnerException?.Message.Contains("eigenen Netz", StringComparison.Ordinal) == true ? e.InnerException!.Message : e.Message)
                        : e is HttpRequestException { StatusCode: { } code } ? $"Das Gerät antwortet mit Fehler {(int)code} – prüfe die Adresse (Pfad) im Gerät."
                        : "Das Gerät im eigenen Netz ist nicht erreichbar – ist es eingeschaltet und die Adresse richtig?";
                }
                Dispatcher.UIThread.Post(() =>
                {
                    _twHttpBusy.Remove(src.Id); if (_twClosed) return;
                    // Sammelstand 0.9.106 (N1): Quelle inzwischen entfernt oder per Rückgängig ersetzt → Ergebnis verwerfen (wie bei Dateien)
                    if (Twin.Model.Sources.FirstOrDefault(x => x.Id == src.Id) is not { } cur || cur.Target != src.Target) return;
                    cur.LastError = err;
                    if (v is { } x) { Twin.Record(DateTime.Now, cur.Asset, cur.Sensor, x, false); }
                    Twin.Save(); TwinCheckRules();
                    if (CurrentView == "zwilling") Show("zwilling");
                });
            });
        }
        var files = Twin.Model.Sources.Where(s => s.Kind == "datei")
            .Select(s => new TwinSource { Id = s.Id, Kind = s.Kind, Target = s.Target, Asset = s.Asset, Sensor = s.Sensor, JsonField = s.JsonField, Offset = s.Offset, Resync = s.Resync, FileId = s.FileId }).ToList();
        if (files.Count == 0) { TwinCheckRules(); done?.Invoke(0); return true; }
        if (_twFileBusy) return false;
        _twFileBusy = true;
        var hook = TwinBackgroundHook;
        _ = Task.Run(() =>
        {
            var now = DateTime.Now;
            var res = new List<(TwinSource Snap, TwinFileChunk? Chunk, string Error)>();
            foreach (var f in files)
            {
                try { hook?.Invoke("datei"); res.Add((f, TwinFeed.ReadFile(f, now), "")); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { res.Add((f, null, FriendlyError.Say(e))); }
            }
            Dispatcher.UIThread.Post(() =>
            {
                _twFileBusy = false;
                if (_twClosed) return;
                var n = 0; var errs = 0;
                foreach (var (snap, chunk, err) in res)
                {
                    // Quelle inzwischen entfernt, ersetzt oder schon weitergelesen → Ergebnis verwerfen
                    if (Twin.Model.Sources.FirstOrDefault(x => x.Id == snap.Id) is not { } s || s.Target != snap.Target || s.Offset != snap.Offset) continue;
                    if (chunk is null) { s.LastError = err; errs++; continue; }
                    n += TwinFeed.Apply(Twin, s, chunk, save: false);
                    if (chunk.Warning.Length > 0) errs++;
                }
                Twin.Save();
                _twSrcMsg = errs > 0 ? $"⚠ {n} neue Werte, {errs} Quelle(n) mit Hinweis – siehe Quelle." : $"{n} neue Werte aus Dateien.";
                TwinCheckRules();
                done?.Invoke(n);
                if (CurrentView == "zwilling") Show("zwilling");
            });
        });
        return true;
    }

    /// <summary>Alarmregeln prüfen: neue Auslösungen/Aufhebungen ins Protokoll (Modell + Prüfprotokoll von Trendo) und als Benachrichtigung zeigen.</summary>
    public List<TwinAlarm> TwinCheckRules()
    {
        _twLastCheck = DateTime.Now;
        var fresh = TwinRules.Check(Twin.Model);
        if (fresh.Count == 0) return fresh;
        Twin.Save();
        foreach (var a in fresh)
        {
            var unit = Twin.Model.Assets.FirstOrDefault(x => x.Name.Equals(a.Asset, StringComparison.OrdinalIgnoreCase))?.Sensors.FirstOrDefault(x => x.Name.Equals(a.Sensor, StringComparison.OrdinalIgnoreCase))?.Unit ?? "";
            var text = a.Raised ? $"⚠ Alarm „{a.Rule}“: {a.Asset} – {a.Sensor} {Fach.F(a.Value, 1)} {unit}".TrimEnd() : $"✓ Alarm „{a.Rule}“ vorbei: {a.Asset} – {a.Sensor} {Fach.F(a.Value, 1)} {unit}".TrimEnd();
            TwinAlarmNotes.Add(text);
            _core.Audit.Append(Principal.LocalOwner.Name, a.Raised ? "zwilling.alarm" : "zwilling.alarm.ende", $"{a.Rule}: {a.Asset}/{a.Sensor} = {a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        // höchstens drei Benachrichtigungen auf einmal, damit nichts überflutet
        foreach (var t in TwinAlarmNotes.TakeLast(Math.Min(3, fresh.Count))) Toast(t, ("Ansehen", () => Show("zwilling")));
        return fresh;
    }

    /// <summary>MQTT-Nachrichten kommen schnell: Regeln höchstens alle 2 Sekunden prüfen.</summary>
    void TwinCheckRulesThrottled() { if ((DateTime.Now - _twLastCheck).TotalSeconds >= 2) TwinCheckRules(); }

    // ---------- Regeln bearbeiten ----------
    public TwinRule TwinAddRule(string asset, string sensor, bool above, double threshold, double durationSeconds, double hysteresis, string? name = null)
    {
        var r = new TwinRule { Asset = asset, Sensor = sensor, Above = above, Threshold = threshold, DurationSeconds = durationSeconds, Hysteresis = hysteresis, Created = DateTime.Now };
        var unit = Twin.Model.Assets.FirstOrDefault(a => a.Name == asset)?.Sensors.FirstOrDefault(s => s.Name == sensor)?.Unit ?? "";
        r.Name = string.IsNullOrWhiteSpace(name) ? $"{sensor} {r.Describe(unit)}" : name.Trim();
        TwinRules.Validate(r);
        Twin.Model.Rules.Add(r); Twin.Save();
        _core.Audit.Append(Principal.LocalOwner.Name, "zwilling.regel.neu", $"{asset}/{sensor}: {r.Describe(unit)}");
        return r;
    }

    public void TwinRemoveRule(string id) { Twin.Model.Rules.RemoveAll(r => r.Id == id); Twin.Save(); }
    public void TwinToggleRule(string id, bool on) { if (Twin.Model.Rules.FirstOrDefault(r => r.Id == id) is { } r && TwinRules.SetEnabled(Twin.Model, r, on)) Twin.Save(); } // Gegenprüfung: wieder an = nichts aus der Aus-Zeit melden

    /// <summary>Alle Regeln als Datei speichern (Arbeitsordner „Anlagen-Monitor“).</summary>
    public string TwinExportRules(string? path = null)
    {
        if (path is not null) UserFile(path); // T-0764 (12. Prüfrunde): Ort/Name bestimmt der Nutzer – Kinder nur Freigabe, keine ausführbare Endung
        if (Twin.Model.Rules.Count == 0) throw new InvalidOperationException("Es gibt noch keine Alarmregel.");
        path ??= Path.Combine(WorkFolder("Anlagen-Monitor"), "alarmregeln.json");
        AtomicFile.WriteAllText(path, TwinRules.ExportJson(Twin.Model.Rules));
        return path;
    }

    /// <summary>Regeln aus einer Datei hinzufügen (gleiche Regeln werden nicht doppelt angelegt).</summary>
    public int TwinImportRules(string path)
    {
        if (!File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        if (new FileInfo(path).Length > TwinRules.MaxFileBytes) throw new FormatException($"Die Regel-Datei ist zu groß (höchstens {TwinRules.MaxFileBytes / 1024} KB).");
        var list = TwinRules.ImportJson(File.ReadAllText(path), DateTime.Now);
        var n = 0;
        foreach (var r in list)
        {
            if (Twin.Model.Rules.Any(x => x.Asset == r.Asset && x.Sensor == r.Sensor && x.Above == r.Above && x.Threshold == r.Threshold && x.DurationSeconds == r.DurationSeconds && x.Hysteresis == r.Hysteresis)) continue;
            if (!Twin.Model.Assets.Any(a => a.Name.Equals(r.Asset, StringComparison.OrdinalIgnoreCase) && a.Sensors.Any(s => s.Name.Equals(r.Sensor, StringComparison.OrdinalIgnoreCase)))) Twin.Define(r.Asset, r.Sensor, "");
            Twin.Model.Rules.Add(r); n++;
        }
        Twin.Save();
        return n;
    }

    public string TwinExportAlarmLog()
    {
        if (Twin.Model.Alarms.Count == 0) throw new InvalidOperationException("Das Alarmprotokoll ist noch leer.");
        var p = Path.Combine(WorkFolder("Anlagen-Monitor"), "alarmprotokoll.csv");
        AtomicFile.WriteAllText(p, TwinRules.LogCsv(Twin.Model.Alarms));
        return p;
    }

    // ---------- Quellen ----------
    public TwinSource TwinAddFileSource(string path, string asset, string sensor, string? jsonField = null)
    {
        path = path.Trim().Trim('"');
        if (TwinFeed.IsNetworkPath(path) || (path.Length > 0 && TwinFeed.IsNetworkPath(Path.GetFullPath(path)))) throw new ArgumentException(TwinFeed.NetworkPathRefused); // Gegenprüfung: vor jedem Dateizugriff
        if (path.Length == 0 || !File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(sensor)) throw new ArgumentException("Bitte Anlage und Sensor für die Werte der Datei eintragen.");
        if (Twin.Model.Sources.Any(s => s.Kind == "datei" && string.Equals(Path.GetFullPath(s.Target), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Diese Datei wird schon beobachtet.");
        var s = new TwinSource { Kind = "datei", Target = Path.GetFullPath(path), Asset = asset.Trim(), Sensor = sensor.Trim(), JsonField = string.IsNullOrWhiteSpace(jsonField) ? null : jsonField.Trim() };
        if (!Twin.Model.Assets.Any(a => a.Name == s.Asset && a.Sensors.Any(x => x.Name == s.Sensor))) Twin.Define(s.Asset, s.Sensor, "");
        Twin.Model.Sources.Add(s); Twin.Save();
        _core.Audit.Append(Principal.LocalOwner.Name, "zwilling.quelle.datei", Path.GetFileName(s.Target));
        return s;
    }

    /// <summary>HTTP-Quelle im eigenen Netz. Ohne <paramref name="approved"/> (ausdrückliche Freigabe) wird sie nicht angelegt.</summary>
    public TwinSource TwinAddHttpSource(string url, string asset, string sensor, string? jsonField, bool approved)
    {
        if (!TwinFeed.IsLocalUrl(url, out var u)) throw new ArgumentException("Nur Adressen im eigenen Netz, z. B. http://192.168.1.20/status oder http://sensor.local/werte.");
        if (!approved) throw new ArgumentException("Bitte zuerst bestätigen, dass Trendo diese Adresse regelmäßig lesen darf.");
        if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(sensor)) throw new ArgumentException("Bitte Anlage und Sensor für die Werte eintragen.");
        var s = new TwinSource { Kind = "http", Target = u!.ToString(), Asset = asset.Trim(), Sensor = sensor.Trim(), JsonField = string.IsNullOrWhiteSpace(jsonField) ? null : jsonField.Trim(), Approved = true };
        if (!Twin.Model.Assets.Any(a => a.Name == s.Asset && a.Sensors.Any(x => x.Name == s.Sensor))) Twin.Define(s.Asset, s.Sensor, "");
        Twin.Model.Sources.Add(s); Twin.Save();
        _core.Audit.Append(Principal.LocalOwner.Name, "zwilling.quelle.http.freigabe", $"{u.Host}:{u.Port} (nur lesen)");
        return s;
    }

    public void TwinRemoveSource(string id) { Twin.Model.Sources.RemoveAll(s => s.Id == id); Twin.Save(); }

    /// <summary>Linke Spalte: Quellen anlegen und auflisten.</summary>
    Control TwinSourcesPanel()
    {
        var sp = new StackPanel { Spacing = 4, Name = "twin-src" };
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 0) }; b.Click += (_, _) => TwinRun(go); return b; }
        sp.Children.Add(P("Nur lesen: Trendo zeigt Werte an und meldet Alarme, schaltet aber nie ein Gerät.", null, 10.5));
        var asset = new TextBox { Name = "twin-src-asset", PlaceholderText = "Anlage", Text = TwinAsset ?? "" };
        var sensor = new TextBox { Name = "twin-src-sensor", PlaceholderText = "Sensor (z. B. Temperatur)" };
        var field = new TextBox { Name = "twin-src-field", PlaceholderText = "JSON-Feld (optional, z. B. ENERGY.Power)" };
        sp.Children.Add(asset); sp.Children.Add(sensor); sp.Children.Add(field);
        var file = new TextBox { Name = "twin-src-file", PlaceholderText = "Wachsende Datei mit Messwerten (.csv, .txt, .jsonl, .log)" };
        sp.Children.Add(file);
        sp.Children.Add(B("twin-src-file-add", "Datei hinzufügen", () => { var s = TwinAddFileSource(file.Text ?? "", asset.Text ?? "", sensor.Text ?? "", field.Text); TwinAsset = s.Asset; TwinSensor = s.Sensor; _twMsg = TwinPollSources(n => _twMsg = $"Datei wird beobachtet – {n} Werte gelesen.") ? "Datei wird gelesen …" : "Datei wird beobachtet – sie wird bei der nächsten Abfrage gelesen."; }));
        var url = new TextBox { Name = "twin-src-url", PlaceholderText = "HTTP im eigenen Netz, z. B. http://192.168.1.20/status" };
        var ok = new CheckBox { Name = "twin-src-http-ok", Content = new TextBlock { Text = "Ich erlaube Trendo, diese Adresse regelmäßig zu lesen.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Muted } };
        sp.Children.Add(url); sp.Children.Add(ok);
        sp.Children.Add(B("twin-src-http-add", "Adresse hinzufügen", () => { var s = TwinAddHttpSource(url.Text ?? "", asset.Text ?? "", sensor.Text ?? "", field.Text, ok.IsChecked == true); TwinAsset = s.Asset; TwinSensor = s.Sensor; TwinPollSources(); _twMsg = "Adresse freigegeben – Trendo liest sie bei jeder Abfrage."; }));
        foreach (var s in Twin.Model.Sources)
        {
            var id = s.Id;
            var label = (s.Kind == "http" ? "HTTP " + new Uri(s.Target).Authority : "Datei " + Path.GetFileName(s.Target)) + $" → {s.Asset} – {s.Sensor}";
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var del = B("twin-src-del-" + id, "Entfernen", () => { TwinRemoveSource(id); _twMsg = "Quelle entfernt."; });
            DockPanel.SetDock(del, Avalonia.Controls.Dock.Right); row.Children.Add(del);
            row.Children.Add(new TextBlock { Name = "twin-src-item-" + id, Text = label + (s.LastError.Length > 0 ? "\n⚠ " + s.LastError : ""), FontSize = 11, Foreground = s.LastError.Length > 0 ? Brush.Parse("#F87171") : Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            sp.Children.Add(row);
        }
        if (Twin.Model.Sources.Count > 0) sp.Children.Add(B("twin-src-poll", "Quellen jetzt abfragen", () => { _twMsg = TwinPollSources(_ => _twMsg = _twSrcMsg) ? "Quellen werden gelesen …" : "Die Quellen werden gerade schon gelesen …"; }));
        return new Expander { Name = "twin-src-box", Header = "Weitere Quellen", IsExpanded = Twin.Model.Sources.Count > 0, Content = sp, Margin = new Thickness(0, 6, 0, 0) };
    }

    /// <summary>Rechte Spalte: Alarmregeln des gewählten Sensors, Export/Import, Protokoll.</summary>
    Control TwinRulesPanel(string an, string sn, string unit)
    {
        var sp = new StackPanel { Spacing = 4, Name = "twin-rules" };
        Button B(string name, string label, Action go) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) }; b.Click += (_, _) => TwinRun(go); return b; }
        sp.Children.Add(H("Alarmregeln", 12));
        foreach (var r in Twin.Model.Rules.Where(r => r.Asset == an && r.Sensor == sn))
        {
            var id = r.Id; var active = TwinRules.IsActive(r); // Gegenprüfung: gespeicherter Zustand, kein Nachrechnen beim Neuzeichnen
            var on = new CheckBox { Name = "twin-rule-on-" + id, IsChecked = r.Enabled, Content = new TextBlock { Text = (active ? "● ausgelöst · " : r.Enabled ? "○ ruhig · " : "aus · ") + r.Describe(unit), FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = active ? Brush.Parse("#F87171") : Text } };
            on.IsCheckedChanged += (_, _) => TwinRun(() => TwinToggleRule(id, on.IsChecked == true));
            var row = new DockPanel();
            var del = B("twin-rule-del-" + id, "Entfernen", () => { TwinRemoveRule(id); _twMsg = "Regel entfernt."; });
            DockPanel.SetDock(del, Avalonia.Controls.Dock.Right); row.Children.Add(del); row.Children.Add(on);
            sp.Children.Add(row);
        }
        var dir = new ComboBox { Name = "twin-rule-dir", ItemsSource = new[] { "darüber liegt", "darunter liegt" }, SelectedIndex = 0, Width = 130 };
        var thr = new TextBox { Name = "twin-rule-thr", PlaceholderText = "Schwelle", Width = 80 };
        var dur = new TextBox { Name = "twin-rule-dur", Text = "0", Width = 80 };
        var hys = new TextBox { Name = "twin-rule-hys", Text = "0", Width = 80 };
        sp.Children.Add(Row(P("Alarm, wenn der Wert", Text, 12), dir));
        sp.Children.Add(Row(P(unit.Length > 0 ? $"Schwelle ({unit})" : "Schwelle", Text, 12), thr));
        sp.Children.Add(Row(P("mindestens (Sekunden)", Text, 12), dur));
        sp.Children.Add(Row(P("Hysterese", Text, 12), hys));
        sp.Children.Add(B("twin-rule-add", "Regel anlegen", () =>
        {
            if (string.IsNullOrWhiteSpace(thr.Text)) throw new ArgumentException("Bitte eine Schwelle eintragen.");
            var r = TwinAddRule(an, sn, dir.SelectedIndex != 1, Fach.Num(thr.Text), Fach.Num(dur.Text ?? "0"), Fach.Num(hys.Text ?? "0"));
            _twMsg = $"Regel angelegt: {r.Describe(unit)}. Sie gilt für neue Messwerte.";
        }));
        // Pfadfeld mit „Datei wählen …“ und Ziehen und Ablegen (V-07): nach der Auswahl liest „Regeln einlesen“ sofort ein
        var path = new TextBox { Name = "twin-rules-path", PlaceholderText = "Regel-Datei (.json) zum Einlesen" };
        sp.Children.Add(path);
        sp.Children.Add(new WrapPanel { Children = {
            B("twin-rules-import", "Regeln einlesen", () => { var f = Need(path, "die Regel-Datei"); var n = TwinImportRules(f.Trim('"')); _twMsg = $"{n} Alarmregel(n) übernommen."; }),
            B("twin-rules-export", "Regeln speichern", () => { var p = TwinExportRules(); _twMsg = "Gespeichert: " + p; NotifyFileResult(_twMsg); }) } });
        var log = Twin.Model.Alarms.Where(a => a.Asset == an && a.Sensor == sn).TakeLast(8).Reverse().ToList();
        sp.Children.Add(H($"Alarmprotokoll ({Twin.Model.Alarms.Count})", 12));
        sp.Children.Add(new SelectableTextBlock { Name = "twin-alarm-log", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Text = log.Count == 0 ? "Noch kein Alarm für diesen Sensor." : string.Join("\n", log.Select(a => $"{a.Time:dd.MM. HH:mm:ss} {(a.Raised ? "⚠ ausgelöst" : "✓ aufgehoben")} ({Fach.F(a.Value, 1)}) – {a.Rule}")) });
        if (Twin.Model.Alarms.Count > 0) sp.Children.Add(B("twin-alarm-export", "Protokoll speichern (CSV)", () => { var p = TwinExportAlarmLog(); _twMsg = "Gespeichert: " + p; NotifyFileResult(_twMsg); }));
        return sp;
    }
}
