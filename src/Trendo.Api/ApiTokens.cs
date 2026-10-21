using System.Security.Cryptography;
using System.Text;
using Trendo.Core;

namespace Trendo.Api;

/// <summary>Ein Zugriffstoken pro Rolle, gespeichert ausschließlich über ISecretStore (Windows: Credential Manager). Tokens werden nie geloggt.</summary>
public sealed class ApiTokens(ISecretStore secrets)
{
    private static string Key(Role r) => $"api-token-{r.ToString().ToLowerInvariant()}";

    /// <summary>Liefert das Token einer Rolle; erzeugt es beim ersten Aufruf (256 Bit Zufall).</summary>
    public string GetOrCreate(Role r)
    {
        var t = secrets.Get(Key(r));
        if (!string.IsNullOrEmpty(t)) return t;
        t = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        secrets.Set(Key(r), t);
        return t;
    }

    public void Rotate(Role r) => secrets.Delete(Key(r));

    /// <summary>Prüft ein Token in konstanter Zeit gegen alle Rollen.</summary>
    public Role? Resolve(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var given = Encoding.UTF8.GetBytes(token);
        Role? hit = null;
        foreach (var r in Enum.GetValues<Role>())
        {
            var stored = secrets.Get(Key(r));
            if (stored is null) continue;
            if (CryptographicOperations.FixedTimeEquals(given, Encoding.UTF8.GetBytes(stored))) hit = r;
        }
        return hit;
    }
}
