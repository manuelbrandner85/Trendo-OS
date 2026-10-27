using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>KI-Quellen-Einstellungen: gleichzeitiges Speichern (Hintergrund + Klick) verliert nichts und erzeugt keine halbe Datei.</summary>
public class KiSourceSettingsTests
{
    [Fact(DisplayName = "KI-Quellen: gleichzeitige Änderungen gehen nicht verloren, Lesen sieht nie eine halbe Datei")]
    public async Task Concurrent_updates_are_safe()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-kiset-" + Guid.NewGuid().ToString("N"));
        try
        {
            KiSourceSettings.Update(root, s => { s.Cloud.Add(KiCatalog.Cloud[0].Id); s.AutoModels = false; });
            var resets = 0;
            var writers = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            {
                for (var n = 0; n < 25; n++) KiSourceSettings.Update(root, s => s.AutoFailed[$"m{i}"] = s.AutoFailed.GetValueOrDefault($"m{i}") + 1);
            })).ToArray();
            var reader = Task.Run(() =>
            {
                while (!writers.All(w => w.IsCompleted))
                    if (KiSourceSettings.Load(root).Cloud.Count == 0) Interlocked.Increment(ref resets); // halbe Datei → still Standard
            });
            await Task.WhenAll([.. writers, reader]);
            var final = KiSourceSettings.Load(root);
            for (var i = 0; i < 8; i++) Assert.Equal(25, final.AutoFailed[$"m{i}"]);
            Assert.Equal(0, resets);
            Assert.False(final.AutoModels);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "ki"), "*.tmp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
