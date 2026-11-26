using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Buero;

namespace Trendo.Core.Wissen;

// ===================== STUDIO-COURSE-01 Kurse erstellen (T-0779) =====================
// Eigener Kern (Clean-Room, Anforderungen in docs/nachbau/kurse/anforderungen.md): Kapitel → Lektionen → Bausteine (Text, Überschrift, Liste, Hinweis, Bild, Quiz).
// Quiz-Bausteine sind vollständige Quizze aus „Quiz erstellen“ (<see cref="Quiz"/>) und laufen mit demselben Spieler.

/// <summary>Ein Baustein einer Lektion. <see cref="Art"/>: text, ueberschrift, liste (eine Zeile je Punkt), hinweis, bild, quiz.</summary>
public sealed class KursBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Art { get; set; } = "text";
    public string Text { get; set; } = "";
    /// <summary>Bild: Pfad der Bilddatei (wird beim Export in die Seite eingebettet).</summary>
    public string Bild { get; set; } = "";
    public Quiz? Quiz { get; set; }
}

public sealed class KursLektion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Titel { get; set; } = "Neue Lektion";
    public int Minuten { get; set; } = 5;
    public List<KursBlock> Bloecke { get; set; } = [];
}

public sealed class KursKapitel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Titel { get; set; } = "Neues Kapitel";
    public List<KursLektion> Lektionen { get; set; } = [];
}

/// <summary>Eine lernende Person mit ihrem Fortschritt: erledigte Lektionen und Quiz-Ergebnisse (Baustein → Prozent).</summary>
public sealed class KursLernender
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public List<string> Erledigt { get; set; } = [];
    public Dictionary<string, int> Quiz { get; set; } = [];
    public string Zuletzt { get; set; } = "";
    public string Datum { get; set; } = "";
}

public sealed class Kurs
{
    public string Titel { get; set; } = "Neuer Kurs";
    public string Beschreibung { get; set; } = "";
    public List<KursKapitel> Kapitel { get; set; } = [];
    public List<KursLernender> Lernende { get; set; } = [];
    /// <summary>Quiz in einer Lektion gilt ab so viel Prozent als bestanden.</summary>
    public int Bestehen { get; set; } = 60;
    /// <summary>Lektionen nur der Reihe nach (die nächste öffnet sich, wenn die vorige erledigt ist).</summary>
    public bool Reihenfolge { get; set; } = true;

    internal static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // Umlaute lesbar; '</' entschärft QuizHtml.ScriptJson
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static Kurs FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<Kurs>(O) ?? new(); } catch (JsonException) { return new(); }
    }
    public string ToJsonText(bool ohneLernende = false)
    {
        var j = ToJson(); if (ohneLernende) j["lernende"] = new JsonArray();
        return j.ToJsonString(O);
    }
}

public static class KursKit
{
    public static readonly (string Id, string Name)[] Arten = [("text", "Text"), ("ueberschrift", "Überschrift"), ("liste", "Liste"), ("hinweis", "Hinweis"), ("bild", "Bild"), ("quiz", "Quiz")];
    public static string ArtName(string id) => Arten.FirstOrDefault(a => a.Id == id).Name ?? id;

    public static List<(KursKapitel Kapitel, KursLektion Lektion)> Lektionen(Kurs k) => [.. k.Kapitel.SelectMany(c => c.Lektionen.Select(l => (c, l)))];
    public static KursLektion? Lektion(Kurs k, string id) => Lektionen(k).FirstOrDefault(x => x.Lektion.Id == id).Lektion;
    public static int Minuten(Kurs k) => Lektionen(k).Sum(x => x.Lektion.Minuten);

    /// <summary>Quiz-Bausteine einer Lektion.</summary>
    public static IEnumerable<KursBlock> Quizze(KursLektion l) => l.Bloecke.Where(b => b.Art == "quiz" && b.Quiz is not null);

    /// <summary>Ist die Lektion für diese Person offen? Mit „der Reihe nach“ erst, wenn die vorige erledigt ist.</summary>
    public static bool Offen(Kurs k, KursLernender p, string lektionId)
    {
        if (!k.Reihenfolge) return true;
        var all = Lektionen(k); var i = all.FindIndex(x => x.Lektion.Id == lektionId);
        return i <= 0 || p.Erledigt.Contains(all[i - 1].Lektion.Id);
    }

    /// <summary>Fehlt noch etwas, damit die Lektion als erledigt zählt? (alle Quizze bestanden) – null, wenn sie abgeschlossen werden kann.</summary>
    public static string? Fehlt(Kurs k, KursLernender p, KursLektion l)
    {
        if (!Offen(k, p, l.Id)) return "Erst die vorige Lektion abschließen.";
        foreach (var b in Quizze(l))
            if (!p.Quiz.TryGetValue(b.Id, out var pct) || pct < k.Bestehen) return $"Das Quiz „{b.Quiz!.Titel}“ braucht mindestens {k.Bestehen} %.";
        return null;
    }

    /// <summary>Quiz-Ergebnis eintragen (das beste zählt).</summary>
    public static void QuizErgebnis(KursLernender p, string blockId, int prozent, DateTime? wann = null)
    {
        p.Quiz[blockId] = Math.Max(p.Quiz.GetValueOrDefault(blockId, -1), Math.Clamp(prozent, 0, 100));
        p.Datum = (wann ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Lektion abschließen. Wirft mit einem Satz für die Person, wenn noch etwas fehlt.</summary>
    public static void Abschliessen(Kurs k, KursLernender p, string lektionId, DateTime? wann = null)
    {
        var l = Lektion(k, lektionId) ?? throw new ArgumentException("Diese Lektion gibt es nicht.");
        if (Fehlt(k, p, l) is { } why) throw new InvalidOperationException(why);
        if (!p.Erledigt.Contains(l.Id)) p.Erledigt.Add(l.Id);
        p.Zuletzt = l.Id; p.Datum = (wann ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public sealed record Stand(int Erledigt, int Gesamt, int Prozent, int? QuizSchnitt, bool Fertig);

    public static Stand Fortschritt(Kurs k, KursLernender p)
    {
        var all = Lektionen(k); var done = all.Count(x => p.Erledigt.Contains(x.Lektion.Id));
        var qids = all.SelectMany(x => Quizze(x.Lektion)).Select(b => b.Id).ToList();
        var got = qids.Where(p.Quiz.ContainsKey).Select(id => p.Quiz[id]).ToList();
        return new(done, all.Count, all.Count == 0 ? 0 : (int)Math.Round(100.0 * done / all.Count), got.Count == 0 ? null : (int)Math.Round(got.Average()), all.Count > 0 && done == all.Count);
    }

    public static List<CvFinding> Pruefen(Kurs k)
    {
        var r = new List<CvFinding>();
        if (k.Titel.Trim().Length == 0) r.Add(new(true, "Kurs", "Der Kurs hat keinen Titel."));
        if (k.Kapitel.Count == 0) r.Add(new(true, "Kurs", "Es gibt noch kein Kapitel."));
        if (k.Bestehen is < 0 or > 100) r.Add(new(true, "Kurs", "„Quiz bestanden ab“ muss zwischen 0 und 100 % liegen."));
        foreach (var c in k.Kapitel)
        {
            if (c.Lektionen.Count == 0) r.Add(new(true, c.Titel, "Das Kapitel hat noch keine Lektion."));
            foreach (var l in c.Lektionen)
            {
                var n = $"{c.Titel} › {l.Titel}";
                if (l.Titel.Trim().Length == 0) r.Add(new(true, c.Titel, "Eine Lektion hat keinen Titel."));
                if (l.Bloecke.Count == 0) r.Add(new(true, n, "Die Lektion ist leer."));
                foreach (var b in l.Bloecke)
                {
                    if (b.Art == "bild" && (b.Bild.Length == 0 || !File.Exists(b.Bild))) r.Add(new(true, n, "Ein Bild fehlt oder die Datei gibt es nicht mehr."));
                    if (b.Art == "quiz" && b.Quiz is null) r.Add(new(true, n, "Ein Quiz-Baustein hat kein Quiz."));
                    if (b.Art == "quiz" && b.Quiz is { } q && QuizKit.Pruefen(q).FirstOrDefault(x => x.Fehler) is { } e) r.Add(new(true, n, $"Quiz „{q.Titel}“: {e.Feld} – {e.Text}"));
                    if (b.Art is "text" or "ueberschrift" or "liste" or "hinweis" && b.Text.Trim().Length == 0) r.Add(new(false, n, $"Ein {ArtName(b.Art)}-Baustein ist leer."));
                }
            }
        }
        return r;
    }

    /// <summary>Lektion aus einfachem Text: „## Überschrift“, „- Punkt“ (Liste), „! Hinweis“, sonst Absätze (Leerzeile trennt).</summary>
    public static List<KursBlock> AusText(string text)
    {
        var res = new List<KursBlock>(); var para = new List<string>(); var list = new List<string>();
        void Flush()
        {
            if (para.Count > 0) { res.Add(new KursBlock { Art = "text", Text = string.Join(" ", para) }); para.Clear(); }
            if (list.Count > 0) { res.Add(new KursBlock { Art = "liste", Text = string.Join("\n", list) }); list.Clear(); }
        }
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim();
            if (l.Length == 0) { Flush(); continue; }
            if (l.StartsWith('#')) { Flush(); res.Add(new KursBlock { Art = "ueberschrift", Text = l.TrimStart('#').Trim() }); continue; }
            if (l.StartsWith("- ", StringComparison.Ordinal) || l.StartsWith("* ", StringComparison.Ordinal) || l.StartsWith("• ", StringComparison.Ordinal)) { if (para.Count > 0) { res.Add(new KursBlock { Art = "text", Text = string.Join(" ", para) }); para.Clear(); } list.Add(l[2..].Trim()); continue; }
            if (l.StartsWith("! ", StringComparison.Ordinal) || l.StartsWith("Hinweis:", StringComparison.OrdinalIgnoreCase)) { Flush(); res.Add(new KursBlock { Art = "hinweis", Text = l[(l.IndexOf(l.StartsWith('!') ? ' ' : ':') + 1)..].Trim() }); continue; }
            if (list.Count > 0) Flush();
            para.Add(l);
        }
        Flush();
        return res;
    }

    /// <summary>Kurs aus einer Gliederung: „# Kapitel“ beginnt ein Kapitel, „## Lektion“ eine Lektion, der Text darunter wird zu Bausteinen (<see cref="AusText"/>).
    /// Ohne „#“-Zeilen wird alles eine Lektion.</summary>
    public static Kurs AusGliederung(string titel, string text)
    {
        var k = new Kurs { Titel = (titel ?? "").Trim().Length > 0 ? titel.Trim() : "Neuer Kurs" };
        KursKapitel? cap = null; KursLektion? les = null; var buf = new StringBuilder();
        void Close() { if (les is not null) { les.Bloecke = AusText(buf.ToString()); les.Minuten = Math.Clamp(buf.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 150 + 2, 2, 60); } buf.Clear(); }
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith("## ", StringComparison.Ordinal) && !l.StartsWith("###", StringComparison.Ordinal))
            {
                Close(); if (cap is null) { cap = new KursKapitel { Titel = "Kapitel 1" }; k.Kapitel.Add(cap); }
                les = new KursLektion { Titel = l[3..].Trim() }; cap.Lektionen.Add(les); continue;
            }
            if (l.StartsWith("# ", StringComparison.Ordinal)) { Close(); les = null; cap = new KursKapitel { Titel = l[2..].Trim() }; k.Kapitel.Add(cap); continue; }
            if (les is null)
            {
                if (l.Length == 0) continue;
                if (cap is null) { cap = new KursKapitel { Titel = "Kapitel 1" }; k.Kapitel.Add(cap); }
                les = new KursLektion { Titel = $"Lektion {cap.Lektionen.Count + 1}" }; cap.Lektionen.Add(les);
            }
            buf.AppendLine(raw);
        }
        Close();
        return k;
    }

    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    /// <summary>Fortschritt aller Lernenden als CSV (Semikolon, UTF-8 mit BOM – öffnet sich in Excel und LibreOffice Calc).</summary>
    public static string Csv(Kurs k)
    {
        var sb = new StringBuilder("Name;Erledigt;Lektionen;Fortschritt %;Quiz-Schnitt %;Abgeschlossen;Zuletzt;Datum\n");
        foreach (var p in k.Lernende)
        {
            var s = Fortschritt(k, p);
            var last = Lektion(k, p.Zuletzt)?.Titel ?? "";
            sb.Append(Q(p.Name)).Append(';').Append(s.Erledigt).Append(';').Append(s.Gesamt).Append(';').Append(s.Prozent).Append(';').Append(s.QuizSchnitt?.ToString(CultureInfo.InvariantCulture) ?? "")
              .Append(';').Append(s.Fertig ? "ja" : "nein").Append(';').Append(Q(last)).Append(';').Append(p.Datum).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Malkurs aus dem Künstlerstudio (<see cref="Kreativ.Courses"/>) als Kurs: jeder Schritt eine Lektion mit Aufgabe und Tipp, am Ende ein kurzes Quiz.</summary>
    public static Kurs AusMalkurs(string id)
    {
        var c = Kreativ.Courses.All.FirstOrDefault(x => x.Id == id) ?? Kreativ.Courses.All[0];
        var k = new Kurs { Titel = "Malkurs: " + c.Title, Beschreibung = $"{c.Goal} – Stufe {c.Level}. Die Übungen machst du im Künstlerstudio." };
        var cap = new KursKapitel { Titel = c.Title };
        for (var i = 0; i < c.Steps.Count; i++)
            cap.Lektionen.Add(new KursLektion { Titel = $"Schritt {i + 1}", Minuten = 5, Bloecke = [new() { Art = "text", Text = c.Steps[i].Text }, new() { Art = "hinweis", Text = c.Steps[i].Hint }] });
        var quiz = new Quiz { Titel = "Kurz geprüft", FragenMischen = false, Fragen = [new QuizFrage { Typ = "wahrfalsch", Text = "Ebenen liegen wie Folien übereinander – was oben liegt, verdeckt, was darunter liegt.", Antworten = QuizKit.WahrFalsch(true) }] };
        cap.Lektionen[^1].Bloecke.Add(new KursBlock { Art = "quiz", Quiz = quiz });
        k.Kapitel.Add(cap);
        return k;
    }
}

/// <summary>Kurs als Webseite: eine einzige HTML-Datei mit Gliederung, Lektionen, eingebetteten Bildern und Quizzen (Quiz-Spieler aus <see cref="QuizHtml"/>).
/// Der Fortschritt bleibt im Browser (je Name) – im SCORM-Paket meldet die Seite ihn zusätzlich an die Lernplattform (SCORM 1.2: Status, Punkte, Lesezeichen, Stand).</summary>
public static class KursHtml
{
    const string Css = """
.k-top{display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap;margin-bottom:14px}.k-grid{display:grid;grid-template-columns:260px 1fr;gap:16px;max-width:1100px;margin:0 auto;padding:20px 16px}
@media(max-width:760px){.k-grid{grid-template-columns:1fr}}.k-nav{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:12px;align-self:start}
.k-nav h3{font-size:13px;color:var(--muted);text-transform:uppercase;letter-spacing:.04em;margin:12px 0 6px}.k-nav button{display:block;width:100%;text-align:left;margin:3px 0;padding:8px 10px;font-size:14px}
.k-nav button.cur{border-color:var(--gold)}.k-nav button.done::before{content:"✓ ";color:var(--ok)}.k-nav button.lock{opacity:.5}.k-prog{height:8px;background:#2a2218;border-radius:4px;overflow:hidden;margin:6px 0}
.k-prog i{display:block;height:100%;background:var(--gold)}.k-note{border-left:4px solid var(--gold);background:#231c12;padding:10px 14px;border-radius:8px;margin:12px 0}.k-body img{max-width:100%;border-radius:10px}
.k-body p{margin:0 0 12px}.k-body ul{margin:0 0 12px 20px}
""";

    const string Js = """
var K=JSON.parse(document.getElementById('k-data').textContent);var API=null;
function kFindApi(w){for(var n=0;w&&n<10;n++){if(w.API)return w.API;if(w.parent===w)break;w=w.parent;}return null;}
if(K.scorm){API=kFindApi(window)||(window.opener?kFindApi(window.opener):null);if(API){try{API.LMSInitialize('');}catch(e){API=null;}}}
var lessons=[];K.kapitel.forEach(function(c){c.lektionen.forEach(function(l){lessons.push({kap:c,l:l});});});
var who=null,st=null,cur=0;
function kKey(){return 'trendo-kurs:'+K.titel+':'+who;}
function kLoad(){st={erledigt:[],quiz:{}};try{var s=localStorage.getItem(kKey());if(s)st=JSON.parse(s);}catch(e){}
 if(API){try{var sd=API.LMSGetValue('cmi.suspend_data');if(sd){var o=JSON.parse(sd);st.erledigt=o.erledigt||st.erledigt;st.quiz=o.quiz||st.quiz;}}catch(e){}}}
function kSave(){try{localStorage.setItem(kKey(),JSON.stringify(st));}catch(e){}
 if(API){var done=lessons.filter(function(x){return st.erledigt.indexOf(x.l.id)>=0;}).length;var qs=Object.keys(st.quiz).map(function(k){return st.quiz[k];});
  var score=qs.length?Math.round(qs.reduce(function(a,b){return a+b;},0)/qs.length):Math.round(100*done/Math.max(1,lessons.length));
  try{API.LMSSetValue('cmi.suspend_data',JSON.stringify(st).slice(0,4000));API.LMSSetValue('cmi.core.lesson_location',lessons[cur]?lessons[cur].l.id:'');
  API.LMSSetValue('cmi.core.score.raw',String(score));API.LMSSetValue('cmi.core.score.min','0');API.LMSSetValue('cmi.core.score.max','100');
  API.LMSSetValue('cmi.core.lesson_status',done===lessons.length?(score>=K.bestehen?'passed':'failed'):'incomplete');API.LMSCommit('');}catch(e){}}}
function kOpen(i){if(!K.reihenfolge||i===0)return true;return st.erledigt.indexOf(lessons[i-1].l.id)>=0;}
function kMissing(i){if(!kOpen(i))return 'Erst die vorige Lektion abschließen.';var m=null;lessons[i].l.bloecke.forEach(function(b){if(m)return;if(b.art==='quiz'&&b.quiz){var p=st.quiz[b.id];if(p==null||p<K.bestehen)m='Das Quiz „'+b.quiz.titel+'“ braucht mindestens '+K.bestehen+' %.';}});return m;}
function kNav(){var n=document.getElementById('k-nav');n.innerHTML='';var done=lessons.filter(function(x){return st.erledigt.indexOf(x.l.id)>=0;}).length;
 n.appendChild(tqEl('div','tq-muted',who+' · '+done+' von '+lessons.length+' Lektionen'));var pb=tqEl('div','k-prog');var pi=tqEl('i');pi.style.width=Math.round(100*done/Math.max(1,lessons.length))+'%';pb.appendChild(pi);n.appendChild(pb);
 var last=null;lessons.forEach(function(x,i){if(x.kap!==last){n.appendChild(tqEl('h3',null,x.kap.titel));last=x.kap;}var b=tqEl('button',(i===cur?'cur ':'')+(st.erledigt.indexOf(x.l.id)>=0?'done ':'')+(kOpen(i)?'':'lock'),x.l.titel);b.dataset.l=x.l.id;b.onclick=function(){if(kOpen(i)){cur=i;kShow();}};n.appendChild(b);});}
function kShow(){kNav();var m=document.getElementById('k-main');m.innerHTML='';var x=lessons[cur];if(!x){m.appendChild(tqEl('p',null,'Dieser Kurs hat noch keine Lektion.'));return;}
 var c=tqEl('div','tq-card k-body');c.appendChild(tqEl('div','tq-muted',x.kap.titel+' · etwa '+x.l.minuten+' Minuten'));c.appendChild(tqEl('h1',null,x.l.titel));
 x.l.bloecke.forEach(function(b){if(b.art==='ueberschrift')c.appendChild(tqEl('h2',null,b.text));else if(b.art==='liste'){var u=tqEl('ul');b.text.split('\n').filter(function(s){return s.trim();}).forEach(function(s){u.appendChild(tqEl('li',null,s));});c.appendChild(u);}
  else if(b.art==='hinweis')c.appendChild(tqEl('div','k-note',b.text));else if(b.art==='bild'){if(K.bilder[b.id]){var im=tqEl('img');im.src=K.bilder[b.id];im.alt=b.text||'';c.appendChild(im);if(b.text)c.appendChild(tqEl('div','tq-muted',b.text));}}
  else if(b.art==='quiz'&&b.quiz){var box=tqEl('div');box.className='k-quiz';box.dataset.b=b.id;c.appendChild(box);if(st.quiz[b.id]!=null)c.appendChild(tqEl('div','tq-muted','Bisher bestes Ergebnis: '+st.quiz[b.id]+' %'));
   TrendoQuiz(box,b.quiz,{auto:location.hash==='#selbsttest',onDone:function(r){st.quiz[b.id]=Math.max(st.quiz[b.id]||0,r.prozent);kSave();kFoot();}});}
  else c.appendChild(tqEl('p',null,b.text));});
 m.appendChild(c);var f=tqEl('div','tq-card');f.id='k-foot';m.appendChild(f);kFoot();if(API){kSave();}}
function kFoot(){var f=document.getElementById('k-foot');if(!f)return;f.innerHTML='';var miss=kMissing(cur);var done=st.erledigt.indexOf(lessons[cur].l.id)>=0;
 if(done)f.appendChild(tqEl('p',null,'✓ Diese Lektion ist erledigt.'));else if(miss)f.appendChild(tqEl('p','tq-muted',miss));
 var row=tqEl('div','tq-row');var b=tqEl('button','tq-main',done?(cur+1<lessons.length?'Nächste Lektion':'Kurs abgeschlossen'):'Lektion abschließen');b.id='k-done';b.disabled=!done&&!!miss;
 b.onclick=function(){if(!done){st.erledigt.push(lessons[cur].l.id);kSave();}if(cur+1<lessons.length){cur++;kShow();}else{kShow();}};row.appendChild(b);f.appendChild(row);
 var all=lessons.filter(function(x){return st.erledigt.indexOf(x.l.id)>=0;}).length;document.body.setAttribute('data-k-done',all);}
function kStart(){var m=document.getElementById('k-main');m.innerHTML='';var c=tqEl('div','tq-card');c.appendChild(tqEl('h1',null,K.titel));if(K.beschreibung)c.appendChild(tqEl('p',null,K.beschreibung));
 c.appendChild(tqEl('p','tq-muted',lessons.length+' Lektionen · etwa '+lessons.reduce(function(a,x){return a+x.l.minuten;},0)+' Minuten'));var i=tqEl('input');i.type='text';i.id='k-name';i.placeholder='Dein Name';
 var b=tqEl('button','tq-main','Kurs beginnen');b.id='k-begin';b.onclick=function(){who=(i.value||'').trim()||'Gast';kLoad();cur=0;for(var j=0;j<lessons.length;j++){if(st.erledigt.indexOf(lessons[j].l.id)<0){cur=j;break;}}kShow();};
 var row=tqEl('div','tq-row');row.appendChild(i);row.appendChild(b);c.appendChild(row);m.appendChild(c);
 if(API){try{var n=API.LMSGetValue('cmi.core.student_name');if(n){i.value=n;}}catch(e){}}if(location.hash==='#selbsttest'){i.value='Selbsttest';b.click();kAuto();}}
function kAuto(){var guard=0;function step(){if(guard++>500)return;var btn=document.getElementById('k-done');if(btn&&!btn.disabled){var last=cur+1>=lessons.length&&st.erledigt.indexOf(lessons[cur].l.id)>=0;if(last){document.body.setAttribute('data-k-selftest','fertig');return;}btn.click();}setTimeout(step,10);}setTimeout(step,10);}
window.addEventListener('beforeunload',function(){if(API){try{kSave();API.LMSFinish('');}catch(e){}}});
kStart();
""";

    static string Mime(string file) => Path.GetExtension(file).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml", _ => "application/octet-stream" };

    public static string Html(Kurs k, bool scorm = false)
    {
        var data = k.ToJson(); data["lernende"] = new JsonArray(); data["scorm"] = scorm;
        var pics = new JsonObject();
        foreach (var (_, l) in KursKit.Lektionen(k))
            foreach (var b in l.Bloecke.Where(b => b.Art == "bild" && b.Bild.Length > 0 && File.Exists(b.Bild)))
                pics[b.Id] = $"data:{Mime(b.Bild)};base64,{Convert.ToBase64String(File.ReadAllBytes(b.Bild))}";
        data["bilder"] = pics;
        foreach (var c in (data["kapitel"] as JsonArray)!.OfType<JsonObject>()) foreach (var l in (c["lektionen"] as JsonArray)!.OfType<JsonObject>()) foreach (var b in (l["bloecke"] as JsonArray)!.OfType<JsonObject>()) b["bild"] = ""; // keine lokalen Pfade weitergeben
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<meta name=\"generator\" content=\"Trendo OS – Kurse erstellen\"><title>").Append(WebUtility.HtmlEncode(k.Titel)).Append("</title><style>").Append(QuizHtml.Css).Append(Css).Append("</style></head>\n<body>");
        sb.Append("<div class=\"k-grid\"><nav class=\"k-nav\" id=\"k-nav\"></nav><main id=\"k-main\"><noscript>Für diesen Kurs muss JavaScript an sein.</noscript></main></div>\n");
        sb.Append("<script id=\"k-data\" type=\"application/json\">").Append(QuizHtml.ScriptJson(data.ToJsonString(Kurs.O))).Append("</script>\n<script>").Append(QuizHtml.PlayerJs).Append('\n').Append(Js).Append("</script></body></html>\n");
        return sb.ToString();
    }

    public static void Write(Kurs k, string path) => AtomicFile.WriteAllText(path, Html(k));

    /// <summary>SCORM-1.2-Manifest (ein SCO, eine Organisation) – so lesen es Moodle, ILIAS und andere Lernplattformen.</summary>
    public static string Manifest(Kurs k, string id)
    {
        var t = WebUtility.HtmlEncode(k.Titel);
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<manifest identifier="{id}" version="1.0"
  xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2"
  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="http://www.imsproject.org/xsd/imscp_rootv1p1p2 imscp_rootv1p1p2.xsd http://www.imsglobal.org/xsd/imsmd_rootv1p2p1 imsmd_rootv1p2p1.xsd http://www.adlnet.org/xsd/adlcp_rootv1p2 adlcp_rootv1p2.xsd">
  <metadata>
    <schema>ADL SCORM</schema>
    <schemaversion>1.2</schemaversion>
  </metadata>
  <organizations default="org-1">
    <organization identifier="org-1">
      <title>{t}</title>
      <item identifier="item-1" identifierref="res-1" isvisible="true">
        <title>{t}</title>
        <adlcp:masteryscore>{Math.Clamp(k.Bestehen, 0, 100)}</adlcp:masteryscore>
      </item>
    </organization>
  </organizations>
  <resources>
    <resource identifier="res-1" type="webcontent" adlcp:scormtype="sco" href="index.html">
      <file href="index.html"/>
    </resource>
  </resources>
</manifest>
""";
    }

    /// <summary>SCORM-1.2-Paket (ZIP mit imsmanifest.xml und index.html) zum Hochladen in eine Lernplattform.</summary>
    public static void WriteScorm(Kurs k, string path)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var id = "trendo-kurs-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(k.Titel)))[..12].ToLowerInvariant();
            void Add(string name, string text) { var e = zip.CreateEntry(name, CompressionLevel.Optimal); e.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); using var s = e.Open(); var b = new UTF8Encoding(false).GetBytes(text); s.Write(b, 0, b.Length); }
            Add("imsmanifest.xml", Manifest(k, id));
            Add("index.html", Html(k, scorm: true));
        }
        AtomicFile.WriteAllBytes(path, ms.ToArray());
    }
}
