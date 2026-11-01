using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Messdaten;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// T-0315: Programm-Assistent in der Auftragsleiste – Plan mit Live-Haken, Vorschau an der Arbeitskopie (Original bleibt exakt gleich),
/// Übernehmen als ein rückgängig machbarer Schritt, Verwerfen, Vorschläge, Echtzeit-Vorschau beim Tippen. Ohne KI (Regeln).
[Collection("Claude")]
public class Assist_Ui
{
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static MeasureSheet Sheet(MainWindow w) => MeasureSheet.From(w.ProgramActive("wissenschaft")!.Data);

    [AvaloniaFact(DisplayName = "T-0315: „Füge 3 Zeilen hinzu und zwei Spalten“ → Plan, Live-Haken, Vorschau ohne Änderung am Original; Übernehmen = ein Schritt, Rückgängig nimmt ihn zurück")]
    public async Task Plan_preview_apply_undo()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            w.ProgramNew("wissenschaft"); w.Show("wissenschaft"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var doc = w.ProgramActive("wissenschaft")!;
            var (cols, rows, undo) = (Sheet(w).Columns.Count, Sheet(w).Columns[0].Values.Count, doc.History.Count);
            var box = w.Find<TextBox>("auftrag-prompt");
            Assert.Contains("Messdaten", box.PlaceholderText);
            box.Text = "Füge 3 Zeilen hinzu und zwei Spalten";
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.AssistLast?.Phase == "vorschau");
            Assert.Equal(["Zeile (3×)", "Spalte (2×)"], w.AssistLast!.Value.Steps); // Befehle ohne Symbole (aus „Zeile +“)
            Assert.True(w.TryFind<Border>("assist-preview") is not null, string.Join(" | ", w.AssistLog));
            Assert.Contains("ohne KI", w.Find<TextBlock>("assist-source").Text);
            // Original exakt unverändert – Daten und Rückgängig-Liste
            Assert.Equal((cols, rows, undo), (Sheet(w).Columns.Count, Sheet(w).Columns[0].Values.Count, doc.History.Count));
            w.Find<Button>("assist-apply").Press();
            await Until(() => w.AssistLast?.Phase == "fertig");
            Assert.Equal((cols + 2, rows + 3), (Sheet(w).Columns.Count, Sheet(w).Columns[0].Values.Count));
            Assert.Equal(undo + 1, doc.History.Count); Assert.StartsWith("KI: Füge 3 Zeilen", doc.UndoLabel);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "assist.apply");
            w.Find<Button>("assist-undo").Press();
            Assert.Equal((cols, rows), (Sheet(w).Columns.Count, Sheet(w).Columns[0].Values.Count));
        }
        finally { MainWindow.AssistStepDelayMs = 140; }
    }

    [AvaloniaFact(DisplayName = "T-0315: Verwerfen lässt alles, wie es war; Unverstandenes geht an den Studio-Ablauf; Echtzeit-Vorschau beim Tippen zeigt, was passieren würde")]
    public async Task Discard_fallback_live()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.AssistStepDelayMs = 0; MainWindow.AssistLiveDelayMs = 10;
        try
        {
            w.ProgramNew("wissenschaft"); w.Show("wissenschaft"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var before = w.ProgramActive("wissenschaft")!.Data.ToJsonString();
            var box = w.Find<TextBox>("auftrag-prompt");
            box.Text = "Zeile hinzufügen";
            await Until(() => w.Find<TextBlock>("auftrag-vorschau").Text?.StartsWith("Ich würde: Zeile") == true);
            Assert.NotNull(w.TryFind<Border>("assist-live-thumb"));
            Assert.Equal(before, w.ProgramActive("wissenschaft")!.Data.ToJsonString()); // die Live-Vorschau ändert nichts
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.AssistLast?.Phase == "vorschau");
            w.Find<Button>("assist-discard").Press();
            Assert.Equal("verworfen", w.AssistLast!.Value.Phase); Assert.Equal(before, w.ProgramActive("wissenschaft")!.Data.ToJsonString());
            box = w.Find<TextBox>("auftrag-prompt"); box.Text = "Backe einen Kuchen";
            await Until(() => w.Find<TextBlock>("auftrag-vorschau").Text?.Contains("Noch nicht verstanden") == true);
        }
        finally { MainWindow.AssistStepDelayMs = 140; MainWindow.AssistLiveDelayMs = 450; }
    }

    [AvaloniaFact(DisplayName = "T-0315: Rückfrage als Knöpfe mit Empfehlung zuerst, „Immer so“ merkt die Antwort; gemerkte Vorliebe sichtbar und löschbar")]
    public async Task Questions_and_prefs()
    {
        var (w, _, root) = Ui.Open();
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            w.ProgramNew("wissenschaft"); w.Show("wissenschaft"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            // eigene Anbindung mit Auswahl-Parameter (wie Haushaltsbuch/Brief es haben)
            var t = new FakeTarget();
            w.RegisterAssistAdapter("wissenschaft", () => t);
            w.Show("wissenschaft");
            var box = w.Find<TextBox>("auftrag-prompt"); box.Text = "Diagramm zeichnen";
            w.Find<Button>("auftrag-pruefen").Press();
            await Until(() => w.AssistLast?.Phase == "fragen");
            Assert.Equal("Linie (Empfohlen)", w.Find<Button>("assist-q-0-1").Content);
            w.Find<CheckBox>("assist-q-0-remember").IsChecked = true;
            w.Find<Button>("assist-q-0-2").Press();
            await Until(() => w.AssistLast?.Phase == "vorschau");
            Assert.Equal("Diagramm – Art: Balken", t.Ran.Single());
            Assert.Equal("Balken", new AssistPrefs(root, "wissenschaft").Values["diagramm.art"]);
            Assert.NotNull(w.TryFind<Expander>("assist-prefs"));
            w.Find<Button>("assist-pref-del-0").Press();
            Assert.Empty(new AssistPrefs(root, "wissenschaft").All);
        }
        finally { MainWindow.AssistStepDelayMs = 140; }
    }

    sealed class FakeTarget : IAssistTarget
    {
        public readonly List<string> Ran = [];
        public string Key => "wissenschaft"; public string Title => "Messdaten"; public string Role => "";
        public IReadOnlyList<AssistAction> Actions { get; } = [new("diagramm", "Diagramm", ["diagramm", "zeichnen"], [new AssistParam("art", "Art", AssistKind.Choice, ["Punkte", "Linie", "Balken"], "Linie")])];
        public IAssistSession Begin() => new S(this);
        public IEnumerable<AssistSuggestion> Hints() => [];
        public bool Undo() => false;
        sealed class S(FakeTarget t) : IAssistSession
        {
            public void Run(AssistStep step) => t.Ran.Add(step.Text);
            public Control? Preview() => new TextBlock { Text = "Vorschau" };
            public bool Changed => true;
            public void Apply(string label, IReadOnlyList<AssistStep> sideEffects) { }
        }
    }
}
