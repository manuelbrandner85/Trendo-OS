using System.Text.Json;

namespace Trendo.Core;

/// <summary>Trendo Musik V4 (wie Suno, freigegeben 07.11.2026): Arbeitsbereiche (jedes Lied in genau einem), Playlists und Alben (Lieder in fester Reihenfolge,
/// ein Lied kann in mehreren sein). Gespeichert in „musik/sammlungen.json“ neben der Liederliste. Löschen einer Sammlung löscht keine Lieder.</summary>
public sealed class MusicCollections(string dataRoot)
{
    public const string Workspace = "arbeitsbereich", Playlist = "playlist", Album = "album";
    public const string DefaultWorkspace = "Mein Arbeitsbereich";
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public sealed class Collection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Kind { get; set; } = Playlist;
        public string Name { get; set; } = "";
        public List<string> SongIds { get; set; } = [];
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }

    string FilePath => Path.Combine(dataRoot, "musik", "sammlungen.json");

    public List<Collection> All()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Collection>>(File.ReadAllText(FilePath), Opt) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    void Save(List<Collection> l)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".neu"; File.WriteAllText(tmp, JsonSerializer.Serialize(l, Opt)); SafeFile.Move(tmp, FilePath);
    }

    public List<Collection> OfKind(string kind) => [.. All().Where(c => c.Kind == kind).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)];
    public Collection? Get(string id) => All().FirstOrDefault(c => c.Id == id);

    public Collection Create(string kind, string name)
    {
        if (kind is not (Workspace or Playlist or Album)) throw new ArgumentException("Unbekannte Sammlung: " + kind);
        var n = name.Trim(); if (n.Length == 0) throw new ArgumentException("Bitte einen Namen angeben.");
        if (n.Length > 60) n = n[..60];
        var l = All();
        if (l.Any(c => c.Kind == kind && c.Name.Equals(n, StringComparison.CurrentCultureIgnoreCase)) || (kind == Workspace && n.Equals(DefaultWorkspace, StringComparison.CurrentCultureIgnoreCase)))
            throw new ArgumentException($"„{n}“ gibt es schon.");
        var c = new Collection { Kind = kind, Name = n }; l.Add(c); Save(l); return c;
    }

    public void Rename(string id, string name) { var n = name.Trim(); if (n.Length == 0) return; Change(id, c => c.Name = n.Length > 60 ? n[..60] : n); }
    public void Delete(string id) { var l = All(); l.RemoveAll(c => c.Id == id); Save(l); }

    void Change(string id, Action<Collection> a) { var l = All(); var c = l.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diese Sammlung gibt es nicht mehr."); a(c); Save(l); }

    /// <summary>Lied in eine Playlist oder ein Album (ans Ende, doppelt nicht).</summary>
    public void AddSong(string id, string songId) => Change(id, c => { if (!c.SongIds.Contains(songId)) c.SongIds.Add(songId); });
    public void RemoveSong(string id, string songId) => Change(id, c => c.SongIds.Remove(songId));

    /// <summary>Reihenfolge ändern: Lied um <paramref name="delta"/> Plätze verschieben.</summary>
    public void Move(string id, string songId, int delta) => Change(id, c =>
    {
        var i = c.SongIds.IndexOf(songId); if (i < 0) return;
        var j = Math.Clamp(i + delta, 0, c.SongIds.Count - 1); c.SongIds.RemoveAt(i); c.SongIds.Insert(j, songId);
    });

    /// <summary>Lied in einen Arbeitsbereich verschieben (aus allen anderen heraus); null = „Mein Arbeitsbereich“.</summary>
    public void MoveToWorkspace(string songId, string? workspaceId)
    {
        var l = All();
        foreach (var w in l.Where(c => c.Kind == Workspace)) w.SongIds.Remove(songId);
        if (workspaceId is not null) (l.FirstOrDefault(c => c.Id == workspaceId && c.Kind == Workspace) ?? throw new InvalidOperationException("Diesen Arbeitsbereich gibt es nicht mehr.")).SongIds.Add(songId);
        Save(l);
    }

    public string? WorkspaceOf(string songId) => All().FirstOrDefault(c => c.Kind == Workspace && c.SongIds.Contains(songId))?.Id;
}

/// <summary>V4: Filter, Sortierung und Seiten der Liederliste (wie Suno: Schnellfilter, Sortierung, Seitenweise).</summary>
public static class SongQuery
{
    public static readonly (string Id, string Label)[] Filters = [("alle", "Alle"), ("favoriten", "Favoriten"), ("gesang", "Mit Gesang"), ("instrumental", "Instrumental"),
        ("eigene", "Eigene Aufnahmen"), ("fassungen", "Fassungen (Cover, Abschnitt, länger)"), ("spuren", "Spuren"), ("stimme", "In meiner Stimme")];
    public static readonly (string Id, string Label)[] Sorts = [("neu", "Neueste zuerst"), ("alt", "Älteste zuerst"), ("titel", "Titel A–Z"), ("laenge", "Längste zuerst"), ("fav", "Favoriten zuerst"), ("geschmack", "Für dich (Mein Geschmack)")];
    public const int PageSize = 30;

    static bool Is(MusicSong s, string filter) => filter switch
    {
        "favoriten" => s.Favorite,
        "gesang" => s.Lyrics.Trim() != "[Instrumental]" && s.Kind is not ("begleitung" or "beat" or "schlagzeug" or "bass" or "rest"),
        "instrumental" => s.Lyrics.Trim() == "[Instrumental]" || s.Kind is "begleitung" or "beat",
        "eigene" => s.Kind is "eigen" or "aufnahme" or "mischung",
        "fassungen" => s.Kind is "cover" or "abschnitt" or "laenger",
        "spuren" => s.Kind is "gesang" or "begleitung" or "schlagzeug" or "bass" or "rest",
        "stimme" => s.Kind == "aufnahme",
        _ => true,
    };

    /// <summary>Lieder für die Ansicht: Arbeitsbereich (null = „Mein Arbeitsbereich“ = alle ohne eigenen Bereich, „*“ = alle) oder Sammlung, Filter, Suche, Sortierung, Seite (ab 0).</summary>
    public static (List<MusicSong> Page, int Total, int Pages) Run(IReadOnlyList<MusicSong> visible, MusicCollections col, string? workspace, string? collectionId, string filter, string sort, string search, int page)
    {
        var all = col.All();
        IEnumerable<MusicSong> q = visible;
        if (collectionId is not null && all.FirstOrDefault(c => c.Id == collectionId) is { } c && c.Kind != MusicCollections.Workspace)
        {
            var order = c.SongIds; q = order.Select(id => visible.FirstOrDefault(s => s.Id == id)).OfType<MusicSong>(); // Reihenfolge der Playlist/des Albums
        }
        else if (workspace != "*")
        {
            var inSome = all.Where(w => w.Kind == MusicCollections.Workspace).SelectMany(w => w.SongIds).ToHashSet();
            q = workspace is null ? q.Where(s => !inSome.Contains(s.Id)) : q.Where(s => all.FirstOrDefault(w => w.Id == workspace)?.SongIds.Contains(s.Id) == true);
        }
        q = q.Where(s => Is(s, filter));
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => s.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || s.Style.Contains(search, StringComparison.OrdinalIgnoreCase) || s.Lyrics.Contains(search, StringComparison.OrdinalIgnoreCase));
        var taste = sort == "geschmack" ? MusicTaste.Profile(visible) : []; // V13
        if (collectionId is null || sort != "neu" || all.FirstOrDefault(x => x.Id == collectionId)?.Kind == MusicCollections.Workspace)
            q = sort switch
            {
                "alt" => q.OrderBy(s => s.Created), "titel" => q.OrderBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase), "laenge" => q.OrderByDescending(s => s.Seconds),
                "fav" => q.OrderByDescending(s => s.Favorite).ThenByDescending(s => s.Created),
                "geschmack" => q.OrderByDescending(s => MusicTaste.Score(s, taste)).ThenByDescending(s => s.Created), _ => q.OrderByDescending(s => s.Created),
            };
        var list = q.ToList(); var pages = Math.Max(1, (list.Count + PageSize - 1) / PageSize); var p = Math.Clamp(page, 0, pages - 1);
        return ([.. list.Skip(p * PageSize).Take(PageSize)], list.Count, pages);
    }
}
