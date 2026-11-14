using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>K7 im Studio „Webseite bauen“: Web-Apps mit Daten, lokal zuerst. Die Bausteine „Datenliste“ und „Anmeldung“ speichern in SQLite im Browser der Besucher
/// (Bündel assets/daten.js, sql.js, MIT). Optional zusätzlich im eigenen Supabase-Projekt – nur mit dessen Adresse und dem öffentlichen Schlüssel; geheime Schlüssel
/// werden abgelehnt und im Code gemeldet. Einstellungen in daten.trendo.json.</summary>
public static partial class WebDesignData
{
    public const string FileName = "daten.trendo.json", Bundle = "daten.js";
    public static readonly string[] DataBlocks = ["Datenliste", "Anmeldung"];
    /// <summary>Feldnamen, die die Datenliste selbst belegt.</summary>
    public static readonly HashSet<string> ReservedFields = ["id", "erstellt", "nutzer"];

    [GeneratedRegex(@"^https://[a-z0-9-]{3,63}\.supabase\.co$")] private static partial Regex SupabaseUrl();
    [GeneratedRegex(@"sb_secret_[A-Za-z0-9_-]{10,}")] private static partial Regex SecretKey();
    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{8,}\.(eyJ[A-Za-z0-9_-]{8,})\.[A-Za-z0-9_-]{8,}")] private static partial Regex Jwt();
    [GeneratedRegex("^[a-z][a-z0-9_]{0,29}$")] private static partial Regex TableName();

    public sealed record Supabase(string Url, string Schluessel);

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    public static Supabase? Load(string dir)
    {
        try
        {
            var f = Path.Combine(dir, FileName);
            if (File.Exists(f) && JsonNode.Parse(File.ReadAllText(f))?["supabase"] is JsonObject s && CheckSupabase(Str(s["url"]), Str(s["schluessel"])) is null) return new(Str(s["url"]), Str(s["schluessel"]));
        }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        return null;
    }

    /// <summary>Supabase nur mit eigenem Projekt: https://&lt;projekt&gt;.supabase.co und ein öffentlicher Schlüssel (sb_publishable_… oder JWT mit Rolle „anon“).
    /// Geheime Schlüssel (sb_secret_…, Rolle „service_role“) gehören nie in eine Website und werden abgelehnt.</summary>
    public static string? CheckSupabase(string url, string key)
    {
        if (!SupabaseUrl().IsMatch(url.Trim().TrimEnd('/'))) return "Die Projekt-Adresse bitte wie https://deinprojekt.supabase.co angeben.";
        var k = key.Trim();
        if (SecretKey().IsMatch(k) || JwtRole(k) == "service_role") return "Das ist ein geheimer Schlüssel. In eine Website gehört nur der öffentliche Schlüssel (publishable/anon). Den geheimen Schlüssel bitte in Supabase neu erzeugen, falls er schon irgendwo stand.";
        if (k.StartsWith("sb_publishable_", StringComparison.Ordinal) && k.Length is > 20 and < 200 && k.All(c => char.IsLetterOrDigit(c) || c is '_' or '-')) return null;
        if (JwtRole(k) == "anon") return null;
        return "Bitte den öffentlichen Schlüssel aus Supabase (Project Settings › API) einfügen.";
    }

    static string? JwtRole(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || !parts[0].StartsWith("eyJ", StringComparison.Ordinal)) return null;
        try
        {
            var p = parts[1].Replace('-', '+').Replace('_', '/'); p = p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=');
            return JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(p)))?["role"]?.ToString();
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException) { return null; }
    }

    public static string? SetSupabase(string dir, string url, string key)
    {
        if (CheckSupabase(url, key) is { } err) return err;
        File.WriteAllText(Path.Combine(dir, FileName), new JsonObject { ["supabase"] = new JsonObject { ["url"] = url.Trim().TrimEnd('/'), ["schluessel"] = key.Trim() } }.ToJsonString());
        return null;
    }

    public static void RemoveSupabase(string dir) { var f = Path.Combine(dir, FileName); if (File.Exists(f)) File.Delete(f); }

    /// <summary>Geheime Schlüssel in einem Text (Code, Seitendaten, Export): Supabase sb_secret_… und JWTs mit Rolle service_role. Ausgabe maskiert.</summary>
    public static IReadOnlyList<string> SecretKeys(string text)
    {
        var res = new List<string>();
        foreach (Match m in SecretKey().Matches(text)) res.Add("Supabase-Geheimschlüssel " + SecurityCenter.Mask(m.Value));
        foreach (Match m in Jwt().Matches(text)) if (JwtRole(m.Value) == "service_role") res.Add("Supabase service_role-Schlüssel " + SecurityCenter.Mask(m.Value));
        return res;
    }

    /// <summary>K7: Geheimnisse in den Seitendaten melden (Texte, Links, Felder), bevor exportiert oder hochgeladen wird.</summary>
    public static IReadOnlyList<string> CheckSecrets(string name, string pageJson) =>
        SecurityCenter.ScanText(name, pageJson.Replace("\\n", "\n")).Select(s => $"{name}: mögliches Geheimnis ({s.Kind}: {s.Masked}). Geheimnisse gehören nie in eine Website.")
            .Concat(SecretKeys(pageJson).Select(k => $"{name}: {k}. Geheimnisse gehören nie in eine Website.")).ToList();

    /// <summary>Nutzt die Seite Daten-Bausteine? Dann kommt assets/daten.js in den Export.</summary>
    public static bool UsesData(JsonNode? n) => n switch
    {
        JsonObject o => DataBlocks.Contains(Str(o["type"])) || o.Any(kv => UsesData(kv.Value)),
        JsonArray a => a.Any(UsesData),
        _ => false,
    };

    /// <summary>Konfiguration für das Datenbündel in der Seite: eigener Schlüssel je Website (Datenbank auf dem Gerät), Supabase nur wenn eingerichtet.</summary>
    public static string ConfigScript(string dir)
    {
        var site = WebDesignSite.Load(dir);
        var o = new JsonObject { ["schluessel"] = "trendo-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(site.Basis.Length > 0 ? site.Basis : Path.GetFileName(dir))))[..16].ToLowerInvariant() };
        if (Load(dir) is { } s) o["supabase"] = new JsonObject { ["url"] = s.Url, ["schluessel"] = s.Schluessel };
        return "window.TRENDO_DATEN = " + o.ToJsonString().Replace("</", "<\\/") + ";";
    }

    /// <summary>SQL für das eigene Supabase-Projekt: Tabellen der Datenlisten mit Zeilenschutz (RLS): Besucher dürfen nur einfügen. Gelesen wird aus Supabase nichts (die Liste zeigt die Einträge auf dem Gerät), darum gibt es keine öffentliche Leseregel.</summary>
    public static string SupabaseSql(IEnumerable<JsonObject> datenlisten)
    {
        var sb = new StringBuilder("-- Für den SQL-Editor deines Supabase-Projekts. Trendo führt nichts aus.\n");
        foreach (var p in datenlisten)
        {
            var t = Str(p["tabelle"]); if (!TableName().IsMatch(t)) continue;
            var cols = (p["felder"] as JsonArray ?? []).OfType<JsonObject>().Select(f => Str(f["name"])).Where(n => TableName().IsMatch(n) && !ReservedFields.Contains(n)).Distinct().ToList();
            sb.Append($"create table if not exists public.{t} (id bigint generated always as identity primary key, erstellt timestamptz default now()");
            foreach (var c in cols) sb.Append($", {c} text check (length({c}) <= 5000)");
            sb.Append(");\n");
            sb.Append($"alter table public.{t} enable row level security;\n");
            sb.Append($"create policy \"{t}_einfuegen\" on public.{t} for insert to anon with check (true);\n");
            sb.Append($"-- Hinweis: Jeder mit dem öffentlichen Schlüssel kann Einträge anlegen. Gegen Massen-Einträge eine Ratenbegrenzung oder Captcha in Supabase einrichten.\n");
        }
        return sb.ToString();
    }
}
