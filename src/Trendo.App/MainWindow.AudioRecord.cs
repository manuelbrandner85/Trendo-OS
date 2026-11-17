using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0732 (Audit-Restliste Rang 2) für „Audio bearbeiten“:
/// · Aufnahme läuft, bis „Stopp“ gedrückt wird – mit Pegelanzeige und Zeit (vorher fest 10 s ohne Anzeige).
/// · Leertaste startet und hält die Wiedergabe an (Menü „Wiedergabe“, gilt überall im Programm außer beim Tippen in Feldern).
/// · Effekte mit einstellbaren Werten (<see cref="AuFxValues"/>) statt fester Zahlen.</summary>
public partial class MainWindow
{
    MicRecorder? _auRec; ProgramDocument? _auRecDoc; DispatcherTimer? _auRecTimer;
    Border? _auRecMeter, _auRecPeak; TextBlock? _auRecTime;
    double _auPlayLen;

    /// <summary>Tests und Belege: statt des Windows-Mikrofons eine andere FFmpeg-Eingabe (z. B. „-re -f lavfi -i sine=…“) – FFmpeg läuft echt, nur die Quelle ist ersetzt.</summary>
    public Func<string[]>? MicInputOverride { get; set; }
    /// <summary>Läuft gerade eine Aufnahme?</summary>
    public bool AuRecording => _auRec is not null;
    /// <summary>Aufgenommene Sekunden der laufenden Aufnahme.</summary>
    public double AuRecSeconds => _auRec?.Seconds ?? 0;
    /// <summary>Pegel der laufenden Aufnahme (0…1).</summary>
    public double AuRecLevel => _auRec?.Level ?? 0;

    /// <summary>Einstellbare Werte der Effekte (eine Sitzung lang, für alle Tondateien).</summary>
    public sealed class AuFxValues
    {
        public double GainDb { get; set; } = 3;          // Lauter/Leiser
        public double FadeSec { get; set; } = 2;         // Ein-/Ausblenden ohne Auswahl
        public double EchoSec { get; set; } = 0.3;       // Echo-Abstand
        public double HighpassHz { get; set; } = 80;     // Tiefen weg
        public double LowpassHz { get; set; } = 8000;    // Höhen weg
        public double SpeedPct { get; set; } = 10;       // Schneller/Langsamer
        public double GateDb { get; set; } = -45;        // Rauschen unterdrücken
        public double CompressDb { get; set; } = -20;    // Kompressor-Schwelle
        public double SilenceSec { get; set; } = 1;      // Stille einfügen
        public double NormalizeDb { get; set; } = -1;    // Normalisieren auf
    }
    public AuFxValues AuFx { get; } = new();

    /// <summary>Spielt gerade etwas? (Abspielkopf läuft und ist noch nicht am Ende.)</summary>
    public bool AuPlaying => _auTimer is { IsEnabled: true } && _mPos < _auPlayLen - 0.05;

    /// <summary>Leertaste: abspielen oder anhalten.</summary>
    public void AuTogglePlay(ProgramDocument d)
    {
        if (AuPlaying) { AuStop(); _progMsg[AUK] = "Angehalten."; }
        else AuPlay(d);
    }

    /// <summary>Aufnahme starten oder – wenn sie läuft – beenden und einfügen.</summary>
    public void AuRecordToggle(ProgramDocument d)
    {
        if (_auRec is not null) { _ = AuRecordStop(); return; }
        AuRecordStart(d);
    }

    /// <summary>Aufnahme starten (bis „Stopp“). Wirft mit deutscher Meldung, wenn FFmpeg oder ein Mikrofon fehlt.</summary>
    public void AuRecordStart(ProgramDocument d)
    {
        if (_auRec is not null) return;
        var ff = FfmpegPath ?? throw new InvalidOperationException("Zum Aufnehmen braucht Trendo FFmpeg – unter „Programme“ mit einem Klick einrichten.");
        string[] input;
        if (MicInputOverride is { } o) input = o();
        else if (_opt.Isolated) throw new InvalidOperationException("Testmodus: es wird nichts aufgenommen.");
        else if (DesktopCapture.Devices(ff).Microphones.FirstOrDefault() is { } mic) input = MicRecorder.DshowInput(mic.Id);
        else throw new InvalidOperationException("Kein Mikrofon gefunden – bitte eines anschließen (oder in Windows freigeben) und noch einmal „Aufnehmen“.");
        AuStop();
        var wav = Path.Combine(AuTemp(), $"aufnahme-{Guid.NewGuid():N}.wav");
        _auRec = MicRecorder.Start(ff, input, wav); _auRecDoc = d;
        _auRecTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => AuRecTick());
        _auRecTimer.Start();
        _progMsg[AUK] = "Aufnahme läuft – „Stopp“ beendet sie und fügt sie an der Marke ein.";
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.aufnahme.start", d.Id);
    }

    void AuRecTick()
    {
        if (_auRec is not { } r) { _auRecTimer?.Stop(); return; }
        if (!r.Running && r.Seconds == 0) { _ = AuRecordStop(); return; } // FFmpeg hat aufgegeben (Mikrofon belegt o. ä.)
        if (_auRecTime is not null) _auRecTime.Text = AudioEdit.Time(r.Seconds);
        if (_auRecMeter?.Parent is Control host && host.Bounds.Width > 0)
        {
            var db = MicRecorder.Db(r.Level); var frac = double.IsNegativeInfinity(db) ? 0 : Math.Clamp((db + 60) / 60, 0, 1);
            _auRecMeter.Width = host.Bounds.Width * frac;
            _auRecMeter.Background = r.Level >= 0.99 ? Brush.Parse("#EF4444") : db > -6 ? Brush.Parse("#F59E0B") : Brush.Parse("#F5B83D");
            if (_auRecPeak is not null) _auRecPeak.Background = r.MaxLevel >= 0.99 ? Brush.Parse("#EF4444") : Brush.Parse("#33FFFFFF");
        }
    }

    /// <summary>Aufnahme beenden: Datei abschließen, in den Ordner „Aufnahmen“ legen und an der Marke einfügen. Liefert die Aufnahme oder null.</summary>
    public async Task<string?> AuRecordStop()
    {
        if (_auRec is not { } r) return null;
        var (d, secs, max) = AuRecordTake(r);
        var wav = await Task.Run(() => r.Stop());
        return AuRecordFinish(r, wav, d, secs, max, redraw: true);
    }

    /// <summary>Gegenprüfung T-0732: Beim Schließen des Fensters (oder von Trendo) eine laufende Aufnahme nicht verlieren – FFmpeg bekommt wie bei „Stopp“
    /// ein „q“ und schließt die WAV sauber ab, die Aufnahme kommt nach „Aufnahmen“ und in das Dokument, in dem sie gestartet wurde. Die Hülle sichert
    /// das Dokument direkt danach (<see cref="ProgramAutosaveAll"/>), beim nächsten Start bietet sie es zum Wiederherstellen an.</summary>
    public string? AuRecordStopOnClose()
    {
        if (_auRec is not { } r) return null;
        var (d, secs, max) = AuRecordTake(r);
        var wav = r.Stop(5000);
        return AuRecordFinish(r, wav, d, secs, max, redraw: false);
    }

    (ProgramDocument? Doc, double Secs, double Max) AuRecordTake(MicRecorder r)
    {
        _auRec = null; _auRecTimer?.Stop();
        var d = _auRecDoc; _auRecDoc = null;
        return (d, r.Seconds, r.MaxLevel);
    }

    string? AuRecordFinish(MicRecorder r, string? wav, ProgramDocument? d, double secs, double max, bool redraw)
    {
        var err = r.Errors; r.Dispose();
        if (wav is null || secs < 0.05)
        {
            TryDelete(r.Wav);
            _progMsg[AUK] = "Aufnahme ging nicht" + (err.Length > 0 ? ": " + Short(err) : " – kam vom Mikrofon nichts an?");
            if (redraw && CurrentView == AUK) Show(AUK); return null;
        }
        var dir = Path.Combine(WorkFolder("Medien"), "Aufnahmen"); Directory.CreateDirectory(dir);
        var keep = Path.Combine(dir, $"Aufnahme {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");
        for (var i = 2; File.Exists(keep); i++) keep = Path.Combine(dir, $"Aufnahme {DateTime.Now:yyyy-MM-dd HH-mm-ss} ({i}).wav");
        SafeFile.Move(wav, keep);
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.aufnahme.ende", $"{secs:0.0}s");
        try
        {
            var where = AuRecordInsert(d, keep);
            _progMsg[AUK] = $"Aufnahme ({AudioEdit.Time(AudioFile.Read(keep).Seconds)}) eingefügt{where} – gespeichert unter „Aufnahmen“." + (max >= 0.99 ? " Achtung: zu laut, stellenweise übersteuert." : "");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { _progMsg[AUK] = "Aufnahme gespeichert, aber nicht eingefügt: " + ex.Message; }
        if (redraw && CurrentView == AUK) Show(AUK);
        return keep;
    }

    /// <summary>Gegenprüfung T-0732: Die Aufnahme gehört in das Dokument, in dem sie gestartet wurde – auch wenn inzwischen ein anderes vorn ist.
    /// Dafür wird das gemerkte Dokument kurz aktiv (Marke, Mehrspur, Rückgängig gelten dort) und danach das vordere wieder hergestellt.
    /// Wurde es inzwischen geschlossen, entsteht ein neues Dokument mit der Aufnahme. Liefert einen Zusatz für die Meldung.</summary>
    string AuRecordInsert(ProgramDocument? d, string keep)
    {
        var l = ProgramDocs(AUK); var idx = d is null ? -1 : l.IndexOf(d);
        if (d is null) { AuOpen(keep); return ""; }
        if (idx < 0) { ProgramNew(AUK, "leer"); AuOpen(keep); return " (als neues Dokument – das alte war geschlossen)"; }
        var prev = _progActive.GetValueOrDefault(AUK);
        _progActive[AUK] = idx;
        try
        {
            if (AudioMulti.IsMulti(d.Data)) AuAddTrack(keep);
            else if (AuRendered(d) is null) AuOpen(keep);
            else AuInsertFile(keep);
        }
        finally { if (prev != idx && prev < l.Count) _progActive[AUK] = prev; }
        return prev != idx ? $" in „{d.Title}“" : "";
    }

    /// <summary>Leiste „Aufnahme läuft“ über der Wellenform: Zeit, Pegel, Stopp.</summary>
    Control? AuRecBar()
    {
        if (_auRec is not { } r) { _auRecMeter = _auRecPeak = null; _auRecTime = null; return null; }
        _auRecTime = new TextBlock { Name = "au-rec-time", Text = AudioEdit.Time(r.Seconds), Foreground = DawDigits, FontSize = 16, FontFamily = new FontFamily("Consolas, monospace"), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        _auRecMeter = new Border { Name = "au-rec-meter", Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = Brush.Parse("#F5B83D"), CornerRadius = new CornerRadius(3) };
        _auRecPeak = new Border { Name = "au-rec-clip", Width = 10, Background = Brush.Parse("#33FFFFFF"), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Right };
        ToolTip.SetTip(_auRecPeak, "Wird rot, wenn die Aufnahme übersteuert – dann leiser sprechen oder Abstand halten");
        var meter = new Grid { Name = "au-rec-level", Height = 14, MinWidth = 220, VerticalAlignment = VerticalAlignment.Center, Background = Brush.Parse("#22FFFFFF"), Children = { _auRecMeter, _auRecPeak } };
        ToolTip.SetTip(meter, "Pegel: gelb = gut, orange = laut, rot = übersteuert");
        var stop = new Button { Name = "au-rec-stop", Content = "■  Stopp", FontSize = 14, Background = Brush.Parse("#EF4444"), Foreground = Brushes.White, CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(stop, "Aufnahme beenden und an der Marke einfügen");
        stop.Click += (_, _) => _ = AuRecordStop();
        var dot = new TextBlock { Text = "● Aufnahme", Foreground = Brush.Parse("#EF4444"), FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 12, Children = { dot, Pos(_auRecTime, 1), Pos(meter, 2), Pos(stop, 3) } };
        return new Border { Name = "au-rec-bar", Background = Brush.Parse("#2A1414"), BorderBrush = Brush.Parse("#66EF4444"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 8), Child = g };
    }

    /// <summary>Rechte Spalte: Werte der Effekte einstellen.</summary>
    Control AuFxPanel()
    {
        var sp = new StackPanel { Name = "au-fx", Spacing = 4 };
        sp.Children.Add(H("Effekt-Werte", 13));
        sp.Children.Add(P("Diese Werte nutzen die Knöpfe oben (Lauter, Echo, Tiefen weg …).", Muted, 11.5));
        Control Num(string name, string label, Func<double> get, Action<double> set, double min, double max, double step, string fmt, string unit)
        {
            var n = new NumericUpDown { Name = name, Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)step, Value = (decimal)get(), FormatString = fmt, MinWidth = 96, HorizontalAlignment = HorizontalAlignment.Stretch };
            n.ValueChanged += (_, _) => { if (n.Value is { } v) set((double)v); };
            var t = new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var u = new TextBlock { Text = unit, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Width = 26 };
            return new Grid { ColumnDefinitions = new ColumnDefinitions("*,96,Auto"), ColumnSpacing = 6, Children = { t, Pos(n, 1), Pos(u, 2) } };
        }
        var f = AuFx;
        sp.Children.Add(Num("au-fx-gain", "Lauter/Leiser um", () => f.GainDb, v => f.GainDb = v, 0.5, 24, 0.5, "0.0", "dB"));
        sp.Children.Add(Num("au-fx-norm", "Normalisieren auf", () => f.NormalizeDb, v => f.NormalizeDb = v, -24, 0, 0.5, "0.0", "dB"));
        sp.Children.Add(Num("au-fx-fade", "Blende ohne Auswahl", () => f.FadeSec, v => f.FadeSec = v, 0.1, 30, 0.5, "0.0", "s"));
        sp.Children.Add(Num("au-fx-echo", "Echo-Abstand", () => f.EchoSec, v => f.EchoSec = v, 0.05, 2, 0.05, "0.00", "s"));
        sp.Children.Add(Num("au-fx-hp", "Tiefen weg unter", () => f.HighpassHz, v => f.HighpassHz = v, 20, 1000, 10, "0", "Hz"));
        sp.Children.Add(Num("au-fx-lp", "Höhen weg über", () => f.LowpassHz, v => f.LowpassHz = v, 1000, 20000, 500, "0", "Hz"));
        sp.Children.Add(Num("au-fx-speed", "Tempo-Schritt", () => f.SpeedPct, v => f.SpeedPct = v, 1, 75, 1, "0", "%"));
        sp.Children.Add(Num("au-fx-gate", "Rauschen unter", () => f.GateDb, v => f.GateDb = v, -80, -10, 1, "0", "dB"));
        sp.Children.Add(Num("au-fx-comp", "Kompressor ab", () => f.CompressDb, v => f.CompressDb = v, -60, -1, 1, "0", "dB"));
        sp.Children.Add(Num("au-fx-sil", "Stille einfügen", () => f.SilenceSec, v => f.SilenceSec = v, 0.1, 60, 0.5, "0.0", "s"));
        return sp;
    }
}
