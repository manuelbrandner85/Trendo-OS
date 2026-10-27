using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: Trendo auf dem Handy voll nutzen. Das Handy sieht den Trendo-Bildschirm und tippt, schreibt und scrollt darin –
/// alles läuft auf dem PC, das Handy ist nur Bildschirm und Finger. Nur für Handys, die der PC-Nutzer dafür freigegeben hat.</summary>
public partial class MainWindow
{
    TextBox? _remoteFocus;
    /// <summary>Was zuletzt vom Handy angetippt wurde (Name des Elements, für Protokoll und Tests).</summary>
    public string? PhoneLastHit { get; private set; }
    readonly Dictionary<string, DateTime> _remoteToast = [];

    /// <summary>Bild des Trendo-Bildschirms (PNG, höchstens <paramref name="maxWidth"/> Pixel breit).</summary>
    public byte[]? PhoneScreen(int maxWidth)
    {
        var size = Bounds.Size;
        if (size.Width < 10 || size.Height < 10) return null;
        var scale = Math.Min(1.0, maxWidth / size.Width);
        var px = new PixelSize(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
        using var rtb = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        rtb.Render(this);
        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        return ms.ToArray();
    }

    /// <summary>Antippen an einer Stelle (Anteile 0…1 der Fensterbreite/-höhe). Liefert, was getroffen wurde.</summary>
    public string PhoneTap(double fx, double fy)
    {
        var p = new Point(fx * Bounds.Width, fy * Bounds.Height);
        if (this.InputHitTest(p) is not Visual hit) return "nichts";
        PhoneLastHit = hit.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Name is { Length: > 0 } n && !n.StartsWith("PART_"))?.Name;
        foreach (var v in hit.GetSelfAndVisualAncestors())
        {
            switch (v)
            {
                case TextBox tb when tb.IsEffectivelyEnabled:
                    tb.Focus(); _remoteFocus = tb; tb.CaretIndex = tb.Text?.Length ?? 0;
                    return "Eingabefeld";
                case ToggleButton t when t.IsEffectivelyEnabled:
                    t.IsChecked = !(t.IsChecked ?? false);
                    t.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return "Schalter";
                case Button b when b.IsEffectivelyEnabled:
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (b.Command is { } cmd && cmd.CanExecute(b.CommandParameter)) cmd.Execute(b.CommandParameter);
                    return "Knopf";
                case ComboBox cb when cb.IsEffectivelyEnabled:
                    cb.IsDropDownOpen = !cb.IsDropDownOpen;
                    return "Auswahl";
                case ListBoxItem li:
                    li.IsSelected = true;
                    return "Eintrag";
                case TabItem ti:
                    ti.IsSelected = true;
                    return "Reiter";
            }
        }
        return "nichts";
    }

    TextBox? RemoteBox() => _remoteFocus is { } tb && tb.IsEffectivelyVisible && TopLevel.GetTopLevel(tb) is not null ? tb : FocusManager?.GetFocusedElement() as TextBox;

    /// <summary>Text ins zuletzt angetippte Eingabefeld schreiben (an der Schreibmarke).</summary>
    public void PhoneType(string text)
    {
        if (RemoteBox() is not { } tb || tb.IsReadOnly) return;
        var t = tb.Text ?? ""; var c = Math.Clamp(tb.CaretIndex, 0, t.Length);
        tb.Text = t.Insert(c, text); tb.CaretIndex = c + text.Length;
    }

    /// <summary>Sondertasten vom Handy: Löschen, Enter, Tab (nächstes Feld), Esc.</summary>
    public void PhoneKey(string key)
    {
        var tb = RemoteBox();
        switch (key)
        {
            case "back" when tb is { IsReadOnly: false }:
                var t = tb.Text ?? ""; var c = Math.Clamp(tb.CaretIndex, 0, t.Length);
                if (c > 0) { tb.Text = t.Remove(c - 1, 1); tb.CaretIndex = c - 1; }
                break;
            case "enter" when tb is { AcceptsReturn: true, IsReadOnly: false }:
                PhoneType("\n");
                break;
            case "enter":
                ((Control?)tb ?? this).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                break;
            case "tab" when tb is not null:
                var boxes = this.GetVisualDescendants().OfType<TextBox>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
                if (boxes.IndexOf(tb) is var i and >= 0 && i + 1 < boxes.Count) { boxes[i + 1].Focus(); _remoteFocus = boxes[i + 1]; }
                break;
            case "esc":
                ((Control?)tb ?? this).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                break;
        }
    }

    /// <summary>Wischen: die Liste/Seite unter dem Finger scrollen (<paramref name="dy"/> in Bildschirmhöhen, positiv = nach unten).</summary>
    public void PhoneScroll(double fx, double fy, double dy)
    {
        var p = new Point(fx * Bounds.Width, fy * Bounds.Height);
        var sv = (this.InputHitTest(p) as Visual)?.GetSelfAndVisualAncestors().OfType<ScrollViewer>().FirstOrDefault(s => s.Extent.Height > s.Viewport.Height);
        if (sv is null) return;
        sv.Offset = new Vector(sv.Offset.X, Math.Clamp(sv.Offset.Y + dy * sv.Viewport.Height, 0, Math.Max(0, sv.Extent.Height - sv.Viewport.Height)));
    }

    /// <summary>Am PC sichtbar machen, dass ein Handy gerade bedient (höchstens alle 10 Minuten ein Hinweis je Handy).</summary>
    void PhoneRemoteStarted(string device)
    {
        if (_remoteToast.TryGetValue(device, out var last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(10)) return;
        _remoteToast[device] = DateTime.UtcNow;
        Toast($"📱 {device} bedient Trendo gerade vom Handy aus.", ("Sperren", new Action(() =>
        {
            if (PhoneAccess.Devices().FirstOrDefault(d => d.Name == device) is { } d) PhoneSetRemote(d.Id, d.Name, false);
            if (CurrentView == "handy") Show("handy");
        })));
    }

    /// <summary>Brücke vom Handy-Server (eigener Thread) in die Oberfläche (UI-Thread).</summary>
    sealed class PhoneRemoteBridge(MainWindow w) : IPhoneRemote
    {
        public async Task<byte[]?> ScreenAsync(int maxWidth) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneScreen(maxWidth));
        public async Task<string> TapAsync(double fx, double fy) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneTap(fx, fy));
        public async Task TypeAsync(string text) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneType(text));
        public async Task KeyAsync(string key) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneKey(key));
        public async Task ScrollAsync(double fx, double fy, double dy) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneScroll(fx, fy, dy));
        public void Started(string device) => Dispatcher.UIThread.Post(() => w.PhoneRemoteStarted(device));
    }
}
