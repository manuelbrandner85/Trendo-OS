using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0723 (Audit D6 „Code mit KI“ echt machen): Die KI des einen Assistenzwegs (T-0707, <see cref="AssistChain"/> und <c>IntelligenceCore</c>:
/// Claude nur über das eigene Abo, wenn es Haupt-KI ist – sonst das beste Modell auf diesem PC nach T-0703/T-0712) ändert Code im offenen Projekt nachvollziehbar:
/// Auftrag prüfen → Rechte → Plan → Vorschau als Unterschied je Datei → Ja/Nein je Änderung → Anwenden als EIN Rückgängig-Schritt (Dokument und Dateien) →
/// Tests nach Freigabe (<see cref="ApprovalGate"/>) mit dem vorhandenen <see cref="DevBuild"/> → Ergebnis; bei roten Tests die Rückfrage „zurücknehmen?“.
/// Projektgrenzen setzt <see cref="CodeKiChange.Blocked"/> durch (nur Dateien im Projektordner).</summary>
public partial class MainWindow
{
    /// <summary>Phasen: "" (bereit), "denkt", "vorschau", "angewendet", "testet", "gruen", "rot", "zurueck", "fehler".</summary>
    string _ckPhase = "", _ckMsg = "", _ckOut = "", _ckWish = "", _ckModel = "", _ckLabel = "";
    CodeKiChange.Preview? _ckPreview;
    readonly Dictionary<string, HashSet<int>> _ckAccept = new(StringComparer.OrdinalIgnoreCase);
    DevBuild.TestRun? _ckTests;
    /// <summary>Je Dokument die Schritte, die gerade auf der Platte angewendet sind (in Reihenfolge) – Grundlage für Rückgängig/Wiederholen.</summary>
    readonly Dictionary<ProgramDocument, List<string>> _ckApplied = new(ReferenceEqualityComparer.Instance);

    public string CodeAiKiPhase => _ckPhase;
    public string CodeAiKiMessage => _ckMsg;
    public string CodeAiKiOutput => _ckOut;
    public CodeKiChange.Preview? CodeAiKiPreview => _ckPreview;
    public DevBuild.TestRun? CodeAiKiTests => _ckTests;
    /// <summary>Für Tests und Beleg: letzte Antwort der KI (nur im Speicher).</summary>
    public string? CodeAiKiLastAnswer { get; private set; }
    /// <summary>Für Tests: zuletzt gestarteter Lauf (Vorschlag oder Übernehmen mit Test).</summary>
    public Task? CodeAiKiTask { get; private set; }

    string CkStepFile(string root, string id) => Path.Combine(Path.GetDirectoryName(CaSnapFile(root))!, "ki-schritte", id + ".json");

    /// <summary>Dokument einmal an Rückgängig/Wiederholen der Hülle anbinden: dann folgen die Dateien dem Dokument.</summary>
    void CkHook(ProgramDocument d)
    {
        if (_ckApplied.ContainsKey(d)) return;
        _ckApplied[d] = [.. CodeKiChange.AppliedIds(d.Data)]; // beim Öffnen gilt: was im Dokument steht, ist auf der Platte
        d.Restored += CkSync;
    }

    /// <summary>Nach Rückgängig/Wiederholen: Schritte, die nicht mehr im Dokument stehen, auf der Platte zurücknehmen; wieder aufgenommene erneut anwenden.</summary>
    void CkSync(ProgramDocument d)
    {
        if (CaRoot(d) is not { } r || !_ckApplied.TryGetValue(d, out var applied)) return;
        var want = CodeKiChange.AppliedIds(d.Data);
        var notes = new List<string>();
        foreach (var id in applied.AsEnumerable().Reverse().Where(id => !want.Contains(id)).ToList())
        {
            if (CodeKiChange.Load(CkStepFile(r, id)) is not { } step) { notes.Add("Ein KI-Schritt ist nicht mehr im Verlauf – seine Dateien bleiben."); applied.Remove(id); continue; }
            var skipped = CodeKiChange.Write(step.Files, r, back: true);
            applied.Remove(id);
            _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.rueckgaengig", $"{id} dateien={step.Files.Count} uebersprungen={skipped.Count}");
            notes.Add(skipped.Count == 0 ? $"Rückgängig: {step.Files.Count} Datei(en) sind wieder wie vor der KI." : $"Rückgängig, aber nicht bei {string.Join(", ", skipped)} – die Datei wurde danach geändert und bleibt.");
            if (_ckPhase is "angewendet" or "gruen" or "rot" or "testet") { _ckPhase = "zurueck"; _ckMsg = notes[^1]; }
        }
        foreach (var id in want.Where(id => !applied.Contains(id)).ToList())
        {
            if (CodeKiChange.Load(CkStepFile(r, id)) is not { } step) continue;
            var skipped = CodeKiChange.Write(step.Files, r, back: false);
            applied.Add(id);
            _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.wiederholt", $"{id} dateien={step.Files.Count} uebersprungen={skipped.Count}");
            notes.Add(skipped.Count == 0 ? $"Wiederholt: die KI-Änderung an {step.Files.Count} Datei(en) gilt wieder." : $"Wiederholt, aber nicht bei {string.Join(", ", skipped)} – die Datei wurde inzwischen geändert.");
        }
        if (notes.Count == 0) return;
        if (_caFor == d.Id && _caDocFile is { } open) { if (File.Exists(open)) try { CaLoad(open); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { } else { _caDoc = null; _caDocFile = null; } }
        _progMsg[CodeAiKey] = string.Join(" ", notes);
    }

    /// <summary>Schritt 1–4 der Kette: Auftrag prüfen, Rechte, KI fragen, Vorschau bauen. true = es gibt eine Vorschau mit mindestens einer Änderung.</summary>
    public Task<bool> CodeAiKiPropose(ProgramDocument d) { var t = CkProposeRun(d); CodeAiKiTask = t; return t; }

    async Task<bool> CkProposeRun(ProgramDocument d)
    {
        CaState(d); CkHook(d);
        bool Stop(string msg, string phase = "fehler") { _ckPhase = phase; _ckMsg = msg; _progMsg[CodeAiKey] = msg; Show(CodeAiKey); return false; }
        if (_ckPhase is "denkt" or "testet") return Stop("Die KI arbeitet schon an einem Auftrag.", _ckPhase);
        if (CaRoot(d) is not { } r) return Stop("Öffne zuerst einen Projektordner.", "");
        AssistOrder order;
        try { order = AssistChain.Validate(CodeAiKey, d.Text("wish"), AssistEntry.Programmbefehl); }
        catch (ArgumentException e) { return Stop(e.Message.Replace("im Dokument", "im Projekt"), ""); }
        if (AssistChain.RightsBlock(CurrentPrincipal, _core.Approvals) is { } denied) { _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.rechte", denied); return Stop(denied); }
        // Ungespeicherter Editortext zuerst auf die Platte – sonst plant die KI auf einem alten Stand
        if (_caDoc is not null && _caDocFile is not null && _caDoc.Text != _caSaved) CodeAiSaveFile(d);
        var all = CodeProject.Tree(r, null, 4000).Where(e => !e.IsDir).Select(e => Path.GetRelativePath(r, e.Path).Replace('\\', '/')).ToList();
        var texts = new List<(string, string)>();
        foreach (var rel in all.Take(200))
        {
            var full = Path.Combine(r, rel.Replace('/', Path.DirectorySeparatorChar));
            try { if (new FileInfo(full).Length <= 64 * 1024 && CodeKiChange.Blocked(r, rel) is null) texts.Add((rel, CodeProject.ReadText(full))); }
            catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        }
        var openRel = _caDocFile is { } f ? Path.GetRelativePath(r, f).Replace('\\', '/') : null;
        var p = DevBuild.Detect(r); var testPlan = DevBuild.Test(p);
        var system = CodeKiChange.SystemPrompt(p.Label, testPlan.Cmd?.Display);
        var user = CodeKiChange.UserPrompt(order.Prompt, texts, openRel, all);
        _ckPreview = null; _ckAccept.Clear(); _ckTests = null; _ckOut = ""; _ckWish = order.Prompt;
        _ckPhase = "denkt"; _ckMsg = "Die KI plant die Änderung …"; _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey);
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.auftrag", $"{Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar))} zeichen={order.Prompt.Length}");
        var ki = await Intel.AskAsync(new KiRequest(user, system, PreferredModel: KiModelName, UseKnowledge: false));
        CodeAiKiLastAnswer = ki.Text;
        if (!ki.Ok) { _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.fehler", ki.Text.Length > 200 ? ki.Text[..200] : ki.Text); return Stop("Keine KI hat geantwortet: " + ki.Text); }
        var proposal = CodeKiChange.Parse(ki.Text);
        if (proposal.Files.Count == 0)
        {
            // genau einmal nachfragen: Plan ohne Dateien oder abgeschnittener Block
            _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.nachfrage", $"plan={proposal.Plan.Count}");
            var again = await Intel.AskAsync(new KiRequest("Es fehlen die DATEI-Blöcke. Antworte noch einmal im verlangten Aufbau: PLAN und für jede geänderte Datei einen DATEI-Block mit dem vollständigen Inhalt.", system,
                PreferredModel: KiModelName, History: [new KiMessage("user", user), new KiMessage("assistant", AssistChain.CleanAnswer(ki.Text))], UseKnowledge: false));
            if (again.Ok) { CodeAiKiLastAnswer = again.Text; proposal = CodeKiChange.Parse(again.Text); ki = again; }
        }
        _ckModel = $"{ki.Backend}{(ki.Model is { Length: > 0 } m ? ", " + m : "")}";
        CodeKiChange.Preview preview;
        try { preview = CodeKiChange.Build(r, proposal); }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { return Stop("Die Vorschau ging nicht: " + e.Message); }
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.plan", $"modell={_ckModel} dateien={preview.Files.Count} abgelehnt={preview.Rejected.Count}");
        if (preview.Files.Count == 0)
            return Stop(preview.Rejected.Count > 0 ? "Die KI wollte nur Dateien ändern, die sie nicht ändern darf: " + string.Join(" ", preview.Rejected) : "Die KI hat keine Änderung vorgeschlagen. Beschreib den Wunsch etwas genauer.");
        _ckPreview = preview;
        foreach (var fp in preview.Files) _ckAccept[fp.Path] = [.. Enumerable.Range(0, fp.Hunks.Count)]; // vorbelegt: alles ja – die Person nimmt einzelne Änderungen heraus
        _ckPhase = "vorschau";
        _ckMsg = $"Vorschlag: {preview.Files.Count} Datei(en), {preview.Files.Sum(x => x.Hunks.Count)} Änderung(en). Noch ist nichts geändert – prüfe jede Änderung und entscheide mit Ja oder Nein.";
        _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey);
        return true;
    }

    /// <summary>Ja/Nein zu einer Änderung (Block <paramref name="hunk"/> in <paramref name="path"/>); <paramref name="hunk"/> = -1: die ganze Datei.</summary>
    public void CodeAiKiDecide(string path, int hunk, bool yes)
    {
        if (_ckPreview?.Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)) is not { } f) throw new InvalidOperationException("Diese Änderung gibt es im Vorschlag nicht.");
        var set = _ckAccept.TryGetValue(f.Path, out var s) ? s : _ckAccept[f.Path] = [];
        foreach (var h in hunk < 0 ? Enumerable.Range(0, f.Hunks.Count) : [hunk]) if (yes) set.Add(h); else set.Remove(h);
    }

    /// <summary>Vorschlag verwerfen – nichts wird geändert.</summary>
    public void CodeAiKiDiscard()
    {
        if (_ckPreview is null) return;
        _ckPreview = null; _ckAccept.Clear(); _ckPhase = ""; _ckMsg = "Verworfen – es wurde nichts geändert.";
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.verworfen", "");
        _progMsg[CodeAiKey] = _ckMsg;
    }

    /// <summary>Schritt 5–7: Angenommenes anwenden (ein Rückgängig-Schritt für Dokument und Dateien), speichern, dann Tests nach Freigabe.</summary>
    public Task CodeAiKiApply(ProgramDocument d) { var t = CkApplyRun(d); CodeAiKiTask = t; return t; }

    async Task CkApplyRun(ProgramDocument d)
    {
        CaState(d); CkHook(d);
        if (_ckPreview is not { } preview || _ckPhase != "vorschau") { _progMsg[CodeAiKey] = "Es gibt keinen Vorschlag, der auf eine Entscheidung wartet."; Show(CodeAiKey); return; }
        if (CaRoot(d) is not { } r) return;
        if (AssistChain.RightsBlock(CurrentPrincipal, _core.Approvals) is { } denied) { _ckMsg = denied; _progMsg[CodeAiKey] = denied; Show(CodeAiKey); return; }
        CodeKiChange.Step? step;
        try { step = CodeKiChange.Apply(r, preview, _ckAccept.ToDictionary(k => k.Key, k => (IReadOnlySet<int>)k.Value, StringComparer.OrdinalIgnoreCase), _ckWish, _ckModel); }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException) { _ckPhase = "fehler"; _ckMsg = e.Message; _progMsg[CodeAiKey] = e.Message; Show(CodeAiKey); return; }
        if (step is null) { CodeAiKiDiscard(); _ckMsg = "Du hast keine Änderung angenommen – es wurde nichts geändert."; _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey); return; }
        CodeKiChange.Save(CkStepFile(r, step.Id), step);
        _ckLabel = AssistChain.UndoPrefix + (_ckWish.Length > 48 ? _ckWish[..48] + "…" : _ckWish);
        d.Edit(_ckLabel, data => { var arr = data[CodeKiChange.StepsField] as JsonArray; if (arr is null) data[CodeKiChange.StepsField] = arr = []; arr.Add(CodeKiChange.DocEntry(step)); });
        _ckApplied[d].Add(step.Id);
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.apply", $"{step.Id} dateien={string.Join(",", step.Files.Select(f => f.Path))}");
        if (_caDocFile is { } open && File.Exists(open)) try { CaLoad(open); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        else if (step.Files.FirstOrDefault(x => x.Path.EndsWith(".py") || x.Path.EndsWith(".cs") || x.Path.EndsWith(".js")) is { } first) try { CodeAiOpenFile(d, Path.Combine(r, first.Path)); } catch (InvalidOperationException) { }
        _ckPreview = null; _ckAccept.Clear();
        var saved = "";
        try
        {
            if (d.Path is not null || AutoSaveDocs) saved = " Projektdatei gespeichert: " + Path.GetFileName(ProgramSave(CodeAiKey)) + ".";
            else DocBackup(d);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { saved = " Speichern der Projektdatei ging nicht: " + FriendlyError.Say(e); }
        _ckPhase = "angewendet";
        _ckMsg = $"✓ Übernommen: {step.Files.Count} Datei(en) geändert ({string.Join(", ", step.Files.Select(f => f.Path))}). Rückgängig nimmt alles auf einmal zurück (Strg+Z).{saved}";
        _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey);
        await CkTest(d, r);
    }

    /// <summary>Bauen/Testen nach der Änderung: vorhandenes <see cref="DevBuild.Test"/>, Start nur mit Freigabe (Rückfrage, ApprovalGate.Execute).</summary>
    async Task CkTest(ProgramDocument d, string r)
    {
        var p = DevBuild.Detect(r);
        var results = Path.Combine(_core.DataRoot, "code-ki", "testlaeufe", Guid.NewGuid().ToString("N")[..12]);
        var plan = DevBuild.Test(p, results);
        if (plan.Cmd is not { } cmd) { _ckMsg += " " + (plan.Blocked ?? plan.Note ?? "Für dieses Projekt kennt Trendo keinen Test."); _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey); return; }
        if (!DarfAusfuehren("codeai.ki.test", out var nein)) { _ckOut = nein; Show(CodeAiKey); return; } // T-0764 (5. Prüfrunde H3)
        if (_opt.Isolated && CodeAiRunOverride is null && !CodeRealToolsInTests) { _ckOut = "Testmodus: Trendo startet keine Programme."; Show(CodeAiKey); return; }
        var limit = int.TryParse(d.Text("timeout"), out var t) ? Math.Clamp(t, 10, 3600) : 300;
        var name = Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar));
        var w = new Wirkung("code-ki.test", r, "Tests ausführen",
            $"Trendo startet jetzt „{cmd.Display}“ im Projektordner „{name}“ (Zeitlimit {limit} s), um die Änderung der KI zu prüfen. Dabei läuft der Code des Projekts auf diesem PC.", "Tests starten");
        if (await FreigabeAsync(w) is not { } id) { _ckMsg += " Tests nicht gestartet (keine Freigabe) – „Tests ausführen“ geht jederzeit."; _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey); return; }
        Directory.CreateDirectory(results);
        _ckPhase = "testet"; _ckOut = $"Läuft: {cmd.Display} …"; Show(CodeAiKey);
        Cli.Result res;
        try
        {
            res = await _core.Approvals.Execute(id, "code-ki.test", r, () => Task.Run(() => CodeAiRunOverride is { } o ? o(cmd.Exe, cmd.Args, cmd.Cwd, limit * 1000)
                : Cli.Run(cmd.Exe, cmd.Args, cmd.Cwd, limit * 1000, cmd.Env, cmd.Utf8 ? new System.Text.UTF8Encoding(false) : null)));
        }
        catch (Exception e) when (e is ApprovalException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { res = new Cli.Result(-1, "", e.Message); }
        _ckTests = DevBuild.ReadResults(p, results, res.Out + "\n" + res.Err);
        try { Directory.Delete(results, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var green = res.Ok && _ckTests.Failed == 0;
        var body = (res.Out + "\n" + res.Err).Trim(); if (body.Length > 20000) body = "… (Anfang gekürzt)\n" + body[^10000..];
        _ckOut = (green ? "✓ " : "✕ ") + $"Tests {(green ? "bestanden" : "nicht bestanden")}: {_ckTests.Summary} (Exit {res.Exit})\n$ {cmd.Display}\n{body}";
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.ki.test", $"{name} exit={res.Exit} {_ckTests.Summary}");
        _ckPhase = green ? "gruen" : "rot";
        _ckMsg = green ? $"✓ Änderung übernommen und geprüft: {_ckTests.Summary}." : $"✕ Die Tests sind nach der KI-Änderung rot ({_ckTests.Summary}). KI-Änderung zurücknehmen?";
        _progMsg[CodeAiKey] = _ckMsg; Show(CodeAiKey);
    }

    /// <summary>„Zurücknehmen“ nach roten Tests: genau der KI-Schritt – über das Rückgängig der Hülle, damit Dokument und Dateien zusammen zurückgehen.</summary>
    public void CodeAiKiUndo(ProgramDocument d)
    {
        if (d.UndoLabel != _ckLabel || _ckLabel.Length == 0) throw new InvalidOperationException("Der KI-Schritt ist nicht mehr der letzte Schritt – nimm erst die späteren Schritte mit „Rückgängig“ zurück.");
        d.Undo();
        _ckPhase = "zurueck"; _ckMsg = _progMsg.TryGetValue(CodeAiKey, out var m) ? m : "Zurückgenommen."; _ckOut = ""; _ckTests = null;
    }

    /// <summary>Rote Tests, aber behalten (z. B. weil der Test selbst falsch ist).</summary>
    public void CodeAiKiKeep() { if (_ckPhase == "rot") { _ckPhase = "angewendet"; _ckMsg = "Behalten – die Tests sind noch rot. Rückgängig geht weiter mit Strg+Z."; _progMsg[CodeAiKey] = _ckMsg; } }

    // ───────────── Oberfläche ─────────────

    Control CaKiCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ca-ki", Spacing = 8 };
        sp.Children.Add(H("Mit KI ändern", 14));
        var ki = KiModelChoice.Model;
        sp.Children.Add(P($"Die KI plant die Änderung und zeigt sie dir als Unterschied – geändert wird erst nach deinem Ja. KI: {(ClaudeTakesStudioPrompts ? "Claude (dein Abo)" : string.IsNullOrEmpty(ki) ? "keine eingerichtet" : "auf diesem PC, " + ki)}.", Muted, 12));
        var wish = new TextBox { Name = "ca-ki-wish", Text = d.Text("wish"), PlaceholderText = "z. B. „Ergänze eine Funktion multipliziere(a, b) und schreib einen Test dafür“", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        void Commit() { var now = wish.Text ?? ""; if (now != d.Text("wish")) d.Set("wish", now, "Wunsch an die KI"); }
        wish.LostFocus += (_, _) => Commit();
        sp.Children.Add(wish);
        var busy = _ckPhase is "denkt" or "testet";
        var go = new Button { Name = "ca-ki-propose", Content = _ckPhase == "denkt" ? "KI plant …" : "Plan und Vorschau holen", IsEnabled = !busy && _ckPhase != "vorschau", Padding = new Thickness(14, 6) };
        go.Click += (_, _) => { try { Commit(); } catch (InvalidOperationException) { } _ = CodeAiKiPropose(d); };
        sp.Children.Add(go);
        if (_ckMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ca-ki-msg", Text = _ckMsg, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = _ckPhase is "rot" or "fehler" ? K4Bad : _ckPhase == "gruen" ? K4Good : Text });

        if (_ckPhase == "vorschau" && _ckPreview is { } pv)
        {
            if (pv.Plan.Count > 0)
            {
                var plan = new StackPanel { Name = "ca-ki-plan", Spacing = 2 };
                plan.Children.Add(P("Plan der KI" + (_ckModel.Length > 0 ? $" ({_ckModel})" : "") + ":", Text, 12));
                var n = 1; foreach (var s in pv.Plan.Take(12)) plan.Children.Add(P($"{n++}. {s}", Muted, 12));
                sp.Children.Add(plan);
            }
            foreach (var why in pv.Rejected) sp.Children.Add(P("Nicht erlaubt: " + why, K4Warn, 12));
            var fi = 0;
            foreach (var f in pv.Files)
            {
                var file = f; var acc = _ckAccept.TryGetValue(f.Path, out var a) ? a : [];
                var all = new CheckBox { Name = $"ca-ki-file-{fi}", IsChecked = acc.Count == f.Hunks.Count ? true : acc.Count == 0 ? false : null, IsThreeState = false,
                    Content = new TextBlock { Text = $"{f.Path} · {(f.IsNew ? "neu" : "geändert")} · +{f.Added} −{f.Removed}", FontWeight = FontWeight.SemiBold, FontSize = 13 } };
                all.IsCheckedChanged += (_, _) => { if (all.IsChecked is { } y) { CodeAiKiDecide(file.Path, -1, y); Show(CodeAiKey); } };
                var block = new StackPanel { Spacing = 4 };
                block.Children.Add(all);
                for (var h = 0; h < f.Hunks.Count; h++)
                {
                    var hh = h; var hunk = f.Hunks[h];
                    var yes = new CheckBox { Name = $"ca-ki-hunk-{fi}-{h}", IsChecked = acc.Contains(h), Content = new TextBlock { Text = $"Änderung {h + 1}: +{hunk.Added} −{hunk.Removed} – Ja, übernehmen", FontSize = 12 } };
                    yes.IsCheckedChanged += (_, _) => { CodeAiKiDecide(file.Path, hh, yes.IsChecked == true); Show(CodeAiKey); };
                    var lines = new StackPanel();
                    for (var i = Math.Max(0, hunk.From - 2); i <= Math.Min(f.Diff.Count - 1, hunk.To + 2); i++)
                    {
                        var l = f.Diff[i];
                        lines.Children.Add(new Border { Background = l.Op switch { CodeAiKit.Op.Neu => Brush.Parse("#1A3D2A"), CodeAiKit.Op.Weg => Brush.Parse("#4A1F24"), _ => Brushes.Transparent },
                            Child = K4Mono($"{(l.Old > 0 ? l.Old.ToString(CultureInfo.InvariantCulture) : ""),5} {(l.New > 0 ? l.New.ToString(CultureInfo.InvariantCulture) : ""),5} {(l.Op switch { CodeAiKit.Op.Neu => '+', CodeAiKit.Op.Weg => '-', _ => ' ' })} {l.Text}") });
                    }
                    block.Children.Add(yes);
                    block.Children.Add(new Border { Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Opacity = acc.Contains(h) ? 1 : 0.5,
                        Child = new ScrollViewer { MaxHeight = 320, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = lines } });
                }
                sp.Children.Add(new Border { Name = $"ca-ki-diff-{fi++}", BorderBrush = Brush.Parse("#3A3326"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = block });
            }
            var n2 = _ckAccept.Values.Sum(x => x.Count);
            var apply = new Button { Name = "ca-ki-apply", Content = n2 == 0 ? "Nichts ausgewählt" : $"Ja – {n2} Änderung(en) übernehmen und testen", IsEnabled = n2 > 0, Padding = new Thickness(14, 6) };
            apply.Classes.Add("accent");
            apply.Click += (_, _) => _ = CodeAiKiApply(d);
            var discard = new Button { Name = "ca-ki-discard", Content = "Nein – alles verwerfen", Padding = new Thickness(12, 6), Margin = new Thickness(8, 0, 0, 0) };
            discard.Click += (_, _) => ProgramRun(CodeAiKey, CodeAiKiDiscard);
            sp.Children.Add(new WrapPanel { Children = { apply, discard } });
        }
        if (_ckPhase == "rot")
        {
            var back = new Button { Name = "ca-ki-undo", Content = "Ja, KI-Änderung zurücknehmen", Padding = new Thickness(14, 6) };
            back.Classes.Add("accent");
            back.Click += (_, _) => ProgramRun(CodeAiKey, () => CodeAiKiUndo(d));
            var keep = new Button { Name = "ca-ki-keep", Content = "Nein, behalten", Padding = new Thickness(12, 6), Margin = new Thickness(8, 0, 0, 0) };
            keep.Click += (_, _) => ProgramRun(CodeAiKey, CodeAiKiKeep);
            sp.Children.Add(new WrapPanel { Children = { back, keep } });
        }
        if (_ckOut.Length > 0)
            sp.Children.Add(new Border { Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8),
                Child = new ScrollViewer { MaxHeight = 260, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = new SelectableTextBlock { Name = "ca-ki-output", Text = _ckOut, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 12,
                        Foreground = _ckOut.StartsWith('✓') ? K4Good : _ckOut.StartsWith('✕') ? K4Bad : Text } } });
        return KrCard(sp, "ca-ki-card");
    }
}
