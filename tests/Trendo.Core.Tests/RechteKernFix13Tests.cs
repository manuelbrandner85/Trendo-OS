using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 13. Prüfrunde: Pfade aus Oberflächenfeldern (frei getippte Ordner, Projektordner der KI) laufen für Kinder über ChildWrite –
/// M1 „Code mit KI“ (start.bat im Autostart-Ordner), M2 „Viele Dateien umbenennen“ (spiel → spiel.cmd), dazu Sortieren, Fotos ordnen, Quarantäne.
/// Die Prüfung sitzt im Kern (<c>Principal by</c>), nicht nur in der Oberfläche. Schreibt nie in echte Systemorte: der „Autostart“ ist ein Testordner
/// außerhalb der Freigabe.</summary>
[Collection("ChildWrite")]
public class RechteKernFix13
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    sealed class Env : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "trendo-f13-" + Guid.NewGuid().ToString("N"));
        public string Home => Path.Combine(Root, "freigabe");
        public string Autostart => Path.Combine(Root, "AppData-Test", "Startmenue", "Autostart"); // steht für %APPDATA%\…\Startup – nicht freigegeben
        public AuditLog Audit { get; }
        readonly IDisposable _scope;
        public Env() { Directory.CreateDirectory(Home); Directory.CreateDirectory(Autostart); Audit = new AuditLog(Path.Combine(Root, "audit.jsonl")); _scope = ChildWrite.TestScope(Home); }
        public void Dispose() { _scope.Dispose(); try { Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    static CodeKiChange.Proposal Bat(params string[] names) => new(["Startdatei anlegen"], [.. names.Select(n => new CodeKiChange.FileProposal(n, "@echo off\r\necho hallo\r\n"))]);
    static Dictionary<string, IReadOnlySet<int>> All(CodeKiChange.Preview p) => p.Files.ToDictionary(f => f.Path, f => (IReadOnlySet<int>)Enumerable.Range(0, f.Hunks.Count).ToHashSet(), StringComparer.OrdinalIgnoreCase);

    [Fact(DisplayName = "T-0764 M1 (Kern): „Code mit KI“ – Kind legt keine start.bat im Autostart-Testordner an (Vorschau lehnt ab, Übernehmen wirft, keine Datei); README.md in der Freigabe geht; Erwachsene unverändert")]
    public void CodeKi_Kind()
    {
        using var e = new Env();
        // Vorschau: für Kinder abgelehnt – im Autostart und auch in der Freigabe (ausführbare Endung)
        foreach (var root in new[] { e.Autostart, e.Home })
        {
            var pv = CodeKiChange.Build(Principal.ChildProfile, root, Bat("start.bat"));
            Assert.Empty(pv.Files); Assert.Contains(pv.Rejected, r => r.Contains("start.bat"));
        }
        // Übernehmen mit einer (von Erwachsenen gebauten) Vorschau: Kern wirft, nichts geschrieben, protokolliert
        var adultPv = CodeKiChange.Build(Principal.LocalOwner, e.Autostart, Bat("start.bat"));
        Assert.Single(adultPv.Files);
        Assert.Throws<UnauthorizedAccessException>(() => CodeKiChange.Apply(Principal.ChildProfile, e.Audit, e.Autostart, adultPv, All(adultPv), "w", "m"));
        Assert.False(File.Exists(Path.Combine(e.Autostart, "start.bat")));
        var inHome = CodeKiChange.Build(Principal.LocalOwner, e.Home, Bat("start.bat"));
        Assert.Throws<UnauthorizedAccessException>(() => CodeKiChange.Apply(Principal.ChildProfile, e.Audit, e.Home, inHome, All(inHome), "w", "m"));
        Assert.False(File.Exists(Path.Combine(e.Home, "start.bat")));
        Assert.Contains(e.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("start.bat"));
        // Erlaubtes bleibt: Text/Markdown in der Freigabe
        var md = CodeKiChange.Build(Principal.ChildProfile, e.Home, new(["x"], [new("README.md", "# Hallo\n")]));
        Assert.Single(md.Files);
        Assert.NotNull(CodeKiChange.Apply(Principal.ChildProfile, e.Audit, e.Home, md, All(md), "w", "m"));
        Assert.True(File.Exists(Path.Combine(e.Home, "README.md")));
        // Erwachsene: wie bisher (auch außerhalb der Freigabe)
        var step = CodeKiChange.Apply(Principal.LocalOwner, e.Audit, e.Autostart, adultPv, All(adultPv), "w", "m")!;
        Assert.True(File.Exists(Path.Combine(e.Autostart, "start.bat")));
        // Rückgängig/Wiederholen durch ein Kind: nicht im Autostart
        Assert.Throws<UnauthorizedAccessException>(() => CodeKiChange.Write(Principal.ChildProfile, e.Audit, step.Files, e.Autostart, back: true));
        Assert.True(File.Exists(Path.Combine(e.Autostart, "start.bat")));
    }

    [Fact(DisplayName = "T-0764 M2 (Kern): Umbenennen – Kind macht aus „spiel“ kein „spiel.cmd“, nichts außerhalb der Freigabe; Muster ohne neue Endung geht; Erwachsene unverändert")]
    public void Umbenennen_Kind()
    {
        using var e = new Env();
        var dir = Path.Combine(e.Home, "spiele"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "spiel"), "x");
        var undo = Path.Combine(e.Root, "undo");
        var cmd = Renamer.Plan(Directory.GetFiles(dir), "{name}.cmd{ext}");
        Assert.EndsWith("spiel.cmd", cmd[0].To);
        Assert.Throws<UnauthorizedAccessException>(() => Renamer.Apply(Principal.ChildProfile, e.Audit, cmd, undo));
        Assert.True(File.Exists(Path.Combine(dir, "spiel"))); Assert.False(File.Exists(Path.Combine(dir, "spiel.cmd")));
        // doppelte Endung, Punkt am Ende: ebenso gesperrt
        foreach (var pat in new[] { "{name}.png.bat", "{name}.cmd." }) Assert.Throws<UnauthorizedAccessException>(() => Renamer.Apply(Principal.ChildProfile, e.Audit, Renamer.Plan(Directory.GetFiles(dir), pat), undo));
        // erlaubt: ohne neue Endung
        var u = Renamer.Apply(Principal.ChildProfile, e.Audit, Renamer.Plan(Directory.GetFiles(dir), "Spiel_{n:00}"), undo);
        Assert.True(File.Exists(Path.Combine(dir, "Spiel_01")));
        Renamer.Undo(Principal.ChildProfile, e.Audit, u); Assert.True(File.Exists(Path.Combine(dir, "spiel")));
        // außerhalb der Freigabe (Autostart-Testordner): auch harmlose Namen nicht
        File.WriteAllText(Path.Combine(e.Autostart, "notiz.txt"), "x");
        Assert.Throws<UnauthorizedAccessException>(() => Renamer.Apply(Principal.ChildProfile, e.Audit, Renamer.Plan(Directory.GetFiles(e.Autostart), "a_{n}"), undo));
        Assert.True(File.Exists(Path.Combine(e.Autostart, "notiz.txt")));
        // Downloads nach Art sortieren: setup.exe bleibt setup.exe – geht für Kinder in der Freigabe
        var dl = Path.Combine(e.Home, "Downloads"); Directory.CreateDirectory(dl); File.WriteAllText(Path.Combine(dl, "setup.exe"), "x"); File.WriteAllText(Path.Combine(dl, "brief.pdf"), "x");
        var su = Renamer.Apply(Principal.ChildProfile, e.Audit, Web.SortPlan(dl), undo);
        Assert.True(File.Exists(Path.Combine(dl, "Programme", "setup.exe")));
        Renamer.Undo(Principal.ChildProfile, e.Audit, su); Assert.True(File.Exists(Path.Combine(dl, "setup.exe")));
        // Erwachsene: wie bisher
        Renamer.Apply(Principal.LocalOwner, e.Audit, cmd, undo);
        Assert.True(File.Exists(Path.Combine(dir, "spiel.cmd")));
    }

    [Fact(DisplayName = "T-0764 M2 (Kern): Fotos ordnen in einen Zielordner außerhalb der Freigabe und Quarantäne außerhalb – für Kinder abgelehnt, nichts bewegt")]
    public void Fotos_und_Quarantaene_Kind()
    {
        using var e = new Env();
        var src = Path.Combine(e.Home, "fotos"); Directory.CreateDirectory(src); var jpg = Path.Combine(src, "a.jpg"); File.WriteAllBytes(jpg, [1, 2, 3]);
        var plan = new List<Move> { new(jpg, Path.Combine(e.Autostart, "2026", "a.jpg")) };
        foreach (var copy in new[] { true, false })
            Assert.Throws<UnauthorizedAccessException>(() => PhotoOrganizer.Run(Principal.ChildProfile, e.Audit, plan, Path.Combine(e.Root, "undo"), copy));
        Assert.True(File.Exists(jpg)); Assert.False(Directory.Exists(Path.Combine(e.Autostart, "2026")));
        var ok = PhotoOrganizer.Run(Principal.ChildProfile, e.Audit, [new(jpg, Path.Combine(e.Home, "sortiert", "a.jpg"))], Path.Combine(e.Root, "undo"), copy: true);
        Assert.True(File.Exists(Path.Combine(e.Home, "sortiert", "a.jpg")));
        Assert.Equal((1, 0), PhotoOrganizer.Undo(Principal.ChildProfile, e.Audit, ok));
        var data = Path.Combine(e.Root, "daten");
        var fremd = Path.Combine(e.Autostart, "wichtig.lnk"); File.WriteAllText(fremd, "x");
        Assert.Throws<UnauthorizedAccessException>(() => Web.Quarantine(Principal.ChildProfile, data, fremd, e.Audit));
        Assert.True(File.Exists(fremd));
        var boese = Path.Combine(e.Home, "boese.exe"); File.WriteAllText(boese, "x");
        var q = Web.Quarantine(Principal.ChildProfile, data, boese, e.Audit); Assert.False(File.Exists(boese)); // Wegsperren in der Freigabe: erlaubt
        // Zurücklegen einer weggesperrten .exe: nur Erwachsene (es entstünde wieder eine ausführbare Datei)
        Assert.Throws<UnauthorizedAccessException>(() => Web.Restore(Principal.ChildProfile, data, q.Id, e.Audit)); Assert.False(File.Exists(boese));
        Web.Restore(Principal.LocalOwner, data, q.Id, e.Audit); Assert.True(File.Exists(boese));
        var txt = Path.Combine(e.Home, "liste.txt"); File.WriteAllText(txt, "x");
        var q2 = Web.Quarantine(Principal.ChildProfile, data, txt, e.Audit); Web.Restore(Principal.ChildProfile, data, q2.Id, e.Audit); Assert.True(File.Exists(txt));
    }

    [Fact(DisplayName = "T-0764 (13. Prüfrunde): Kern-Helfer für Ordner-Werkzeuge verlangen die Rolle (Principal als erster Parameter, Reflexion)")]
    public void Kern_mit_Rolle()
    {
        var need = new (Type T, string M)[]
        {
            (typeof(Renamer), nameof(Renamer.Apply)), (typeof(Renamer), nameof(Renamer.Undo)),
            (typeof(PhotoOrganizer), nameof(PhotoOrganizer.Run)), (typeof(PhotoOrganizer), nameof(PhotoOrganizer.Undo)),
            (typeof(CodeKiChange), nameof(CodeKiChange.Apply)), (typeof(CodeKiChange), nameof(CodeKiChange.Write)), (typeof(CodeKiChange), nameof(CodeKiChange.Build)),
            (typeof(Web), nameof(Web.Quarantine)), (typeof(Web), nameof(Web.Restore)), (typeof(Archives), nameof(Archives.UnpackAs)),
        };
        foreach (var (t, m) in need)
        {
            var ms = t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == m).ToList();
            Assert.NotEmpty(ms);
            Assert.All(ms, x => Assert.True(x.GetParameters() is [{ ParameterType: var p }, ..] && p == typeof(Principal), $"{t.Name}.{m} ohne Principal"));
        }
    }

    // ───────────── Quelltext-Wächter (Heuristik) ─────────────

    static readonly HashSet<string> Seg = ["folder", "dir", "ordner", "ziel", "target", "path", "pfad", "datei", "file", "root", "src", "source", "dest", "zip", "import", "export", "out", "image", "img", "doc", "csv", "template", "main", "pack"];
    static readonly Regex DKey = new(@"^(root|file|folder|dir|target|source|path|master|image|audio|music|intro|outro|subs|built|godotProject|unityProject|fp_recomp|fp_elf|batchPath|batchFile|gPatternImage|lastLog|lastSaved)$", RegexOptions.IgnoreCase);
    static readonly Regex Guard = new(@"UserFile\(|UserDir\w*\(|UserBeside\(|UserMoves\(|ChildWrite\.|\(CurrentPrincipal,");
    static readonly Regex FieldVar = new(@"var (\w+) = Field\(""([a-z0-9-]+)""");
    static readonly Regex DText = new(@"\bd\.Text\(""([A-Za-z0-9_]+)""");
    // Mitglieder auf Klassenebene (4 Leerzeichen oder Tab), mit oder ohne Zugriffsmodifizierer („Control BcSide()“) – lokale Funktionen zählen zur Methode
    static readonly Regex Hdr = new(@"(?m)^(?:    |\t)(?!(?:return|await|var|if|else|throw|yield|using|foreach|for|while|switch|case|lock|try|catch|finally)\b)(?:(?:public|internal|private|protected|static|async|override|readonly|unsafe|partial|new|virtual|extern)\s+)*[\w<>\[\]?,.() ]*?\s(\w+)\s*\(");
    static readonly Regex Acts = new(@"\b(Act2?|B|Btn|btn)\(""");
    static readonly HashSet<string> Kw = ["if", "for", "foreach", "while", "switch", "catch", "using", "return", "new", "lock", "nameof", "typeof", "when"];

    /// <summary>Zeichenketten und Kommentare durch Leerzeichen ersetzen (gleiche Länge) – Klammern darin zählen nicht.</summary>
    static string Mask(string s)
    {
        var o = s.ToCharArray(); var n = s.Length; var i = 0;
        void Blank(int a, int b) { for (var k = a; k < b && k < n; k++) if (o[k] != '\n') o[k] = ' '; }
        while (i < n)
        {
            var c = s[i];
            if (c == '/' && i + 1 < n && s[i + 1] == '/') { var j = s.IndexOf('\n', i); j = j < 0 ? n : j; Blank(i, j); i = j; continue; }
            if (c == '/' && i + 1 < n && s[i + 1] == '*') { var j = s.IndexOf("*/", i + 2, StringComparison.Ordinal); j = j < 0 ? n : j + 2; Blank(i, j); i = j; continue; }
            if (c == '"' || (c is '$' or '@' && i + 1 < n && s[i + 1] is '"' or '$' or '@'))
            {
                var j = i; var verb = false;
                while (j < n && s[j] is '$' or '@') { verb |= s[j] == '@'; j++; }
                int e;
                if (string.CompareOrdinal(s, j, "\"\"\"", 0, 3) == 0) { e = s.IndexOf("\"\"\"", j + 3, StringComparison.Ordinal); e = e < 0 ? n : e + 3; }
                else
                {
                    e = j + 1;
                    while (e < n)
                    {
                        if (verb && s[e] == '"' && e + 1 < n && s[e + 1] == '"') { e += 2; continue; }
                        if (!verb && s[e] == '\\') { e += 2; continue; }
                        if (s[e] == '"') { e++; break; }
                        e++;
                    }
                }
                Blank(i, e); i = e; continue;
            }
            if (c == '\'')
            {
                var e = i + 1;
                while (e < n && s[e] != '\'') e += s[e] == '\\' ? 2 : 1;
                e++;
                if (e - i <= 8) { Blank(i, e); i = e; continue; }
            }
            i++;
        }
        return new string(o);
    }

    static int Close(string m, int p, char open, char close)
    {
        var d = 0;
        for (var k = p; k < m.Length; k++) { if (m[k] == open) d++; else if (m[k] == close && --d == 0) return k; }
        return m.Length - 1;
    }

    sealed record Meth(string Name, int Start, int Body, int End, bool Expr, string Head);

    static List<Meth> Methods(string s, string m)
    {
        var res = new List<Meth>();
        foreach (Match h in Hdr.Matches(m))
        {
            var name = h.Groups[1].Value; if (Kw.Contains(name)) continue;
            var p = h.Index + h.Length - 1; var q = Close(m, p, '(', ')');
            var rest = m.Substring(q + 1, Math.Min(400, m.Length - q - 1));
            var mm = Regex.Match(rest, @"^[^{;=]*?(=>|\{|;)");
            if (!mm.Success || mm.Groups[1].Value == ";") continue;
            var start = q + 1 + mm.Groups[1].Index;
            if (mm.Groups[1].Value == "=>")
            {
                var d = 0; var k = start;
                for (; k < m.Length; k++) { if (m[k] is '(' or '{') d++; else if (m[k] is ')' or '}') d--; else if (m[k] == ';' && d <= 0) break; }
                res.Add(new(name, h.Index, start, Math.Min(k, m.Length - 1), true, s[h.Index..(q + 1)]));
            }
            else res.Add(new(name, h.Index, start, Close(m, start, '{', '}'), false, s[h.Index..(q + 1)]));
        }
        var outer = new List<Meth>(); var last = -1;
        foreach (var r in res.OrderBy(r => r.Start)) if (r.Start > last) { outer.Add(r); last = r.End; }
        return outer;
    }

    static int InnerBlock(string m, int p, int lo)
    {
        var d = 0;
        for (var k = p; k >= lo; k--) { if (m[k] == '}') d++; else if (m[k] == '{') { if (d == 0) return k; d--; } }
        return lo;
    }

    /// <summary>Alle Lesungen eines Pfades aus einem Oberflächenfeld (Field mit Pfad-Schlüssel, d.Text mit Pfad-Schlüssel, Methoden, die so einen Pfad
    /// liefern) in <c>src/Trendo.App</c> – je „Datei.Methode:Schlüssel“: geschützt (true), wenn die Einheit (Methode; in Ansichten mit Knöpfen der
    /// Knopf-Block bzw. die Anweisung) UserFile/UserDir…/UserBeside/UserMoves/ChildWrite aufruft oder die Rolle an einen Kern-Helfer gibt.</summary>
    internal static SortedDictionary<string, (bool Ok, int Line)> Flows()
    {
        var files = Directory.EnumerateFiles(Repo("src/Trendo.App"), "*.cs").OrderBy(f => f, StringComparer.Ordinal)
            .ToDictionary(f => f, f => { var s = File.ReadAllText(f, Encoding.UTF8).TrimStart('﻿'); return (S: s, M: Mask(s)); });
        var derived = new HashSet<string>();
        foreach (var (_, (s, m)) in files)
            foreach (var x in Methods(s, m))
                if (x.Expr && Regex.IsMatch(x.Head, @"\bstring\??\s+" + x.Name + @"\b") && DText.Matches(s[x.Start..x.End]).Any(t => DKey.IsMatch(t.Groups[1].Value))) derived.Add(x.Name);
        var res = new SortedDictionary<string, (bool, int)>(StringComparer.Ordinal);
        foreach (var (f, (s, m)) in files)
        {
            var shortName = Path.GetFileNameWithoutExtension(f).Replace("MainWindow.", "");
            foreach (var x in Methods(s, m))
            {
                var body = s[x.Start..(x.End + 1)]; var mbody = m[x.Start..(x.End + 1)];
                var view = Acts.Matches(body).Count >= 2;
                var fv = FieldVar.Matches(body).Where(t => t.Groups[2].Value.Split('-').Any(Seg.Contains)).GroupBy(t => t.Groups[1].Value).ToDictionary(g => g.Key, g => g.Last().Groups[2].Value);
                var hits = new List<(int P, string K)>();
                foreach (var (v, k) in fv)
                    foreach (Match t in Regex.Matches(mbody, @"(?:Clean|Need)\(\s*" + v + @"\b|\b" + v + @"\.Text\b")) hits.Add((x.Start + t.Index, k));
                foreach (Match t in DText.Matches(body)) if (DKey.IsMatch(t.Groups[1].Value)) hits.Add((x.Start + t.Index, t.Groups[1].Value));
                foreach (var dm in derived.Where(dm => dm != x.Name))
                    foreach (Match t in Regex.Matches(mbody, @"(?<![.\w])" + dm + @"\(")) hits.Add((x.Start + t.Index, dm + "()"));
                foreach (var (p, k) in hits)
                {
                    string unit;
                    if (view && !x.Expr)
                    {
                        var blk = InnerBlock(m, p, x.Body);
                        if (blk > x.Body) unit = s[blk..(Close(m, blk, '{', '}') + 1)];
                        else
                        {
                            var ls = m.LastIndexOf('\n', p) + 1; var d = 0; var k2 = ls;
                            for (; k2 <= x.End; k2++) { if (m[k2] == '(') d++; else if (m[k2] == ')') d--; else if (m[k2] == ';' && d <= 0) break; }
                            unit = s[ls..Math.Min(s.Length, k2 + 1)];
                        }
                    }
                    else unit = body;
                    var key = $"{shortName}.{x.Name}:{k}"; var line = s.AsSpan(0, p).Count('\n') + 1;
                    var ok = Guard.IsMatch(unit);
                    if (!res.TryGetValue(key, out var old)) res[key] = (ok, line);
                    else if (old.Item1 && !ok) res[key] = (false, line);
                }
            }
        }
        return res;
    }

    /// <summary>Durchgesehen: Pfad aus einem Feld, aber in dieser Einheit ohne ChildWrite – jeweils mit Grund. Verwaiste Einträge machen den Test rot.</summary>
    internal static readonly Dictionary<string, string> FieldIntern = new()
    {
        ["AudioEditor.AuProperties:source"] = "zeigt nur Name/Länge der Aufnahme",
        ["AudioEditor.InitAudioEditor:source"] = "Vorlage, liest",
        ["AudioFoley.AuFoleyAdd:source"] = "liest die Aufnahme, Ergebnis in Trendos Audio-Ordner",
        ["ConsolePort.ConsoleSend:image"] = "nur Text im Auftrag an Claude (Claude: nur Erwachsene)",
        ["ConsolePort.InitConsolePort:image"] = "Vorlage/Prüfung, liest",
        ["DocScan.ScanFolder:folder"] = "liefert nur den Pfad – Aufrufer ScanSavePdf/ScanSearchablePdf prüfen (UserDirFixedNames + UserFile)",
        ["Formulare.FoFields:file"] = "liest das Formular",
        ["Formulare.FoPlace:file"] = "liest (Seitengröße)",
        ["Formulare.FoSave:file"] = "nur der Name; Ziel in Trendos Ordner „Formulare“, FoExport prüft (UserFile)",
        ["Formulare.FoSuggest:file"] = "liest",
        ["Formulare.InitFormulare:file"] = "Vorlage, liest",
        ["FreePort.FpElfSha:fp_elf"] = "liest (Prüfsumme)",
        ["FreePort.FreePortTrial:fp_recomp"] = "nur Erwachsene (DarfAusfuehrenStill, startet das Programm)",
        ["Helfer.EditorView:ed-path"] = "Öffnen liest; Speichern geht über EditorSave (UserFile)",
        ["KreativStudios2.ImgGenerate:dir"] = "nur eine geprüfte Kennung (32 Hex), Ordner unter Trendos ki-bilder",
        ["KreativStudios3.VideoBuild:music"] = "liest die Musik, Video in Trendos Ordner",
        ["KreativStudios4.CaClaudeChangeRun:CaRoot()"] = "Claude ändert nur mit Erwachsenen-Recht (ClaudeRunProcess); Sicherung in Trendos Datenordner",
        ["KreativStudios4.CaFile:CaRoot()"] = "liefert nur den Pfad (im Projektordner)",
        ["KreativStudios4.CaFile:file"] = "liefert nur den Pfad (im Projektordner)",
        ["KreativStudios4.CaRoot:root"] = "liefert nur den Pfad – Schreibstellen prüfen selbst (CkApplyRun, CodeAiSaveFile, Kern CodeKiChange)",
        ["KreativStudios4.CodeAiChanges:CaRoot()"] = "liest (Vergleich mit Schnappschuss)",
        ["KreativStudios4.CodeAiClaudeAccept:CaRoot()"] = "löscht nur Trendos Sicherung",
        ["KreativStudios4.CodeAiClaudeRevert:CaRoot()"] = "stellt nur den Stand vor einem Claude-Lauf (nur Erwachsene) wieder her, Namen aus der Sicherung",
        ["KreativStudios4.CodeAiDiff:CaRoot()"] = "liest",
        ["KreativStudios4.CodeAiOpenFile:CaRoot()"] = "liest (öffnet)",
        ["KreativStudios4.CodeAiRunTests:CaRoot()"] = "nur Erwachsene (DarfAusfuehren)",
        ["KreativStudios4.CodeAiSnapshot:CaRoot()"] = "liest; Schnappschuss in Trendos Datenordner",
        ["KreativStudios4.InitCodeAi:CaRoot()"] = "Anzeige",
        ["KreativStudios4.InitVision:image"] = "Vorlage, liest",
        ["KreativStudios4.VisionOcr:image"] = "liest (Texterkennung)",
        ["ProgGame.GameBuildView:built"] = "Anzeige; Bauordner legt Trendo an (Spiele-Ordner)",
        ["ProgGameBoard.GamePreviewCard:built"] = "Anzeige",
        ["ProgGameEngines.GameEnginesView:godotProject"] = "Anzeige; Engine-Läufe nur Erwachsene (GameEngineTest)",
        ["ProgGameEngines.GameEnginesView:unityProject"] = "Anzeige; Engine-Läufe nur Erwachsene",
        ["ProgGameEngines.GameToEngine:built"] = "liest; Engine-Projekt in Trendos Spiele-Ordner",
        ["ProgGameFree.GameBrowserCheck:built"] = "liest (Prüfung im Browser)",
        ["ProgGameMixamo.MixamoToUnreal:built"] = "Unreal-Lauf nur Erwachsene (EngineRun)",
        ["ProgGameRelease.GameReleaseView:built"] = "Anzeige; Paket über GameUnrealPackage (UserDirFixedNames)",
        ["ProgGameUnreal.GameUnrealCheck:built"] = "nur Erwachsene (Unreal-Lauf)",
        ["ProgGameUnreal.GameUnrealPlay:built"] = "nur Erwachsene (Unreal-Lauf)",
        ["ProgGameUnreal.GameUnrealView:built"] = "Anzeige",
        ["ProgModeGarment.GarmentLook:gPatternImage"] = "liest (Stoffbild)",
        ["ProgModeGarment.GarmentTryOn:gPatternImage"] = "liest (Stoffbild)",
        ["ProgramShell.ProgramWorkParts:prog-saveas-path"] = "geht an ProgramSave (UserFile)",
        ["SimpleMode.GameNextCard:built"] = "Anzeige",
        ["Spec01.ArchProperties:arch-bg-path"] = "liest (Hintergrundbild)",
        ["Spec04.XrView:xr-file-path"] = "liest (Modell öffnen)",
        ["Spec05.SheetProperties:ms-csv-path"] = "liest (CSV)",
        ["Spec09.ScanProperties:pc-cmp-path"] = "liest (zweiter Scan)",
        ["Spec09.ScanProperties:pc-file-path"] = "liest (Scan laden)",
        ["Spec10.TwinView:twin-csv-path"] = "liest (CSV)",
        ["Spec13.MapProperties:mk-gpx-path"] = "liest (GPX)",
        ["Spec13.MapProperties:mk-pack-path"] = "liest (Kartenpaket)",
        ["Spec14.CncProperties:cnc-file-path"] = "liest (G-Code)",
        ["Sys02.AlltagFilesView:du-folder"] = "liest (Speicherplatz)",
        ["Sys02.AlltagFilesView:dup-folder"] = "liest (Doppelte, löscht nichts)",
        ["Sys02.AlltagFilesView:zip-file"] = "merkt nur den getippten Pfad; Entpacken: Archives.UnpackAs mit Rolle",
        ["Sys02.AlltagFilesView:zip-src"] = "merkt nur den getippten Pfad; Packen: UserFile(…zip)",
        ["Sys02.AlltagHelperView:color-file"] = "liest (Pipette)",
        ["Sys03.NetBrowserView:bm-folder"] = "Name einer Lesezeichen-Gruppe, kein Dateipfad",
        ["Sys03.NetBrowserView:bm-import"] = "liest (Lesezeichen-Datei)",
        ["Sys03.NetCheckView:hash-file"] = "liest (Prüfsumme)",
        ["Sys03.NetCheckView:q-file"] = "Defender-Prüfung liest; Wegsperren über Web.Quarantine mit Rolle",
        ["Sys10.DokuPdfView:form-file"] = "liest die Felder; Speichern prüft (UserBeside)",
        ["Sys10.DokuPdfView:meta-file"] = "Barrierefreiheit liest, Bericht in Trendos Dokumente-Ordner; Entfernen prüft (UserBeside)",
        ["Sys10.DokuPdfView:pdf-split-file"] = "liest; Ergebnis in Trendos Dokumente-Ordner, Name „…-seiten-a-b.pdf“",
        ["Sys10.DokuPdfView:pdf-text-file"] = "liest; Ergebnis in Trendos Dokumente-Ordner (.txt/.pdf)",
        ["Sys10.DokuPdfView:print-file"] = "liest (Vorschau/Drucken)",
        ["Sys10.DokuWriteView:docsearch-folder"] = "liest (Suche)",
        ["Sys10.DokuWriteView:merge-csv"] = "liest; Serienbriefe in Trendos Dokumente-Ordner",
        ["Sys10.DokuWriteView:merge-template"] = "liest; Serienbriefe in Trendos Dokumente-Ordner",
        ["Sys10.DokuWriteView:pkg-main"] = "liest; Paket in Trendos Dokumente-Ordner",
        ["Sys10.DokuWriteView:table-file"] = "liest (Tabelle ansehen)",
        ["Sys10.DokuWriteView:tpl-file"] = "liest; Vorlage in Trendos Datenordner",
        ["Sys11.ProgCareView:offline-folder"] = "liest (Prüfsummen); Starten nur nach Bestätigung mit Software-Recht",
        ["Sys11.ProgCheckView:restore-file"] = "liest (Liste)",
        ["Sys12.BedienHelpView:settings-file"] = "liest; übernimmt nur Bedienfelder (A11y.ImportSettings behält Rolle, Person, Projektordner, Sperren – 14. Prüfrunde); die Rolle kommt aus der geschützten Quelle (ProfileRole), settings.json landet in Trendos Datenordner",
        ["TrainBatch.TrainHasBatch:batchFile"] = "liest",
        ["VideoExportSettings.EditExportOptions:subs"] = "liest (Untertitel einbrennen)",
        ["VisionErkennen.VisionRecognizeCurrent:image"] = "liest; Seitendatei prüft VisionRecognizeFile (ChildWrite)",
        ["AudioEditor.AuRendered:source"] = "liest (Wiedergabe)",
        ["AudioQa.AuQaSources:source"] = "liest (Prüfung)",
        ["CodeAiKi.CkSync:CaRoot()"] = "Rückgängig/Wiederholen über CkWrite → Kern CodeKiChange.Write mit Rolle",
        ["ConsolePort.ConsoleDevices:image"] = "Anzeige",
        ["ConsolePort.ConsoleGame:image"] = "Anzeige/Prüfung, liest",
        ["ConsolePort.ConsoleRun:image"] = "Auftrag an Claude (nur Erwachsene), liest",
        ["DocScan.DocScanProperties:ScanFolder()"] = "Anzeige des Ablageorts",
        ["Formulare.FoGo:file"] = "liest (öffnet das Formular)",
        ["Formulare.FoWorkspaceCore:file"] = "Anzeige",
        ["Formulare.FoWorkspaceCore:lastSaved"] = "Anzeige (Ordner zeigen)",
        ["FreePort.FpChecks:fp_elf"] = "liest (Prüfungen)",
        ["FreePort.FpChecks:fp_recomp"] = "liest (Prüfungen)",
        ["FreePort.FpCleanArea:fp_recomp"] = "Anzeige; Entfernen über FreePortCleanOld (UserDirFixedNames)",
        ["FreePort.FpJob:fp_elf"] = "liest; Lauf nur Erwachsene (FreePortStart)",
        ["FreePort.FpJob:fp_recomp"] = "liest; Lauf nur Erwachsene (FreePortStart)",
        ["FreePort.FreePortStatus:fp_recomp"] = "Anzeige",
        ["FreePort.FreePortView:fp_elf"] = "Anzeige",
        ["FreePort.FreePortView:fp_recomp"] = "Anzeige",
        ["Helfer.NbTools:nb-image-path"] = "liest; fügt nur einen Bild-Verweis in die Notiz ein",
        ["KreativStudios2.ImgDir:dir"] = "nur eine geprüfte Kennung (32 Hex), Ordner unter Trendos ki-bilder",
        ["KreativStudios2.ImgGenerate:ImgDir()"] = "Trendos Ordner ki-bilder",
        ["KreativStudios2.ImgList:ImgDir()"] = "liest (Trendos Ordner)",
        ["KreativStudios2.PhotosGrid:target"] = "Anzeige",
        ["KreativStudios2.PhotosWorkspace:lastLog"] = "Anzeige",
        ["KreativStudios2.PhotosWorkspace:target"] = "Anzeige",
        ["KreativStudios3.VideoAiWorkspace:music"] = "Anzeige",
        ["KreativStudios3.VideoDir:dir"] = "nur eine geprüfte Kennung (32 Hex), Ordner unter Trendos ki-videos",
        ["KreativStudios3.VideoImages:VideoDir()"] = "Trendos Ordner ki-videos",
        ["KreativStudios3.VideoOwn:VideoDir()"] = "liest (Prüfung, eigener Ordner)",
        ["KreativStudios3.VideoOwnImage:VideoDir()"] = "liest (Prüfung, eigener Ordner)",
        ["KreativStudios3.VideoOwnMedia:VideoDir()"] = "liest (Prüfung, eigener Ordner)",
        ["KreativStudios4.CaClaude:CaRoot()"] = "Auftrag an Claude (nur Erwachsene)",
        ["KreativStudios4.CaExcerpt:CaRoot()"] = "liest (Ausschnitt für die KI)",
        ["KreativStudios4.CaKind:CaRoot()"] = "liest (Projektart)",
        ["KreativStudios4.CaReport:CaRoot()"] = "Anzeige",
        ["KreativStudios4.CaSnapshot:CaRoot()"] = "liest (Trendos Schnappschuss)",
        ["KreativStudios4.CaState:CaRoot()"] = "liest (Baum)",
        ["KreativStudios4.CaTestClick:CaRoot()"] = "Testlauf nur Erwachsene (CodeAiRunTests, DarfAusfuehren)",
        ["KreativStudios4.CodeAiWorkspace:CaRoot()"] = "Anzeige",
        ["KreativStudios4.ViClaude:image"] = "Bild an Claude (nur Erwachsene), liest",
        ["KreativStudios4.VisionWorkspace:folder"] = "Anzeige",
        ["KreativStudios4.VisionWorkspace:image"] = "Anzeige",
        ["Library3D.BibSide:bib-import-path"] = "liest; Import kopiert in Trendos 3D-Bibliothek",
        ["PodcastStudio.PodcRss:audio"] = "liest; Feed-Datei aus der Export-Liste (Trendos Projektordner, feste Endung)",
        ["PodcastStudio.PodcRss:master"] = "liest",
        ["PodcastStudio.PodcWorkspace:intro"] = "Anzeige",
        ["PodcastStudio.PodcWorkspace:master"] = "Anzeige",
        ["PodcastStudio.PodcWorkspace:outro"] = "Anzeige",
        ["ProgGame.GamePlay:built"] = "startet die Vorschau, schreibt nicht",
        ["ProgGameRelease.GameReleasePack:built"] = "liest; Pakete in Trendos Ordner „Spiele/Veröffentlicht“",
        ["ProgGameStage3.GameEngineStatus:built"] = "liest (Prüfbericht)",
        ["ProgGameStage3.GameEngineStatus:godotProject"] = "liest (Prüfbericht)",
        ["ProgGameStage3.GameEngineStatus:unityProject"] = "liest (Prüfbericht)",
        ["ProgGameStage3.GameRunsPanel:built"] = "Anzeige",
        ["TrainBatch.TrainBatchCard:TrainBatchFile()"] = "Trendos Ordner (Name geprüft)",
        ["TrainBatch.TrainBatchCsv:TrainBatchFile()"] = "liest (Trendos Ordner)",
        ["TrainBatch.TrainBatchFile:batchFile"] = "nur ein geprüfter Name (TrainBatchName) in Trendos Ordner",
        ["TrainBatch.TrainHeaderToggled:batchPath"] = "liest die Quelle erneut",
        ["TrainBatch.TrainWriteBatch:TrainBatchFile()"] = "Trendos Ordner, Name geprüft",
        ["VideoExportSettings.EditExportSettings:subs"] = "Anzeige",
        ["VideoKiTon.VideoMoveScene:VideoDir()"] = "Trendos Ordner ki-videos",
        ["VideoKiTon.VideoMusicPick:VideoDir()"] = "Trendos Ordner ki-videos",
        ["VideoKiTon.VideoVoices:VideoDir()"] = "Trendos Ordner ki-videos",
    };

    [Fact(DisplayName = "T-0764 M (Wächter 2, Heuristik): jede Lesung eines Pfades aus einem Feld/Dokument (Field, d.Text, liefernde Methoden) geht in ihrer Einheit an ChildWrite – oder ist begründet ausgenommen")]
    public void Feldfluesse_geprueft()
    {
        var flows = Flows();
        var bad = flows.Where(f => !f.Value.Ok && !FieldIntern.ContainsKey(f.Key)).Select(f => $"{f.Key} (Zeile {f.Value.Line})").ToList();
        Assert.True(bad.Count == 0, "Pfad aus Feld ohne ChildWrite: " + string.Join(", ", bad));
        var orphan = FieldIntern.Keys.Where(k => !flows.TryGetValue(k, out var v) || v.Ok).ToList();
        Assert.True(orphan.Count == 0, "verwaiste oder inzwischen geschützte Ausnahmen: " + string.Join(", ", orphan));
        // die Stellen aus dem Befund sind geschützt
        foreach (var k in new[] { "CodeAiKi.CkApplyRun:CaRoot()", "Sys02.AlltagFilesView:ren-folder", "Sys02.AlltagFilesView:photo-folder", "Sys03.NetLoadView:dlsort-folder", "KreativStudios2.PhotosApply:target", "KreativStudios2.PhotosScan:target" })
            Assert.True(flows.TryGetValue(k, out var v) && v.Ok, k);
    }
}
