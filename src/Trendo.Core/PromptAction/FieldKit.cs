namespace Trendo.Core.PromptAction;

/// <summary>Gemeinsame Feld- und Dateihilfe für lokale Fachadapter. Schreibt nur in das Projekt.</summary>
static class FieldKit
{
    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static string Text(IReadOnlyDictionary<string, string> parameters, params string[] keys)
    {
        foreach (var key in keys)
            if (parameters.TryGetValue(key, out var value) && value.Trim().Length > 0) return value.Trim();
        return "";
    }

    public static bool Has(IReadOnlyDictionary<string, string> parameters, string key) => Text(parameters, key).Length > 0;

    public static bool Yes(IReadOnlyDictionary<string, string> parameters, string key)
    {
        var text = Text(parameters, key).ToLowerInvariant();
        return text is "ja" or "yes" or "true" or "1";
    }

    public static bool TryNum(IReadOnlyDictionary<string, string> parameters, string key, out double value)
    {
        value = 0;
        var text = Text(parameters, key);
        if (text.Length == 0) return false;
        try { value = Fach.Num(text, key); return true; }
        catch (FormatException) { return false; }
    }

    public static double[] Numbers(string raw, string what)
    {
        var text = raw.Replace("\r", "").Trim();
        if (text.Length == 0) throw new FormatException("Bei „" + what + "“ fehlt die Zahl.");
        string[] parts;
        if (text.Contains(';') || text.Contains('\n'))
            parts = text.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        else if (text.Contains(", "))
            parts = text.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        else
            parts = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new FormatException("Bei „" + what + "“ fehlt die Zahl.");
        var list = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++) list[i] = Fach.Num(parts[i], what);
        return list;
    }

    public static AdapterOutcome Save(AdapterCall call, string relative, string body, string preview, QaResult qa, IReadOnlyList<string>? warnings = null)
    {
        call.Projects.WriteFile(call.ProjectId, relative, body);
        var read = call.Projects.ReadFile(call.ProjectId, relative);
        var passed = qa.Passed && read == body;
        var detail = passed ? qa.Detail : "Die Datei stimmt nach dem Lesen nicht.";
        var check = new QaResult(qa.CheckId, passed, detail, qa.Units, qa.Inputs, qa.ToolVersion);
        return new(true, [new ProducedFile(relative, Extension(relative), true)], [check], preview, warnings ?? [], passed ? [] : [detail], null);
    }

    public static AdapterOutcome Fail(string message) => new(true, [], [], null, [], [message], null);

    public static AdapterOutcome Blocked(string message) => new(false, [], [], null, [], [message], null);

    public static string DataRoot(AdapterCall call)
    {
        var files = call.Projects.ProjectFilesRoot(call.ProjectId);
        return Path.GetFullPath(Path.Combine(files, "..", "..", ".."));
    }

    public static string Slot(string folder, int version, string name) => folder + "/v" + version + "/" + name;

    static string Extension(string relative)
    {
        var ext = Path.GetExtension(relative);
        return ext.Length > 1 ? ext[1..] : "txt";
    }
}
