"""Design-41-Referenzansichten (01–10) und zwei Atlanten als HTML – mit Claude Code gebaut, mit Chrome gerendert.

Inhalte folgen den Projektregeln: Logo A, Studionamen und Symbole aus StudioCatalog.cs/StudioIcons.cs,
nur echte Trendo-Bereiche im Dock (keine fremden Programme), ehrliche Statusangaben (GPU „n. v.“ ohne Quelle).
Hintergründe: die mit lokaler Bild-KI erzeugten Assets (src/Trendo.App/Assets).
"""
import html
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src/Trendo.App/Assets"
REF = ROOT / "docs/spec/design-41/referenzen"
W, H = 1920, 1080


def catalog():
    cat = (ROOT / "src/Trendo.App/StudioCatalog.cs").read_text(encoding="utf-8")
    ico = (ROOT / "src/Trendo.App/StudioIcons.cs").read_text(encoding="utf-8")
    studios = {m[0]: {"name": m[1], "satz": m[2], "bereich": m[3]}
               for m in re.findall(r'\["([A-Z0-9-]+)"\] = new\("([^"]+)", "([^"]+)", "([^"]+)"\)', cat)}
    for m in re.findall(r'\["([A-Z0-9-]+)"\] = new\("[^"]*", "([^"]+)", "(#[0-9A-Fa-f]{6})", "(#[0-9A-Fa-f]{6})"\)', ico):
        if m[0] in studios:
            studios[m[0]].update(path=m[1], c1=m[2], c2=m[3])
    bereiche = re.search(r"Areas =\s*\[(.*?)\];", cat, re.S).group(1)
    return studios, re.findall(r'"([^"]+)"', bereiche)


S, BEREICHE = catalog()
# Trendo-Grundbereiche ohne Katalog-Studio (Dateien, Alle Studios, Einstellungen): eigene Symbole, gleiche Bauart
S["FILES"] = {"name": "Dateien", "satz": "", "bereich": "", "c1": "#FFD36B", "c2": "#E79A1B",
              "path": "M2 6.25C2 4.45 3.46 3 5.25 3h3.21c.6 0 1.17.24 1.6.66L11.88 5.5h6.87C20.55 5.5 22 6.96 22 8.75v9C22 19.55 20.54 21 18.75 21H5.25A3.25 3.25 0 0 1 2 17.75z"}
S["ALL"] = dict(S["SYS-01"], name="Alle Studios", c1="#7FD8FF", c2="#2A7FFF")
S["SETTINGS"] = {"name": "Einstellungen", "satz": "", "bereich": "", "c1": "#B6C2D9", "c2": "#5B6B88",
                 "path": "M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7m-1.6-6.5h3.2l.45 2.3q.9.3 1.7.8l1.95-1.3 2.25 2.25-1.3 1.95q.5.8.8 1.7l2.3.45v3.2l-2.3.45q-.3.9-.8 1.7l1.3 1.95-2.25 2.25-1.95-1.3q-.8.5-1.7.8L13.6 22h-3.2l-.45-2.3q-.9-.3-1.7-.8l-1.95 1.3-2.25-2.25 1.3-1.95q-.5-.8-.8-1.7L2 13.6v-3.2l2.3-.45q.3-.9.8-1.7L3.8 6.3l2.25-2.25L8 5.35q.8-.5 1.7-.8z"}
LOGO = (ASSETS / "logo/trendo-logo-b.svg").read_text(encoding="utf-8").replace('<svg ', '<svg class="logo" ', 1)

CSS = """
:root{--glass:rgba(11,23,48,.70);--glass2:rgba(10,20,40,.86);--tile:rgba(20,36,69,.60);--edge:rgba(85,168,255,.20);
--text:#EEF3FB;--muted:#9DB0CF;--accent:#2F86F6;--ok:#22C55E;--warn:#F59E0B}
*{box-sizing:border-box;margin:0;padding:0}
body{width:1920px;height:1080px;overflow:hidden;font-family:"Segoe UI Variable Text","Segoe UI",Inter,system-ui,sans-serif;color:var(--text);background:#050B18}
.bg{position:absolute;inset:0;background-size:cover;background-position:center}
.bg::after{content:"";position:absolute;inset:0;background:linear-gradient(180deg,rgba(3,8,20,.15),rgba(3,8,20,.35))}
.glass{background:var(--glass);border:1px solid var(--edge);border-radius:18px;backdrop-filter:blur(26px) saturate(1.3);
box-shadow:0 24px 60px rgba(0,0,0,.45),0 2px 0 rgba(255,255,255,.04) inset}
.strong{background:var(--glass2)}
.muted{color:var(--muted)} .small{font-size:13px} .h1{font-size:30px;font-weight:600} .h2{font-size:20px;font-weight:600}
.row{display:flex;align-items:center;gap:12px} .col{display:flex;flex-direction:column;gap:10px}
.ico{width:46px;height:46px;border-radius:13px;display:grid;place-items:center;flex:none;box-shadow:0 6px 16px rgba(0,0,0,.35)}
.ico svg{width:26px;height:26px;fill:#fff}
.logo{width:96px;height:96px}
.btn{padding:9px 16px;border-radius:10px;background:var(--tile);border:1px solid var(--edge);font-size:14px}
.btn.pri{background:var(--accent);border-color:transparent}
.chip{padding:7px 14px;border-radius:999px;background:var(--tile);border:1px solid var(--edge);font-size:13px}
.chip.on{background:var(--accent);border-color:transparent}
.bar{height:6px;border-radius:3px;background:rgba(255,255,255,.10);overflow:hidden}.bar>i{display:block;height:100%;background:var(--accent);border-radius:3px}
.search{height:48px;border-radius:24px;padding:0 20px;display:flex;align-items:center;gap:12px;color:var(--muted);font-size:15px}
.win{position:absolute;overflow:hidden}
.tb{height:44px;display:flex;align-items:center;gap:10px;padding:0 14px;border-bottom:1px solid var(--edge);background:rgba(255,255,255,.03)}
.tb .ctl{margin-left:auto;display:flex;gap:16px;color:var(--muted);font-size:14px}
.label{position:absolute;left:28px;bottom:30px;font-size:13px;letter-spacing:.08em;text-transform:uppercase;color:#CFE3FF;opacity:.85}
.dock{position:absolute;left:50%;bottom:22px;transform:translateX(-50%);display:flex;gap:10px;padding:10px 14px;border-radius:22px}
.dock .ico{width:52px;height:52px}
"""
SEARCH_SVG = '<svg width="18" height="18" viewBox="0 0 24 24" fill="#9DB0CF"><path d="M10 3a7 7 0 0 1 5.6 11.2l4.6 4.6-1.4 1.4-4.6-4.6A7 7 0 1 1 10 3m0 2a5 5 0 1 0 0 10 5 5 0 0 0 0-10"/></svg>'


def ico(sid, size=46):
    s = S[sid]
    return (f'<div class="ico" style="width:{size}px;height:{size}px;background:linear-gradient(135deg,{s["c1"]},{s["c2"]})">'
            f'<svg viewBox="0 0 24 24" style="width:{size * .56:.0f}px;height:{size * .56:.0f}px"><path d="{s["path"]}"/></svg></div>')


def name(sid):
    return html.escape(S[sid]["name"])


def page(body, wallpaper="wallpaper.jpg", label=""):
    bg = (ASSETS / wallpaper).as_uri()
    lab = f'<div class="label">{html.escape(label)}</div>' if label else ""
    return f'<!doctype html><html lang="de"><head><meta charset="utf-8"><style>{CSS}</style></head><body><div class="bg" style="background-image:url(\'{bg}\')"></div>{lab}{body}</body></html>'


DOCK = ["FILES", "STUDIO-LETTER-01", "SYS-10", "SYS-03", "CORE-02", "CORE-05", "SYS-09", "SETTINGS"]


def dock():
    start = f'<div class="ico" style="background:linear-gradient(135deg,#1B3150,#060D1A)">{LOGO.replace("logo", "logo-s").replace("class=\"logo-s\"", "style=\"width:34px;height:34px\"")}</div>'
    return '<div class="dock glass">' + start + "".join(ico(s, 52) for s in DOCK if s in S) + "</div>"


def metrics(compact=False):
    rows = [("CPU", "12 %", 12), ("RAM", "5,4 / 16 GB", 34), ("SSD", "120 / 512 GB", 23), ("GPU", "n. v.", 0)]
    out = "".join(f'<div class="col" style="gap:6px"><div class="row small"><span>{a}</span><span class="muted" style="margin-left:auto">{b}</span></div>'
                  f'<div class="bar"><i style="width:{c}%"></i></div></div>' for a, b, c in rows)
    return out + '<div class="row small muted"><span>Netz</span><span style="margin-left:auto">↓ 42 Mbit/s · ↑ 18 Mbit/s</span></div>'


def v01():
    right = (f'<div class="glass" style="position:absolute;right:36px;top:36px;width:340px;padding:22px" ><div class="muted small">Freitag, 24. Oktober 2026</div>'
             f'<div style="font-size:54px;font-weight:300;margin:4px 0 14px">14:37</div><div class="col">{metrics()}</div></div>'
             '<div class="glass" style="position:absolute;right:36px;top:390px;width:340px;padding:18px"><div class="h2" style="font-size:16px">Zuletzt</div>'
             '<div class="col small" style="margin-top:10px"><div class="row">' + ico("STUDIO-LETTER-01", 30) + '<span>Kündigung Fitnessstudio</span></div>'
             '<div class="row">' + ico("CORE-05", 30) + '<span>Projekt Gartenhaus</span></div><div class="row">' + ico("SPEC-11", 30) + '<span>Rechnungen 2026</span></div></div></div>')
    center = (f'<div style="position:absolute;left:0;right:0;top:300px;text-align:center">{LOGO}<div style="font-size:64px;font-weight:600;margin-top:12px;text-shadow:0 4px 30px rgba(0,0,0,.5)">Trendo OS</div>'
              f'<div style="font-size:20px;margin-top:4px;color:#DCE8FA;text-shadow:0 2px 12px rgba(0,0,0,.8)">Deine Ideen. Ein Arbeitsplatz.</div>'
              f'<div class="glass search" style="width:640px;margin:34px auto 0">{SEARCH_SVG}Programme, Studios, Dateien und Einstellungen</div></div>')
    return page(center + right + dock(), label="01 · Desktop")


def v02():
    pins = ["STUDIO-LETTER-01", "SYS-10", "SYS-09", "CORE-02", "CORE-05", "CORE-08", "SYS-03", "FILES", "CORE-07", "SPEC-11", "CORE-06", "SETTINGS"]
    grid = "".join(f'<div class="col" style="align-items:center;gap:8px;width:118px">{ico(p, 54)}<div class="small" style="text-align:center">{name(p)}</div></div>' for p in pins if p in S)
    rec = [("Kündigung Fitnessstudio.docx", "vor 2 Stunden"), ("Gartenhaus.blend", "vor 4 Stunden"), ("Rechnung 2026-031.pdf", "gestern"), ("Urlaubsvideo.mp4", "gestern")]
    recs = "".join(f'<div class="row small" style="width:46%"><div class="ico" style="width:36px;height:36px;background:var(--tile)"></div><div><div>{a}</div><div class="muted">{b}</div></div></div>' for a, b in rec)
    menu = (f'<div class="glass strong" style="position:absolute;left:50%;bottom:110px;transform:translateX(-50%);width:820px;padding:28px">'
            f'<div class="glass search" style="background:var(--tile)">{SEARCH_SVG}Programme, Studios, Dateien und Einstellungen</div>'
            f'<div class="row" style="margin:24px 0 14px"><div class="h2">Angeheftet</div><div class="chip" style="margin-left:auto">Alle Studios ›</div></div>'
            f'<div style="display:flex;flex-wrap:wrap;gap:18px 10px">{grid}</div><div class="h2" style="margin:26px 0 14px">Empfohlen</div>'
            f'<div style="display:flex;flex-wrap:wrap;gap:16px 8%">{recs}</div>'
            f'<div class="row" style="margin-top:26px;padding-top:16px;border-top:1px solid var(--edge)"><div class="ico" style="width:36px;height:36px;border-radius:50%;background:#2F86F6">M</div>'
            f'<span>Erika Muster</span><span class="muted small" style="margin-left:auto">Sperren · Abmelden · Ausschalten</span></div></div>')
    return page(menu + dock(), label="02 · Startmenü")


def window(x, y, w, h, title, inner, sid=None, active=True):
    head = (ico(sid, 26) if sid else "") + f'<span style="font-weight:600">{html.escape(title)}</span>'
    op = "" if active else "opacity:.82;filter:saturate(.8)"
    return (f'<div class="win glass strong" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;{op}">'
            f'<div class="tb">{head}<div class="ctl"><span>–</span><span>□</span><span>✕</span></div></div>{inner}</div>')


def v03():
    chips = '<div class="chip on">Alle</div>' + "".join(f'<div class="chip">{html.escape(b)}</div>' for b in BEREICHE[:7])
    ids = ["STUDIO-LETTER-01", "CORE-05", "CORE-08", "CORE-02", "CORE-06", "SYS-10", "STUDIO-ONLINE-BUSINESS-01", "CORE-07", "SPEC-02", "SYS-09", "CORE-10", "SYS-03"]
    cards = "".join(f'<div class="glass" style="background:var(--tile);padding:18px;border-radius:16px;height:178px;display:flex;flex-direction:column;gap:10px">'
                    f'{ico(i, 50)}<div style="font-weight:600;font-size:16px">{name(i)}</div><div class="muted small" style="line-height:1.35">{html.escape(S[i]["satz"])}</div></div>'
                    for i in ids if i in S)
    inner = (f'<div style="padding:26px 30px"><div class="row"><div><div class="h1">Alle Studios</div><div class="muted">54 Studios in {len(BEREICHE)} Bereichen</div></div>'
             f'<div class="glass search" style="margin-left:auto;width:360px;background:var(--tile)">{SEARCH_SVG}Studio suchen</div></div>'
             f'<div class="row" style="margin:20px 0;flex-wrap:wrap">{chips}</div>'
             f'<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:16px">{cards}</div></div>')
    return page(window(150, 70, 1620, 900, "Alle Studios", inner, "ALL") + dock(), label="03 · Studioübersicht")


def v04():
    nav = "".join(f'<div class="row" style="padding:10px 12px;border-radius:10px;{"background:var(--accent)" if i == 0 else ""}">{t}</div>'
                  for i, t in enumerate(["Neuer Brief", "Vorlagen", "Meine Briefe", "Empfänger", "Fristen", "Einstellungen"]))
    form = "".join(f'<div class="col" style="gap:4px"><span class="muted small">{a}</span><div class="btn" style="background:rgba(255,255,255,.05)">{b}</div></div>'
                   for a, b in [("Empfänger", "FitWelt GmbH, Hauptstraße 12, 80331 München"), ("Betreff", "Kündigung meiner Mitgliedschaft Nr. 4711"),
                                ("Vorlage", "Kündigung – Vertrag"), ("Frist", "zum 31.12.2026 · Eingang bis 30.11.2026")])
    letter = ('<div style="background:#fff;color:#1a1a1a;width:470px;height:665px;padding:46px 50px;font-family:Arial,sans-serif;font-size:12px;line-height:1.5;box-shadow:0 20px 50px rgba(0,0,0,.5)">'
              '<div style="font-size:9px;color:#666;border-bottom:1px solid #ccc;padding-bottom:3px">Erika Muster · Lindenweg 3 · 80333 München</div>'
              '<div style="margin-top:14px">FitWelt GmbH<br>Hauptstraße 12<br>80331 München</div><div style="text-align:right;margin-top:26px">München, 24.10.2026</div>'
              '<div style="font-weight:bold;margin-top:20px">Kündigung meiner Mitgliedschaft Nr. 4711</div><div style="margin-top:14px">Sehr geehrte Damen und Herren,<br><br>'
              'hiermit kündige ich meine Mitgliedschaft fristgerecht zum 31.12.2026. Bitte bestätigen Sie mir die Kündigung und das Vertragsende schriftlich.<br><br>'
              'Mit freundlichen Grüßen<br><br><br>Erika Muster</div></div>')
    inner = (f'<div style="display:flex;height:calc(100% - 44px)"><div class="col" style="width:230px;padding:18px;border-right:1px solid var(--edge)">{nav}</div>'
             f'<div class="col" style="width:520px;padding:26px;gap:16px"><div class="h2">Neuer Brief</div>{form}'
             f'<div class="row small" style="color:#86EFAC">✓ DIN 5008 · Frist geprüft · Pflichtangaben vollständig</div>'
             f'<div class="row" style="margin-top:auto"><div class="btn">Speichern</div><div class="btn">Als Word</div><div class="btn">Als PDF</div><div class="btn pri" style="margin-left:auto">Senden … (Freigabe)</div></div></div>'
             f'<div style="flex:1;display:grid;place-items:center;background:rgba(0,0,0,.18)">{letter}<div class="muted small">Live-Vorschau · Seite 1 von 1</div></div></div>')
    return page(window(110, 60, 1700, 930, "Briefe schreiben", inner, "STUDIO-LETTER-01") + dock(), label="04 · Briefstudio")


def v05():
    msgs = [("ich", "Schreib mir eine Einkaufsliste für ein Grillfest mit 8 Personen."),
            ("ki", "Gern. Für 8 Personen: 2 kg Grillfleisch, 16 Würstchen, 3 Baguettes, 2 Salate, Grillgemüse, Saucen, Getränke. Soll ich die Liste als Notiz speichern?"),
            ("ich", "Ja, und erinnere mich am Samstag um 10 Uhr.")]
    bubbles = "".join(f'<div style="max-width:70%;align-self:{"flex-end" if w == "ich" else "flex-start"};padding:14px 18px;border-radius:16px;'
                      f'background:{"var(--accent)" if w == "ich" else "var(--tile)"};line-height:1.45">{html.escape(t)}</div>' for w, t in msgs)
    sugg = "".join(f'<div class="glass" style="background:var(--tile);padding:14px;border-radius:14px;flex:1" class="small">{t}</div>'
                   for t in ["Brief formulieren", "Text zusammenfassen", "Idee für ein 3D-Modell", "Fehler im Code finden"])
    side = ('<div class="col" style="width:320px;padding:22px;border-left:1px solid var(--edge);gap:14px"><div class="h2" style="font-size:16px">Modell & Status</div>'
            '<div class="small">Lokal: <b>qwen2.5 3B</b></div><div class="row small"><i style="width:9px;height:9px;border-radius:50%;background:var(--ok)"></i>Bereit, läuft auf diesem PC</div>'
            '<div class="row small muted"><i style="width:9px;height:9px;border-radius:50%;background:#64748B"></i>Internet: aus</div>'
            '<div class="small muted">Kontext: Projekt „Grillfest“</div><div class="small muted">Dateien lesen: nur nach Freigabe</div></div>')
    inner = (f'<div style="display:flex;height:calc(100% - 44px)"><div class="col" style="width:260px;padding:18px;border-right:1px solid var(--edge)">'
             '<div class="btn pri">+ Neues Gespräch</div><div class="muted small" style="margin-top:12px">Heute</div><div class="small">Grillfest planen</div><div class="small muted">Gestern</div><div class="small">Kündigung formulieren</div></div>'
             f'<div class="col" style="flex:1;padding:26px;gap:16px">{bubbles}<div class="row" style="margin-top:auto">{sugg}</div>'
             f'<div class="glass search" style="background:var(--tile)">Nachricht an Trendo KI …<span style="margin-left:auto" class="btn pri">Senden</span></div></div>{side}</div>')
    return page(window(160, 70, 1600, 900, "KI fragen", inner, "CORE-02") + dock(), label="05 · Trendo KI")


def files_inner(compact=False):
    side = "".join(f'<div class="small" style="padding:8px 10px;border-radius:8px;{"background:var(--tile)" if i == 3 else ""}">{t}</div>'
                   for i, t in enumerate(["Schnellzugriff", "Desktop", "Downloads", "Dokumente", "Bilder", "Projekte", "Papierkorb"]))
    items = [("Briefe", "📁"), ("Rechnungen", "📁"), ("Projekt Gartenhaus", "📁"), ("Kündigung.docx", "📄"), ("Rechnung 031.pdf", "📕"), ("Urlaub.jpg", "🖼"), ("Gartenhaus.blend", "🧊"), ("Notizen.txt", "📝")]
    grid = "".join(f'<div class="col" style="align-items:center;gap:6px;width:120px;padding:12px;border-radius:12px;{"background:rgba(47,134,246,.25)" if i == 3 else ""}">'
                   f'<div style="font-size:46px">{e}</div><div class="small" style="text-align:center">{t}</div></div>' for i, (t, e) in enumerate(items))
    return (f'<div style="display:flex;height:calc(100% - 44px)"><div class="col" style="width:200px;padding:14px;border-right:1px solid var(--edge);gap:2px">{side}</div>'
            f'<div style="flex:1;padding:18px"><div class="row small muted" style="margin-bottom:14px">Dieser PC › Dokumente<span class="glass search" style="height:34px;margin-left:auto;width:260px;background:var(--tile)">Suchen</span></div>'
            f'<div style="display:flex;flex-wrap:wrap;gap:8px">{grid}</div></div></div>')


def v06():
    back = window(260, 120, 1000, 640, "Texte & Tabellen", '<div style="padding:30px" class="muted">Inaktives Fenster: gedämpft, flacher Schatten</div>', "SYS-10", active=False)
    front = window(620, 260, 1060, 660, "Dateien", files_inner(), "FILES")
    note = ('<div class="glass" style="position:absolute;left:150px;bottom:150px;width:420px;padding:18px" class="small">'
            '<div class="h2" style="font-size:16px">Fensterregeln</div><div class="small muted" style="margin-top:8px;line-height:1.5">'
            'Aktiv: volle Deckkraft, tiefer mehrstufiger Schatten, Akzentkante. Inaktiv: gedämpft. Andocken links/rechts/oben, Taskleisteneintrag pro Fenster.</div></div>')
    return page(back + front + note + dock(), label="06 · Fenster")


def v07():
    nav = "".join(f'<div class="row small" style="padding:10px 12px;border-radius:10px;{"background:var(--accent)" if t == "Aussehen" else ""}">{t}</div>'
                  for t in ["System", "Aussehen", "Anzeige", "Ton", "Netzwerk", "Sicherheit", "Konten", "Über Trendo"])
    themes = [("Obsidian", "wallpaper.jpg"), ("Bergsee Nacht", "wallpapers/bergsee-nacht.jpg"), ("Morgen", "wallpapers/morgen.jpg"),
              ("Polarlicht", "wallpapers/polarlicht.jpg"), ("Wellen", "wallpapers/wellen.jpg")]
    cards = "".join(f'<div class="col" style="gap:6px"><div style="width:190px;height:107px;border-radius:12px;background:url(\'{(ASSETS / f).as_uri()}\') center/cover;'
                    f'border:{"3px solid var(--accent)" if i == 0 else "1px solid var(--edge)"}"></div><span class="small">{t}</span></div>' for i, (t, f) in enumerate(themes))
    accents = "".join(f'<i style="width:30px;height:30px;border-radius:50%;background:{c};{"outline:3px solid #fff;outline-offset:2px" if i == 0 else ""}"></i>'
                      for i, c in enumerate(["#2F86F6", "#22C3A6", "#8B5CF6", "#F472B6", "#F59E0B", "#22C55E"]))
    toggles = "".join(f'<div class="row" style="padding:12px 0;border-bottom:1px solid var(--edge)"><span>{t}</span><span style="margin-left:auto;width:46px;height:26px;border-radius:13px;'
                      f'background:{"var(--accent)" if on else "rgba(255,255,255,.15)"};position:relative"><i style="position:absolute;top:3px;{"right" if on else "left"}:3px;width:20px;height:20px;border-radius:50%;background:#fff"></i></span></div>'
                      for t, on in [("Durchsichtige Flächen (Glas)", True), ("Bewegungen und Übergänge", True), ("Hoher Kontrast", False), ("Akzent folgt dem Hintergrund", True)])
    inner = (f'<div style="display:flex;height:calc(100% - 44px)"><div class="col" style="width:230px;padding:18px;border-right:1px solid var(--edge);gap:2px">{nav}</div>'
             f'<div class="col" style="flex:1;padding:28px;gap:18px"><div class="h1">Aussehen</div><div class="h2" style="font-size:16px">Hintergrund & Thema</div>'
             f'<div class="row" style="gap:16px">{cards}</div><div class="h2" style="font-size:16px">Akzentfarbe</div><div class="row">{accents}</div><div>{toggles}</div></div></div>')
    return page(window(200, 80, 1520, 880, "Einstellungen", inner, "SETTINGS") + dock(), label="07 · Einstellungen")


def ring(label, val, pct, color="#2F86F6"):
    c = 2 * 3.1416 * 52
    arc = f'<circle cx="64" cy="64" r="52" fill="none" stroke="{color}" stroke-width="10" stroke-linecap="round" stroke-dasharray="{c * pct / 100:.0f} {c:.0f}" transform="rotate(-90 64 64)"/>' if pct else ""
    return (f'<div class="col" style="align-items:center"><svg width="128" height="128"><circle cx="64" cy="64" r="52" fill="none" stroke="rgba(255,255,255,.1)" stroke-width="10"/>'
            f'{arc}<text x="64" y="70" text-anchor="middle" fill="#EEF3FB" font-size="22" font-family="Segoe UI">{val}</text></svg><span class="small">{label}</span></div>')


def v08():
    rings = ring("CPU", "12 %", 12) + ring("RAM", "34 %", 34, "#22C3A6") + ring("SSD", "23 %", 23, "#8B5CF6") + ring("GPU", "n. v.", 0)
    spark = "M0,60 " + " ".join(f"L{i * 20},{40 + 18 * __import__('math').sin(i / 2.3) + (i % 3) * 4:.0f}" for i in range(31))
    panel = (f'<div class="glass" style="position:absolute;left:50%;top:180px;transform:translateX(-50%);width:1100px;padding:30px">'
             f'<div class="row"><div class="h1">Leistung anzeigen</div><span class="muted small" style="margin-left:auto">live · alle 2 s</span></div>'
             f'<div class="row" style="justify-content:space-around;margin:26px 0">{rings}</div>'
             f'<div class="h2" style="font-size:16px">Netzwerk</div><svg width="600" height="90" style="margin-top:8px"><path d="{spark}" fill="none" stroke="#7FD8FF" stroke-width="2.5"/></svg>'
             f'<div class="row small muted">↓ 42 Mbit/s · ↑ 18 Mbit/s<span style="margin-left:auto">GPU: keine Messquelle gefunden – ehrlich „nicht verfügbar“</span></div></div>')
    return page(panel + dock(), label="08 · Systemmetriken")


def v09():
    themes = [("Obsidian", "wallpaper.jpg"), ("Bergsee Nacht", "wallpapers/bergsee-nacht.jpg"), ("Morgen", "wallpapers/morgen.jpg"),
              ("Polarlicht", "wallpapers/polarlicht.jpg"), ("Wellen", "wallpapers/wellen.jpg")]
    cards = "".join(f'<div class="col" style="gap:8px"><div style="width:300px;height:169px;border-radius:14px;background:url(\'{(ASSETS / f).as_uri()}\') center/cover;'
                    f'border:{"3px solid var(--accent)" if i == 0 else "1px solid var(--edge)"};box-shadow:0 14px 30px rgba(0,0,0,.4)"></div><span>{t}</span></div>' for i, (t, f) in enumerate(themes))
    prof = "".join(f'<div class="glass" style="background:var(--tile);padding:18px;flex:1"><div style="font-weight:600">{a}</div><div class="small muted" style="margin-top:6px;line-height:1.4">{b}</div></div>'
                   for a, b in [("Höchste Qualität", "Glas, Schatten, sanfte Bewegung, 4K-Hintergrund"), ("Standard", "Glas mit vorberechneter Unschärfe, kurze Übergänge"),
                                ("Raspberry Pi", "Flächen ohne Unschärfe, keine Bewegung, kleiner Hintergrund")])
    panel = (f'<div class="glass strong" style="position:absolute;left:50%;top:120px;transform:translateX(-50%);width:1660px;padding:30px">'
             f'<div class="h1">Hintergründe & Themen</div><div class="muted" style="margin:6px 0 20px">Alle Hintergründe lokal mit KI erzeugt (FLUX.2 klein) – keine Fremdfotos</div>'
             f'<div class="row" style="gap:20px">{cards}</div><div class="h2" style="font-size:16px;margin:26px 0 12px">Leistungsprofil</div><div class="row">{prof}</div></div>')
    return page(panel, wallpaper="wallpapers/polarlicht.jpg", label="09 · Hintergründe & Themen")


def v10():
    return page(window(200, 80, 1520, 860, "Dateien", files_inner(), "FILES") + dock(), wallpaper="wallpapers/wellen.jpg", label="10 · Dateimanager")


VIEWS = {"01_DESKTOP_PREMIUM": (v01, "Desktop-Hauptansicht und Dock"), "02_STARTMENUE": (v02, "Startmenü"),
         "03_STUDIOKARTEN": (v03, "Studioübersicht mit Karten"), "04_BRIEFSTUDIO": (v04, "Briefstudio mit Live-Vorschau"),
         "05_TRENDO_KI": (v05, "Trendo KI – Chat mit ehrlichem Modellstatus"), "06_FENSTER": (v06, "Fenster: aktiv/inaktiv, Tiefe"),
         "07_EINSTELLUNGEN": (v07, "Einstellungen – Aussehen"), "08_SYSTEMMETRIKEN": (v08, "Systemmetriken live"),
         "09_WALLPAPER_THEMES": (v09, "Hintergründe, Themen, Leistungsprofile"), "10_DATEIMANAGER": (v10, "Dateimanager")}


def atlas(names, title):
    tiles = "".join(f'<div style="position:relative"><img src="{(REF / (n + ".png")).as_uri()}" style="width:100%;border-radius:10px;display:block">'
                    f'<div style="position:absolute;left:12px;top:10px;font-size:15px;background:rgba(5,11,24,.7);padding:4px 10px;border-radius:8px">{html.escape(VIEWS[n][1])}</div></div>' for n in names)
    return (f'<!doctype html><html lang="de"><head><meta charset="utf-8"><style>{CSS} body{{width:2400px;height:1030px;padding:40px}}</style></head><body>'
            f'<div class="row" style="margin-bottom:24px">{LOGO.replace("class=\"logo\"", "style=\"width:56px;height:56px\"")}<div><div class="h1">Trendo OS – Design 41 {title}</div>'
            f'<div class="muted">Mit Claude Code erstellt · Hintergründe lokal mit KI (FLUX.2 klein) · Inhalte aus StudioCatalog</div></div></div>'
            f'<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:20px">{tiles}</div></body></html>')


def build(chrome, tmp, shoot):
    REF.mkdir(parents=True, exist_ok=True)
    manifest = {"source": "Mit Claude Code als HTML gebaut und mit Chrome gerendert (tools/design/views.py); Hintergründe lokal mit FLUX.2 klein erzeugt (docs/design/HINTERGRUENDE.json)",
                "views": []}
    for n, (fn, desc) in VIEWS.items():
        f = tmp / f"{n}.html"
        f.write_text(fn(), encoding="utf-8")
        shoot(chrome, f, REF / f"{n}.png", W, H)
        manifest["views"].append({"file": f"referenzen/{n}.png", "description": desc, "pixel_size": [W, H]})
        print("Ansicht:", n)
    for n, names, t in [("00_DESIGN_ATLAS_A", list(VIEWS)[:6], "Atlas A"), ("00_DESIGN_ATLAS_B", list(VIEWS)[4:], "Atlas B")]:
        f = tmp / f"{n}.html"
        f.write_text(atlas(names, t), encoding="utf-8")
        shoot(chrome, f, REF / f"{n}.png", 2400, 1030)
        manifest["views"].insert(0 if n.endswith("A") else 1, {"file": f"referenzen/{n}.png", "description": f"Gesamtübersicht {t}", "pixel_size": [2400, 1030]})
        print("Atlas:", n)
    (REF.parent / "REFERENZEN_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
