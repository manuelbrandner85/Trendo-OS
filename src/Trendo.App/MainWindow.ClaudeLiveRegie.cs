using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Live streamen – Claude als Regie (Claude 25, 05.11.2026): Claude baut Szenen und einen Sendeplan, kennt den Ton (Pegel, Warnungen der Ton-Wache)
/// und warnt bei Aussetzern. Vorschläge erscheinen erst als Plan mit Vorschau; „Ausführen“ legt Szenen an bzw. setzt den Sendeplan.</summary>
public partial class MainWindow
{
    const string LiveClaudeScenes = "live-claude-szenen";

    IEnumerable<ClaudeActions.Field> ClaudeLiveFields()
    {
        var show = LiveShowState;
        yield return new(LiveClaudeScenes, "Szenen (Name: Quelle bildschirm/kamera/fenster, + text=Bauchbinde)",
            string.Join(" / ", show.Scenes.Select(s => $"{s.Name}: {MainName(s.Main)}" + string.Concat(s.Overlays.Where(o => o.Kind == "text").Select(o => " + text=" + o.Value)))));
    }

    static string MainName(VideoKind k) => k switch { VideoKind.Camera => "kamera", VideoKind.Window => "fenster", _ => "bildschirm" };

    string ClaudeLiveFocus()
    {
        var show = LiveShowState; var mix = show.Mix;
        var mic = Live.Mode is LiveMode.Recording or LiveMode.Broadcasting && _liveMic ? ReadMeter(MeterPath("mikrofon")) : [];
        var sb = new System.Text.StringBuilder("Live streamen – du bist die Regie. ");
        sb.Append($"Zustand: {Live.Mode switch { LiveMode.Broadcasting => "LIVE auf Sendung", LiveMode.Recording => "Aufnahme läuft", LiveMode.Preview => "Vorschau", _ => "bereit" }}");
        if (_liveStarted is { } st) sb.Append($" seit {(int)(DateTime.Now - st).TotalMinutes} min");
        sb.Append($". Aktive Szene „{show.Current?.Name}“. Szenen: {string.Join("; ", show.Scenes.Select(s => $"{s.Name} ({MainName(s.Main)}" + (s.Overlays.Count > 0 ? ", " + string.Join(", ", s.Overlays.Select(o => o.Label)) : "") + ")"))}.");
        sb.Append($"\nTon: Mikrofon {(_liveMic ? mix.MicMute ? "stumm" : $"an, {mix.MicDb:+0;-0;0} dB" : "aus")}" + (mic.Count > 0 ? $", Pegel gerade {mic[^1].Lufs:0} LUFS" : "") + $"; Musik {(mix.MusicFile is { Length: > 0 } mf ? (mix.MusicMute ? "stumm" : $"„{Path.GetFileName(mf)}“, {mix.MusicDb:+0;-0;0} dB") : "keine")}.");
        sb.Append(_liveWarn is { } w ? $"\nTON-WACHE MELDET: {w} Sag das dem Nutzer zuerst und schlag eine Lösung vor." : "\nTon-Wache: keine Auffälligkeit.");
        if (_liveProbe is { Count: > 0 } pr) sb.Append("\nLetzter Probelauf: " + string.Join(" · ", pr.Select(c => $"{c.Light}: {c.Text}")));
        sb.Append(show.Plan.Count > 0 ? $"\nSendeplan: {LiveProbe.PlanText(show.Plan, show.Scenes)}" : "\nNoch kein Sendeplan.");
        sb.Append("\nSo arbeitest du: Szenen mit [[trendo:feld | " + LiveClaudeScenes + " | Name: kamera + text=Bauchbinde / Name: bildschirm]] (bestehende gleichen Namens werden angepasst, neue angelegt; höchstens 12),");
        sb.Append(" den Sendeplan mit [[trendo:feld | live-plan-text | 00:00 Szene – Notiz / 05:00 Szene – Notiz]] und danach [[trendo:klick | live-plan-set]]. Live gehen, senden oder Schlüssel ändern macht immer der Nutzer selbst.");
        return sb.ToString();
    }

    /// <summary>Claudes Szenen-Text lesen: „Name: quelle + text=…“, getrennt mit „ / “.</summary>
    static (List<(string Name, VideoKind Main, List<string> Texts)>? Scenes, string? Error) ParseLiveScenes(string value)
    {
        var res = new List<(string, VideoKind, List<string>)>();
        foreach (var part in System.Text.RegularExpressions.Regex.Split(value, @"\s+/\s+|\n"))
        {
            var p = part.Trim(); if (p.Length == 0) continue;
            var colon = p.IndexOf(':'); if (colon < 1) return (null, $"„{p}“ – Form „Name: quelle“");
            var name = p[..colon].Trim(); var rest = p[(colon + 1)..].Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (rest.Length == 0) return (null, $"„{name}“ ohne Quelle");
            VideoKind? main = rest[0].ToLowerInvariant() switch { "kamera" => VideoKind.Camera, "fenster" => VideoKind.Window, "bildschirm" => VideoKind.Screen, _ => null };
            if (main is null) return (null, $"Quelle „{rest[0]}“ – bildschirm, kamera oder fenster");
            var texts = rest.Skip(1).Where(r => r.StartsWith("text=", StringComparison.OrdinalIgnoreCase)).Select(r => r[5..].Trim()).Where(t => t.Length > 0).ToList();
            if (name.Length > 40 || texts.Any(t => t.Length > 80)) return (null, $"„{name}“ – Name höchstens 40, Bauchbinde höchstens 80 Zeichen");
            res.Add((name, main.Value, texts));
        }
        if (res.Count == 0) return (null, "keine Szene");
        if (res.Count > 12) return (null, "höchstens 12 Szenen");
        return (res, null);
    }

    Control? ClaudeLivePreview(ClaudeActions.Step s)
    {
        if (s.Target != LiveClaudeScenes || s.Value is not { Length: > 0 } v) return null;
        var (list, err) = ParseLiveScenes(v);
        if (list is null) return new TextBlock { Name = "claude-plan-warn", Text = "So nicht möglich: " + err, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var wp = new WrapPanel { Name = "claude-plan-live" };
        foreach (var (name, main, texts) in list)
        {
            var exists = LiveShowState.Scenes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            wp.Children.Add(new Border { Background = Brush.Parse(exists ? "#14FFFFFF" : "#22D4A84B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 4),
                Child = new TextBlock { Text = (exists ? "" : "＋ ") + name + " · " + MainName(main) + string.Concat(texts.Select(t => " · „" + t + "“")), Foreground = ClaudeFg, FontSize = 11.5 } });
        }
        return wp;
    }

    bool ClaudeLiveApply(ClaudeActions.Step s)
    {
        if (s.Target != LiveClaudeScenes || s.Value is not { Length: > 0 } v) return false;
        var (list, err) = ParseLiveScenes(v);
        if (list is null) { _liveMsg = "Claudes Szenen nicht übernommen: " + err + "."; return false; }
        var show = LiveShowState;
        foreach (var (name, main, texts) in list)
        {
            var sc = show.Scenes.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sc is null) { if (show.Scenes.Count >= 12) break; sc = show.Add(name, main); }
            else { var i = show.Scenes.IndexOf(sc); sc = sc with { Main = main }; show.Scenes[i] = sc; }
            sc.Overlays.RemoveAll(o => o.Kind == "text");
            foreach (var t in texts) show.AddOverlay(sc.Id, "text", t);
        }
        LiveSave(); _liveMsg = $"Claude hat {list.Count} Szene{(list.Count == 1 ? "" : "n")} eingerichtet – links auswählen, „Vorschau starten“ zeigt sie.";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.live.szenen", list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }
}
