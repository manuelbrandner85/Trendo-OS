using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 7 – CORE-06: alle Level eines Plattform-Spiels als Tiled-Karten (.tmj) mit Kachelbild und Welt-Datei ausgeben –
/// zum Weiterbauen in Tiled, Godot (Importer), Phaser, Defold oder LÖVE.</summary>
public partial class MainWindow
{
    static readonly Regex FirstLevelKey = new(@"^\d+,\d+:", RegexOptions.Compiled);

    /// <summary>Eigenschaften eines Levels ohne Level-Vorsilbe („x,y:name“).</summary>
    static Dictionary<string, T> LevelKeys<T>(IReadOnlyDictionary<string, T> all, int i)
    {
        var prefix = GameKeyPrefix(i);
        return all.Where(kv => i == 0 ? FirstLevelKey.IsMatch(kv.Key) : kv.Key.StartsWith(prefix, StringComparison.Ordinal) && FirstLevelKey.IsMatch(kv.Key[prefix.Length..]))
                  .ToDictionary(kv => kv.Key[prefix.Length..], kv => kv.Value);
    }

    /// <summary>Ausgeben (öffentlich für Tests). Rückgabe: Ordner mit den Karten.</summary>
    public string GameExportTiled(ProgramDocument d)
    {
        var props = GameProps(d); var texts = GameDesign.Texts(d.Data);
        var levels = Enumerable.Range(0, GameLevelCount(d))
            .Select(i => new TiledExport.Level(GameLevelName(d, i), GameLevelRows(d, i), GameDecorRows(d, i), LevelKeys(props, i), LevelKeys(texts, i))).ToList();
        var dir = Path.Combine(WorkFolder("Spiele"), "Tiled", DevStudio.SafeName(d.Text("title", "Spiel")));
        var files = TiledExport.Write(dir, levels);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.export.tiled", $"{levels.Count} Level");
        _progMsg[GK] = $"{levels.Count} Level als Tiled-Karten gespeichert ({files.Count} Dateien in „{Path.GetFileName(dir)}“) – öffnen mit Tiled, in Godot über einen Tiled-Importer, auch für Phaser, Defold und LÖVE.";
        return dir;
    }
}
