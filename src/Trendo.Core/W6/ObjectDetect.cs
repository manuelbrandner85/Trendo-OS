using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 7 – CORE-09 „Kameras &amp; Roboter steuern“: Objekte im Kamerabild erkennen und zählen mit YOLOX-S
/// (Megvii, Apache-2.0) als ONNX-Modell über onnxruntime (MIT) in Trendos KI-Laufzeit. Das Modell kommt aus dem offiziellen GitHub-Release
/// und wird nur mit passender Prüfsumme übernommen. Alles läuft lokal – kein Bild verlässt den PC.</summary>
public static class ObjectDetect
{
    public const string ModelUrl = "https://github.com/Megvii-BaseDetection/YOLOX/releases/download/0.1.1rc0/yolox_s.onnx";
    public const string ModelSha256 = "c5c2d13e59ae883e6af3b45daea64af4833a4951c92d116ec270d9ddbe998063";
    public const long ModelBytes = 35_858_002;
    public const string ModelFile = "yolox_s.onnx";

    /// <summary>Die 80 COCO-Klassen auf Deutsch (Einzahl, Mehrzahl).</summary>
    public static readonly IReadOnlyDictionary<string, (string One, string Many)> German = new Dictionary<string, (string, string)>
    {
        ["person"] = ("Person", "Personen"), ["bicycle"] = ("Fahrrad", "Fahrräder"), ["car"] = ("Auto", "Autos"), ["motorcycle"] = ("Motorrad", "Motorräder"),
        ["airplane"] = ("Flugzeug", "Flugzeuge"), ["bus"] = ("Bus", "Busse"), ["train"] = ("Zug", "Züge"), ["truck"] = ("Lastwagen", "Lastwagen"), ["boat"] = ("Boot", "Boote"),
        ["traffic light"] = ("Ampel", "Ampeln"), ["fire hydrant"] = ("Hydrant", "Hydranten"), ["stop sign"] = ("Stoppschild", "Stoppschilder"), ["parking meter"] = ("Parkuhr", "Parkuhren"),
        ["bench"] = ("Bank", "Bänke"), ["bird"] = ("Vogel", "Vögel"), ["cat"] = ("Katze", "Katzen"), ["dog"] = ("Hund", "Hunde"), ["horse"] = ("Pferd", "Pferde"),
        ["sheep"] = ("Schaf", "Schafe"), ["cow"] = ("Kuh", "Kühe"), ["elephant"] = ("Elefant", "Elefanten"), ["bear"] = ("Bär", "Bären"), ["zebra"] = ("Zebra", "Zebras"),
        ["giraffe"] = ("Giraffe", "Giraffen"), ["backpack"] = ("Rucksack", "Rucksäcke"), ["umbrella"] = ("Schirm", "Schirme"), ["handbag"] = ("Handtasche", "Handtaschen"),
        ["tie"] = ("Krawatte", "Krawatten"), ["suitcase"] = ("Koffer", "Koffer"), ["frisbee"] = ("Frisbee", "Frisbees"), ["skis"] = ("Ski", "Ski"), ["snowboard"] = ("Snowboard", "Snowboards"),
        ["sports ball"] = ("Ball", "Bälle"), ["kite"] = ("Drachen", "Drachen"), ["baseball bat"] = ("Baseballschläger", "Baseballschläger"), ["baseball glove"] = ("Baseballhandschuh", "Baseballhandschuhe"),
        ["skateboard"] = ("Skateboard", "Skateboards"), ["surfboard"] = ("Surfbrett", "Surfbretter"), ["tennis racket"] = ("Tennisschläger", "Tennisschläger"), ["bottle"] = ("Flasche", "Flaschen"),
        ["wine glass"] = ("Weinglas", "Weingläser"), ["cup"] = ("Tasse", "Tassen"), ["fork"] = ("Gabel", "Gabeln"), ["knife"] = ("Messer", "Messer"), ["spoon"] = ("Löffel", "Löffel"),
        ["bowl"] = ("Schüssel", "Schüsseln"), ["banana"] = ("Banane", "Bananen"), ["apple"] = ("Apfel", "Äpfel"), ["sandwich"] = ("Sandwich", "Sandwiches"), ["orange"] = ("Orange", "Orangen"),
        ["broccoli"] = ("Brokkoli", "Brokkoli"), ["carrot"] = ("Karotte", "Karotten"), ["hot dog"] = ("Hotdog", "Hotdogs"), ["pizza"] = ("Pizza", "Pizzen"), ["donut"] = ("Donut", "Donuts"),
        ["cake"] = ("Kuchen", "Kuchen"), ["chair"] = ("Stuhl", "Stühle"), ["couch"] = ("Sofa", "Sofas"), ["potted plant"] = ("Topfpflanze", "Topfpflanzen"), ["bed"] = ("Bett", "Betten"),
        ["dining table"] = ("Tisch", "Tische"), ["toilet"] = ("Toilette", "Toiletten"), ["tv"] = ("Fernseher", "Fernseher"), ["laptop"] = ("Laptop", "Laptops"), ["mouse"] = ("Maus", "Mäuse"),
        ["remote"] = ("Fernbedienung", "Fernbedienungen"), ["keyboard"] = ("Tastatur", "Tastaturen"), ["cell phone"] = ("Handy", "Handys"), ["microwave"] = ("Mikrowelle", "Mikrowellen"),
        ["oven"] = ("Backofen", "Backöfen"), ["toaster"] = ("Toaster", "Toaster"), ["sink"] = ("Spüle", "Spülen"), ["refrigerator"] = ("Kühlschrank", "Kühlschränke"), ["book"] = ("Buch", "Bücher"),
        ["clock"] = ("Uhr", "Uhren"), ["vase"] = ("Vase", "Vasen"), ["scissors"] = ("Schere", "Scheren"), ["teddy bear"] = ("Teddybär", "Teddybären"), ["hair drier"] = ("Föhn", "Föhne"),
        ["toothbrush"] = ("Zahnbürste", "Zahnbürsten"),
    };

    public sealed record Hit(string Class, string Label, double Score, int X1, int Y1, int X2, int Y2);
    public sealed record Result(int Width, int Height, int Ms, IReadOnlyList<Hit> Hits)
    {
        /// <summary>Zählung je Klasse, häufigste zuerst.</summary>
        public IReadOnlyList<(string Class, int Count)> Counts => Hits.GroupBy(h => h.Class).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        /// <summary>„3 Personen, 1 Auto“ – oder „Nichts erkannt.“</summary>
        public string Summary => Hits.Count == 0 ? "Nichts erkannt." : string.Join(", ", Counts.Select(c => $"{c.Count} {Name(c.Class, c.Count)}"));
    }

    public static string Name(string cls, int count = 1) => German.TryGetValue(cls, out var n) ? (count == 1 ? n.One : n.Many) : cls;

    /// <summary>Ausgabe von trendo_objekte.py lesen.</summary>
    public static Result Parse(string json)
    {
        var j = JsonNode.Parse(json) ?? throw new FormatException("Keine Antwort.");
        var hits = new List<Hit>();
        foreach (var o in j["objekte"]?.AsArray() ?? [])
        {
            var c = o!["klasse"]!.GetValue<string>(); var b = o["rahmen"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
            if (b.Length != 4) throw new FormatException("Rahmen unvollständig.");
            hits.Add(new(c, Name(c), o["sicherheit"]!.GetValue<double>(), b[0], b[1], b[2], b[3]));
        }
        return new(j["breite"]?.GetValue<int>() ?? 0, j["hoehe"]?.GetValue<int>() ?? 0, j["ms"]?.GetValue<int>() ?? 0, hits);
    }

    /// <summary>Argumente für das Startskript (nach dem Repo-Platzhalter).</summary>
    public static IReadOnlyList<string> Args(string model, string image, string? annotatedOut, double threshold) =>
        [model, image, annotatedOut ?? "-", Math.Clamp(threshold, 0.1, 0.9).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)];

    /// <summary>Modell laden; nur mit passender Prüfsumme übernommen, sonst verworfen.</summary>
    public static async Task<(bool Ok, string Message)> DownloadModelAsync(Func<string, Task<Stream>> fetch, string dir, string url = ModelUrl, string sha = ModelSha256)
    {
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, ModelFile + ".part");
        try
        {
            await using (var s = await fetch(url)) await using (var f = File.Create(tmp)) await s.CopyToAsync(f);
            var got = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tmp)));
            if (got != sha) { File.Delete(tmp); return (false, "Download verworfen: Prüfsumme stimmt nicht."); }
            SafeFile.Move(tmp, Path.Combine(dir, ModelFile));
            return (true, "Modell YOLOX-S geladen (36 MB, Apache-2.0).");
        }
        catch (Exception e) when (e is not OutOfMemoryException) { try { File.Delete(tmp); } catch (IOException) { } return (false, "Download fehlgeschlagen: " + e.Message); }
    }
}
