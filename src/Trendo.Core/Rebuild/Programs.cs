using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Trendo.Core.Rebuild;

public sealed class MyProgram
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PageKey { get; set; } = "";
    public string ProjectDir { get; set; } = "";
    public string Hint { get; set; } = "";
}

/// <summary>Eigene Apps im Startmenü unter „Meine Programme“.</summary>
public static class MyPrograms
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FileOf(string dataRoot) => Path.Combine(dataRoot, "meine-programme.json");

    public static List<MyProgram> Load(string dataRoot)
    {
        var file = FileOf(dataRoot);
        if (!File.Exists(file)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<MyProgram>>(File.ReadAllText(file), Json) ?? [];
        }
        catch (JsonException) { return []; }
    }

    public static void Save(string dataRoot, IReadOnlyList<MyProgram> programs)
    {
        Directory.CreateDirectory(dataRoot);
        var tmp = FileOf(dataRoot) + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(programs, Json));
        File.Move(tmp, FileOf(dataRoot), true);
    }

    public static MyProgram Upsert(string dataRoot, string name, string projectDir, string hint)
    {
        var id = DevStudio.SafeName(name).ToLowerInvariant();
        if (id.Length > 40) id = id[..40];
        var page = "mein-" + id;
        var list = Load(dataRoot);
        var existing = list.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new MyProgram { Id = id, PageKey = page };
            list.Add(existing);
        }
        existing.Name = name;
        existing.PageKey = page;
        existing.ProjectDir = projectDir;
        existing.Hint = hint ?? "";
        Save(dataRoot, list);
        return existing;
    }

    public static MyProgram? FindByPage(string dataRoot, string page) =>
        Load(dataRoot).FirstOrDefault(p => string.Equals(p.PageKey, page, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Installation nur nach eigenem Haken und nur unter Windows. Die Datei bleibt unsigniert.</summary>
public static class InstallGate
{
    public static string? Block(bool confirmed, bool windows, bool fileExists)
    {
        if (!fileExists) return "Es gibt noch keine Setup-Datei.";
        if (!confirmed) return "Erst den Haken setzen, dass du installieren willst.";
        if (!windows) return "Die Installation startet nur unter Windows. Windows fragt dann selbst nach. Die Datei ist nicht signiert.";
        return null;
    }
}

public interface ISetupLauncher
{
    void Start(string path);
}

public sealed class WindowsSetupLauncher : ISetupLauncher
{
    public void Start(string path)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
}

/// <summary>Lädt nur die selbst gebaute Bibliothek. Seiten aus dem Internet werden nie ausgeführt.</summary>
public static class RebuildHost
{
    public static LoadedLogic? TryLoad(string dll)
    {
        if (!File.Exists(dll)) return null;
        AssemblyLoadContext? alc = null;
        try
        {
            var bytes = File.ReadAllBytes(dll);
            if (bytes.Length < 64) return null;
            alc = new AssemblyLoadContext("trendo-rebuild-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            var asm = alc.LoadFromStream(new MemoryStream(bytes));
            var type = asm.GetType("RebuildApp.AppLogic");
            if (type is null)
            {
                alc.Unload();
                return null;
            }
            return new LoadedLogic(alc, type);
        }
        catch (Exception)
        {
            try { alc?.Unload(); } catch (Exception) { }
            return null;
        }
    }
}

public sealed class LoadedLogic
{
    private readonly AssemblyLoadContext _alc;
    private readonly Type _type;

    internal LoadedLogic(AssemblyLoadContext alc, Type type)
    {
        _alc = alc;
        _type = type;
        Title = ReadString("Title");
        Kind = ReadString("Kind");
        ButtonHeight = ReadInt("ButtonHeight");
        FontSize = ReadInt("FontSize");
        Dark = _type.GetField("Dark")?.GetValue(null) is true;
        Features = _type.GetField("Features")?.GetValue(null) as string[] ?? [];
        Notes = _type.GetField("Notes")?.GetValue(null) as string[] ?? [];
    }

    public string Title { get; }
    public string Kind { get; }
    public int ButtonHeight { get; }
    public int FontSize { get; }
    public bool Dark { get; }
    public string[] Features { get; }
    public string[] Notes { get; }
    public bool Has(string id) => Features.Contains(id, StringComparer.Ordinal);

    public void Save(string dir, string text) => _type.GetMethod("Save")!.Invoke(null, [dir, text]);
    public string Load(string dir) => _type.GetMethod("Load")!.Invoke(null, [dir]) as string ?? "";
    public int Count(string? text, string? term) => _type.GetMethod("Count")!.Invoke(null, [text, term]) is int n ? n : 0;
    public string Greet(string? name) => _type.GetMethod("Greet")!.Invoke(null, [name]) as string ?? "";

    public void Release()
    {
        try { _alc.Unload(); } catch (Exception) { }
    }

    string ReadString(string field) => _type.GetField(field)?.GetValue(null) as string ?? "";
    int ReadInt(string field) => _type.GetField(field)?.GetValue(null) is int n ? n : 0;
}
