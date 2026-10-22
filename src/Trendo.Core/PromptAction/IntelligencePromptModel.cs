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
    /// <summary>Registrierte Fachadapter. Alles andere bleibt „nicht eingebaut“.</summary>
    public static IReadOnlyList<IStudioActionAdapter> CreateDefault() =>
        [
            new LetterPromptAdapter(), new SheetPromptAdapter(), new MailSendAdapter(), new MailDraftAdapter(),
            new InvoicePromptAdapter(), new MoneyPayAdapter(), new BusinessPlanAdapter()
        ];
}
