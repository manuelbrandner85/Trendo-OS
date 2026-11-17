using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0705 Büro-Ausbau: gemeinsame Bedienhilfen der sieben Büro-Studios auf der Arbeitsfläche – Ziehen zum Umsortieren (Seiten, Folien, Scans),
/// Dateien aus dem Explorer ablegen und Rechtsklick-Menüs. Nur Oberfläche: die Fachfunktionen bleiben in den Studio-Dateien, keine zweite Engine.</summary>
public partial class MainWindow
{
    /// <summary>Ab dieser Strecke (Pixel) ist ein gedrückter Klick ein Ziehen.</summary>
    const double BueroDragMin = 8;
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> BueroWired = new();
    Control? _brDragSource; int _brDragFrom = -1; Point _brDragAt;

    /// <summary>Knopf <paramref name="item"/> (Stelle <paramref name="index"/> in <paramref name="panel"/>) mit gedrückter linker Maustaste auf einen anderen Knopf
    /// derselben Leiste ziehen → <paramref name="move"/>(von, nach). Ein kurzer Klick bleibt ein Klick (Auswahl), Loslassen außerhalb der Leiste bricht ab.</summary>
    void BueroReorder(Panel panel, Control item, int index, string key, Action<int, int> move)
    {
        item.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(item).Properties.IsLeftButtonPressed) return;
            _brDragSource = item; _brDragFrom = index; _brDragAt = e.GetPosition(panel);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        item.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_brDragSource != item) return;
            var p = e.GetPosition(panel);
            if (Math.Abs(p.X - _brDragAt.X) + Math.Abs(p.Y - _brDragAt.Y) >= BueroDragMin) { item.Opacity = 0.55; item.Cursor = new Cursor(StandardCursorType.DragMove); }
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        item.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_brDragSource != item) return;
            var from = _brDragFrom; _brDragSource = null; _brDragFrom = -1; item.Opacity = 1; item.Cursor = Cursor.Default;
            var p = e.GetPosition(panel);
            if (Math.Abs(p.X - _brDragAt.X) + Math.Abs(p.Y - _brDragAt.Y) < BueroDragMin) return; // nur ein Klick
            var to = BueroIndexAt(panel, p);
            if (to < 0 || to == from) return;
            // erst nach dem Loslassen neu zeichnen – der Knopf beendet seinen Klick-Ablauf noch (kein Klick: losgelassen über einem anderen Knopf)
            _ui.Post(() => ProgramRun(key, () => move(from, to)));
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Welcher Eintrag der Leiste liegt an der Stelle? Hinter dem letzten Eintrag zählt der letzte, davor der erste; außerhalb der Leiste −1.</summary>
    static int BueroIndexAt(Panel panel, Point p)
    {
        var kids = panel.Children.Where(c => c.IsVisible).ToList();
        if (kids.Count == 0 || p.X < -20 || p.Y < -20 || p.X > panel.Bounds.Width + 20 || p.Y > panel.Bounds.Height + 20) return -1;
        for (var i = 0; i < kids.Count; i++) if (kids[i].Bounds.Contains(p)) return panel.Children.IndexOf(kids[i]);
        // in einer Lücke: der nächstgelegene Eintrag
        var best = kids.OrderBy(c => Math.Abs(c.Bounds.Center.X - p.X) + Math.Abs(c.Bounds.Center.Y - p.Y)).First();
        return panel.Children.IndexOf(best);
    }

    /// <summary>Dateien aus dem Explorer auf <paramref name="target"/> ablegen. Gilt auch für die Karte „erster Schritt“, die die Hülle über die leere Arbeitsfläche legt –
    /// gerade dort zieht man die erste Datei hin. <paramref name="accept"/> prüft die Endung; Unpassendes wird mit Hinweis abgelehnt, nichts wird kopiert.</summary>
    void BueroFileDrop(Control target, string key, Func<string, bool> accept, string what, Action<ProgramDocument, List<string>> handle)
    {
        void Wire(Control c)
        {
            if (BueroWired.TryGetValue(c, out _)) return;
            BueroWired.Add(c, key); DragDrop.SetAllowDrop(c, true);
            c.AddHandler(DragDrop.DragOverEvent, (_, e) =>
            {
                e.DragEffects = BueroDropFiles(e).Any(accept) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
            });
            c.AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                e.Handled = true;
                var all = BueroDropFiles(e); var ok = all.Where(accept).ToList();
                if (ProgramActive(key) is not { } d) return;
                if (ok.Count == 0) { _progMsg[key] = all.Count == 0 ? "Hier lassen sich nur Dateien ablegen." : $"Das passt hier nicht – bitte {what} ablegen."; Show(key); return; }
                ProgramRun(key, () =>
                {
                    handle(d, ok);
                    if (ok.Count < all.Count) _progMsg[key] = (_progMsg.GetValueOrDefault(key) ?? "") + $" ({all.Count - ok.Count} Datei(en) übersprungen – nur {what}.)";
                });
            });
        }
        Wire(target);
        target.AttachedToVisualTree += (_, _) =>
        {
            // die Hülle legt den ersten Schritt als Geschwister über die Arbeitsfläche: diese Karte ebenfalls annehmen lassen
            foreach (var panel in target.GetVisualAncestors().OfType<Panel>().Take(6))
                if (panel.Children.OfType<Control>().FirstOrDefault(c => c.Name == "prog-first") is { } card) { Wire(card); break; }
        };
    }

    /// <summary>Lokale Pfade aus einem Ablegen-Ereignis (Explorer).</summary>
    static List<string> BueroDropFiles(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];

    static Func<string, bool> BueroExt(params string[] ext) => f => ext.Contains(Path.GetExtension(f).ToLowerInvariant());

    /// <summary>Rechtsklick-Menü: Einträge (Name, Text, Aktion, aktiv). Vor der Aktion läuft <paramref name="before"/> (z. B. „diese Seite wählen“).</summary>
    ContextMenu BueroMenu(string key, string name, Action? before, params (string Name, string Header, Action Run, bool Enabled)[] items)
    {
        var menu = new ContextMenu { Name = name };
        foreach (var (n, h, run, en) in items)
        {
            if (n == "-") { menu.Items.Add(new Separator()); continue; }
            var mi = new MenuItem { Name = n, Header = h, IsEnabled = en };
            mi.Click += (_, _) => ProgramRun(key, () => { before?.Invoke(); run(); });
            menu.Items.Add(mi);
        }
        return menu;
    }
}
