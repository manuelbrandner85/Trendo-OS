namespace Trendo.Core.Programs;

/// <summary>Spielmenüs fürs Browser-Spiel (Paket 01 S1-14 „Ultimate Game Interface“): Startmenü, Pause, Einstellungen mit Barrierefreiheit
/// (Lautstärke, hoher Kontrast, große Schrift, ruhige Effekte), Karte, Steuerungshilfe und Controller (Gamepad). Einstellungen bleiben je Spiel im Browser.</summary>
public static class GameMenus
{
    public const string SettingsKey = "trendo-einstellungen:";
    public static readonly string[] MenuStart = ["Fortsetzen", "Spielen", "Einstellungen", "Steuerung"];
    public static readonly string[] MenuPause = ["Weiter", "Neu beginnen", "Einstellungen", "Zum Menü"];
    public static readonly string[] Settings = ["Lautstärke", "Hoher Kontrast", "Große Schrift", "Ruhige Effekte", "Karte"];

    public const string Js = """
const MK='trendo-einstellungen:'+document.title;let S={vol:100,contrast:false,big:false,calm:false,map:true};
try{const o=JSON.parse(localStorage.getItem(MK)||'{}');if(o&&typeof o==='object')Object.assign(S,o);}catch(e){}
function saveS(){try{localStorage.setItem(MK,JSON.stringify(S));}catch(e){}}
let mode=CFG.menu===false?'play':'menu',sel=0,back='menu';
function items(){if(mode==='menu')return (SV0?['Fortsetzen']:[]).concat(['Spielen','Einstellungen','Steuerung']);
if(mode==='pause')return ['Weiter','Neu beginnen','Einstellungen','Zum Menü'];
if(mode==='settings')return ['Lautstärke: '+S.vol+' %','Hoher Kontrast: '+(S.contrast?'an':'aus'),'Große Schrift: '+(S.big?'an':'aus'),'Ruhige Effekte: '+(S.calm?'an':'aus'),'Karte: '+(S.map?'an':'aus'),'Zurück'];
return ['Zurück'];}
function choose(t){const w=t.split(':')[0];
if(w==='Fortsetzen'||w==='Weiter')mode='play';
else if(w==='Spielen'||w==='Neu beginnen'){dropSave();lives=CFG.lives;over=false;reset(true);mode='play';}
else if(w==='Einstellungen'||w==='Steuerung'){back=mode;mode=w==='Einstellungen'?'settings':'help';sel=0;}
else if(w==='Zum Menü'){mode='menu';sel=0;}
else if(w==='Lautstärke'){S.vol=(S.vol+25)%125;saveS();}
else if(w==='Hoher Kontrast'){S.contrast=!S.contrast;saveS();}
else if(w==='Große Schrift'){S.big=!S.big;saveS();}
else if(w==='Ruhige Effekte'){S.calm=!S.calm;saveS();}
else if(w==='Karte'){S.map=!S.map;saveS();}
else if(w==='Zurück'){mode=back;sel=0;}}
function menuKey(cd){if(mode==='play'){if(cd==='Escape'||cd==='KeyP'){mode='pause';sel=0;return true;}return false;}
const it=items();if(cd==='ArrowUp'||cd==='KeyW')sel=(sel+it.length-1)%it.length;else if(cd==='ArrowDown'||cd==='KeyS')sel=(sel+1)%it.length;
else if(cd==='Enter'||cd==='Space'||cd==='KeyJ')choose(it[Math.min(sel,it.length-1)]);
else if((cd==='Escape'||cd==='KeyP')&&mode==='pause')mode='play';else if(cd==='Escape'&&(mode==='settings'||mode==='help')){mode=back;sel=0;}
return true;}
addEventListener('keydown',e=>{if(menuKey(e.code)){if(e.preventDefault)e.preventDefault();if(e.stopImmediatePropagation)e.stopImmediatePropagation();}else if(/^Arrow|^Space$/.test(e.code)&&e.preventDefault)e.preventDefault();});
let pad={};function pollPad(){if(typeof navigator==='undefined'||!navigator.getGamepads)return;const g=[...navigator.getGamepads()].find(q=>q);if(!g)return;
const b=i=>g.buttons[i]&&g.buttons[i].pressed;const ax=g.axes[0]||0,ay=g.axes[1]||0;
const now={L:ax<-.4||b(14),R:ax>.4||b(15),U:ay<-.5||b(12),D:ay>.5||b(13),A:b(0),B:b(1),X:b(2),St:b(9)};
const fire=(kk,cd)=>{if(now[kk]&&!pad[kk])dispatchEvent(new KeyboardEvent('keydown',{code:cd}));if(!now[kk]&&pad[kk])dispatchEvent(new KeyboardEvent('keyup',{code:cd}));};
fire('L','ArrowLeft');fire('R','ArrowRight');fire('U','ArrowUp');fire('D','ArrowDown');fire('A','Space');fire('B','KeyE');fire('X','KeyX');fire('St','Escape');pad=now;}
function drawMenu(){const big=S.big?1.35:1;
if(S.map&&mode==='play'){const m=3,cols=Math.min(C,50),off=Math.max(0,Math.min(C-cols,Math.floor(pl.x/T)-25)),ox=c.width-cols*m-8,oy=c.height-R*m-8;
x.fillStyle='rgba(0,0,0,.55)';x.fillRect(ox-2,oy-2,cols*m+4,R*m+4);x.fillStyle=S.contrast?'#FFF':'#8899AA';
for(let yy=0;yy<R;yy++)for(let xx=0;xx<cols;xx++)if(at(xx+off,yy)==='#')x.fillRect(ox+xx*m,oy+yy*m,m,m);
x.fillStyle=S.contrast?'#FFFF00':CFG.accent;x.fillRect(ox+(Math.floor((pl.x+12)/T)-off)*m,oy+Math.floor(pl.y/T)*m,m,m*2);}
if(mode==='play')return;
x.fillStyle=S.contrast?'rgba(0,0,0,.94)':'rgba(5,10,25,.84)';x.fillRect(0,0,c.width,c.height);x.textAlign='center';x.fillStyle='#FFF';
x.font='bold '+Math.round(26*big)+'px system-ui';x.fillText(mode==='menu'?document.title:mode==='pause'?'Pause':mode==='settings'?'Einstellungen':'Steuerung',c.width/2,64);
if(mode==='help'){x.font=Math.round(13*big)+'px system-ui';['← → laufen · Leertaste springen · Umschalt sprinten','X oder J angreifen · E aussteigen','Esc oder P Pause · N neues Spiel','Controller: Stick laufen · A springen · X angreifen · B aussteigen · Start Pause'].forEach((t,i)=>x.fillText(t,c.width/2,104+i*24*big));}
const it=items();x.font=Math.round(17*big)+'px system-ui';it.forEach((t,i)=>{const y=(mode==='help'?220:120)+i*32*big;if(i===sel){x.fillStyle=S.contrast?'#FFFF00':CFG.accent;x.fillRect(c.width/2-140,y-21*big,280,29*big);x.fillStyle='#000';}else x.fillStyle='#FFF';x.fillText(t,c.width/2,y);});
x.font='12px system-ui';x.fillStyle='#AAB';x.fillText('↑ ↓ wählen · Enter bestätigen'+(mode==='pause'?' · Esc weiter':''),c.width/2,c.height-14);x.textAlign='left';}

""";
}
