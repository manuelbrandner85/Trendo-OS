namespace Trendo.Core.Pod;

/// <summary>Ergebnis eines Dienstes im Testlauf.</summary>
public enum PodProbeState
{
    /// <summary>Kein Schlüssel der Person – nichts wurde gesendet.</summary>
    Blockiert,
    /// <summary>Der Dienst hat den Schlüssel angenommen (nur lesende Abfrage).</summary>
    Bereit,
    /// <summary>Der Dienst hat abgelehnt oder war nicht erreichbar.</summary>
    Fehler,
    /// <summary>Schlüssel vorhanden, aber für den Testlauf nicht zulässig (Stripe-Live-Schlüssel).</summary>
    Abgelehnt,
    /// <summary>In Trendo nicht angebunden.</summary>
    Fehlt,
}

public sealed record PodProbe(string Service, PodProbeState State, string Message, IReadOnlyList<string> Steps);

/// <summary>T-0753: Testlauf der POD-Anbieter mit den eigenen Schlüsseln der Person. Ohne Schlüssel wird nichts gesendet und der Dienst steht ehrlich auf BLOCKIERT,
/// mit Anleitung, was die Person selbst einrichten muss. Mit Schlüssel laufen nur lesende Abfragen: nichts wird angelegt, bestellt oder bezahlt.
/// Stripe nur mit Testschlüssel (sk_test_/rk_test_).</summary>
public static class PodTestRun
{
    public const string PrintfulKey = "pod-printful-token", PrintifyKey = "pod-printify-token", StripeKey = "pod-stripe-key", NetlifyKey = "pod-netlify-token";

    public static readonly IReadOnlyList<string> PrintfulSteps =
    [
        "Konto bei Printful anlegen (printful.com, kostenlos) und einen Shop vom Typ „Manuelle Bestellung / API“ erstellen.",
        "Auf " + PrintfulClient.TokenPage + " einen privaten Schlüssel für diesen Shop erzeugen (Rechte: Katalog, Produkte, Mockups, Bestellungen).",
        "In Trendo im Studio „Print on Demand“ unter „Anbieter“ den Schlüssel einfügen und „Verbinden“ drücken.",
    ];
    public static readonly IReadOnlyList<string> PrintifySteps =
    [
        "Konto bei Printify anlegen (printify.com, kostenlos) und einen Shop anlegen; in den Shop-Einstellungen Bestellungen auf manuelle Freigabe stellen.",
        "Auf " + PrintifyClient.TokenPage + " ein persönliches Zugangstoken erzeugen.",
        "In Trendo im Studio „Print on Demand“ unter „Anbieter“ das Token einfügen und den Shop wählen.",
    ];
    public static readonly IReadOnlyList<string> StripeSteps =
    [
        "Konto bei Stripe anlegen (stripe.com). Für den Testlauf reicht der Testmodus, ohne Firmenangaben und ohne Bankkonto.",
        "Im Stripe-Dashboard den Testmodus einschalten und auf https://dashboard.stripe.com/test/apikeys den geheimen Testschlüssel (beginnt mit sk_test_) kopieren.",
        "In Trendo unter „Online verkaufen“ → „Print on Demand im Shop“ den Testschlüssel einfügen und „Verbinden“ drücken.",
    ];
    public static readonly IReadOnlyList<string> NetlifySteps =
    [
        "Konto bei Netlify anlegen (netlify.com, kostenlos). Netlify dient als öffentliche Ablage für Druckdateien und für den Shop.",
        "Auf " + NetlifyDesignHost.TokenPage + " einen persönlichen Zugangsschlüssel erzeugen.",
        "In Trendo im Studio „Print on Demand“ unter „Anbieter“ die Ablage mit diesem Schlüssel einrichten.",
    ];

    public static bool IsStripeTestKey(string key) => key.StartsWith("sk_test_", StringComparison.Ordinal) || key.StartsWith("rk_test_", StringComparison.Ordinal);

    public static async Task<IReadOnlyList<PodProbe>> RunAsync(IWebSend web, Func<string, string?> secret, CancellationToken ct = default)
    {
        var list = new List<PodProbe>
        {
            await Probe("Printful", secret(PrintfulKey), PrintfulSteps, async k => { await new PrintfulClient(web, k).CheckAsync(ct); return "Schlüssel gültig (nur gelesen: Berechtigungen)."; }),
            await Probe("Printify", secret(PrintifyKey), PrintifySteps, async k => { var s = await new PrintifyClient(web, k).ShopsAsync(ct); return s.Count == 0 ? "Schlüssel gültig, aber noch kein Shop angelegt." : $"Schlüssel gültig, {s.Count} Shop(s) (nur gelesen)."; }),
        };
        var st = secret(StripeKey);
        if (st is not null && !IsStripeTestKey(st))
            list.Add(new("Stripe", PodProbeState.Abgelehnt, "Hinterlegt ist ein Live-Schlüssel. Der Testlauf nutzt nur einen Testschlüssel (sk_test_…) – es wurde nichts gesendet.", StripeSteps));
        else
            list.Add(await Probe("Stripe", st, StripeSteps, async k =>
            {
                var s = new StripeClient(web, k); await s.CheckAsync(ct);
                var paid = await s.PaidOrdersAsync(1, ct);
                return "Testschlüssel gültig (nur gelesen: Kontostand und letzte Testzahlung" + (paid.Count > 0 ? ", eine Testzahlung gefunden" : "") + ").";
            }));
        list.Add(await Probe("Netlify", secret(NetlifyKey), NetlifySteps, async k => { await new NetlifyDesignHost(web, k).CheckAsync(ct); return "Schlüssel gültig (nur gelesen: Benutzer)."; }));
        list.Add(new("Gelato", PodProbeState.Fehlt, "Gelato ist in Trendo noch nicht angebunden – dafür gibt es keinen Testlauf.", []));
        return list;
    }

    static async Task<PodProbe> Probe(string name, string? key, IReadOnlyList<string> steps, Func<string, Task<string>> read)
    {
        if (string.IsNullOrWhiteSpace(key)) return new(name, PodProbeState.Blockiert, $"BLOCKIERT: Kein {name}-Schlüssel hinterlegt. Es wurde nichts an {name} gesendet.", steps);
        try { return new(name, PodProbeState.Bereit, await read(key.Trim()), []); }
        catch (PodServiceException e) { return new(name, PodProbeState.Fehler, e.Message, steps); }
        catch (Exception e) when (e is TaskCanceledException or IOException || e.GetType().Name == "HttpRequestException") { return new(name, PodProbeState.Fehler, $"{name} ist nicht erreichbar. Ist das Internet verbunden?", []); }
    }
}
