using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik V11 wie Suno (07.11.2026): Personas – „Klang dieses Lieds merken“ (Remix-Menü), Liste auf der Seite „Stimmen“,
/// Auswahl im Erstellen-Bereich („+ Persona“). Beim Erstellen wird die Persona Klangvorbild (reference_audio) und ihr Stil steht vor den eigenen Wörtern.</summary>
public partial class MainWindow
{
    public MusicPersonas Personas => new(_core.DataRoot);
    string? _msPersona; string _msPersonaName = "";
    /// <summary>Gewählte Persona beim Erstellen (null = keine).</summary>
    public string? MsPersona { get => _msPersona; set => _msPersona = value; }

    /// <summary>Persona aus einem Lied anlegen.</summary>
    public MusicPersonas.Persona? MsMakePersona(string songId, string name)
    {
        var s = Songs.Get(songId) ?? throw new InvalidOperationException("Dieses Lied gibt es nicht mehr.");
        try
        {
            var p = Personas.Create(s, name);
            _core.Audit.Append(Principal.LocalOwner.Name, "musik.persona.neu", $"{p.Id} aus {s.Id}");
            _msMsg = $"✓ Persona „{p.Name}“ gemerkt – beim Erstellen unter „+ Persona“ wählen."; Show(MS);
            return p;
        }
        catch (ArgumentException e) { _msMsg = e.Message; Show(MS); return null; }
    }

    /// <summary>Persona für das nächste Lied wählen und zur Seite „Erstellen“ springen.</summary>
    public void MsUsePersona(string? id)
    {
        _msPersona = id is not null && Personas.Get(id) is { } p ? p.Id : null;
        _msMsg = _msPersona is null ? "Ohne Persona." : $"Persona „{Personas.Get(_msPersona)!.Name}“ – jetzt beschreiben und „Erstellen“.";
        MsPage("erstellen");
    }

    /// <summary>Für den Auftrag: Klangvorbild und Stil mit Persona (eigenes Klangvorbild hat Vorrang).</summary>
    (string? Ref, string Style) MsPersonaApply(string style)
    {
        if (_msPersona is null || Personas.Get(_msPersona) is not { } p || !File.Exists(p.File)) return (_msRefFile, style);
        return (_msRefFile ?? p.File, MusicPersonas.MergeStyle(p.Style, style));
    }

    Control MsPersonaChip(Func<string, string, string, Func<Task>, Button> b)
    {
        var p = _msPersona is null ? null : Personas.Get(_msPersona);
        return b("ms-ref-persona", p is null ? "+ Persona" : "✓ Persona: " + p.Name, "Klang eines deiner Lieder wiederverwenden (Stimme, Stil, Stimmung) – unter „Stimmen“ verwalten.", () => { MsPage("stimmen"); return Task.CompletedTask; });
    }

    /// <summary>Abschnitt „Personas“ auf der Seite „Stimmen“.</summary>
    Control MsPersonasPanel()
    {
        var sp = new StackPanel { Name = "ms-personas", Spacing = 6 };
        var all = Personas.All().OrderByDescending(p => p.Uses).ThenByDescending(p => p.Created).ToList();
        sp.Children.Add(new TextBlock { Text = $"PERSONAS ({all.Count})", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(P("Eine Persona merkt sich den Klang eines deiner Lieder – Stimme, Stil und Stimmung – für neue Lieder. Im Lied: „Remix → Persona aus diesem Lied“.", Muted, 11.5));
        if (all.Count == 0) sp.Children.Add(P("Noch keine Persona.", Muted, 12));
        var n = 0;
        foreach (var p in all)
        {
            var i = n++; var id = p.Id;
            var active = _msPersona == id;
            var use = new Button { Name = $"ms-persona-use-{i}", Content = active ? "✓ gewählt" : "Benutzen", FontSize = 11.5, Padding = new Thickness(10, 3), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0),
                Background = active ? Brush.Parse("#55D4AF37") : Brush.Parse("#22FFFFFF") };
            use.Click += (_, _) => MsUsePersona(active ? null : id);
            var del = new Button { Name = $"ms-persona-del-{i}", Content = "🗑", FontSize = 12, Padding = new Thickness(8, 3), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(del, "Persona löschen (das Lied bleibt)");
            del.Click += (_, _) => { Personas.Delete(id); if (_msPersona == id) _msPersona = null; _msMsg = $"Persona „{p.Name}“ gelöscht."; Show(MS); };
            var info = new StackPanel { Children = { new TextBlock { Text = "👤 " + p.Name, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = (p.Style.Length > 0 ? p.Style : "ohne Stil-Wörter") + $" · aus „{p.SongTitle}“" + (p.Uses > 0 ? $" · {p.Uses}× benutzt" : ""), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, Children = { info, Pos(use, 1), Pos(del, 2) } };
            use.VerticalAlignment = VerticalAlignment.Center; del.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(new Border { Name = $"ms-persona-{i}", Child = row, Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse(active ? "#2AD4AF37" : "#14FFFFFF") });
        }
        return sp;
    }

    /// <summary>Formular „Persona aus diesem Lied“ (im Reiter Bearbeiten).</summary>
    Control MsPersonaForm(MusicSong s)
    {
        var sp = new StackPanel { Name = "ms-persona-form", Spacing = 4 };
        sp.Children.Add(H("Persona aus diesem Lied", 13));
        if (MusicPersonas.Problem(s) is { } why) { sp.Children.Add(P(why, Muted, 11.5)); return sp; }
        sp.Children.Add(P("Klang, Stimme und Stil dieses Lieds für neue Lieder merken.", Muted, 11.5));
        var name = new TextBox { Name = "ms-persona-name", Text = _msPersonaName, Watermark = "Name, z. B. „Sommerstimme“ (leer = Liedtitel)", FontSize = 12 };
        name.TextChanged += (_, _) => _msPersonaName = name.Text ?? "";
        sp.Children.Add(name);
        sp.Children.Add(Act("ms-persona-go", "👤 Persona merken", () => { if (MsMakePersona(s.Id, name.Text ?? "") is not null) _msPersonaName = ""; }));
        return sp;
    }
}
