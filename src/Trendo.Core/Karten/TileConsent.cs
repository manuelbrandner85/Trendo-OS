using System.Text.Json;

namespace Trendo.Core.Karten;

/// <summary>T-0726: Kartenbilder (Kacheln) von OpenStreetMap lädt Trendo nur nach einer einmaligen Zustimmung über das Freigabe-Tor
/// (Wirkung <see cref="Action"/>, Ziel <see cref="Target"/>). Die Zustimmung bleibt als Einstellung im Datenordner der Person (wie „Freie Modelle
/// automatisch laden“) und lässt sich in der Karte jederzeit wieder ausschalten. Ohne Zustimmung: Zwischenspeicher, Offline-Paket oder Gitter.</summary>
public static class TileConsent
{
    public const string Action = "karten.kacheln.internet";
    public const string Target = "tile.openstreetmap.org";

    sealed record Stored(int Version, bool An, DateTimeOffset Seit, string Freigabe);

    static string FileOf(string dataRoot) => Path.Combine(dataRoot, "karten", "internet-kacheln.json");

    /// <summary>Hat die Person zugestimmt? Unlesbar oder fehlend = nein.</summary>
    public static bool Allowed(string dataRoot)
    {
        try { return File.Exists(FileOf(dataRoot)) && JsonSerializer.Deserialize<Stored>(File.ReadAllText(FileOf(dataRoot))) is { Version: 1, An: true }; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    /// <summary>Zustimmung speichern – nur innerhalb von <c>ApprovalGate.Execute</c> mit der erteilten Freigabe aufrufen (Id steht in der Datei).</summary>
    public static void Grant(string dataRoot, string approvalId) =>
        AtomicFile.WriteAllText(FileOf(dataRoot), JsonSerializer.Serialize(new Stored(1, true, DateTimeOffset.Now, approvalId)));

    /// <summary>Zustimmung zurücknehmen (braucht keine Freigabe – es wird danach nur weniger geladen).</summary>
    public static void Revoke(string dataRoot)
    {
        try { if (File.Exists(FileOf(dataRoot))) File.Delete(FileOf(dataRoot)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Text der Rückfrage: was geladen wird, wohin, was der Dienst dabei erfährt, wie lange es gilt.</summary>
    public const string Text = "Wirkung: Beim Ansehen der Karte lädt Trendo Kartenbilder (Kacheln) von OpenStreetMap aus dem Internet.\n"
        + "Ziel: tile.openstreetmap.org – der Dienst erfährt dabei deine IP-Adresse und welchen Kartenausschnitt du ansiehst.\n"
        + "Geladene Kacheln bleiben 30 Tage auf diesem PC, höchstens zwei gleichzeitig, nur was du ansiehst.\n\n"
        + "Die Erlaubnis gilt, bis du sie in der Karte wieder ausschaltest. Ohne Erlaubnis zeigt die Karte den Zwischenspeicher, ein Offline-Kartenpaket oder ein Gitter.";
}
