using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.PromptAction;

namespace Trendo.App;

/// <summary>Auftragsleiste für Verbesserung 42. Liegt über dem bestehenden Seiteninhalt und ändert die Studio-Layouts nicht.</summary>
public partial class MainWindow
{
    private StudioWorkflow? _promptFlow;
    private string? _promptProject;
    private string? _promptRun;

    private StudioWorkflow PromptFlow => _promptFlow ??= new StudioWorkflow(_core, model: new IntelligencePromptModel(Intel, BackendsAreLocal));

    private bool BackendsAreLocal()
    {
        var list = KiBackendsOverride?.Invoke() ?? DefaultKiBackends();
        return list.All(b => IntelligencePromptModel.Loopback(b.BaseUrl));
    }

    private string? NewestProject() => _core.Projects.List().OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).FirstOrDefault()?.Id;

    /// <summary>Hängt die Leiste an. Der Startbildschirm bleibt unverändert, damit der parallele Desktop-Auftrag dort frei bleibt.</summary>
    private Control WithPromptBar(string key, Control view)
    {
        // start: paralleler Desktop-Auftrag. 3d: die Seite füllt die Höhe; die Leiste sitzt dort im linken Rollbereich.
        if (key is "start" or "3d" || key.StartsWith("mein-", StringComparison.OrdinalIgnoreCase)) return view;
        var studio = ModuleOf(key);
        if (studio is null || CapabilityCatalog.Find(studio) is null) return view;
        var dock = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var bar = BuildPromptBar(studio);
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        dock.Children.Add(bar);
        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.VerticalAlignment = VerticalAlignment.Stretch;
        dock.Children.Add(view);
        return dock;
    }

    private Control BuildPromptBar(string studio)
    {
        var box = new TextBox
        {
            Name = "auftrag-prompt",
            PlaceholderText = "Was soll dieses Studio tun?",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 32,
            MaxHeight = 72,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(box, "Auftrag an dieses Studio");
        var status = new TextBlock { Name = "auftrag-status", Text = "Noch nicht gestartet", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220 };
        AutomationProperties.SetName(status, "Stand des Auftrags");
        var detail = new TextBlock { Name = "auftrag-detail", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var ability = P(CapabilityCatalog.UiLine(studio, PromptFlow.Adapters) + " Cloud-KI ist aus.", null, 12);
        ability.Name = "auftrag-faehigkeit";
        ability.TextTrimming = TextTrimming.CharacterEllipsis;
        ability.MaxHeight = 18;

        var check = new Button { Name = "auftrag-pruefen", Content = "Auftrag prüfen", Height = 32, MinWidth = 132, Background = Accent, Foreground = Brushes.White };
        AutomationProperties.SetName(check, "Auftrag prüfen");
        var cancel = new Button { Name = "auftrag-abbrechen", Content = "Abbrechen", Height = 32, MinWidth = 104 };
        AutomationProperties.SetName(cancel, "Auftrag abbrechen");
        var approve = new Button { Name = "auftrag-freigabe", Content = "Einmal freigeben", Height = 32, MinWidth = 148, IsVisible = false };
        AutomationProperties.SetName(approve, "Diesen einen Vorgang freigeben");
        var answer = new Button { Name = "auftrag-antworten", Content = "Antwort übernehmen", Height = 32, MinWidth = 156, IsVisible = false };
        AutomationProperties.SetName(answer, "Antwort auf die Rückfrage übernehmen");

        void ShowResult(StudioExecutionResult result)
        {
            _promptRun = result.RunId;
            status.Text = Trendo.Core.PromptAction.StatusText.German(result.Status);
            var lines = new List<string>();
            if (result.RoutingReason.Length > 0) lines.Add(result.RoutingReason);
            lines.AddRange(result.Questions.Select(q => q.Question));
            lines.AddRange(result.Errors);
            lines.AddRange(result.Warnings);
            if (result.Preview is { Length: > 0 }) lines.Add(result.Preview.Length > 500 ? result.Preview[..500] : result.Preview);
            foreach (var file in result.Artifacts) lines.Add(file.RelativePath + (file.Sha256 is null ? "" : " · " + file.Sha256[..12]));
            if (result.Receipt is not null) lines.Add("Beleg: " + result.Receipt);
            detail.Text = string.Join("\n", lines.Where(l => l.Length > 0));
            detail.IsVisible = detail.Text is { Length: > 0 };
            approve.IsVisible = result.Status == RunStatus.AwaitingApproval;
            answer.IsVisible = result.Status == RunStatus.AwaitingInput;
            AfterPromptResult(studio, result);
        }

        check.Click += async (_, _) =>
        {
            status.Text = "Wird wirklich ausgeführt";
            check.IsEnabled = false;
            try
            {
                var project = NewestProject();
                if (project is null)
                {
                    status.Text = "Rückfrage";
                    detail.Text = "Lege zuerst ein Projekt an.";
                    detail.IsVisible = true;
                    answer.IsVisible = false;
                    approve.IsVisible = false;
                    return;
                }
                _promptProject = project;
                var id = "ui-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var request = new StudioPromptRequest(id, project, studio, box.Text ?? "", [], "de", null, PrivacyMode.Local, new Provenance("oberflaeche", null, null));
                var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
                var result = await Task.Run(() => PromptFlow.Start(request, ctx));
                await Dispatcher.UIThread.InvokeAsync(() => ShowResult(result));
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
            finally { check.IsEnabled = true; }
        };
        cancel.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null)
            {
                status.Text = "Nichts zum Abbrechen.";
                detail.Text = "";
                detail.IsVisible = false;
                return;
            }
            try
            {
                var result = PromptFlow.Cancel(_promptProject, _promptRun, Principal.LocalOwner, new StudioExecutionContext(_promptProject, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default));
                ShowResult(result);
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };
        approve.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null) return;
            try
            {
                var result = PromptFlow.Approve(_promptProject, _promptRun, Principal.LocalOwner, new StudioExecutionContext(_promptProject, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default));
                ShowResult(result);
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };
        answer.Click += (_, _) =>
        {
            if (_promptProject is null || _promptRun is null) return;
            try
            {
                var result = PromptFlow.Answer(_promptProject, _promptRun, PromptFields.Read(box.Text), new StudioExecutionContext(_promptProject, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default));
                ShowResult(result);
            }
            catch (Exception ex)
            {
                status.Text = "Fehlgeschlagen";
                detail.Text = ex.Message;
                detail.IsVisible = true;
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { check, answer, approve, cancel }
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(box, 0);
        Grid.SetColumn(buttons, 1);
        Grid.SetColumn(status, 2);
        row.Children.Add(box);
        row.Children.Add(buttons);
        row.Children.Add(status);
        return new Border
        {
            Name = "auftrag-leiste",
            Background = TileBg,
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(8, 6),
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            Child = new StackPanel { Spacing = 4, Children = { ability, row, detail } }
        };
    }

    partial void AfterPromptResult(string studio, StudioExecutionResult result);
}
