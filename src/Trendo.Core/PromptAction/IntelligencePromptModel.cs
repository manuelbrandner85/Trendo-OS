using Trendo.Core;

namespace Trendo.Core.PromptAction;

/// <summary>Dünne Hülle um IntelligenceCore.AskAsync. Keine zweite Modellverwaltung.</summary>
public sealed class IntelligencePromptModel(IntelligenceCore core, Func<bool> local) : IPromptModel
{
    public string Name => "Trendo Intelligence";
    public bool IsLocal => local();

    public async Task<string?> CompleteAsync(string system, string user, CancellationToken cancellation)
    {
        if (!IsLocal) return null;
        var answer = await core.AskAsync(new KiRequest(user, system, UseKnowledge: false), cancellation);
        return answer.Ok ? answer.Text : null;
    }

    public static bool Loopback(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return u.IsLoopback || u.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
    }
}

public static class PromptActions
{
    /// <summary>Registrierte Fachadapter. Gesperrte Aktionen bleiben unsupported und schreiben keine Datei.</summary>
    public static IReadOnlyList<IStudioActionAdapter> CreateDefault() =>
        [
            new LetterPromptAdapter(), new SheetPromptAdapter(), new MailSendAdapter(), new MailDraftAdapter(),
            new InvoicePromptAdapter(), new MoneyPayAdapter(), new BusinessPlanAdapter(),
            new SoftwareScaffoldAdapter(), new SoftwarePublishAdapter(),
            new ModelPromptAdapter(), new BlenderPromptAdapter(),
            new GameBrowserAdapter(), new GameUnrealAdapter(), new GameLaunchAdapter(),
            new MediaConvertAdapter(), new MediaUploadAdapter(),
            new CalcNoteAdapter(), new ProjectCreateAdapter(),
            new RoomPlanAdapter(), new CircuitAdapter(), new BeamCheckAdapter(),
            new TwinSeriesAdapter(), new RouteAdapter(),
            new CncMachineAdapter(), new CncFeedAdapter(),
            new VehicleCostAdapter(), new StatsAdapter(),
            new KnowledgeRememberAdapter(), new LearnCardAdapter(), new PromptFillAdapter(), new FashionSizeAdapter(),
            new StreamSendAdapter(), new RundownAdapter(), new TvListAdapter(),
            new LicenseFetchAdapter(), new LicenseNoteAdapter(),
            new XrBudgetAdapter(), new ScanBlockedAdapter(),
            ..SystemActions.Create()
        ];
}
