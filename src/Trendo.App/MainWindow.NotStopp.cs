using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0005 / IMP-19: Not-Stopp für alle lokalen Automationen. Ein Klick (oder die Pause-Taste) hält alles an:
/// Freigaben verfallen, nichts wird mehr ausgeführt, Abläufe brechen ab, Freigabe im Netz, Download, Sendung und Roboterarm stoppen.</summary>
public partial class MainWindow
{
    private Button? _haltButton;

    private const string StopIcon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M9,9H15V15H9";
    private const string PlayIcon = "M8,5.14V19.14L19,12.14L8,5.14Z";

    private Button HaltButton()
    {
        var b = new Button { Name = "nav-notstopp", Padding = new Thickness(10, 5), CornerRadius = new CornerRadius(9), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
        b.Click += async (_, _) => { if (_core.Approvals.Halted) ResumeAll(); else await HaltAll("Not-Stopp-Knopf"); };
        _haltButton = b;
        MarkHalt();
        _core.Approvals.HaltChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(MarkHalt);
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Pause), Command = new RelayCommand(() => _ = HaltAll("Pause-Taste")) });
        return b;
    }

    private void MarkHalt()
    {
        if (_haltButton is not { } b) return;
        var on = _core.Approvals.Halted;
        b.Background = Brush.Parse(on ? "#15803D" : "#DC2626");
        b.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Ico(on ? PlayIcon : StopIcon, 15, Brushes.White), new TextBlock { Text = on ? "Fortsetzen" : "Not-Stopp", FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White } } };
        var tip = on ? "Not-Stopp ist aktiv: nichts wird freigegeben oder ausgeführt. Klick setzt fort." : "Not-Stopp: alle Automationen sofort anhalten (Pause-Taste)";
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, on ? "Fortsetzen" : "Not-Stopp");
    }

    /// <summary>Alles anhalten. Stoppen darf jeder – die Rechte prüft erst das Fortsetzen.</summary>
    public async Task HaltAll(string reason)
    {
        if (_core.Approvals.Halted) return;
        var dropped = _core.Approvals.Halt(Principal.LocalOwner, reason);
        var stopped = new List<string>();
        if (dropped > 0) stopped.Add($"{dropped} Freigaben verworfen");
        if (_dlCts is { IsCancellationRequested: false } dl) { dl.Cancel(); stopped.Add("Download angehalten"); }
        if (ActiveShare is not null) { await StopShareAsync(); stopped.Add("Freigabe im Netz beendet"); }
        if (_live is { Mode: not LiveMode.Idle } live) { live.Stop(); stopped.Add("Sendung/Aufnahme beendet"); }
        if (!Bench.Drive.Emergency) { Bench.Drive.EmergencyStop(); stopped.Add("Roboterarm Not-Aus"); }
        HaltSummary = stopped.Count == 0 ? "Nichts lief gerade. Ab jetzt wird nichts ausgeführt." : string.Join(" · ", stopped);
        MarkHalt();
        Show(CurrentView is "" ? "start" : CurrentView);
    }

    public string HaltSummary { get; private set; } = "";

    public void ResumeAll()
    {
        _core.Approvals.Resume(Principal.LocalOwner);
        HaltSummary = "";
        MarkHalt();
        Show(CurrentView is "" ? "start" : CurrentView);
    }

    /// <summary>Hinweis oben auf der Freigabeseite, solange der Not-Stopp aktiv ist.</summary>
    private Control? HaltBanner()
    {
        if (!_core.Approvals.Halted) return null;
        var resume = new Button { Name = "btn-notstopp-fortsetzen", Content = "Fortsetzen", Background = Brush.Parse("#15803D"), Foreground = Brushes.White, Padding = new Thickness(12, 5), CornerRadius = new CornerRadius(8) };
        resume.Click += (_, _) => ResumeAll();
        return new Border { Name = "notstopp-banner", Background = Brush.Parse("#33DC2626"), BorderBrush = Brush.Parse("#DC2626"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Not-Stopp ist aktiv", Foreground = Text, FontWeight = FontWeight.SemiBold },
                    P(HaltSummary.Length > 0 ? HaltSummary : "Nichts wird freigegeben oder ausgeführt.", Text, 12) } },
                Pos(resume, 1) } } };
    }
}
