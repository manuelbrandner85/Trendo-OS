namespace Trendo.App;

/// <summary>„3D-Modelle ansehen“ (Kennung SPEC-04): Vorschau aus Augenhöhe an/aus – so, wie man einen Gegenstand im Raum stehend sieht.
/// T-0722 (Nutzerentscheidung 16.11.2026): Brillen-Ansicht (beide Augen), Texturgrenze je Brille und „Für die Brille reparieren“ sind entfernt.</summary>
public partial class MainWindow
{
    bool _xrEye;
    /// <summary>Vorschau aus Augenhöhe an/aus.</summary>
    public bool XrEye { get => _xrEye; set => _xrEye = value; }
}
