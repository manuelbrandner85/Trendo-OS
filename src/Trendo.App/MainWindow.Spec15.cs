using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-15: Sendung & Streaming.</summary>
public partial class MainWindow
{
    private BroadcastDesk? _bc;
    public BroadcastDesk Broadcast => _bc ??= new BroadcastDesk(_core.DataRoot);

    private void InitSpec15() => RegisterTab(FachTabs, "sendung", "Sendung & Stream", "SPEC-15", BroadcastView, "streaming", "stream", "livestream", "obs", "sendung", "ablauf", "podcast", "youtube", "twitch", "bitrate", "kapitel");

    private Control BroadcastView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var over = Broadcast.Over;
        sp.Children.Add(new TextBlock { Name = "bc-total", Text = $"{Broadcast.Plan.Show}: {BroadcastDesk.Clock(Broadcast.Total)} von {Broadcast.Plan.SlotMinutes} min" + (over > TimeSpan.Zero ? $" – {BroadcastDesk.Clock(over)} zu lang!" : $" – noch {BroadcastDesk.Clock(-over)} frei"), Foreground = over > TimeSpan.Zero ? Brush.Parse("#F87171") : Text, FontSize = 14 });
        foreach (var r in Broadcast.Rows())
        {
            var i = r.Index;
            var up = new Button { Name = $"bc-up-{i}", Content = "↑", Padding = new Thickness(6, 2) }; up.Click += (_, _) => { Broadcast.Move(i, -1); Show("sendung"); };
            var del = new Button { Name = $"bc-del-{i}", Content = "Entfernen", Padding = new Thickness(6, 2) }; del.Click += (_, _) => { Broadcast.Remove(i); Show("sendung"); };
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Name = $"bc-row-{i}", Text = $"{r.Start:HH:mm:ss}  {r.Title}", Foreground = Text }, P($"Dauer {BroadcastDesk.Clock(TimeSpan.FromSeconds(r.Seconds))}{(r.Note == "" ? "" : " · " + r.Note)}", null, 11) } }, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { up, del } }));
        }
        sp.Children.Add(FachTool("bc-setup", "Sendung", "Name, Beginn (TT.MM.JJJJ HH:MM) und Länge des Sendeplatzes.",
            [("show", "Name", Broadcast.Plan.Show), ("start", "Beginn", Broadcast.Plan.Start.ToString("dd.MM.yyyy HH:mm")), ("slot", "Sendeplatz (min)", Broadcast.Plan.SlotMinutes.ToString())],
            v => { if (!DateTime.TryParse(v("start"), Fach.De, System.Globalization.DateTimeStyles.None, out var st)) throw new FormatException("Beginn bitte als „21.10.2026 20:00“."); Broadcast.Plan.Show = v("show").Trim(); Broadcast.Plan.Start = st; Broadcast.Plan.SlotMinutes = (int)Fach.Num(v("slot"), "Sendeplatz"); Broadcast.Save(); Show("sendung"); return "Gespeichert."; }, "Übernehmen"));
        sp.Children.Add(FachTool("bc-add", "Programmpunkt hinzufügen", "Dauer als „90“ (Sekunden), „4:30“ oder „1:00:00“.",
            [("title", "Titel", "Begrüßung"), ("dur", "Dauer", "2:00"), ("note", "Hinweis (Kamera, Gast, Einspieler)", "")],
            v => { Broadcast.Add(v("title"), BroadcastDesk.ParseDuration(v("dur")), v("note")); Show("sendung"); return "Hinzugefügt."; }, "Hinzufügen"));
        var exp = new Button { Name = "bc-export", Content = "Ablaufplan & Kapitel speichern", IsEnabled = Broadcast.Plan.Segments.Count > 0 };
        exp.Click += async (_, _) =>
        {
            var d = Fach.StoreDir(_core.DataRoot, "sendung"); File.WriteAllText(Path.Combine(d, "ablaufplan.csv"), Broadcast.Sheet());
            try { var ch = Broadcast.Chapters(); File.WriteAllText(Path.Combine(d, "kapitel.txt"), ch); if (Clipboard is { } cb) await cb.SetTextAsync(ch); _w45Msg = "Ablaufplan gespeichert, Kapitelmarken kopiert – in die YouTube-Beschreibung einfügen."; }
            catch (InvalidOperationException ex) { _w45Msg = "Ablaufplan gespeichert. Kapitel: " + ex.Message; }
            Launch(d); Show("sendung");
        };
        sp.Children.Add(exp);
        sp.Children.Add(FachTool("bc-rate", "Reicht meine Leitung?", "Upload-Geschwindigkeit z. B. aus einem Speedtest. Qualität wie „1080p60“.",
            [("pf", "Plattform", "YouTube"), ("q", "Qualität", "1080p60"), ("up", "Upload (Mbit/s)", "10"), ("min", "Aufnahme (min)", "60")],
            v => { var p = BroadcastDesk.PlanStream(v("pf"), v("q"), Fach.Num(v("up"), "Upload")); return $"{(p.Fits ? "✓" : "⚠")} Video {Fach.F(p.VideoKbps, 0)} kbit/s + Ton {p.AudioKbps} kbit/s → du brauchst ca. {Fach.F(p.NeededUploadMbps, 1)} Mbit/s Upload. {p.Advice}\nAufnahme auf Platte: ca. {Fach.F(BroadcastDesk.StorageGb(p.VideoKbps + p.AudioKbps, Fach.Num(v("min"), "Minuten")), 1)} GB"; }, "Prüfen"));
        return FachPage("Sendung & Stream", sp);
    }
}
