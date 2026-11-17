namespace Trendo.Core.Programs;

/// <summary>T-0710 (Wunsch 17.11.2026 „Menüleiste wie bei Windows, wo Datei usw. steht“, gewählt: klassische Menüleiste): ein Eintrag eines Menüs.
/// <paramref name="Run"/> ist derselbe Handler wie der Knopf des Programms – die Menüleiste hat keine eigene Befehlslogik.
/// <paramref name="Enabled"/> wird beim Aufklappen gefragt (ausgegraut = nicht möglich), <paramref name="Checked"/> macht einen Schalter mit Haken,
/// <paramref name="Sub"/> ein Untermenü („Exportieren ▸“). <paramref name="Shortcut"/> steht rechts in der Zeile und gilt im Programm wirklich.</summary>
public sealed record MenuCmd(string Id, string Label, Action? Run = null, string Shortcut = "", Func<bool>? Enabled = null,
    IReadOnlyList<MenuCmd>? Sub = null, Func<bool>? Checked = null, string? Tip = null)
{
    public bool IsSeparator => Id == "-";
    /// <summary>Trennlinie zwischen zwei Gruppen eines Menüs.</summary>
    public static MenuCmd Separator => new("-", "");
    public bool CanRun => Run is not null && (Enabled?.Invoke() ?? true);
}

/// <summary>Ein Menü der Leiste („Datei“, „Bearbeiten“, „Bild“ …).</summary>
public sealed record MenuTop(string Title, List<MenuCmd> Items);

/// <summary>T-0710: Regeln der klassischen Menüleiste für alle Programme – Reihenfolge, unterstrichene Buchstaben (Alt+Buchstabe), Tastenkürzel und deren Prüfung.</summary>
public static class ProgramMenu
{
    public const string Datei = "Datei", Bearbeiten = "Bearbeiten", Ansicht = "Ansicht", Ki = "KI", Hilfe = "Hilfe";

    /// <summary>Feste Buchstaben der Standardmenüs wie unter Windows (Alt+D Datei, Alt+B Bearbeiten, Alt+A Ansicht, Alt+H Hilfe).</summary>
    public static readonly IReadOnlyDictionary<string, char> FixedKeys = new Dictionary<string, char>
    {
        [Datei] = 'D', [Bearbeiten] = 'B', [Ansicht] = 'A', [Ki] = 'K', [Hilfe] = 'H',
    };

    /// <summary>Übliche Tastenkürzel (deutsche Schreibweise), wie Windows-Programme sie zeigen.</summary>
    public static class Keys
    {
        public const string Neu = "Strg+N", Oeffnen = "Strg+O", Speichern = "Strg+S", SpeichernUnter = "Strg+Umschalt+S", Drucken = "Strg+P", Schliessen = "Strg+W";
        public const string Rueckgaengig = "Strg+Z", Wiederholen = "Strg+Y", Ausschneiden = "Strg+X", Kopieren = "Strg+C", Einfuegen = "Strg+V", AllesMarkieren = "Strg+A", Suchen = "Strg+F", Ersetzen = "Strg+H";
        public const string Hilfe = "F1", Befehle = "Strg+K", Tastenkuerzel = "Strg+Umschalt+K", Vollbild = "F11";
    }

    /// <summary>Kürzel, die Trendo im ganzen Fenster schon belegt (Befehlsleiste, Hilfe, Fokus …) – ein Programm darf sie nicht für etwas anderes zeigen.
    /// Wert: wofür sie stehen. Einträge, die genau diese Funktion auslösen, dürfen das Kürzel zeigen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Global = new Dictionary<string, string>
    {
        ["Strg+K"] = "Befehlsleiste", ["Strg+Q"] = "Befehlsleiste", ["Strg+Umschalt+K"] = "Alle Tastenkürzel", ["Strg+Umschalt+A"] = "Markierung an Claude",
        ["Strg+Umschalt+F"] = "Suche im System", ["F1"] = "Hilfe", ["F11"] = "Fokusmodus", ["Alt+Links"] = "Zurück", ["F10"] = "Menüleiste", ["Alt"] = "Menüleiste",
        ["Strg+1"] = "Startbildschirm", ["Strg+2"] = "Studios", ["Strg+3"] = "Projekte", ["Strg+4"] = "Freigaben", ["Strg+5"] = "System", ["Strg+Tab"] = "Fenster wechseln",
    };

    /// <summary>Einträge, die genau die globale Funktion auslösen und deshalb deren Kürzel zeigen dürfen (Hilfe F1, Befehlsleiste Strg+K, Übersicht Strg+Umschalt+K, Fokusmodus F11).</summary>
    public static readonly IReadOnlySet<string> GlobalEntries = new HashSet<string> { "help-show", "help-palette", "help-keys", "view-focus" };

    /// <summary>Reihenfolge: Datei · Bearbeiten · Ansicht · fachliche Menüs (in der Reihenfolge des Programms) · KI · Hilfe; leere Menüs fallen weg,
    /// Trennlinien am Anfang, am Ende und doppelt hintereinander auch. Gleichnamige Menüs werden zusammengelegt.</summary>
    public static List<MenuTop> Normalize(IEnumerable<MenuTop> menus)
    {
        var merged = new List<MenuTop>();
        foreach (var m in menus)
        {
            if (merged.FirstOrDefault(x => x.Title == m.Title) is { } have)
            {
                if (have.Items.Count > 0 && m.Items.Count > 0) have.Items.Add(MenuCmd.Separator);
                have.Items.AddRange(m.Items);
            }
            else merged.Add(new MenuTop(m.Title, [.. m.Items]));
        }
        static int Rank(string t) => t switch { Datei => 0, Bearbeiten => 1, Ansicht => 2, Ki => 8, Hilfe => 9, _ => 5 };
        return merged.Select((m, i) => (m, i)).OrderBy(x => Rank(x.m.Title)).ThenBy(x => x.i)
            .Select(x => new MenuTop(x.m.Title, Clean(x.m.Items))).Where(m => m.Items.Any(i => !i.IsSeparator)).ToList();
    }

    static List<MenuCmd> Clean(List<MenuCmd> items)
    {
        var l = new List<MenuCmd>();
        foreach (var i in items)
        {
            if (i.IsSeparator && (l.Count == 0 || l[^1].IsSeparator)) continue;
            if (i.Sub is { } sub)
            {
                var s = Clean([.. sub]);
                if (s.Count == 0) continue; // leeres Untermenü: weg
                l.Add(i with { Sub = s });
            }
            else l.Add(i);
        }
        while (l.Count > 0 && l[^1].IsSeparator) l.RemoveAt(l.Count - 1);
        return l;
    }

    /// <summary>Unterstrichener Buchstabe je Eintrag (Alt+Buchstabe bzw. Buchstabe im offenen Menü): feste Buchstaben zuerst, sonst der erste noch freie Buchstabe des Wortes.
    /// Ohne freien Buchstaben: '\0' (der Eintrag bleibt mit Pfeiltasten erreichbar).</summary>
    public static List<char> AccessKeys(IReadOnlyList<string> labels, bool fixedFirst = false)
    {
        var used = new HashSet<char>(); var keys = new char[labels.Count];
        if (fixedFirst)
            for (var i = 0; i < labels.Count; i++)
                if (FixedKeys.TryGetValue(labels[i], out var f) && used.Add(f)) keys[i] = f;
        for (var i = 0; i < labels.Count; i++)
        {
            if (keys[i] != '\0') continue;
            foreach (var ch in labels[i])
            {
                var u = char.ToUpperInvariant(ch);
                if (char.IsLetterOrDigit(u) && u < 128 && used.Add(u)) { keys[i] = u; break; } // nur A–Z/0–9: Umlaute liegen auf ausländischen Tastaturen woanders
            }
        }
        return [.. keys];
    }

    /// <summary>Beschriftung mit Unterstrich-Markierung für Avalonia („_Datei“); ein vorhandener Unterstrich im Text wird verdoppelt.</summary>
    public static string WithAccess(string label, char key)
    {
        var text = label.Replace("_", "__");
        if (key == '\0') return text;
        var at = text.IndexOf(key.ToString(), StringComparison.OrdinalIgnoreCase);
        return at < 0 ? text : text.Insert(at, "_");
    }

    /// <summary>Kürzel in die Bestandteile zerlegen: („Strg+Umschalt+S“) → (Strg, Umschalt, Alt, Taste „S“). Leere oder unbekannte Schreibweise: null.</summary>
    public static (bool Ctrl, bool Shift, bool Alt, string Key)? Parse(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return null;
        var parts = shortcut.Split('+'); bool c = false, s = false, a = false;
        foreach (var p in parts[..^1])
            switch (p) { case "Strg": c = true; break; case "Umschalt": s = true; break; case "Alt": a = true; break; default: return null; }
        var k = parts[^1];
        return k.Length == 0 ? null : (c, s, a, k);
    }

    /// <summary>Alle Einträge mit Kürzel (auch in Untermenüs).</summary>
    public static IEnumerable<(string Menu, MenuCmd Cmd)> WithShortcuts(IEnumerable<MenuTop> menus)
    {
        foreach (var m in menus) foreach (var c in Flat(m.Items)) if (c.Shortcut.Length > 0) yield return (m.Title, c);
    }

    public static IEnumerable<MenuCmd> Flat(IEnumerable<MenuCmd> items)
    {
        foreach (var i in items)
        {
            if (i.IsSeparator) continue;
            yield return i;
            if (i.Sub is { } s) foreach (var x in Flat(s)) yield return x;
        }
    }

    /// <summary>Konflikte einer Menüleiste: dasselbe Kürzel für zwei verschiedene Einträge, ein Kürzel, das Trendo schon für etwas anderes nutzt
    /// (<paramref name="globalOk"/>: Einträge, die genau die globale Funktion auslösen), zwei Menüs mit demselben unterstrichenen Buchstaben.</summary>
    public static List<string> Conflicts(IReadOnlyList<MenuTop> menus, IReadOnlyCollection<string>? globalOk = null)
    {
        var bad = new List<string>();
        foreach (var g in WithShortcuts(menus).GroupBy(x => x.Cmd.Shortcut).Where(g => g.Select(x => x.Cmd.Label).Distinct().Count() > 1))
            bad.Add($"{g.Key}: {string.Join(" / ", g.Select(x => x.Menu + " › " + x.Cmd.Label))}");
        foreach (var (menu, c) in WithShortcuts(menus))
            if (Global.TryGetValue(c.Shortcut, out var what) && !GlobalEntries.Contains(c.Id) && !(globalOk?.Contains(c.Id) ?? false)) bad.Add($"{c.Shortcut} ist in Trendo „{what}“ – {menu} › {c.Label}");
        var keys = AccessKeys(menus.Select(m => m.Title).ToList(), true);
        foreach (var g in keys.Select((k, i) => (k, menus[i].Title)).Where(x => x.k != '\0').GroupBy(x => x.k).Where(g => g.Count() > 1))
            bad.Add($"Alt+{g.Key}: {string.Join(" / ", g.Select(x => x.Title))}");
        foreach (var m in menus)
        {
            var ik = AccessKeys(m.Items.Where(i => !i.IsSeparator).Select(i => i.Label).ToList());
            // innerhalb eines Menüs darf ein Buchstabe nur einmal unterstrichen sein (sonst springt die Taste nur zwischen den Einträgen)
            foreach (var g in ik.Where(k => k != '\0').GroupBy(k => k).Where(g => g.Count() > 1)) bad.Add($"{m.Title}: Buchstabe {g.Key} doppelt");
        }
        return bad;
    }
}
