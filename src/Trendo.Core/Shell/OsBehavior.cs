namespace Trendo.Core.Shell;

/// <summary>Bedienverhalten je Bedienstil (04.11.2026) – so, wie es das jeweilige System macht (öffentliche Hilfe von Microsoft, Apple und GNOME):
/// Ziehen und Ablegen (verschieben oder kopieren), Name einer Kopie, Name eines neuen Ordners, Tastenkürzel im Dateimanager und in Fenstern.
/// Stil „trendo“ verhält sich wie bisher (Windows-ähnlich).</summary>
public static class OsBehavior
{
    [Flags] public enum Mods { None = 0, Ctrl = 1, Shift = 2, Alt = 4, Meta = 8 }

    /// <summary>Befehlstaste: auf dem Mac ⌘ (Meta). Auf PC-Tastaturen übernimmt Strg die Rolle von ⌘ – wie bei Apple-Tastaturbelegungen unter Windows.</summary>
    public static bool Cmd(Mods m, bool macHardware) => macHardware ? m.HasFlag(Mods.Meta) : m.HasFlag(Mods.Ctrl);

    /// <summary>Ziehen und Ablegen: true = kopieren, false = verschieben.
    /// Windows/GNOME: gleiches Laufwerk verschieben, anderes kopieren; Strg erzwingt Kopieren, Umschalt Verschieben.
    /// Mac: gleiches Volume bewegen, anderes kopieren; Wahltaste (Alt) kopiert, ⌘ bewegt.</summary>
    public static bool DropCopies(string style, bool sameVolume, Mods m, bool macHardware = false)
    {
        if (style == "mac")
        {
            if (m.HasFlag(Mods.Alt)) return true;
            if (Cmd(m, macHardware)) return false;
            return !sameVolume;
        }
        if (m.HasFlag(Mods.Ctrl)) return true;
        if (m.HasFlag(Mods.Shift)) return false;
        return !sameVolume;
    }

    /// <summary>Name einer Kopie im selben Ordner – Windows „Datei - Kopie.txt“, „Datei - Kopie (2).txt“; Mac „Datei Kopie.txt“, „Datei Kopie 2.txt“;
    /// GNOME „Datei (Kopie).txt“, „Datei (weitere Kopie).txt“, „Datei (3. Kopie).txt“.</summary>
    public static string CopyName(string style, string name, bool isDir, Func<string, bool> exists)
    {
        var stem = isDir ? name : Path.GetFileNameWithoutExtension(name); var ext = isDir ? "" : Path.GetExtension(name);
        if (!exists(name)) return name;
        for (var n = 1; n < 10000; n++)
        {
            var c = style switch
            {
                "mac" => stem + (n == 1 ? " Kopie" : $" Kopie {n}") + ext,
                "linux" => stem + (n == 1 ? " (Kopie)" : n == 2 ? " (weitere Kopie)" : $" ({n}. Kopie)") + ext,
                _ => stem + (n == 1 ? " - Kopie" : $" - Kopie ({n})") + ext,
            };
            if (!exists(c)) return c;
        }
        throw new InvalidOperationException("Zu viele Kopien.");
    }

    /// <summary>Name eines neuen Ordners: Windows/Mac „Neuer Ordner“ (dann „Neuer Ordner (2)“ bzw. „Neuer Ordner 2“), GNOME „Unbenannter Ordner“.</summary>
    public static string NewFolderName(string style, Func<string, bool> exists)
    {
        var b = style == "linux" ? "Unbenannter Ordner" : "Neuer Ordner";
        if (!exists(b)) return b;
        for (var n = 2; ; n++) { var c = style == "mac" || style == "linux" ? $"{b} {n}" : $"{b} ({n})"; if (!exists(c)) return c; }
    }

    /// <summary>Tastenkürzel im Dateimanager → Befehl. Tasten als Namen wie in Avalonia („Enter“, „F2“, „Back“, „Delete“, „Up“, „C“ …).</summary>
    public static string? FileKey(string style, string key, Mods m, bool macHardware = false)
    {
        if (key == "Return") key = "Enter"; // Avalonia nennt Enter „Return“
        var cmd = Cmd(m, macHardware);
        if (style == "mac")
        {
            return (key, cmd, m.HasFlag(Mods.Shift), m.HasFlag(Mods.Alt)) switch
            {
                ("T", true, _, _) => "newtab",                 // Finder-Tabs: ⌘T neu, ⌘W schließen, ⌘⇧] / ⌘⇧[ wechseln
                ("W", true, _, _) => "closetab",
                ("OemCloseBrackets", true, true, _) => "nexttab",
                ("OemOpenBrackets", true, true, _) => "prevtab",
                ("Enter", false, _, _) => "rename",          // Finder: Zeilenschalter benennt um
                ("O", true, _, _) or ("Down", true, _, _) => "open",
                ("Back", true, _, _) or ("Delete", true, _, _) => "trash",
                ("C", true, _, _) => "copy",
                ("V", true, _, true) => "move-here",           // ⌘⌥V: Objekt hierher bewegen (Finder kennt kein Ausschneiden)
                ("V", true, _, false) => "paste",
                ("N", true, true, _) => "newfolder",
                ("D", true, _, _) => "duplicate",
                ("Up", true, _, _) => "up",
                ("OemOpenBrackets", true, _, _) or ("Left", true, _, _) => "back",
                ("OemCloseBrackets", true, _, _) or ("Right", true, _, _) => "forward",
                ("Space", false, _, _) => "preview",           // Übersicht (Quick Look)
                ("A", true, _, _) => "selectall",
                ("F", true, _, _) => "search",
                ("I", true, _, _) => "info",
                _ => null,
            };
        }
        var ctrl = m.HasFlag(Mods.Ctrl); var shift = m.HasFlag(Mods.Shift); var alt = m.HasFlag(Mods.Alt);
        return (key, ctrl, shift, alt) switch
        {
            ("T", true, false, _) => "newtab",                 // Registerkarten: Strg+T neu, Strg+W schließen, Strg+Bild↓/↑ wechseln
            ("W", true, false, _) => "closetab",
            ("PageDown", true, _, _) => "nexttab",
            ("PageUp", true, _, _) => "prevtab",
            ("Enter", false, false, false) => "open",
            ("Enter", false, false, true) => "info",
            ("F2", _, _, _) => "rename",
            ("Delete", false, false, _) => "trash",
            ("C", true, _, _) => "copy",
            ("X", true, _, _) => "cut",
            ("V", true, _, _) => "paste",
            ("N", true, true, _) => "newfolder",
            ("A", true, _, _) => "selectall",
            ("Up", false, false, true) => "up",
            ("Left", false, false, true) => "back",
            ("Right", false, false, true) => "forward",
            ("Back", false, false, false) => style == "linux" ? "back" : "back",
            ("F5", _, _, _) => "refresh",
            ("F", true, _, _) or ("E", true, _, _) when style != "linux" => "search",
            ("F", true, _, _) => "search",
            ("L", true, _, _) => "location",
            ("H", true, _, _) when style == "linux" => "hidden",
            ("Space", false, false, false) when style == "linux" => "preview", // Sushi-Vorschau
            _ => null,
        };
    }

    /// <summary>Tastenkürzel für Fenster: Schließen, Minimieren, Maximieren, Fenster wechseln, Übersicht/Startmenü.</summary>
    public static string? WindowKey(string style, string key, Mods m, bool macHardware = false)
    {
        if (key == "Return") key = "Enter";
        var cmd = Cmd(m, macHardware);
        return style switch
        {
            "mac" => (key, cmd, m.HasFlag(Mods.Alt)) switch
            {
                ("W", true, _) => "close", ("M", true, _) => "minimize", ("Q", true, _) => "close", ("H", true, _) => "minimize",
                ("Space", true, _) => "spotlight", ("OemComma", true, _) => "settings", ("Tab", true, _) => "switch",
                ("F", true, _) when m.HasFlag(Mods.Ctrl) && macHardware => "maximize",
                _ => null,
            },
            "linux" => (key, m) switch
            {
                ("F4", Mods.Alt) => "close", ("W", Mods.Ctrl) => "close", ("Q", Mods.Ctrl) => "close", ("H", Mods.Meta) => "minimize", ("Up", Mods.Meta) => "maximize", ("Down", Mods.Meta) => "restore",
                ("LWin", Mods.None) or ("RWin", Mods.None) or ("LWin", Mods.Meta) => "overview", ("Tab", Mods.Alt) => "switch", ("A", Mods.Meta) => "apps",
                _ => null,
            },
            _ => (key, m) switch
            {
                ("F4", Mods.Alt) => "close", ("Up", Mods.Meta) => "maximize", ("Down", Mods.Meta) => "minimize", ("D", Mods.Meta) => "desktop",
                ("LWin", Mods.None) or ("RWin", Mods.None) or ("LWin", Mods.Meta) => "start", ("Tab", Mods.Alt) => "switch", ("E", Mods.Meta) => "files", ("W", Mods.Meta) => "widgets", ("S", Mods.Meta) => "search",
                _ => null,
            },
        };
    }

    /// <summary>Doppelklick auf die Titelleiste: Windows und GNOME maximieren, Mac zoomt (Standard der Systemeinstellung).</summary>
    public static string TitleDoubleClick(string style) => "maximize";

    /// <summary>Öffnet ein Einfachklick in der Seitenleiste den Ort? (überall ja). Im Inhalt öffnet überall erst der Doppelklick.</summary>
    public static bool SingleClickOpensInContent(string style) => false;

    /// <summary>Wo „Löschen“ landet und wie es heißt.</summary>
    public static string TrashLabel(string style) => style switch { "mac" => "In den Papierkorb legen", "linux" => "In den Papierkorb verschieben", _ => "Löschen" };
}
