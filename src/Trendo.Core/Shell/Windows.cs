namespace Trendo.Core.Shell;

/// <summary>Ein Fenster auf dem Trendo-Schreibtisch. Id = Fenstergruppe (Registerkarten einer Gruppe teilen ein Fenster), Page = gerade angezeigte Seite.</summary>
public sealed class DeskWindow(string id, string page, string title)
{
    public string Id { get; } = id;
    public string Page { get; set; } = page;
    public string Title { get; set; } = title;
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public bool Minimized { get; set; }
    public bool Maximized { get; set; }
    /// <summary>Vom Benutzer verschoben – dann passt Trendo die Größe nicht mehr automatisch an.</summary>
    public bool Moved { get; set; }
}

/// <summary>Fensterverwaltung innerhalb von Trendo: öffnen, nach vorn holen, minimieren, maximieren, schließen, verschieben. Reihenfolge = Stapel (letztes = oben). Ersetzt nicht die Windows-Taskleiste.</summary>
public sealed class WindowManager
{
    /// <summary>Ab dieser Zahl erscheint ein Hinweis. Es wird trotzdem nichts geschlossen und keine Eingabe verworfen.</summary>
    public const int NoticeAfter = 24;
    public const string ManyWindowsNotice = "Viele Fenster sind offen. Trendo schließt keines davon und verwirft keine Eingaben. Schließe Fenster, die du nicht mehr brauchst.";
    public const double MinW = 360, MinH = 240, TitleBar = 36;
    /// <summary>Hinweis, wenn sehr viele Fenster offen sind. Null, solange alles übersichtlich bleibt.</summary>
    public string? Notice { get; private set; }
    private readonly List<DeskWindow> _stack = [];
    private readonly List<DeskWindow> _taskOrder = [];
    public double AreaW { get; set; } = 1000;
    public double AreaH { get; set; } = 640;

    /// <summary>Fenster in Taskleisten-Reihenfolge (Öffnungsreihenfolge).</summary>
    public IReadOnlyList<DeskWindow> TaskOrder => _taskOrder;
    /// <summary>Von unten nach oben.</summary>
    public IReadOnlyList<DeskWindow> Stack => _stack;
    /// <summary>Oberstes sichtbares Fenster, null wenn alle minimiert/geschlossen (dann sieht man den Startbildschirm).</summary>
    public DeskWindow? Active => _stack.LastOrDefault(w => !w.Minimized);
    public DeskWindow? Get(string id) => _stack.FirstOrDefault(w => w.Id == id);

    /// <summary>Öffnet ein neues Fenster oder holt ein vorhandenes nach vorn. Ein bereits geöffnetes Studio wird aktiviert, nie dupliziert. Andere Fenster bleiben offen – nichts wird still geschlossen.</summary>
    public (DeskWindow Window, DeskWindow? Closed) Open(string id, string page, string title)
    {
        if (Get(id) is { } w) { w.Page = page; w.Title = title; Activate(id); return (w, null); }
        if (_stack.Count >= NoticeAfter) Notice ??= ManyWindowsNotice;
        var n = _taskOrder.Count % 8;
        w = new DeskWindow(id, page, title) { W = Math.Max(MinW, AreaW * 0.9), H = Math.Max(MinH, AreaH * 0.9), X = 16 + n * 28, Y = 8 + n * 28 };
        Clamp(w); _stack.Add(w); _taskOrder.Add(w); return (w, null);
    }

    /// <summary>Zustand aller Fenster (Taskleisten-Reihenfolge) und Stapel von unten nach oben – zum Wiederherstellen nach dem Neuaufbau.</summary>
    public sealed record Saved(string Id, string Page, double X, double Y, double W, double H, bool Minimized, bool Maximized, bool Moved);
    public (List<Saved> Windows, List<string> Stack) Snapshot() =>
        (_taskOrder.Select(w => new Saved(w.Id, w.Page, w.X, w.Y, w.W, w.H, w.Minimized, w.Maximized, w.Moved)).ToList(), _stack.Select(w => w.Id).ToList());

    /// <summary>Übernimmt Lage, Größe und Zustand; ein bereits geöffnetes Fenster wird angepasst, Stapel wie gespeichert.</summary>
    public void Apply((List<Saved> Windows, List<string> Stack) s)
    {
        foreach (var x in s.Windows) if (Get(x.Id) is { } w) { (w.X, w.Y, w.W, w.H, w.Maximized, w.Moved) = (x.X, x.Y, x.W, x.H, x.Maximized, x.Moved); Clamp(w); }
        _taskOrder.Sort((a, b) => Index(a.Id).CompareTo(Index(b.Id)));
        foreach (var id in s.Stack) if (Get(id) is { } w) { _stack.Remove(w); _stack.Add(w); }
        foreach (var x in s.Windows) if (Get(x.Id) is { } w) w.Minimized = x.Minimized;
        int Index(string id) { var i = s.Windows.FindIndex(x => x.Id == id); return i < 0 ? int.MaxValue : i; }
    }

    public void Activate(string id) { if (Get(id) is not { } w) return; w.Minimized = false; _stack.Remove(w); _stack.Add(w); }
    public void Minimize(string id) { if (Get(id) is { } w) w.Minimized = true; }
    public void ToggleMaximize(string id) { if (Get(id) is { } w) { w.Maximized = !w.Maximized; Activate(id); } }
    public void Close(string id)
    {
        if (Get(id) is { } w) { _stack.Remove(w); _taskOrder.Remove(w); }
        if (_stack.Count <= NoticeAfter) Notice = null;
    }
    public void MinimizeAll() { foreach (var w in _stack) w.Minimized = true; }

    /// <summary>Taskleisten-Klick: aktives Fenster minimieren, sonst nach vorn holen.</summary>
    public void TaskbarClick(string id) { if (Active?.Id == id) Minimize(id); else Activate(id); }

    /// <summary>Verschieben; die Titelleiste bleibt immer erreichbar (nie ganz aus dem Bereich).</summary>
    public void Move(string id, double dx, double dy)
    {
        if (Get(id) is not { } w || w.Maximized) return;
        w.X += dx; w.Y += dy; w.Moved = true; Clamp(w);
    }
    /// <summary>Bereichsgröße geändert: nicht verschobene Fenster wachsen mit (90 %), alle bleiben erreichbar.</summary>
    public void Resize(double w, double h)
    {
        AreaW = w; AreaH = h;
        foreach (var x in _stack) { if (!x.Moved) { x.W = AreaW * 0.9; x.H = AreaH * 0.9; } Clamp(x); }
    }
    /// <summary>Größe ändern an Rand oder Ecke. edge enthält L, R, T, B (z. B. „BR“ = Ecke unten rechts). Mindestgröße bleibt.</summary>
    public void ResizeEdge(string id, string edge, double dx, double dy)
    {
        if (Get(id) is not { } w) return;
        if (w.Maximized) { w.Maximized = false; }
        w.Moved = true;
        if (edge.Contains('R')) w.W = Math.Max(MinW, w.W + dx);
        if (edge.Contains('B')) w.H = Math.Max(MinH, w.H + dy);
        if (edge.Contains('L')) { var nw = Math.Max(MinW, w.W - dx); w.X += w.W - nw; w.W = nw; }
        if (edge.Contains('T')) { var nh = Math.Max(MinH, w.H - dy); w.Y += w.H - nh; w.H = nh; }
        if (w.X < 0 && edge.Contains('L')) { w.W += w.X; w.X = 0; }
        if (w.Y < 0) { if (edge.Contains('T')) w.H += w.Y; w.Y = 0; }
        if (w.X + w.W > AreaW && edge.Contains('R')) w.W = Math.Max(MinW, AreaW - w.X);
        if (w.Y + w.H > AreaH && edge.Contains('B')) w.H = Math.Max(MinH, AreaH - w.Y);
    }

    /// <summary>Wie bei Windows: Fenster an den linken/rechten Rand gezogen → halbe Fläche. pointerX = Mausposition im Bereich. Gibt die Seite zurück („links“/„rechts“) oder null.</summary>
    public string? SnapAt(string id, double pointerX, double margin = 12)
    {
        if (Get(id) is not { } w) return null;
        string? side = pointerX <= margin ? "links" : pointerX >= AreaW - margin ? "rechts" : null;
        if (side is null) return null;
        w.Maximized = false; w.Moved = true;
        w.W = AreaW / 2; w.H = AreaH; w.Y = 0; w.X = side == "links" ? 0 : AreaW / 2;
        return side;
    }

    public void Clamp(DeskWindow w)
    {
        w.W = Math.Clamp(w.W, MinW, Math.Max(MinW, AreaW)); w.H = Math.Clamp(w.H, MinH, Math.Max(MinH, AreaH));
        w.X = Math.Clamp(w.X, 80 - w.W, Math.Max(0, AreaW - 80)); w.Y = Math.Clamp(w.Y, 0, Math.Max(0, AreaH - TitleBar));
    }
    public (double X, double Y, double W, double H) Rect(DeskWindow w) => w.Maximized ? (0, 0, AreaW, AreaH) : (w.X, w.Y, w.W, w.H);
}
