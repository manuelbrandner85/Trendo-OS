using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude baut 3D-Modelle (Claude-Ausbau 1, 05.11.2026): aus einem Satz oder Foto eine Szene mit echten Maßen, PBR-Materialien, Licht und Kamera.
/// Claude bekommt die Szene als Text, schlägt Teile vor („[[trendo:feld | 3d-teile | …]]“), der Plan zeigt das Modell als Vorschau, erst „Ausführen“ ändert es
/// (Rückgängig wie immer). Danach kann Claude „Render“ klicken lassen und das Bild prüfen. Komplexe Figuren baut Claude sichtbar in Blender (Model3DRules).</summary>
public partial class MainWindow
{
    const string F3DParts = "3d-teile", F3DLight = "3d-licht", F3DCamera = "3d-kamera";

    /// <summary>Ein Teil als Textzeile: Name;Form;Breite;Tiefe;Höhe;X;Y;Z;DrehX;DrehY;DrehZ;Material;Gruppe (cm, Grad, Punkt als Dezimalzeichen).</summary>
    static string PartLine(Part3D p) => string.Join(";", Clean(p.Name), p.Shape, N(p.Sx), N(p.Sy), N(p.Sz), N(p.X), N(p.Y), N(p.Z), N(p.Rx), N(p.Ry), N(p.Rz), p.Material ?? "", Clean(p.Group ?? ""));
    static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
    static string Clean(string s) => s.Replace(";", ",").Replace("/", "-").Replace("]", ")").Replace("\n", " ").Trim();

    /// <summary>Teile aus Claudes Text lesen und prüfen (Formen, Maße 0,1–10 000 cm, bekannte Materialien) – bei Fehlern null und der Grund.</summary>
    public static (List<Part3D>? Parts, string? Error) Parse3DParts(string value)
    {
        var rows = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length == 0) return (null, "keine Teile");
        if (rows.Length > 200) return (null, "höchstens 200 Teile");
        var list = new List<Part3D>();
        foreach (var (row, i) in rows.Select((r, i) => (r, i + 1)))
        {
            var c = row.Split(';').Select(x => x.Trim()).ToArray();
            if (c.Length < 5) return (null, $"Teil {i}: zu wenige Angaben (Name;Form;Breite;Tiefe;Höhe …)");
            var shape = c[1].ToLowerInvariant();
            if (!Scene3D.PartShapes.Any(s => s.Id == shape)) return (null, $"Teil {i}: unbekannte Form „{c[1]}“ (erlaubt: {string.Join(", ", Scene3D.PartShapes.Select(s => s.Id))})");
            double D(int k, double def) => k < c.Length && double.TryParse(c[k].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : def;
            var p = new Part3D { Name = c[0].Length == 0 ? $"Teil {i}" : c[0].Length > 60 ? c[0][..60] : c[0], Shape = shape, Sx = D(2, 10), Sy = D(3, 10), Sz = D(4, 10), X = D(5, 0), Y = D(6, 0), Z = D(7, 0), Rx = D(8, 0), Ry = D(9, 0), Rz = D(10, 0) };
            if (new[] { p.Sx, p.Sy, p.Sz }.Any(s => s is < 0.1 or > 10000)) return (null, $"Teil {i} „{p.Name}“: Maße zwischen 0,1 und 10 000 cm");
            if (new[] { p.X, p.Y, p.Z }.Any(v => Math.Abs(v) > 100000)) return (null, $"Teil {i} „{p.Name}“: Lage zu weit weg");
            if (c.Length > 11 && c[11].Length > 0)
            {
                if (!Studio3DPro.Materials.Any(m => m.Id == c[11])) return (null, $"Teil {i}: unbekanntes Material „{c[11]}“");
                p.Material = c[11]; if (Studio3DPro.MaterialOf(c[11]).Tint is { } t) p.Color = t;
            }
            if (c.Length > 12 && c[12].Length > 0) p.Group = c[12].Length > 40 ? c[12][..40] : c[12];
            if (shape == "text") p.Text = p.Name;
            list.Add(p);
        }
        return (list, null);
    }

    IEnumerable<ClaudeActions.Field> Claude3DFields()
    {
        yield return new(F3DParts, "Teile der 3D-Szene", string.Join(" / ", _scene.Parts.Where(p => !p.Hidden).Select(PartLine)));
        yield return new(F3DLight, "Licht", _scene.Light ?? "");
        yield return new(F3DCamera, "Kamera (Brennweite mm;Blickhöhe cm)", _scene.CameraLens > 0 ? $"{N(_scene.CameraLens)};{N(_scene.CameraHeight)}" : "");
    }

    /// <summary>Für Claude ausführlich (Feldwerte werden im Seitenbericht gekürzt): Szene, Materialien, Licht, Kamera und wie Claude sie ändert.</summary>
    public string Claude3DFocus()
    {
        var sb = new System.Text.StringBuilder($"3D-Modelle erstellen: Szene „{_scene.Name}“, {_scene.Parts.Count} Teile" + (Selected is { } sp ? $", ausgewählt: {sp.Name}" : "") + ". Maße in cm, Z nach oben, Boden bei Z = 0 (X/Y/Z ist die Mitte des Teils).");
        sb.Append("\nTeile (Name;Form;Breite;Tiefe;Höhe;X;Y;Z;DrehX;DrehY;DrehZ;Material;Gruppe): " + (_scene.Parts.Count == 0 ? "keine" : string.Join(" / ", _scene.Parts.Where(p => !p.Hidden).Select(PartLine))));
        sb.Append($"\nFormen: {string.Join(", ", Scene3D.PartShapes.Select(s => s.Id))}. Materialien (PBR): {string.Join(", ", Studio3DPro.Materials.Select(m => m.Id))}. Licht: {string.Join(", ", Studio3DPro.Lights.Select(l => l.Id))}.");
        sb.Append($"\nLicht jetzt: {_scene.Light ?? "keins"}; Kamera: {(_scene.CameraLens > 0 ? N(_scene.CameraLens) + " mm" : "keine")}; Renders: {Studio3DPro.Renders(Dir3D()).Count}.");
        sb.Append("\nSo baust du ein Modell: [[trendo:feld | 3d-teile | Teil1 / Teil2 / …]] ersetzt alle Teile (echte Maße eines echten Gegenstands, jedes Teil mit passendem Material, zusammengehörige Teile in einer Gruppe), dazu [[trendo:feld | 3d-licht | studio]] und [[trendo:feld | 3d-kamera | 85;0]]. "
            + "Zum Prüfen danach [[trendo:klick | 3d-step-render]] und [[trendo:klick | 3d-render]] – das Bild bewertest du ehrlich (Proportionen, Kontakt zum Boden, Material, Licht).");
        return sb.ToString();
    }

    bool Claude3DApply(ClaudeActions.Step s)
    {
        if (s.Kind != "feld" || s.Value is null) return false;
        switch (s.Target)
        {
            case F3DParts:
                var (parts, err) = Parse3DParts(s.Value);
                if (parts is null) { Say("Claude-Vorschlag nicht übernommen: " + err + ".", "3d"); return false; }
                Push(); _scene.Parts = parts; _selId = parts.FirstOrDefault()?.Id; _selGroup = null; _step3d = "modell";
                Say($"Claude hat {parts.Count} Teil(e) gebaut – „Rückgängig“ holt den alten Stand zurück.", "3d");
                return true;
            case F3DLight:
                if (!Studio3DPro.Lights.Any(l => l.Id == s.Value.Trim())) return false;
                Studio3DSetLight(s.Value.Trim(), _scene.LightPower); return true;
            case F3DCamera:
                var c = s.Value.Split(';');
                if (!double.TryParse(c[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lens) || lens is < 10 or > 300) return false;
                var h = c.Length > 1 && double.TryParse(c[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var hh) ? hh : 0;
                Studio3DSetCamera(lens, h); return true;
        }
        return false;
    }

    Control? Claude3DPreview(ClaudeActions.Step s)
    {
        if (s.Kind != "feld" || s.Target != F3DParts || s.Value is not { Length: > 0 } v) return null;
        var (parts, err) = Parse3DParts(v);
        if (parts is null) return new TextBlock { Name = "claude-plan-warn", Text = "⚠ So nicht möglich: " + err, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var view = new CadView { Name = "claude-plan-3d", Width = 300, Height = 200, IsHitTestVisible = false, Parts = parts.Select(p => (p.Color, PartSolid(p), false)).ToList(),
            Looks = parts.Select(p => p.Material is { } m ? (Studio3DPro.MaterialOf(m).Metallic, Studio3DPro.MaterialOf(m).Roughness, Studio3DPro.MaterialOf(m).Transmission) : (0.0, 0.5, 0.0)).ToList() };
        var bs = parts.Select(Bounds3D).ToList();
        var size = $"{N(bs.Max(b => b.Max.X) - bs.Min(b => b.Min.X))} × {N(bs.Max(b => b.Max.Y) - bs.Min(b => b.Min.Y))} × {N(bs.Max(b => b.Max.Z) - bs.Min(b => b.Min.Z))} cm";
        var floating = bs.Min(b => b.Min.Z) > 0.5 ? " · ⚠ schwebt über dem Boden" : "";
        return new StackPanel { Spacing = 2, Children = { new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Child = view },
            new TextBlock { Name = "claude-plan-3d-info", Text = $"{parts.Count} Teile · gesamt {size}{floating}", Foreground = ClaudeMuted, FontSize = 11.5 } } };
    }
}
