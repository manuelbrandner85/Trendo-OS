using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.PromptAction;

namespace Trendo.App;

/// <summary>Ein Arbeitsplatz pro Studio: Auftrag, Datei, Vorschau und Prüfung an einer Stelle. Facharbeit macht nur der bestehende Ablauf.</summary>
public partial class MainWindow
{
    private string _workspaceStudio = "X-11";
    private string? _workspaceProject;
    private string? _workspaceRun;
    private StudioExecutionResult? _workspaceResult;
    private int _platzSeq;
    private TextBox? _platzTitel;
    private TextBox? _platzText;
    private TextBox? _platzInhalt;
    private TextBox? _platzProjektId;
    private TextBlock? _platzProjekt;
    private TextBlock? _platzHinweis;
    private TextBlock? _platzFaehigkeit;
    private TextBlock? _platzStatus;
    private TextBlock? _platzGrund;
    private TextBlock? _platzFragen;
    private TextBlock? _platzFreigabeText;
    private TextBlock? _platzPfad;
    private TextBlock? _platzVorschau;
    private TextBlock? _platzPruefung;
    private TextBlock? _platzExport;
    private Button? _platzAntworten;
    private Button? _platzFreigabe;
    private Button? _platzAendern;

    public void ShowWorkspace(string studioId)
    {
        if (CapabilityCatalog.Find(studioId) is null) studioId = "X-11";
        _workspaceStudio = studioId;
        Show("arbeitsplatz");
    }

    public void SubmitWorkspace(string titel, string text)
    {
        if (_platzTitel is not null) _platzTitel.Text = titel;
        if (_platzText is not null) _platzText.Text = text;
        var project = _workspaceProject ?? NewestProject();
        if (project is null)
        {
            _workspaceResult = null;
            _workspaceRun = null;
            Paint(null, "Rückfrage", "Lege zuerst ein Projekt an.");
            return;
        }
        _workspaceProject = project;
        var id = "platz" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + _platzSeq++.ToString("00");
        var request = new StudioPromptRequest(id, project, _workspaceStudio, ArbeitsplatzBlattAdapter.Prompt(titel, text), [], "de", null, PrivacyMode.Local, new Provenance("arbeitsplatz", null, null));
        var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        var result = PromptFlow.Start(request, ctx);
        _workspaceRun = result.RunId;
        _workspaceResult = result;
        Paint(result);
    }

    private Control WorkspaceView()
    {
        var info = StudioCatalog.For(_workspaceStudio);
        _platzProjektId = new TextBox { Name = "platz-projekt-id", PlaceholderText = "projekt-id (a-z, 0-9, -)", Width = 240, FontSize = 14 };
        var create = new Button { Name = "platz-projekt-anlegen", Content = "Projekt anlegen", Height = 32, MinWidth = 140 };
        AutomationProperties.SetName(create, "Projekt anlegen");
        create.Click += (_, _) =>
        {
            try
            {
                var id = (_platzProjektId.Text ?? "").Trim();
                _core.Projects.Create(id, id);
                _workspaceProject = id;
                if (_platzHinweis is not null) _platzHinweis.Text = "";
                Paint(_workspaceResult);
            }
            catch (Exception ex)
            {
                if (_platzHinweis is not null) _platzHinweis.Text = ex.Message;
            }
        };

        _platzTitel = new TextBox { Name = "platz-titel", PlaceholderText = "Kurzer Titel", FontSize = 14 };
        AutomationProperties.SetName(_platzTitel, "Titel des Arbeitsblatts");
        _platzText = new TextBox { Name = "platz-text", PlaceholderText = "Was soll festgehalten werden?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, FontSize = 14 };
        AutomationProperties.SetName(_platzText, "Text des Auftrags");
        _platzInhalt = new TextBox { Name = "platz-inhalt", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, FontSize = 14, IsReadOnly = true };
        AutomationProperties.SetName(_platzInhalt, "Text der gespeicherten Notiz");

        var check = new Button { Name = "platz-pruefen", Content = "Auftrag prüfen", Height = 32, MinWidth = 140, Background = Accent, Foreground = Brushes.White };
        AutomationProperties.SetName(check, "Auftrag prüfen");
        check.Click += (_, _) => SubmitWorkspace(_platzTitel.Text ?? "", _platzText.Text ?? "");
        _platzAntworten = new Button { Name = "platz-antworten", Content = "Antwort übernehmen", Height = 32, MinWidth = 160, IsVisible = false };
        AutomationProperties.SetName(_platzAntworten, "Antwort auf die Rückfrage übernehmen");
        _platzAntworten.Click += (_, _) => AnswerWorkspace();
        _platzFreigabe = new Button { Name = "platz-freigabe", Content = "Einmal freigeben", Height = 32, MinWidth = 150, IsVisible = false };
        AutomationProperties.SetName(_platzFreigabe, "Diesen einen Vorgang freigeben");
        _platzFreigabe.Click += (_, _) => GateWorkspace(approve: true);
        _platzAendern = new Button { Name = "platz-aendern", Content = "Änderung speichern", Height = 32, MinWidth = 160, IsVisible = false };
        AutomationProperties.SetName(_platzAendern, "Änderung am Text speichern");
        _platzAendern.Click += (_, _) => ReviseWorkspace();
        var cancel = new Button { Name = "platz-abbrechen", Content = "Abbrechen", Height = 32, MinWidth = 110 };
        AutomationProperties.SetName(cancel, "Auftrag abbrechen");
        cancel.Click += (_, _) => GateWorkspace(approve: false);
        var back = new Button { Name = "platz-zurueck", Content = "Zum Studio", Height = 32, MinWidth = 110 };
        AutomationProperties.SetName(back, "Zurück zum Studio");
        back.Click += (_, _) =>
        {
            var page = PageForStudio(_workspaceStudio);
            Show(page ?? "studios");
        };

        _platzProjekt = P("", Text, 13); _platzProjekt.Name = "platz-projekt";
        _platzHinweis = P(""); _platzHinweis.Name = "platz-projekt-hinweis";
        _platzFaehigkeit = P("", null, 12); _platzFaehigkeit.Name = "platz-faehigkeit";
        _platzStatus = new TextBlock { Name = "platz-status", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(_platzStatus, "Stand des Auftrags");
        _platzGrund = P(""); _platzGrund.Name = "platz-grund";
        _platzFragen = P(""); _platzFragen.Name = "platz-fragen";
        _platzFreigabeText = P(""); _platzFreigabeText.Name = "platz-freigabe-text";
        _platzPfad = P(""); _platzPfad.Name = "platz-pfad";
        _platzVorschau = P(""); _platzVorschau.Name = "platz-vorschau";
        _platzPruefung = P(""); _platzPruefung.Name = "platz-pruefung";
        _platzExport = P(""); _platzExport.Name = "platz-export";

        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { check, _platzAntworten, _platzFreigabe, _platzAendern, cancel, back }
        };
        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                P((info?.Name ?? _workspaceStudio) + (info is null ? "" : " · " + info.Sentence), Text, 13),
                _platzFaehigkeit,
                Card(new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        H("Projekt", 14),
                        _platzProjekt,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _platzProjektId, create } },
                        _platzHinweis
                    }
                }),
                Card(new StackPanel { Spacing = 6, Children = { H("Auftrag", 14), _platzTitel, _platzText, buttons, _platzStatus, _platzGrund } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Prüfung", 14), _platzPruefung } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Export", 14), _platzExport } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Vorschau", 14), _platzVorschau } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Datei", 14), _platzPfad, _platzInhalt } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Rückfrage", 14), _platzFragen } }),
                Card(new StackPanel { Spacing = 4, Children = { H("Freigabe", 14), _platzFreigabeText } })
            }
        };
        Paint(_workspaceResult);
        return Page("Ein Arbeitsplatz", body);
    }

    private void AnswerWorkspace()
    {
        if (_workspaceProject is null || _workspaceRun is null) return;
        var answers = new Dictionary<string, string>
        {
            ["titel"] = _platzTitel?.Text ?? "",
            ["text"] = _platzText?.Text ?? ""
        };
        var result = PromptFlow.Answer(_workspaceProject, _workspaceRun, answers, Ctx());
        _workspaceResult = result;
        Paint(result);
    }

    private void ReviseWorkspace()
    {
        if (_workspaceProject is null || _workspaceRun is null) return;
        var prompt = ArbeitsplatzBlattAdapter.Prompt(_platzTitel?.Text ?? "", _platzInhalt?.Text ?? "");
        var result = PromptFlow.Revise(_workspaceProject, _workspaceRun, prompt, Ctx());
        _workspaceResult = result;
        Paint(result);
    }

    private void GateWorkspace(bool approve)
    {
        if (_workspaceProject is null || _workspaceRun is null)
        {
            if (_platzStatus is not null) _platzStatus.Text = "Nichts zum Abbrechen.";
            return;
        }
        var ctx = Ctx();
        var result = approve
            ? PromptFlow.Approve(_workspaceProject, _workspaceRun, Principal.LocalOwner, ctx)
            : PromptFlow.Cancel(_workspaceProject, _workspaceRun, Principal.LocalOwner, ctx);
        _workspaceResult = result;
        Paint(result);
    }

    private StudioExecutionContext Ctx() =>
        new(_workspaceProject ?? "", Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);

    private void Paint(StudioExecutionResult? result, string? status = null, string? question = null)
    {
        var board = WorkspaceBoard.Compose(result);
        var projectId = _workspaceProject ?? NewestProject();
        var project = projectId is null ? null : _core.Projects.List().FirstOrDefault(p => p.Id == projectId);
        if (_platzProjekt is not null)
            _platzProjekt.Text = project is null ? "Kein Projekt. Lege zuerst eines an." : "Projekt: " + project.Name + " (" + project.Id + ")";
        if (_platzFaehigkeit is not null)
            _platzFaehigkeit.Text = CapabilityCatalog.UiLine(_workspaceStudio, PromptFlow.Adapters) + " Cloud-KI ist aus. Eine kostenpflichtige KI ist nicht nötig.";
        if (_platzStatus is not null) _platzStatus.Text = status ?? board.Status;
        if (_platzGrund is not null) _platzGrund.Text = board.Detail;
        if (_platzFragen is not null) _platzFragen.Text = question ?? board.Question;
        if (_platzFreigabeText is not null) _platzFreigabeText.Text = board.Approval;
        if (_platzVorschau is not null) _platzVorschau.Text = board.Preview;
        if (_platzPruefung is not null) _platzPruefung.Text = board.Qa;
        if (_platzExport is not null) _platzExport.Text = board.Export;
        if (_platzAntworten is not null) _platzAntworten.IsVisible = board.ShowAnswer;
        if (_platzFreigabe is not null) _platzFreigabe.IsVisible = board.ShowApprove;
        if (_platzAendern is not null) _platzAendern.IsVisible = board.ShowRevise;
        var file = result?.Artifacts.FirstOrDefault(a => a.Producer == ArbeitsplatzBlattAdapter.ActionIdValue);
        if (_platzPfad is not null) _platzPfad.Text = file is null ? "Noch keine Datei." : file.RelativePath;
        if (_platzInhalt is not null)
        {
            _platzInhalt.IsReadOnly = file is null;
            if (file is null || projectId is null) _platzInhalt.Text = "";
            else
            {
                try { _platzInhalt.Text = ArbeitsplatzBlattAdapter.BodyOf(_core.Projects.ReadFile(projectId, file.RelativePath)); }
                catch (Exception ex) { _platzInhalt.Text = ex.Message; }
            }
        }
    }
}
