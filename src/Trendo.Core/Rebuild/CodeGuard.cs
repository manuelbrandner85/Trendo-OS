namespace Trendo.Core.Rebuild;

public sealed record GenFile(string RelativePath, string Content);

/// <summary>KI-Code darf nur die bestätigten Dateien ändern und keine Werkzeuge nach außen öffnen.</summary>
public static class CodeGuard
{
    public static readonly string[] Allowed =
    [
        "Kern/AppLogic.cs", "App/AppView.cs", "App/MainWindow.cs"
    ];

    static readonly string[] Banned =
    [
        "Process", "DllImport", "HttpClient", "WebClient", "TcpClient", "Socket", "Assembly.Load",
        "powershell", "cmd.exe", "/bin/sh", "Reflection.Emit", "Microsoft.Win32", "unsafe", "System.Diagnostics"
    ];

    public static IReadOnlyList<string> Violations(string? relativePath, string? code)
    {
        var hits = new List<string>();
        var path = (relativePath ?? "").Replace('\\', '/');
        if (!Allowed.Contains(path)) hits.Add("Pfad " + path);
        var text = code ?? "";
        foreach (var b in Banned)
            if (text.Contains(b, StringComparison.Ordinal)) hits.Add(b);
        foreach (var mark in OwnName.Mentioned(text))
            hits.Add("Marke " + mark);
        if (Untrusted.IsInstruction(text) && text.Contains("rm -rf", StringComparison.OrdinalIgnoreCase))
            hits.Add("Befehl");
        return hits;
    }

    public static bool UseTemplate(string? reply) =>
        reply is not null && reply.Contains("\"useTemplate\"", StringComparison.OrdinalIgnoreCase) && reply.Contains("true", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<GenFile>? ParseFiles(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply) || UseTemplate(reply)) return null;
        var a = reply.IndexOf('{');
        var b = reply.LastIndexOf('}');
        if (a < 0 || b <= a) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(reply[a..(b + 1)]);
            if (!doc.RootElement.TryGetProperty("files", out var files) || files.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var list = new List<GenFile>();
            foreach (var el in files.EnumerateArray())
            {
                var path = el.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                var content = el.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                list.Add(new GenFile(path.Replace('\\', '/'), content));
            }
            return list;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
