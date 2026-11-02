using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

/// 0.9.47 Webseiten-Studio: Technik „Immersiv“ – fotorealistische 3D-Szene mit Storyscrolling, eigene Bewegungs-Engine (Clean-Room statt GSAP/Lenis), alles lokal.
public class WebsiteImmersiv
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-web-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Webseite Immersiv: erkannt aus „hyperrealistisch … Storyscrolling“, Szene, Kapitel, Wort-für-Wort-Text, Bewegung abschaltbar, alles lokal")]
    public void Immersive_page()
    {
        var root = Tmp();
        try
        {
            var built = WebsiteStudio.Build(root, new WebsiteBrief("Hyperrealistische, moderne Seite mit 3D-Objekt und Storyscrolling für ein Uhrengeschäft.", "Uhrwerk"));
            Assert.Equal("immersiv", built.Content.Technique);
            var dir = built.Directory; var html = File.ReadAllText(Path.Combine(dir, "index.html"));
            Assert.Contains("<canvas id=\"szene\">", html);
            Assert.Contains("immersiv-kapitel", html);
            Assert.Contains("<h1 data-split>", html);
            Assert.Contains("data-reveal", html);
            Assert.Contains("assets/vendor/trendo-motion.js", html);
            Assert.Contains("assets/vendor/trendo-three.js", html);
            Assert.DoesNotContain("cdn", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http://", html); Assert.DoesNotContain("https://", html);
            var three = File.ReadAllText(Path.Combine(dir, "assets", "vendor", "trendo-three.js"));
            Assert.Contains("immersiv", three); // eigene Szene im lokalen Bündel
            var motion = File.ReadAllText(Path.Combine(dir, "assets", "vendor", "trendo-motion.js"));
            Assert.Contains("prefers-reduced-motion", motion);
            Assert.DoesNotContain("gsap", motion.Replace("GSAP/ScrollTrigger/SplitText", "").Replace("GSAP, ScrollTrigger", ""), StringComparison.OrdinalIgnoreCase);
            var css = File.ReadAllText(Path.Combine(dir, "stil.css"));
            Assert.Contains(".immersiv-buehne", css); Assert.Contains("prefers-reduced-motion", css);
            Assert.Contains("TrendoWeb3.immersiv", File.ReadAllText(Path.Combine(dir, "seite.js")));
            Assert.Contains("trendo-motion.js", File.ReadAllText(Path.Combine(dir, "assets", "vendor", "LIZENZEN.txt")));
            Assert.True(built.Quality.Score >= 90, built.Quality.Plain());
        }
        finally { Directory.Delete(root, true); }
    }
}
