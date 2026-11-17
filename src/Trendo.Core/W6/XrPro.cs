namespace Trendo.Core;

/// <summary>„3D-Modelle ansehen“ (Kennung SPEC-04): Vorschau aus Augenhöhe – so, wie man einen Gegenstand im Raum stehend sieht (Architektur, Einrichtung).
/// T-0722 (Nutzerentscheidung 16.11.2026): die Brillen-Teile sind entfernt – Texturgrenze je Brille, Brillen-Ansicht (Sichtfeld und Augenabstand je Gerät)
/// und „Für die Brille reparieren“ (Blender-Skript).</summary>
public static class XrPro
{
    /// <summary>Augenhöhe stehend (cm) und Abstand zum Objekt für die Vorschau.</summary>
    public const double EyeCm = 160, ViewDistanceCm = 200;
}
