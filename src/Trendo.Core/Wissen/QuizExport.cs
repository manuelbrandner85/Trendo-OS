using System.Net;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Wissen;

/// <summary>Eigenständiges HTML-Quiz zum Weitergeben: eine einzige Datei ohne Internet, ohne fremde Skripte – Spielmodus mit Zeit, Mischen, Punkten und Auswertung.
/// Der Spieler (<see cref="PlayerJs"/>) ist derselbe, den auch die Kurs-Webseite und das SCORM-Paket nutzen.
/// Selbsttest: Mit „#selbsttest“ hinter der Adresse beantwortet die Seite alle Fragen richtig und schreibt das Ergebnis in <c>data-tq-result</c> (Beleg im Fremd-Browser).</summary>
public static class QuizHtml
{
    /// <summary>Farben und Formen (Dunkel-Gold) für Quiz und Kurs.</summary>
    public const string Css = """
:root{--bg:#14110d;--card:#1f1a14;--line:#3a3024;--gold:#c9a227;--gold2:#e8c75a;--text:#f3ead7;--muted:#b7a98f;--ok:#4ade80;--bad:#f87171;--warn:#fbbf24}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.5 "Segoe UI",system-ui,sans-serif}
.tq-wrap{max-width:760px;margin:0 auto;padding:24px 16px}.tq-card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:22px;margin:0 0 14px}
h1,h2,h3{margin:0 0 10px;line-height:1.25}h1{font-size:28px}h2{font-size:22px}.tq-muted{color:var(--muted);font-size:14px}
button{font:inherit;cursor:pointer;border-radius:10px;border:1px solid var(--line);background:#2a2218;color:var(--text);padding:10px 16px}
button.tq-main{background:var(--gold);border-color:var(--gold);color:#1a1408;font-weight:600}button:disabled{opacity:.45;cursor:default}
.tq-opts{display:grid;gap:8px;margin:14px 0}.tq-opt{text-align:left;display:flex;gap:10px;align-items:center}.tq-opt.on{border-color:var(--gold);background:#3a2f1a}
.tq-opt.right{border-color:var(--ok)}.tq-opt.wrong{border-color:var(--bad)}input[type=text],select{font:inherit;background:#120f0b;color:var(--text);border:1px solid var(--line);border-radius:8px;padding:6px 8px}
.tq-gap{width:9em;margin:0 4px}.tq-pair{display:flex;gap:10px;align-items:center;margin:6px 0}.tq-pair span{flex:1}
.tq-bar{height:6px;background:#2a2218;border-radius:4px;overflow:hidden;margin:8px 0}.tq-bar i{display:block;height:100%;background:var(--gold);width:100%}
.tq-fb{margin:12px 0;padding:10px 12px;border-radius:10px;background:#18140f;border:1px solid var(--line)}.tq-fb.ok{border-color:var(--ok)}.tq-fb.bad{border-color:var(--bad)}.tq-fb.part{border-color:var(--warn)}
table{border-collapse:collapse;width:100%;font-size:14px}td,th{border-bottom:1px solid var(--line);padding:6px;text-align:left}.tq-big{font-size:42px;font-weight:700;color:var(--gold2)}
.tq-row{display:flex;gap:8px;flex-wrap:wrap;margin-top:10px}
""";

    /// <summary>Quiz-Spieler in reinem JavaScript (keine Bibliothek). Gleiche Bewertung wie <see cref="QuizKit.Anteil"/>.</summary>
    public const string PlayerJs = """
function tqEl(t,c,x){var e=document.createElement(t);if(c)e.className=c;if(x!=null)e.textContent=x;return e;}
function tqGaps(t){var r=[],m,re=/\[([^\[\]]+)\]/g;while((m=re.exec(t||""))){var a=m[1].split('|').map(function(s){return s.trim();}).filter(function(s){return s.length>0;});if(a.length)r.push(a);}return r;}
function tqNorm(s){return (s||"").trim().toLowerCase().split(/\s+/).filter(function(x){return x;}).join(' ').replace(/[.!]+$/,'');}
function tqLev(a,b){var d=[],i,j;for(i=0;i<=a.length;i++){d[i]=[i];}for(j=0;j<=b.length;j++)d[0][j]=j;for(i=1;i<=a.length;i++)for(j=1;j<=b.length;j++)d[i][j]=Math.min(d[i-1][j]+1,d[i][j-1]+1,d[i-1][j-1]+(a[i-1]===b[j-1]?0:1));return d[a.length][b.length];}
function tqTypo(e,t){e=tqNorm(e);t=tqNorm(t);if(e===t)return true;return t.length>0&&tqLev(e,t)<=Math.max(1,Math.floor(e.length/6));}
function tqShuffle(a){a=a.slice();for(var i=a.length-1;i>0;i--){var j=Math.floor(Math.random()*(i+1));var x=a[i];a[i]=a[j];a[j]=x;}return a;}
function tqRange(n){var a=[];for(var i=0;i<n;i++)a.push(i);return a;}
function tqAnteil(f,inp){
 if(f.typ==='auswahl'||f.typ==='wahrfalsch'){return inp.sel&&inp.sel.length===1&&f.antworten[inp.sel[0]]&&f.antworten[inp.sel[0]].richtig?1:0;}
 if(f.typ==='mehrfach'){var r=0,w=0,h=0,m=0;f.antworten.forEach(function(a,i){if(a.richtig)r++;else w++;if((inp.sel||[]).indexOf(i)>=0){if(a.richtig)h++;else m++;}});if(!r)return 0;return Math.max(0,Math.min(1,h/r-(w?m/w:0)));}
 if(f.typ==='luecke'){var g=tqGaps(f.text);if(!g.length)return 0;var ok=0;g.forEach(function(alts,i){var t=(inp.gaps||[])[i]||"";if(t.trim()&&alts.some(function(s){return tqTypo(s,t);}))ok++;});return ok/g.length;}
 if(f.typ==='zuordnen'){if(!f.paare.length)return 0;var o=0;f.paare.forEach(function(p,i){var z=(inp.map||[])[i];if(z!=null&&z>=0&&f.paare[z]&&f.paare[z].rechts.trim().toLowerCase()===p.rechts.trim().toLowerCase())o++;});return o/f.paare.length;}
 return 0;}
function tqSolution(f){if(f.typ==='luecke')return tqGaps(f.text).map(function(g){return g[0];}).join(', ');if(f.typ==='zuordnen')return f.paare.map(function(p){return p.links+' → '+p.rechts;}).join('; ');return f.antworten.filter(function(a){return a.richtig;}).map(function(a){return a.text;}).join(', ');}
function TrendoQuiz(root,quiz,opt){
 opt=opt||{};var order,pos,res,t0,timer,limit;
 function time(f){return f.sekunden>0?f.sekunden:(quiz.sekunden||0);}
 function clear(){root.innerHTML='';if(timer){clearInterval(timer);timer=null;}}
 function start(){clear();var c=tqEl('div','tq-card');c.appendChild(tqEl('h1',null,quiz.titel));if(quiz.beschreibung)c.appendChild(tqEl('p',null,quiz.beschreibung));
  var info=quiz.fragen.length+' Fragen · '+(quiz.sekunden>0?quiz.sekunden+' Sekunden je Frage':'ohne Zeitlimit')+' · bestanden ab '+quiz.bestehen+' %'+(quiz.tempoBonus&&quiz.sekunden>0?' · Tempo-Bonus':'');c.appendChild(tqEl('p','tq-muted',info));
  var b=tqEl('button','tq-main','Los geht\u2019s');b.id='tq-start';b.onclick=begin;c.appendChild(b);root.appendChild(c);}
 function begin(){order=quiz.fragenMischen?tqShuffle(tqRange(quiz.fragen.length)):tqRange(quiz.fragen.length);pos=0;res=[];show();}
 function show(){clear();if(pos>=order.length)return end();var f=quiz.fragen[order[pos]];var inp={sel:[],gaps:[],map:[]};var done=false;
  var c=tqEl('div','tq-card');c.appendChild(tqEl('div','tq-muted','Frage '+(pos+1)+' von '+order.length+' · '+f.punkte+(f.punkte===1?' Punkt':' Punkte')));
  limit=time(f);var bar=null;if(limit>0){var bw=tqEl('div','tq-bar');bar=tqEl('i');bw.appendChild(bar);c.appendChild(bw);}
  var body=tqEl('div');c.appendChild(body);
  if(f.typ!=='luecke')body.appendChild(tqEl('h2',null,f.text));
  var opts=[];
  if(f.typ==='auswahl'||f.typ==='wahrfalsch'||f.typ==='mehrfach'){var box=tqEl('div','tq-opts');var idx=(quiz.antwortenMischen&&f.typ!=='wahrfalsch')?tqShuffle(tqRange(f.antworten.length)):tqRange(f.antworten.length);
   idx.forEach(function(i){var b=tqEl('button','tq-opt',(f.typ==='mehrfach'?'\u2610 ':'')+f.antworten[i].text);b.dataset.i=i;b.onclick=function(){if(done)return;if(f.typ==='mehrfach'){var k=inp.sel.indexOf(i);if(k>=0)inp.sel.splice(k,1);else inp.sel.push(i);b.classList.toggle('on');b.textContent=(b.classList.contains('on')?'\u2611 ':'\u2610 ')+f.antworten[i].text;}else{inp.sel=[i];opts.forEach(function(o){o.classList.remove('on');});b.classList.add('on');}};opts.push(b);box.appendChild(b);});body.appendChild(box);}
  if(f.typ==='luecke'){var p=tqEl('p');p.style.fontSize='20px';var parts=(f.text||'').split(/\[[^\[\]]+\]/);parts.forEach(function(s,i){p.appendChild(document.createTextNode(s));if(i<parts.length-1){var x=tqEl('input','tq-gap');x.type='text';x.dataset.g=i;x.oninput=function(){inp.gaps[i]=x.value;};p.appendChild(x);}});body.appendChild(p);}
  if(f.typ==='zuordnen'){var right=tqShuffle(tqRange(f.paare.length));f.paare.forEach(function(pp,i){var row=tqEl('div','tq-pair');row.appendChild(tqEl('span',null,pp.links));var s=tqEl('select');s.dataset.l=i;s.appendChild(tqEl('option',null,'– wählen –'));right.forEach(function(r){var o=tqEl('option',null,f.paare[r].rechts);o.value=r;s.appendChild(o);});s.onchange=function(){inp.map[i]=s.selectedIndex>0?parseInt(s.value,10):-1;};row.appendChild(s);body.appendChild(row);});}
  var fb=tqEl('div');c.appendChild(fb);var row=tqEl('div','tq-row');var chk=tqEl('button','tq-main','Antwort prüfen');chk.id='tq-check';row.appendChild(chk);c.appendChild(row);root.appendChild(c);
  t0=Date.now();
  function finish(timeout){if(done)return;done=true;if(timer){clearInterval(timer);timer=null;}var sec=(Date.now()-t0)/1000;var a=timeout?0:tqAnteil(f,inp);var pts=a*f.punkte;if(quiz.tempoBonus&&limit>0&&a>=1)pts+=f.punkte*0.5*Math.max(0,Math.min(1,1-sec/limit));pts=Math.round(pts*100)/100;
   res.push({frage:order[pos],anteil:a,punkte:pts,sekunden:Math.round(sec*10)/10,zeitUm:!!timeout});
   opts.forEach(function(o){var i=parseInt(o.dataset.i,10);if(f.antworten[i].richtig)o.classList.add('right');else if(inp.sel.indexOf(i)>=0)o.classList.add('wrong');});
   fb.className='tq-fb '+(a>=1?'ok':a>0?'part':'bad');fb.appendChild(tqEl('strong',null,timeout?'Zeit abgelaufen.':a>=1?'Richtig!':a>0?'Teilweise richtig.':'Leider falsch.'));fb.appendChild(tqEl('div',null,'Lösung: '+tqSolution(f)));if(f.erklaerung)fb.appendChild(tqEl('div','tq-muted',f.erklaerung));
   chk.remove();var nx=tqEl('button','tq-main',pos+1<order.length?'Weiter':'Zur Auswertung');nx.id='tq-next';nx.onclick=function(){pos++;show();};row.appendChild(nx);if(opt.auto)setTimeout(function(){nx.click();},0);}
  chk.onclick=function(){finish(false);};
  if(limit>0){timer=setInterval(function(){var left=limit-(Date.now()-t0)/1000;if(bar)bar.style.width=Math.max(0,100*left/limit)+'%';if(left<=0)finish(true);},100);}
  if(opt.auto){if(f.typ==='zuordnen')inp.map=tqRange(f.paare.length);else if(f.typ==='luecke')inp.gaps=tqGaps(f.text).map(function(g){return g[0];});else inp.sel=f.antworten.map(function(a,i){return a.richtig?i:-1;}).filter(function(i){return i>=0;});setTimeout(function(){chk.click();},0);}}
 function end(){clear();var max=0,base=0,pts=0;quiz.fragen.forEach(function(f){max+=f.punkte;});res.forEach(function(r){pts+=r.punkte;base+=r.anteil*quiz.fragen[r.frage].punkte;});
  var pct=max>0?Math.round(100*base/max):0;var ok=pct>=quiz.bestehen;var c=tqEl('div','tq-card');c.id='tq-result';c.appendChild(tqEl('h2',null,'Auswertung'));c.appendChild(tqEl('div','tq-big',pct+' %'));
  var bonus=Math.round((pts-base)*100)/100;c.appendChild(tqEl('p',null,(Math.round(base*100)/100)+' von '+max+' Punkten'+(bonus>0?' + '+bonus+' Tempo-Bonus':'')+' · '+(ok?'Bestanden':'Nicht bestanden (ab '+quiz.bestehen+' %)')));
  var tb=tqEl('table');var hr=tqEl('tr');['Frage','Punkte','Zeit','Lösung'].forEach(function(h){hr.appendChild(tqEl('th',null,h));});tb.appendChild(hr);
  res.forEach(function(r){var f=quiz.fragen[r.frage];var tr=tqEl('tr');tr.appendChild(tqEl('td',null,f.typ==='luecke'?f.text.replace(/\[[^\[\]]+\]/g,'___'):f.text));tr.appendChild(tqEl('td',null,r.punkte+' / '+f.punkte));tr.appendChild(tqEl('td',null,r.zeitUm?'Zeit um':r.sekunden+' s'));tr.appendChild(tqEl('td',null,tqSolution(f)));tb.appendChild(tr);});
  c.appendChild(tb);var row=tqEl('div','tq-row');var again=tqEl('button',null,'Nochmal spielen');again.id='tq-again';again.onclick=begin;row.appendChild(again);c.appendChild(row);root.appendChild(c);
  root.dataset.tqResult=pct;root.dataset.tqPassed=ok?'1':'0';if(opt.onDone)opt.onDone({prozent:pct,punkte:Math.round(pts*100)/100,max:max,bestanden:ok});}
 start();if(opt.auto)begin();
 return {begin:begin};}
""";

    /// <summary>JSON sicher in ein &lt;script&gt; einbetten (kein „&lt;/script&gt;“ im Text).</summary>
    public static string ScriptJson(string json) => json.Replace("</", "<\\/").Replace("<!--", "<\\!--").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");

    public static string Html(Quiz q)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<meta name=\"generator\" content=\"Trendo OS – Quiz erstellen\"><title>").Append(WebUtility.HtmlEncode(q.Titel)).Append("</title><style>").Append(Css).Append("</style></head>\n<body><main class=\"tq-wrap\" id=\"tq\"><noscript>Für dieses Quiz muss JavaScript an sein.</noscript></main>\n");
        sb.Append("<script id=\"tq-data\" type=\"application/json\">").Append(ScriptJson(q.ToJsonText())).Append("</script>\n<script>").Append(PlayerJs);
        sb.Append("\nvar tqQuiz=JSON.parse(document.getElementById('tq-data').textContent);TrendoQuiz(document.getElementById('tq'),tqQuiz,{auto:location.hash==='#selbsttest',onDone:function(r){document.body.setAttribute('data-tq-result',r.prozent);}});\n</script></body></html>\n");
        return sb.ToString();
    }

    public static void Write(Quiz q, string path) => AtomicFile.WriteAllText(path, Html(q));
}

/// <summary>Quiz als PDF zum Ausdrucken (A4): Fragen mit Kästchen, Lücken und Zuordnungsliste, auf der letzten Seite die Lösungen. Gezeichnet mit PDFsharp (MIT).</summary>
public static class QuizPdf
{
    const double Mm = 72 / 25.4;
    const double Left = 20, Width = 170, Top = 20, Bottom = 277;

    /// <summary>Schreibt das PDF und gibt die Seitenzahl zurück.</summary>
    public static int Write(Quiz q, string path, bool loesungen = true)
    {
        if (q.Fragen.Count == 0) throw new InvalidOperationException("Das Quiz hat noch keine Frage.");
        Sys.PdfTools.EnsureFonts();
        using var doc = new PdfDocument();
        doc.Info.Title = q.Titel; doc.Info.Subject = "Quiz"; doc.Info.Creator = "Trendo OS – Quiz erstellen";
        var measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
        XFont F(double pt, bool bold = false) => new("Arial", pt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        var gold = XColor.FromArgb(0x9A, 0x74, 0x10); var grey = XColor.FromArgb(0x55, 0x55, 0x55);
        List<string> Wrap(string text, XFont f, double widthMm)
        {
            var res = new List<string>(); var max = widthMm * Mm;
            foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var test = line.Length == 0 ? word : line + " " + word;
                    if (measure.MeasureString(test, f).Width <= max || line.Length == 0) { line = test; continue; }
                    res.Add(line); line = word;
                }
                res.Add(line);
            }
            return res;
        }
        double Lh(XFont f) => f.Size * 0.3528 * 1.3;

        XGraphics? g = null; double y = Top; var pages = new List<PdfPage>();
        void NewPage() { g?.Dispose(); var p = doc.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297); pages.Add(p); g = XGraphics.FromPdfPage(p); y = Top; }
        void Need(double mm) { if (g is null || y + mm > Bottom) NewPage(); }
        void Line(string t, XFont f, XColor c, double x = Left, double w = Width)
        { foreach (var l in Wrap(t, f, w - (x - Left))) { Need(Lh(f)); g!.DrawString(l, f, new XSolidBrush(c), new XRect(x * Mm, y * Mm, (w - (x - Left)) * Mm, Lh(f) * Mm), XStringFormats.TopLeft); y += Lh(f); } }
        void Box(double x, double yy) => g!.DrawRectangle(new XPen(XColors.Black, 0.25 * Mm), x * Mm, (yy + 0.6) * Mm, 3.4 * Mm, 3.4 * Mm);

        NewPage();
        Line(q.Titel, F(18, true), XColors.Black); y += 1;
        if (q.Beschreibung.Trim().Length > 0) Line(q.Beschreibung, F(10.5), grey);
        Line($"{q.Fragen.Count} Fragen · {q.MaxPunkte:0.##} Punkte · bestanden ab {q.Bestehen} %" + (q.Sekunden > 0 ? $" · {q.Sekunden} Sekunden je Frage" : ""), F(9.5), grey);
        y += 2; Line("Name: ______________________________     Datum: ______________", F(10.5), XColors.Black); y += 4;
        var body = F(10.5); var bold = F(11, true);
        for (var i = 0; i < q.Fragen.Count; i++)
        {
            var f = q.Fragen[i];
            var head = $"{i + 1}. " + (f.Typ == "luecke" ? "Ergänze die Lücken." : f.Text);
            var est = Wrap(head, bold, Width).Count * Lh(bold) + (f.Typ switch { "zuordnen" => f.Paare.Count, "luecke" => 3, _ => f.Antworten.Count } + 1) * Lh(body) + 4;
            Need(Math.Min(est, Bottom - Top));
            Line(head, bold, XColors.Black);
            g!.DrawString($"{f.Punkte:0.##} P.", F(8.5), new XSolidBrush(gold), new XRect((Left + Width - 20) * Mm, (y - Lh(bold)) * Mm, 20 * Mm, Lh(bold) * Mm), XStringFormats.TopRight);
            switch (f.Typ)
            {
                case "auswahl" or "mehrfach" or "wahrfalsch":
                    if (f.Typ == "mehrfach") Line("Mehrere Antworten können richtig sein.", F(8.5), grey, Left + 6);
                    var order = QuizKit.AntwortReihenfolge(q, f, 7);
                    foreach (var k in order) { Need(Lh(body)); Box(Left + 6, y); Line(f.Antworten[k].Text, body, XColors.Black, Left + 12); }
                    break;
                case "luecke":
                    Line(QuizKit.LueckenText(f.Text, "______________"), body, XColors.Black, Left + 6);
                    break;
                case "zuordnen":
                {
                    var right = QuizKit.AntwortReihenfolge(q, f, 7);
                    var startY = y;
                    for (var r = 0; r < f.Paare.Count; r++) { Need(Lh(body)); Line($"{r + 1}. {f.Paare[r].Links}   ____", body, XColors.Black, Left + 6, 80); }
                    var endY = y; y = startY;
                    for (var r = 0; r < right.Count; r++) Line($"{(char)('A' + r)}) {f.Paare[right[r]].Rechts}", body, XColors.Black, Left + 95, Width);
                    y = Math.Max(y, endY);
                    break;
                }
            }
            y += 4;
        }
        if (loesungen)
        {
            NewPage();
            Line("Lösungen – " + q.Titel, F(15, true), XColors.Black); y += 2;
            for (var i = 0; i < q.Fragen.Count; i++)
            {
                var f = q.Fragen[i];
                string sol;
                if (f.Typ == "zuordnen") { var right = QuizKit.AntwortReihenfolge(q, f, 7); sol = string.Join(", ", f.Paare.Select((p, r) => $"{r + 1} → {(char)('A' + right.IndexOf(r))}")); }
                else if (f.Typ is "auswahl" or "mehrfach" or "wahrfalsch") sol = string.Join(", ", f.Antworten.Where(a => a.Richtig).Select(a => a.Text));
                else sol = QuizKit.MitLoesung(f.Text);
                Line($"{i + 1}. {sol}", body, XColors.Black);
                if (f.Erklaerung.Trim().Length > 0) Line(f.Erklaerung, F(9), grey, Left + 6);
                y += 1.5;
            }
        }
        g?.Dispose();
        for (var p = 0; p < pages.Count; p++)
        {
            using var fg = XGraphics.FromPdfPage(pages[p], XGraphicsPdfPageOptions.Append);
            fg.DrawString($"{q.Titel} · Seite {p + 1} von {pages.Count}", F(8), new XSolidBrush(grey), new XRect(Left * Mm, 285 * Mm, Width * Mm, 6 * Mm), XStringFormats.TopCenter);
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!; Directory.CreateDirectory(dir);
        using var ms = new MemoryStream(); doc.Save(ms, false);
        AtomicFile.WriteAllBytes(path, ms.ToArray());
        return pages.Count;
    }
}
