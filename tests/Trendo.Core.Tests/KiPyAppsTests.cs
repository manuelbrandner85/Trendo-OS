using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Paket E (Wunsch 02.11.2026): freie Python-KIs (TripoSR, MMAudio) in eigenen Umgebungen über uv.
public class KiPyAppsTests
{
    [Fact(DisplayName = "Freie KIs: TripoSR und MMAudio auf festem Stand, Einrichtung in fester Reihenfolge, uv nur im eigenen Ordner, PyTorch vom offiziellen Server")]
    public void Recipes()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-kipy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new KiLibraryStore(root);
            foreach (var a in KiPyApps.All.Where(x => !x.PackagesOnly)) // Song-Analyse (nur PyPI) prüft MusicToolsTests
            {
                Assert.Matches("^[0-9a-f]{40}$", a.Commit);
                Assert.Equal(KiLibrary.RuntimeInfo(a.Id).Tag, a.Commit); // Bibliothek und Rezept zeigen denselben Stand
                Assert.StartsWith("https://github.com/", a.RepoUrl);
                var p = store.PyPaths(a.Id);
                var steps = KiPyApps.Steps(a, p, nvidia: true);
                Assert.Equal(["clone", "-C", "python", "venv", "pip", "pip"], steps.Select(s => s.Args[0]));
                Assert.Contains("--detach", steps[1].Args); Assert.Contains(a.Commit, steps[1].Args);
                Assert.Contains("--no-bin", steps[2].Args); // keine Verknüpfungen im Benutzerordner
                Assert.Contains("--clear", steps[3].Args);
                Assert.Contains("https://download.pytorch.org/whl/cu128", steps[4].Args);
                Assert.All(steps, s => Assert.True(s.Exe == p.Uv || s.Exe == p.Git));
                Assert.StartsWith(root, p.Dir);
                Assert.False(KiPyApps.Ready(a, p)); Assert.False(store.RuntimeReady(a.Id));
                Directory.CreateDirectory(Path.GetDirectoryName(p.PythonExe)!); File.WriteAllText(p.PythonExe, "MZ");
                Assert.False(store.RuntimeReady(a.Id)); // ohne Merker nicht fertig
                File.WriteAllText(p.Marker, "0000000000000000000000000000000000000000");
                Assert.False(store.RuntimeReady(a.Id)); // anderer Stand
                File.WriteAllText(p.Marker, a.Commit);
                Assert.True(store.RuntimeReady(a.Id));
                Assert.True(KiPyApps.CommitOk(a, a.Commit.ToUpperInvariant() + "\n"));
                Assert.False(KiPyApps.CommitOk(a, "abc"));
            }
            Assert.DoesNotContain("download.pytorch.org/whl/cu128", KiPyApps.Steps(KiPyApps.All[0], store.PyPaths(KiPyApps.All[0].Id), nvidia: false)[^2].Args);
            Assert.Contains("--editable", KiPyApps.Steps(KiPyApps.For(KiRuntime.MMAudio)!, store.PyPaths(KiRuntime.MMAudio), true)[^1].Args);
            Assert.Null(KiPyApps.For(KiRuntime.Piper));

            var env = KiPyApps.UvEnv(store.RuntimeDir(KiRuntime.Uv));
            Assert.Equal("1", env["UV_NO_MODIFY_PATH"]); Assert.Equal("only-managed", env["UV_PYTHON_PREFERENCE"]);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var k in new[] { "UV_CACHE_DIR", "UV_PYTHON_INSTALL_DIR", "UV_PYTHON_BIN_DIR", "UV_TOOL_DIR", "HF_HOME" }) Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(env[k]));
            Assert.DoesNotContain(env.Values, v => v.Equals(Path.Combine(home, ".local", "bin"), StringComparison.OrdinalIgnoreCase));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "KI-Bibliothek: neue Arten „3D aus Bild“ und „Geräusche“ – TripoSR (MIT), MMAudio mit Hinweis „nur nicht-kommerziell“, uv mit Prüfsumme, kein TRELLIS/Hunyuan")]
    public void Library_entries()
    {
        var t = Assert.Single(KiLibrary.OfKind(KiKind.Modell3D)); Assert.Equal("triposr", t.Id); Assert.True(t.OpenLicense);
        var s = Assert.Single(KiLibrary.OfKind(KiKind.Geraeusche)); Assert.Equal("mmaudio", s.Id); Assert.False(s.OpenLicense); Assert.Contains("nicht-kommerziell", s.License);
        var uv = KiLibrary.RuntimeInfo(KiRuntime.Uv);
        Assert.Equal(64, uv.Sha256.Length); Assert.StartsWith("https://github.com/astral-sh/uv/releases/download/", uv.DownloadUrl);
        Assert.DoesNotContain(KiLibrary.Models, m => m.Name.Contains("Hunyuan", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("TRELLIS", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(KiSuggestions.For("CORE-05")); Assert.Contains(KiKind.Modell3D, KiSuggestions.For("CORE-05"));
        Assert.Contains(KiKind.Geraeusche, KiSuggestions.For("CORE-08"));
        Assert.Empty(KiSuggestions.For("gibt-es-nicht")); Assert.Empty(KiSuggestions.For(null));
        Assert.DoesNotContain("SPEC-07", KiSuggestions.Studios); // abgelehnt – nicht wiederbeleben
    }
}
