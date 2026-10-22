using System.Globalization;

namespace Trendo.Core.PromptAction;

/// <summary>CORE-05: OBJ und eingebaute Vorschau. Blender wird nicht gestartet.</summary>
public sealed class ModelPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-05";
    public string ActionId => "modell.bauen";
    public string Label => "Modell bauen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Modell_schreibt_obj_und_vorschau";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "form", "breite", "tiefe", "hoehe", "farbe"];
    public IReadOnlyList<string> OutputKinds => ["obj", "mtl", "png", "json"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = ModelFields.Read(prompt, known);
        return ModelFields.WantsBlender(fields) ? null : new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => ModelFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => ModelFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => ModelFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Blender wurde nicht gestartet."], null);
}

/// <summary>Kein Blend-Lauf aus dem Auftrag. Auch „Blender: ja“ startet nichts.</summary>
public sealed class BlenderPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-05";
    public string ActionId => "modell.blender";
    public string Label => "In Blender öffnen";
    public SideEffectClass Effect => SideEffectClass.Machine;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Modell_startet_blender_nicht";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = ModelFields.Read(prompt, known);
        return ModelFields.WantsBlender(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        new(false, [], [], null, ["Blender wurde nicht gestartet. Es gibt keine Blend-Datei."], ["Blender ist aus dem Auftrag nicht angeschlossen."], null);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Blender wurde nicht gestartet."], null);
}

static class ModelFields
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsBlender(IReadOnlyDictionary<string, string> fields) => Yes(fields, "blender");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        void Need(string field, string label)
        {
            if (Text(parameters, field).Length == 0)
                list.Add(new(field, field, label + " fehlt. Schreib es als „" + label + ": …“.", true));
        }
        Need("name", "Name");
        Need("form", "Form");
        Need("breite", "Breite");
        Need("tiefe", "Tiefe");
        Need("hoehe", "Hoehe");
        Need("farbe", "Farbe");
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!TryPart(parameters, out _, out var error))
            return [new(error.Field, error.Message, true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        if (!TryPart(call.Parameters, out var part, out var error))
            return new(false, [], [], null, [], [error.Message], null);
        var scene = new Scene3D { Name = Text(call.Parameters, "name"), Parts = [part] };
        var folder = "3d/v" + call.Version;
        var stem = Scene3D.Slug(scene.Name);
        var jsonRel = folder + "/" + stem + ".json";
        var objRel = folder + "/" + stem + ".obj";
        var pngRel = folder + "/" + stem + ".png";
        var json = call.Projects.ResolveInProject(call.ProjectId, jsonRel);
        var obj = call.Projects.ResolveInProject(call.ProjectId, objRel);
        Directory.CreateDirectory(Path.GetDirectoryName(json)!);
        File.WriteAllText(json, scene.ToJson());
        var mesh = Mesh3D.FromScene(scene);
        if (mesh.Triangles == 0)
            return new(false, [], [], null, [], ["Aus der Form ist kein Netz entstanden."], null);
        Mesh3D.WriteObj(mesh, obj);
        var png = Mesh3D.RenderPng(scene, "front", 320, 240);
        if (png is null || png.Length < 8)
            return new(false, [], [], null, [], ["Die Vorschau konnte nicht gezeichnet werden."], null);
        File.WriteAllBytes(call.Projects.ResolveInProject(call.ProjectId, pngRel), png);

        var loaded = Scene3D.FromJson(File.ReadAllText(json));
        var jsonOk = loaded.Name == scene.Name && loaded.Parts.Count == 1
            && loaded.Parts[0].Shape == part.Shape
            && Math.Abs(loaded.Parts[0].Sx - part.Sx) < 0.01
            && Math.Abs(loaded.Parts[0].Sy - part.Sy) < 0.01
            && Math.Abs(loaded.Parts[0].Sz - part.Sz) < 0.01
            && string.Equals(loaded.Parts[0].Color, part.Color, StringComparison.OrdinalIgnoreCase);
        var imported = Mesh3D.Import(obj);
        var sizeOk = Math.Abs(imported.Sx - part.Sx) <= Math.Max(0.5, part.Sx * 0.05);
        var pngOk = png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47;
        var preview = part.Line() + "\nOBJ und Vorschau liegen im Projekt. Blender ist nicht gelaufen.";
        var mtlRel = folder + "/" + stem + ".mtl";
        return new(true,
            [
                new ProducedFile(jsonRel, "json", true),
                new ProducedFile(objRel, "obj", true),
                new ProducedFile(mtlRel, "mtl", true),
                new ProducedFile(pngRel, "png", true)
            ],
            [
                new QaResult("modell.json", jsonOk, jsonOk ? "Form und Maße stehen in der Szenendatei." : "Die Szenendatei passt nicht zum Auftrag.", "cm", part.Line(), "Scene3D.FromJson"),
                new QaResult("modell.obj", sizeOk, sizeOk ? "Die OBJ-Datei hat die bestellte Breite." : "Die Breite in der OBJ-Datei weicht ab.", "cm", imported.Sx.ToString("0.##", De), "Mesh3D.Import"),
                new QaResult("modell.png", pngOk, pngOk ? "Die Vorschau ist eine PNG-Datei von Trendo, nicht von Blender." : "Die Vorschau ist keine PNG-Datei.", null, pngRel, "Mesh3D.RenderPng")
            ],
            preview,
            ["Blender ist nicht gelaufen. Die Vorschau ist die eingebaute Zeichnung, kein Foto."], [], null);
    }

    static bool TryPart(IReadOnlyDictionary<string, string> parameters, out Part3D part, out FieldError error)
    {
        part = new Part3D();
        error = default;
        var shape = Shape(Text(parameters, "form"));
        if (shape is null) { error = new("form", "Die Form kenne ich nicht. Zum Beispiel Würfel, Kugel oder Zylinder."); return false; }
        if (!TrySize(Text(parameters, "breite"), out var sx)) { error = new("breite", "Die Breite muss größer als 0 und höchstens 500 cm sein."); return false; }
        if (!TrySize(Text(parameters, "tiefe"), out var sy)) { error = new("tiefe", "Die Tiefe muss größer als 0 und höchstens 500 cm sein."); return false; }
        if (!TrySize(Text(parameters, "hoehe"), out var sz)) { error = new("hoehe", "Die Höhe muss größer als 0 und höchstens 500 cm sein."); return false; }
        var color = Color(Text(parameters, "farbe"));
        if (color is null) { error = new("farbe", "Die Farbe kenne ich nicht. Zum Beispiel Blau oder #2F86F6."); return false; }
        part = new Part3D { Name = Text(parameters, "name"), Shape = shape, Sx = sx, Sy = sy, Sz = sz, Color = color };
        return true;
    }

    static bool TrySize(string raw, out double value)
    {
        value = 0;
        try
        {
            var n = Finance.ParseAmount(raw);
            if (n <= 0 || n > 500) return false;
            value = (double)n;
            return true;
        }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }

    static string? Shape(string raw)
    {
        var n = PromptFields.Norm(raw);
        return n switch
        {
            "wuerfel" or "cube" or "quader" => "cube",
            "kugel" or "sphere" => "sphere",
            "zylinder" or "cylinder" => "cylinder",
            "kegel" or "cone" => "cone",
            "torus" or "ring" => "torus",
            "ebene" or "plane" => "plane",
            _ => null
        };
    }

    static string? Color(string raw)
    {
        if (Scene3D.ValidColor(raw.Trim())) return raw.Trim();
        var hit = Scene3D.Colors.FirstOrDefault(c => string.Equals(c.Name, raw.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(PromptFields.Norm(c.Name), PromptFields.Norm(raw), StringComparison.OrdinalIgnoreCase));
        return hit.Hex is { Length: > 0 } ? hit.Hex : null;
    }

    static bool Yes(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    static string Text(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : "";

    readonly record struct FieldError(string Field, string Message);
}
