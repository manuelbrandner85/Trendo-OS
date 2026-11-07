using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Vorschlag 4 (Tiere): freie, geriggte Tiermodelle ins Unreal-Spiel – Kopie ins Projekt, Import im Welt-Bau, Tier im Spiel, Testspieler-Bot.</summary>
public class UnrealAnimalsTests
{
    [Fact(DisplayName = "Tiere: nur Ordner mit FBX werden kopiert (FBX, Texturen, LIZENZ.txt), Zip-Pakete bleiben im Zwischenspeicher")]
    public void Copy_animals()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-tiere-" + Guid.NewGuid().ToString("N"));
        try
        {
            var src = Path.Combine(root, "tiere"); var proj = Path.Combine(root, "Spiel");
            var h = Directory.CreateDirectory(Path.Combine(src, "hyena_a1", "ue5", "FbxUE5")).FullName;
            File.WriteAllBytes(Path.Combine(h, "Hyenas_A1.Fbx"), [1, 2, 3]);
            Directory.CreateDirectory(Path.Combine(src, "hyena_a1", "ue5", "Texturen")); File.WriteAllBytes(Path.Combine(src, "hyena_a1", "ue5", "Texturen", "Hyenas_A1.png"), [4]);
            File.WriteAllText(Path.Combine(src, "hyena_a1", "LIZENZ.txt"), "CC BY 4.0"); File.WriteAllBytes(Path.Combine(src, "hyena_a1", "source.zip"), [9]);
            Directory.CreateDirectory(Path.Combine(src, "leer")); // ohne Modell: wird übersprungen
            Assert.Equal(0, UnrealGame.CopyAnimals(Path.Combine(root, "fehlt"), proj));
            Assert.Equal(1, UnrealGame.CopyAnimals(src, proj));
            var t = Path.Combine(proj, "Trendo", "Tiere", "hyena_a1");
            Assert.True(File.Exists(Path.Combine(t, "ue5", "FbxUE5", "Hyenas_A1.Fbx"))); Assert.True(File.Exists(Path.Combine(t, "ue5", "Texturen", "Hyenas_A1.png")));
            Assert.True(File.Exists(Path.Combine(t, "LIZENZ.txt"))); Assert.False(File.Exists(Path.Combine(t, "source.zip")));
            Assert.False(Directory.Exists(Path.Combine(proj, "Trendo", "Tiere", "leer")));
            Assert.EndsWith(Path.Combine("assets", "tiere"), UnrealGame.AnimalsDir("C:/Werkzeuge"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Tiere: Welt-Bau importiert mit gemessener Bewegungslänge und legt die Namensnennung ins Spiel; Spiel kennt das Fluchttier")]
    public void Script_and_game()
    {
        Assert.Contains("def import_animal", UnrealGame.WeltScript); Assert.Contains("lizenzen.txt", UnrealGame.WeltScript); Assert.Contains("\"tiere\": tiere", UnrealGame.WeltScript);
        Assert.Contains("get_bone_pose_for_time", UnrealGame.WeltScript); // echte Länge statt gehaltener Schlusspose
        Assert.Contains("used_with_skeletal_mesh", UnrealGame.WeltScript); Assert.Contains("\"materials\":", UnrealGame.WeltScript); // Material wirkt im Spiel; Fell-Varianten
        var dir = Path.Combine(Path.GetTempPath(), "trendo-tiere-spiel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var d = GameDesign.Templates.First(t => t.Id == "openworld").Bible();
            var built = GameDesign.BuildUnreal(d, dir, "5.8");
            string Src(string f) => File.ReadAllText(Directory.GetFiles(Path.Combine(built, "Source"), f, SearchOption.AllDirectories).Single());
            var actors = Src("TrendoActors.cpp");
            Assert.Contains("ATrendoAnimal::Kill", actors); Assert.Contains("flieht", actors); Assert.Contains("GetRandomReachablePointInRadius", actors); Assert.Contains("FurVariant", actors); // jedes Tier eigenes Fell
            Assert.Contains("TRENDO-TIER", Src("TrendoGameMode.cpp")); Assert.Contains("class ATrendoAnimal", Src("TrendoActors.h"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Landschaft aus der Spielbibel: Savanne, Hof oder Wald (Standard); ausdrücklich gewählt gewinnt; level.json trägt sie fürs Spiel")]
    public void Biome_from_bible()
    {
        Assert.Equal("savanne", GameDesign.Biome(new System.Text.Json.Nodes.JsonObject { ["title"] = "Löwenland", ["logline"] = "Eine Safari durch die Savanne Afrikas" }));
        Assert.Equal("wald", GameDesign.Biome(new System.Text.Json.Nodes.JsonObject { ["title"] = "Waldläufer", ["logline"] = "Eine Försterin im Bergwald" }));
        Assert.Equal("hof", GameDesign.Biome(new System.Text.Json.Nodes.JsonObject { ["title"] = "Mein Bauernhof", ["logline"] = "Tiere im Stall und auf der Weide" }));
        Assert.Equal("wald", GameDesign.Biome(new System.Text.Json.Nodes.JsonObject { ["title"] = "Ohne Hinweis" }));
        Assert.Equal("hof", GameDesign.Biome(new System.Text.Json.Nodes.JsonObject { ["title"] = "Waldläufer", ["biome"] = "hof" }));
        Assert.Contains("HABITAT = [(\"hyen\", \"savanne\")", UnrealGame.WeltScript); Assert.Contains("/Game/AnimalVarietyPack", UnrealGame.WeltScript);
        Assert.Contains("/Game/AnimalVarietyPack", UnrealGame.AlwaysCook); // Tiere auch im fertigen Spiel
    }

    [Fact(DisplayName = "Tierpakete (.uasset) aus dem Zwischenspeicher nach Content/<Paket>, nur Unreal-Dateien")]
    public void Copy_packs()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-pakete-" + Guid.NewGuid().ToString("N"));
        try
        {
            var src = Directory.CreateDirectory(Path.Combine(root, "pakete", "AnimalVarietyPack", "Fox", "Meshes")).FullName;
            File.WriteAllBytes(Path.Combine(src, "SK_Fox.uasset"), [1]); File.WriteAllText(Path.Combine(src, "notiz.txt"), "x");
            Directory.CreateDirectory(Path.Combine(root, "pakete", "leer"));
            Assert.Equal(1, UnrealGame.CopyAnimalPacks(Path.Combine(root, "pakete"), Path.Combine(root, "Spiel")));
            Assert.True(File.Exists(Path.Combine(root, "Spiel", "Content", "AnimalVarietyPack", "Fox", "Meshes", "SK_Fox.uasset")));
            Assert.False(File.Exists(Path.Combine(root, "Spiel", "Content", "AnimalVarietyPack", "Fox", "Meshes", "notiz.txt")));
            Assert.Equal(0, UnrealGame.CopyAnimalPacks(Path.Combine(root, "fehlt"), Path.Combine(root, "Spiel")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Testspieler-Bot: Prüfpunkt Tiere aus der Messzeile (flieht ja/nein) und Bild „Tiere“")]
    public void Playtest_animals()
    {
        var ok = GamePlaytest.Read("LogTrendo: Display: TRENDO-TIER Hyaene flieht=ja Abstand 6.0 → 22.8 m Bewegung=run Tempo 28 km/h Fluchten=1");
        Assert.True(Assert.Single(ok, f => f.Area == "Tiere").Ok); Assert.Contains("6.0 → 22.8 m", ok.Single(f => f.Area == "Tiere").Text);
        Assert.False(GamePlaytest.Read("TRENDO-TIER Hyaene flieht=nein Abstand 5.0 → 5.0 m Bewegung=idle Tempo 0 km/h Fluchten=0").Single(f => f.Area == "Tiere").Ok);
    }
}
