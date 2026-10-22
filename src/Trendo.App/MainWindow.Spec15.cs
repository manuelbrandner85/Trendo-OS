using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-15: Sendung & Streaming.</summary>
public partial class MainWindow
{
    private BroadcastDesk? _bc;
    public BroadcastDesk Broadcast => _bc ??= new BroadcastDesk(_core.DataRoot);

    private void InitSpec15()
    {
        RegisterTab(FachTabs, "sendung", "Sendung & Stream", "SPEC-15", BroadcastView, "streaming", "stream", "livestream", "obs", "sendung", "ablauf", "podcast", "youtube", "twitch", "bitrate", "kapitel");
        StartLiveTimer();
    }

    private Control BroadcastView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(LiveSection());
        var plan = new StackPanel { Spacing = 8 };
        var over = Broadcast.Over;
        plan.Children.Add(new TextBlock { Name = "bc-total", Text = $"{Broadcast.Plan.Show}: {BroadcastDesk.Clock(Broadcast.Total)} von {Broadcast.Plan.SlotMinutes} min" + (over > TimeSpan.Zero ? $" – {BroadcastDesk.Clock(over)} zu lang!" : $" – noch {BroadcastDesk.Clock(-over)} frei"), Foreground = over > TimeSpan.Zero ? Brush.Parse("#F87171") : Text, FontSize = 14 });
        foreach (var r in Broadcast.Rows())
        {
            var i = r.Index;
            var up = new Button { Name = $"bc-up-{i}", Content = "↑", Padding = new Thickness(6, 2) }; up.Click += (_, _) => { Broadcast.Move(i, -1); Show("sendung"); };
            var del = new Button { Name = $"bc-del-{i}", Content = "Entfernen", Padding = new Thickness(6, 2) }; del.Click += (_, _) => { Broadcast.Remove(i); Show("sendung"); };
            plan.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Name = $"bc-row-{i}", Text = $"{r.Start:HH:mm:ss}  {r.Title}", Foreground = Text }, P($"Dauer {BroadcastDesk.Clock(TimeSpan.FromSeconds(r.Seconds))}{(r.Note == "" ? "" : " · " + r.Note)}", null, 11) } }, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { up, del } }));
        }
        plan.Children.Add(FachTool("bc-setup", "Sendung", "Name, Beginn (TT.MM.JJJJ HH:MM) und Länge des Sendeplatzes.",
            [("show", "Name", Broadcast.Plan.Show), ("start", "Beginn", Broadcast.Plan.Start.ToString("dd.MM.yyyy HH:mm")), ("slot", "Sendeplatz (min)", Broadcast.Plan.SlotMinutes.ToString())],
            v => { if (!DateTime.TryParse(v("start"), Fach.De, System.Globalization.DateTimeStyles.None, out var st)) throw new FormatException("Beginn bitte als „21.10.2026 20:00“."); Broadcast.Plan.Show = v("show").Trim(); Broadcast.Plan.Start = st; Broadcast.Plan.SlotMinutes = (int)Fach.Num(v("slot"), "Sendeplatz"); Broadcast.Save(); Show("sendung"); return "Gespeichert."; }, "Übernehmen"));
        plan.Children.Add(FachTool("bc-add", "Programmpunkt hinzufügen", "Dauer als „90“ (Sekunden), „4:30“ oder „1:00:00“.",
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
        plan.Children.Add(exp);
        plan.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { P("Ablauf als OBS-Szenensammlung (eine Szene je Programmpunkt):", null, 11), OpenInButton("obs", () => ObsCollectionFile(), "sendung") } });
        plan.Children.Add(FachTool("bc-rate", "Reicht meine Leitung?", "Upload-Geschwindigkeit zum Beispiel aus einem Speedtest. Plattform und Qualität stehen in den Listen.",
            [("pf", "Plattform", "YouTube"), ("q", "Qualität", "1080p60"), ("up", "Upload (Mbit/s)", "10"), ("min", "Aufnahme (min)", "60")],
            v => { var p = BroadcastDesk.PlanStream(v("pf"), v("q"), Fach.Num(v("up"), "Upload")); return $"{(p.Fits ? "✓" : "⚠")} Video {Fach.F(p.VideoKbps, 0)} kbit/s + Ton {p.AudioKbps} kbit/s → du brauchst ca. {Fach.F(p.NeededUploadMbps, 1)} Mbit/s Upload. {p.Advice}\nAufnahme auf Platte: ca. {Fach.F(BroadcastDesk.StorageGb(p.VideoKbps + p.AudioKbps, Fach.Num(v("min"), "Minuten")), 1)} GB"; }, "Prüfen"));
        plan.Children.Add(FachGroup("bc-live", "Während der Sendung", false,
            FachTool("bc-now", "Was läuft gerade?", "Leer lassen = jetzt; oder eine Uhrzeit (TT.MM.JJJJ HH:MM) zum Durchspielen.", [("at", "Zeitpunkt", "")],
                v => { var t = v("at").Trim() == "" ? DateTime.Now : DateTime.Parse(v("at"), Fach.De); var n = BroadcastPlus.NowPlaying(Broadcast, t); return n is null ? (Broadcast.Rows().Count > 0 && t < Broadcast.Plan.Start ? $"Noch nicht auf Sendung – Beginn in {BroadcastDesk.Clock(Broadcast.Plan.Start - t)}." : "Gerade läuft nichts aus dem Ablauf.") : $"▶ {n.Index + 1}. {n.Title} – noch {BroadcastDesk.Clock(n.LeftInSegment)}\nSendung noch {BroadcastDesk.Clock(n.LeftInShow)}{(n.Next is null ? "" : $" · danach: {n.Next}")}"; }, "Anzeigen"),
            FachTool("bc-cards", "Moderationskarten", "Eine Karte pro Programmpunkt mit Zeit, Notiz und Überleitung.", [],
                v => { var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), "moderationskarten.md"); File.WriteAllText(p, BroadcastPlus.CueCards(Broadcast)); Launch(p); return "Gespeichert: " + p; }, "Erstellen"),
            FachTool("bc-speak", "Sprechzeit", "Text der Moderation einfügen. Ruhig ≈ 130 Wörter pro Minute, Nachrichten ≈ 160.", [("t", "Text", ""), ("wpm", "Wörter pro Minute", "130")],
                v => { var (w, t) = BroadcastPlus.SpeakingTime(v("t"), Fach.Num(v("wpm"))); return $"{w} Wörter → etwa {BroadcastDesk.Clock(t)}"; }, "Messen")));
        plan.Children.Add(FachGroup("bc-post", "Bild & Ton", false,
            FachTool("bc-loud", "Lautheit", "Gemessene Lautheit in LUFS und Spitzenpegel in dBTP, zum Beispiel aus Audacity oder OBS.",
                [("pf", "Plattform", "YouTube"), ("lufs", "Gemessen (LUFS)", "-18"), ("tp", "Spitze (dBTP)", "-6")],
                v => { var l = BroadcastPlus.Loudness(v("pf"), Fach.Num(v("lufs")), Fach.Num(v("tp"))); return $"Ziel {Fach.F(l.Target, 0)} LUFS → {(l.Gain >= 0 ? "+" : "")}{Fach.F(l.Gain, 1)} dB · Spitze danach {Fach.F(l.PeakAfter, 1)} dBTP" + (l.Clips ? "\n⚠ Zu laut für die Spitzen – vorher einen Limiter einsetzen." : ""); }, "Berechnen"),
            FachTool("bc-aspect", "Seitenverhältnis", "Auflösung in Pixeln.", [("w", "Breite", "1920"), ("h", "Höhe", "1080")],
                v => { var a = BroadcastPlus.Aspect((int)Fach.Num(v("w")), (int)Fach.Num(v("h"))); return $"{a.W}:{a.H} – {a.Name}"; }, "Prüfen"),
            FachTool("bc-crop", "Hochkant-Ausschnitt", "Ausschnitt für Shorts/Reels aus einem Querformat. Lage: 0 = links, 50 = Mitte, 100 = rechts.", [("w", "Breite", "1920"), ("h", "Höhe", "1080"), ("ratio", "Ziel", "9:16"), ("c", "Lage (%)", "50")],
                v => { var r = v("ratio").Split(':'); if (r.Length != 2) throw new FormatException("Ziel bitte als „9:16“."); var c = BroadcastPlus.Crop((int)Fach.Num(v("w")), (int)Fach.Num(v("h")), (int)Fach.Num(r[0]), (int)Fach.Num(r[1]), Fach.Num(v("c"))); return $"Ausschnitt {c.W} × {c.H} px ab x = {c.X}, y = {c.Y}\nFür OBS/ffmpeg: crop={c.W}:{c.H}:{c.X}:{c.Y}"; }, "Berechnen"),
            FachTool("bc-tc", "Timecode", "Frames → Timecode oder Timecode (hh:mm:ss:ff) → Frames.", [("v", "Wert", "90012"), ("fps", "Bilder pro Sekunde", "25")],
                v => { var f = (int)Fach.Num(v("fps")); var x = v("v").Trim(); return x.Contains(':') ? $"{Fach.F(BroadcastPlus.FromTimecode(x, f), 0)} Frames" : $"Timecode {BroadcastPlus.ToTimecode((long)Fach.Num(x), f)}"; }, "Umrechnen")));
        plan.Children.Add(FachGroup("bc-plan", "Planen & Untertitel", false,
            FachTool("bc-week", "Wochenplan", "Eine Sendung pro Zeile: „Mo 20:00 60 Titel“ (Tag, Beginn, Minuten, Titel).", [("spec", "Sendungen", "Mo 20:00 60 Talk / Fr 18:00 45 Musik"), ("from", "Ab", DateTime.Today.ToString("dd.MM.yyyy")), ("weeks", "Wochen", "2")],
                v => { var (sl, c) = BroadcastPlus.Weekly(v("spec").Replace(" / ", "\n"), DateTime.Parse(v("from"), Fach.De), (int)Fach.Num(v("weeks"))); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), "wochenplan.csv"); File.WriteAllText(p, "Datum;Beginn;Ende;Sendung\n" + string.Concat(sl.Select(x => $"{x.Start:dd.MM.yyyy};{x.Start:HH:mm};{x.End:HH:mm};{x.Title}\n"))); return $"{sl.Count} Termine" + (c.Count == 0 ? " ohne Überschneidung." : "\n⚠ " + string.Join("\n⚠ ", c)) + $"\nGespeichert: {p}"; }, "Planen"),
            FachTool("bc-srt", "Untertitel verschieben", "SRT-Datei und Versatz in Sekunden (negativ = früher). Speichert eine Kopie.", [("file", "SRT-Datei", ""), ("s", "Versatz (s)", "0")],
                v => { var f = v("file").Trim().Trim('"'); if (!File.Exists(f)) throw new IOException("Diese Datei gibt es nicht."); var (srt, n, p) = BroadcastPlus.ShiftSrt(File.ReadAllText(f), TimeSpan.FromSeconds(Fach.Num(v("s")))); var o = Path.Combine(Path.GetDirectoryName(f) ?? ".", Path.GetFileNameWithoutExtension(f) + ".versetzt.srt"); File.WriteAllText(o, srt); return $"{n} Untertitel" + (p.Count == 0 ? " – alles in Ordnung." : "\n⚠ " + string.Join("\n⚠ ", p)) + $"\nGespeichert: {o}"; }, "Verschieben")));
        sp.Children.Add(FachGroup("bc-rundown", "Sendeplan", true, plan));
        return FachPage("Sendung & Stream", sp);
    }
    /// <summary>T-0203: schreibt die OBS-Szenensammlung zur aktuellen Sendung.</summary>
    private string? ObsCollectionFile()
    {
        if (Broadcast.Plan.Segments.Count == 0) throw new InvalidOperationException("Bitte zuerst Programmpunkte anlegen.");
        var name = "Trendo " + (string.IsNullOrWhiteSpace(Broadcast.Plan.Show) ? "Sendung" : Broadcast.Plan.Show.Trim());
        var f = Path.Combine(Fach.StoreDir(_core.DataRoot, "sendung"), string.Concat(name.Split(Path.GetInvalidFileNameChars())) + ".json");
        File.WriteAllText(f, ObsScenes.Collection(name, Broadcast.Plan.Segments.Select(s => s.Title)));
        return f;
    }
}
