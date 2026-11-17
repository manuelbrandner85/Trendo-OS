using System.Text.Json.Nodes;
using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0716 (B6 „Programm-Hülle für alle“, Etappe E4): Web Design Studio an der Notsicherung der Hülle. Vorher lag ein ungespeicherter Entwurf des visuellen Editors
/// nur im Speicher (<c>_wdDraft</c>) – nach einem Absturz oder Schließen des Fensters war er weg. Jetzt: im 20-s-Takt der Hülle atomar nach
/// <c>programme/sicherung/LINK-WEBDESIGN-&lt;Kennung&gt;.tdoc</c> (für dieses Fenster gesperrt), beim nächsten Öffnen von „Webseite bauen“ eine Wiederherstellen-Karte;
/// Zurückholen gibt den Entwurf dem Editor (wie ein ungespeicherter Entwurf: „Speichern“ im Editor schreibt ihn). Rückgängig bleibt das des Editors (Puck, im Browser-Teil) –
/// dieselbe Begründung wie bei Text/Tabelle (T-0714): ein zweites Rückgängig der Hülle neben dem des Editors brächte nichts und liefe auseinander.
/// Gespeichert wird weiter in <c>seite.trendo.json</c> (WebDesignStates, mit Sicherung), das Format bleibt unverändert.</summary>
public partial class MainWindow
{
    ProgramLink? _wdLink;
    string _wdLinkMsg = "";
    /// <summary>Für Tests: ungespeicherter Entwurf des visuellen Editors (null: keiner).</summary>
    public string? WebDesignDraft => _wdDraft;
    /// <summary>Anschluss des visuellen Editors an die Notsicherung der Hülle (für Tests öffentlich).</summary>
    public ProgramLink WebDesignLink => _wdLink ??= new ProgramLink("LINK-WEBDESIGN", "webdesign", null, "Webseite") { Locks = ProgLocks };

    /// <summary>Ist der Entwurf anders als die gespeicherte Seite?</summary>
    bool WebDesignUnsaved() => _wdDraft is { } d && d != WebDesignStates.Read(WebDesignDir, _wdVariant);

    /// <summary>Takt der Hülle (aus <see cref="LinksAutosave"/>): ungespeicherten Entwurf sichern, sonst die Sicherung wegräumen.</summary>
    void WebDesignAutosave()
    {
        if (_wdDraft is null && _wdLink is null) return;
        try
        {
            if (WebDesignUnsaved())
            {
                var title = WebDesignSite.ActiveFile(WebDesignDir) is { Length: > 0 } f ? f : "Webseite";
                WebDesignLink.Track(new JsonObject { ["ordner"] = WebDesignDir, ["variante"] = _wdVariant, ["seite"] = _wdDraft }, _wdVariant is null ? title : title + " (Variante)");
                WebDesignLink.Autosave(ProgBackup);
            }
            else if (_wdLink is { Dirty: true } || _wdLink is not null && File.Exists(_wdLink.Doc.BackupFile(ProgBackup))) _wdLink.Saved(ProgBackup); // gespeichert oder verworfen
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Für Tests und die Karte: nicht gespeicherte Entwürfe aus einer früheren Sitzung oder einem Absturz (neueste zuerst).</summary>
    public List<(string File, string Title, DateTime When)> WebDesignRecoveries() => WebDesignLink.Pending(ProgBackup);

    /// <summary>Entwurf zurückholen (der offene, ungespeicherte bleibt als Sicherung liegen) oder die Sicherung verwerfen.</summary>
    public void WebDesignRecover(int index, bool restore)
    {
        var all = WebDesignRecoveries(); if (index < 0 || index >= all.Count) return;
        if (!restore) { ProgramLink.Discard(all[index].File, ProgLocks); _wdLinkMsg = "Sicherung verworfen."; return; }
        // T-0730: ERST lesen und prüfen, DANN übernehmen. Vorher übernahm der Anschluss die Sicherung schon vor der Prüfung – bei einer unlesbaren Seite
        // galt sie danach als „eigene“ Sicherung dieses Fensters, verschwand aus der Karte und wurde im nächsten Takt (Entwurf = gespeicherte Seite) gelöscht.
        var check = ProgramDocument.Load(all[index].File);
        if (!check.Studio.Equals(WebDesignLink.Studio, StringComparison.OrdinalIgnoreCase) || check.Data["format"].AsText() != WebDesignLink.Format) throw new InvalidDataException("Diese Sicherung gehört zu einem anderen Programm.");
        var pre = check.Data["inhalt"];
        if (pre?["seite"] is not JsonValue pv || !pv.TryGetValue<string>(out var page) || WebDesign.Validate(page).Json is not { } ok) throw new InvalidDataException("Die Sicherung enthält keine lesbare Seite – sie bleibt erhalten.");
        WebDesignAutosave();
        var st = WebDesignLink.Restore(all[index].File, ProgBackup);
        if (st?["ordner"] is JsonValue ov && ov.TryGetValue<string>(out var dir) && Directory.Exists(dir)) _wdDir = dir;
        _wdVariant = st?["variante"] is JsonValue vv && vv.TryGetValue<string>(out var variant) ? variant : null;
        _wdDraft = ok;
        WebDesignLink.Track(st);
        WebDesignReload(ok); // offener Editor zeigt den Entwurf sofort; sonst beim nächsten Öffnen („bereit“)
        _wdLinkMsg = "Entwurf wiederhergestellt – im Editor „Speichern“ wählen, damit er in die Seite geschrieben wird.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.recover", all[index].Title);
    }

    /// <summary>Wiederherstellen-Karte über dem visuellen Editor (Knöpfe „wd-restore-&lt;n&gt;“ / „wd-discard-&lt;n&gt;“).</summary>
    Control? WebDesignRecoveryCard()
    {
        List<(string File, string Title, DateTime When)> all;
        try { all = WebDesignRecoveries(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        return RecoveryCardView("wd-recovery", (k, yes) => $"wd-{(yes ? "restore" : "discard")}-{k}", "Ein Entwurf im visuellen Editor", "Entwürfe im visuellen Editor", all, (k, yes) =>
        {
            try { WebDesignRecover(k, yes); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { _wdLinkMsg = "Zurückholen ging nicht: " + FriendlyError.Say(ex); }
            Show("dev");
        });
    }

    /// <summary>Karte und Hinweis für den Kopf des visuellen Editors.</summary>
    Control? WebDesignLinkHead()
    {
        var sp = new StackPanel { Spacing = 0 };
        if (WebDesignRecoveryCard() is { } card) sp.Children.Add(card);
        if (_wdLinkMsg.Length > 0) { sp.Children.Add(new TextBlock { Name = "wd-link-msg", Text = _wdLinkMsg, FontSize = 12, Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }); _wdLinkMsg = ""; }
        return sp.Children.Count > 0 ? sp : null;
    }
}
