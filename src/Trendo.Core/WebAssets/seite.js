/* Steuerung der erzeugten Seite. Keine fremden Adressen. */
(function () {
  function mark() { document.documentElement.dataset.fertig = '1'; }

  var form = document.getElementById('kontaktform');
  if (form) form.addEventListener('submit', function (e) {
    e.preventDefault();
    var note = document.getElementById('kontakt-hinweis');
    if (note) note.hidden = false;
  });

  var korb = 0;
  [].forEach.call(document.querySelectorAll('[data-waren]'), function (b) {
    b.addEventListener('click', function () {
      korb += 1;
      var el = document.getElementById('korb');
      if (el) el.textContent = 'Im Korb: ' + korb + ' (Beispiel, es wird nichts bestellt)';
    });
  });

  var folge = document.getElementById('bildfolge');
  var daten = document.getElementById('bildfolge-daten');
  if (folge && daten) {
    try {
      var liste = JSON.parse(daten.textContent || '[]');
      folge.width = 1280;
      folge.height = 720;
      folge._bilder = liste.map(function (src) {
        var img = new Image();
        img.src = src;
        return img;
      });
    } catch (e) { /* defekte Liste bleibt leer */ }
  }

  zeichneShader();
  zeichnePartikel();
  if (window.TrendoScroll) window.TrendoScroll.start();

  var buehne = document.getElementById('buehne');
  var modell = document.getElementById('modell-daten');
  if (buehne && window.TrendoWeb3) {
    window.TrendoWeb3.start(buehne, {
      farbe: document.body.dataset.farbe || '#2f6fed',
      hintergrund: document.body.dataset.buehne || '#10141c',
      bewegung: document.body.dataset.bewegung || 'drehen',
      modell: modell ? modell.textContent : '',
      url: document.body.dataset.modellUrl || '',
      links: document.getElementById('dreh-links'),
      rechts: document.getElementById('dreh-rechts'),
      fertig: mark
    });
  } else if (document.readyState === 'complete') mark();
  else window.addEventListener('load', mark);
  setTimeout(mark, 4000);

  function zeichnePartikel() {
    var c = document.getElementById('partikel');
    if (!c) return;
    var w = c.width = 1280, h = c.height = 720;
    var ctx = c.getContext('2d');
    if (!ctx) return;
    var dots = [];
    for (var i = 0; i < 90; i++) dots.push({ x: Math.random() * w, y: Math.random() * h, r: 1 + Math.random() * 2.4, v: 0.25 + Math.random() * 0.9 });
    function frame() {
      ctx.clearRect(0, 0, w, h);
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      dots.forEach(function (d) {
        d.y -= d.v;
        if (d.y < -4) d.y = h + 4;
        ctx.beginPath();
        ctx.arc(d.x, d.y, d.r, 0, 6.283);
        ctx.fill();
      });
    }
    frame();
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (!reduce) setInterval(frame, 50);
  }

  function zeichneShader() {
    var c = document.getElementById('shader');
    if (!c) return;
    c.width = 1280;
    c.height = 720;
    var gl = c.getContext('webgl', { antialias: false, preserveDrawingBuffer: true });
    if (!gl) { flaeche(c); return; }
    function shader(type, src) {
      var s = gl.createShader(type);
      gl.shaderSource(s, src);
      gl.compileShader(s);
      return s;
    }
    var prog = gl.createProgram();
    gl.attachShader(prog, shader(gl.VERTEX_SHADER, 'attribute vec2 p;void main(){gl_Position=vec4(p,0.0,1.0);}'));
    gl.attachShader(prog, shader(gl.FRAGMENT_SHADER, 'precision mediump float;uniform vec2 r;void main(){vec2 u=gl_FragCoord.xy/r;float n=fract(sin(dot(u*36.0,vec2(12.9898,78.233)))*43758.5453);float w=0.55+0.45*sin(u.x*7.0+n*6.0);gl_FragColor=vec4(0.05+0.12*u.y,0.10+0.28*w,0.22+0.45*u.x,1.0);}'));
    gl.linkProgram(prog);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) { flaeche(c); return; }
    gl.useProgram(prog);
    var buf = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buf);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
    var loc = gl.getAttribLocation(prog, 'p');
    gl.enableVertexAttribArray(loc);
    gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
    var ru = gl.getUniformLocation(prog, 'r');
    gl.uniform2f(ru, c.width, c.height);
    gl.viewport(0, 0, c.width, c.height);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  function flaeche(c) {
    var ctx = c.getContext('2d');
    if (!ctx) return;
    var g = ctx.createLinearGradient(0, 0, c.width, c.height);
    g.addColorStop(0, '#0e1a33');
    g.addColorStop(1, '#1d4f91');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, c.width, c.height);
  }
})();
