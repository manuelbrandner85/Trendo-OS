namespace Trendo.Core;

public sealed record HardwareInfo(long RamBytes, string? Gpu, long VramBytes);

/// <summary>Wählt das lokale KI-Modell nach Hardware. Nur freie Modelle; nichts wird ohne Zustimmung geladen.</summary>
public static class KiAdvisor
{
    const long G = 1024L * 1024 * 1024;
    public static (string Model, string Why) Choose(HardwareInfo h)
    {
        if (h.RamBytes >= 15 * G && (h.VramBytes >= 4 * G || h.RamBytes >= 24 * G))
            return (DependencyCatalog.LargeModel, $"{h.RamBytes / G} GB RAM{(h.Gpu is null ? "" : $", {h.Gpu}{(h.VramBytes > 0 ? $" ({h.VramBytes / G} GB)" : "")}")} – das größere Modell läuft flüssig und schreibt besseres Deutsch.");
        return (DependencyCatalog.LocalModel, $"{h.RamBytes / G} GB RAM – das kleine Modell ist auf diesem Gerät schneller.");
    }
}
