namespace Trendo.Core;

/// <summary>Auswahl des lokalen KI-Modells. Gespeichert in &lt;DataRoot&gt;/ki-model.txt; ohne Auswahl: 3B, wenn vorhanden und genug RAM.</summary>
public static class KiModel
{
    public const long RecommendLargeFromBytes = 15L * 1024 * 1024 * 1024; // 16-GB-Geräte melden etwas weniger

    public static bool RecommendLarge(long totalRamBytes) => totalRamBytes >= RecommendLargeFromBytes;

    public static string Current(string dataRoot, long totalRamBytes, Func<string, bool> present)
    {
        var f = Path.Combine(dataRoot, "ki-model.txt");
        if (File.Exists(f))
        {
            var m = File.ReadAllText(f).Trim();
            if ((m == DependencyCatalog.LocalModel || m == DependencyCatalog.LargeModel) && present(m)) return m;
        }
        return RecommendLarge(totalRamBytes) && present(DependencyCatalog.LargeModel) ? DependencyCatalog.LargeModel : DependencyCatalog.LocalModel;
    }

    public static void Select(string dataRoot, string model, AuditLog audit)
    {
        if (model != DependencyCatalog.LocalModel && model != DependencyCatalog.LargeModel) throw new ArgumentException("unbekanntes Modell");
        File.WriteAllText(Path.Combine(dataRoot, "ki-model.txt"), model);
        audit.Append("owner", "ki.model_selected", model);
    }
}
