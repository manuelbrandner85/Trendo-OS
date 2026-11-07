using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Echte Kollision im Unreal-Spiel: Stamm statt Krone, zerlegte Felsen, feste Menschen, Aufprall mit Schaden, Füße auf dem Boden.</summary>
public class UnrealCollisionTests
{
    [Fact(DisplayName = "Welt-Bau: Baum bekommt einen Stamm-Zylinder (gemessen), Felsen/Holz/hohe Sträucher eine Zerlegung, Gras und Farn bleiben durchlaufbar")]
    public void World_collision_shapes()
    {
        var w = UnrealGame.WeltScript;
        Assert.Contains("def collide(sm, kind)", w); Assert.Contains("def trunk(sm)", w); Assert.Contains("append_cylinder", w);
        Assert.Contains("set_convex_decomposition_collisions", w); Assert.Contains("\"ohne Kollision (durchlaufbar)\"", w);
        Assert.Contains("BIOME_GROUNDS", w); // Boden je Landschaft (Rückfall auf das bisherige Paar, wenn Texturen fehlen)
    }

    [Fact(DisplayName = "Menschen versinken nicht: übertragene Geh-/Laufbewegungen halten die Wurzel auf dem ersten Bildstand (Beckenhöhe bleibt), nicht auf der Grundhaltung")]
    public void Root_height_kept()
    {
        var m = UnrealGame.MenschScript;
        Assert.Contains("def keep_root_height(dest)", m); Assert.Contains("RootMotionRootLock.ANIM_FIRST_FRAME", m); Assert.Contains("force_root_lock", m);
        Assert.True(m.IndexOf("keep_root_height(dest))", StringComparison.Ordinal) > m.IndexOf("duplicate_and_retarget", StringComparison.Ordinal)); // erst nach dem Übertragen
    }

    [Fact(DisplayName = "Spiel: Menschen haben einen festen Körper, der Wagen nimmt Schaden aus der Aufprallwucht, Überfahrene fallen als Ragdoll; Proben für Kollision und Füße")]
    public void Game_collision_and_damage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-koll-" + Guid.NewGuid().ToString("N"));
        try
        {
            var built = GameDesign.BuildUnreal(GameDesign.Templates.First(t => t.Id == "openworld").Bible(), dir, "5.8");
            string Src(string f) => File.ReadAllText(Directory.GetFiles(Path.Combine(built, "Source"), f, SearchOption.AllDirectories).Single());
            var actors = Src("TrendoActors.cpp"); var gm = Src("TrendoGameMode.cpp"); var car = Src("TrendoCar.h");
            Assert.Contains("TEXT(\"Koerper\")", actors); Assert.Contains("SetCollisionProfileName(TEXT(\"Pawn\"))", actors); // Figuren blocken
            Assert.Contains("bool TrendoRagdollBody(", actors); Assert.Contains("return TrendoRagdollBody(Body3D", actors); // ein Sturz für Gegner und Überfahrene
            Assert.Contains("float Damage = 0;", car); Assert.Contains("Power()", car);
            Assert.Contains("void ATrendoGameMode::CarImpacts", gm); Assert.Contains("void ATrendoGameMode::RunOver", gm); Assert.Contains("TRENDO-AUFPRALL", gm);
            Assert.Contains("TRENDO-KOLL", gm); Assert.Contains("TRENDO-FUSS", gm);
            Assert.Contains("{ TEXT(\"strauch\"), 1200, 9000, 0.9f, 1.8f, true, 250 }", gm); // hohe Sträucher blocken
            Assert.Contains("Kmh > 6.f", gm); Assert.Contains("Contains(TEXT(\"Landscape\"))", gm); // ab 6 km/h umgestoßen; Bodenwellen sind kein Unfall
            Assert.Contains("TRENDO-GLIED aufgefangen", actors); Assert.Contains("AlwaysTickPoseAndRefreshBones; Sk->SetCollisionEnabled", actors); // Glied fällt nicht durchs Gelände; Tierpose immer gerechnet
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
