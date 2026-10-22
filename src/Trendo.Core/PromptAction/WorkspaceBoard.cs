namespace Trendo.Core.PromptAction;

/// <summary>Anzeige des einen Arbeitsplatzes. Liest nur das Ergebnis des bestehenden Ablaufs und hebt keinen Status an.</summary>
public static class WorkspaceBoard
{
    public sealed record Board(
        string Status,
        string Detail,
        string Question,
        string Approval,
        string Preview,
        string Qa,
        string Export,
        bool QaPassed,
        bool ShowAnswer,
        bool ShowApprove,
        bool ShowRevise);

    public static Board Compose(StudioExecutionResult? result)
    {
        if (result is null)
            return new("Noch nicht gestartet", "", "Keine Rückfrage.", "Keine Wirkung nach außen.", "Noch keine Vorschau.", "Noch keine Prüfung.", "Nichts zu exportieren.", false, false, false, false);

        var detail = new List<string>();
        if (result.RoutingReason.Length > 0) detail.Add(result.RoutingReason);
        detail.AddRange(result.Errors.Where(s => s.Length > 0));
        detail.AddRange(result.Warnings.Where(s => s.Length > 0));

        var question = result.Questions.Count == 0
            ? "Keine Rückfrage."
            : string.Join("\n", result.Questions.Select(q => q.Question));

        var approval = result.Status == RunStatus.AwaitingApproval
            ? "Freigabe nötig. Es wurde noch nichts gesendet, bezahlt, veröffentlicht oder an ein Gerät geschickt."
            : string.IsNullOrWhiteSpace(result.Receipt) ? "Keine Wirkung nach außen." : "Beleg: " + result.Receipt;

        var preview = string.IsNullOrWhiteSpace(result.Preview) ? "Noch keine Vorschau." : result.Preview.Length > 500 ? result.Preview[..500] : result.Preview;

        string qa;
        if (result.Status == RunStatus.Unsupported)
            qa = "Keine Prüfung. Für dieses Studio gibt es noch keine Aktion, die Trendo ausführt.";
        else if (result.QaResults.Count == 0)
            qa = "Keine Prüfung geliefert.";
        else
            qa = string.Join("\n", result.QaResults.Select(q => (q.Passed ? "Bestanden: " : "Nicht bestanden: ") + q.Detail));

        var export = result.Artifacts.Count == 0
            ? "Nichts zu exportieren."
            : string.Join("\n", result.Artifacts.Select(a => a.RelativePath + (a.Sha256 is null ? "" : " · " + a.Sha256[..12])));

        var passed = result.Status == RunStatus.Verified && result.Artifacts.Count > 0 && result.QaResults.Count > 0 && result.QaResults.All(q => q.Passed);
        var revise = result.Artifacts.Any(a => a.Editable && a.Producer == ArbeitsplatzBlattAdapter.ActionIdValue);
        return new(StatusText.German(result.Status), string.Join("\n", detail), question, approval, preview, qa, export, passed, result.Status == RunStatus.AwaitingInput, result.Status == RunStatus.AwaitingApproval, revise);
    }
}
