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

/// <summary>Fensterverwaltung wie bei Windows: öffnen, nach vorn holen, minimieren, maximieren, schließen, verschieben. Reihenfolge = Stapel (letztes = oben).</summary>
public sealed class WindowManager
{
    public const int MaxOpen = 8;
    public const double MinW = 360, MinH = 240, TitleBar = 36;
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

    /// <summary>Öffnet ein neues Fenster oder holt ein vorhandenes nach vorn. Bei mehr als 8 wird das am längsten unbenutzte geschlossen.</summary>
    public (DeskWindow Window, DeskWindow? Closed) Open(string id, string page, string title)
    {
        DeskWindow? closed = null;
        if (Get(id) is { } w) { w.Page = page; w.Title = title; Activate(id); return (w, null); }
        if (_stack.Count >= MaxOpen) { closed = _stack[0]; Close(closed.Id); }
        var n = _taskOrder.Count % 5;
        w = new DeskWindow(id, page, title) { W = Math.Max(MinW, AreaW * 0.9), H = Math.Max(MinH, AreaH * 0.9), X = 16 + n * 24, Y = 8 + n * 24 };
        Clamp(w); _stack.Add(w); _taskOrder.Add(w); return (w, closed);
    }

    public void Activate(string id) { if (Get(id) is not { } w) return; w.Minimized = false; _stack.Remove(w); _stack.Add(w); }
    public void Minimize(string id) { if (Get(id) is { } w) w.Minimized = true; }
    public void ToggleMaximize(string id) { if (Get(id) is { } w) { w.Maximized = !w.Maximized; Activate(id); } }
    public void Close(string id) { if (Get(id) is { } w) { _stack.Remove(w); _taskOrder.Remove(w); } }
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
    public void Clamp(DeskWindow w)
    {
        w.W = Math.Clamp(w.W, MinW, Math.Max(MinW, AreaW)); w.H = Math.Clamp(w.H, MinH, Math.Max(MinH, AreaH));
        w.X = Math.Clamp(w.X, 80 - w.W, Math.Max(0, AreaW - 80)); w.Y = Math.Clamp(w.Y, 0, Math.Max(0, AreaH - TitleBar));
    }
    public (double X, double Y, double W, double H) Rect(DeskWindow w) => w.Maximized ? (0, 0, AreaW, AreaH) : (w.X, w.Y, w.W, w.H);
}
