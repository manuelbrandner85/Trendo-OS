using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0782, 3. Gegenprüfung: „thema“ in der Positivliste nur mit gültiger Kennung – über die echte KI-Kette (KI ersetzt).</summary>
[Collection("Claude")]
public class T0782Gegenpruefung3_Ui
{
    sealed class FixedKi(Func<string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9"; public int Calls;
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Calls++; return Task.FromResult(reply()); }
    }
    static async Task Until(Func<bool> ok, int ms = 15000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static string Set(string pfad, string wert) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = pfad, ["wert"] = wert } }) }.ToJsonString();

    [AvaloniaFact(DisplayName = "M6 (3.): KI setzt „thema“ auf freien Text → abgelehnt, Dokument unverändert; gültige Kennung → Vorschau")]
    public async Task Thema_only_valid_ids()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = Set("thema", "Bruchrechnen ganz frei");
        var ki = new FixedKi(() => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1200; w.ProgramSetLevel(UserLevel.Profi); w.Show(MainWindow.TutorKey); Settle();
            RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(MainWindow.TutorKey)!;
            var before = d.Data.ToJsonString();
            w.Find<TextBox>("auftrag-prompt").Text = "Stell bitte das Thema auf Bruchrechnen um, danke";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > 0 && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.Equal("fehler", w.AssistLast!.Value.Phase); Assert.Contains("Dieses Thema gibt es nicht", w.AssistLast.Value.Message);
            Assert.Equal(before, d.Data.ToJsonString());
            reply = Set("thema", "m-kuerzen"); var c = ki.Calls;
            w.Find<TextBox>("auftrag-prompt").Text = "Stell bitte das Thema auf Brüche kürzen um, danke";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.Calls > c && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen");
            Assert.Equal("vorschau", w.AssistLast!.Value.Phase);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }
}
