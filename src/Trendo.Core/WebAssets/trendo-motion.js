/* trendo-motion.js – eigene Bewegungs-Engine von Trendo OS (0.9.47, Clean-Room).
   Weiches Scrollen, Zeitleisten mit Easing, Text Wort für Wort, Einblenden, Scroll-Scrubbing, Zähler.
   Kein fremder Code: ersetzt die Aufgaben von GSAP/ScrollTrigger/SplitText und Lenis mit eigener, kleiner Umsetzung.
   „Bewegung reduzieren“ im System schaltet alle Bewegungen ab – Inhalte bleiben sofort sichtbar. */
(function () {
  var reduce = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

  var ease = {
    linear: function (t) { return t; },
    out: function (t) { return 1 - Math.pow(1 - t, 3); },
    inOut: function (t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2; },
    expo: function (t) { return t === 1 ? 1 : 1 - Math.pow(2, -10 * t); },
    back: function (t) { var c = 1.70158; return 1 + (c + 1) * Math.pow(t - 1, 3) + c * Math.pow(t - 1, 2); }
  };

  // Ein Element von seinem Zustand zu Zielwerten bewegen: opacity, x, y (px), scale, rotate (Grad)
  function tween(el, ziel, opt) {
    opt = opt || {};
    var dauer = reduce ? 0 : (opt.dauer != null ? opt.dauer : 0.6) * 1000;
    var f = ease[opt.ease] || ease.out;
    var von = el._tm || { opacity: parseFloat(getComputedStyle(el).opacity) || 1, x: 0, y: 0, scale: 1, rotate: 0 };
    var nach = {}; for (var k in von) nach[k] = ziel[k] != null ? ziel[k] : von[k];
    var t0 = performance.now();
    return new Promise(function (fertig) {
      function schritt(jetzt) {
        var t = dauer === 0 ? 1 : Math.min(1, (jetzt - t0) / dauer), e = f(t), w = {};
        for (var k in von) w[k] = von[k] + (nach[k] - von[k]) * e;
        el._tm = w;
        el.style.opacity = w.opacity;
        el.style.transform = 'translate3d(' + w.x + 'px,' + w.y + 'px,0) scale(' + w.scale + ') rotate(' + w.rotate + 'deg)';
        if (t < 1) requestAnimationFrame(schritt); else fertig();
      }
      requestAnimationFrame(schritt);
    });
  }

  // Zeitleiste: Schritte nacheinander oder an fester Stelle (at, Sekunden)
  function timeline() {
    var schritte = [], ende = 0;
    var tl = {
      to: function (el, ziel, opt) {
        opt = opt || {};
        var at = opt.at != null ? opt.at : ende;
        schritte.push({ el: el, ziel: ziel, opt: opt, at: at });
        ende = Math.max(ende, at + (opt.dauer != null ? opt.dauer : 0.6));
        return tl;
      },
      play: function () {
        schritte.forEach(function (s) { setTimeout(function () { tween(s.el, s.ziel, s.opt); }, reduce ? 0 : s.at * 1000); });
        return tl;
      },
      dauer: function () { return ende; }
    };
    return tl;
  }

  // Überschrift in Wörter teilen (Bildschirmleser lesen weiter den ganzen Satz)
  function split(el) {
    if (el.dataset.geteilt) return;
    var text = el.textContent.trim();
    el.setAttribute('aria-label', text);
    el.textContent = '';
    text.split(/\s+/).forEach(function (w, i) {
      var s = document.createElement('span');
      s.className = 'wort'; s.setAttribute('aria-hidden', 'true'); s.style.setProperty('--i', i); s.textContent = w;
      el.appendChild(s); el.appendChild(document.createTextNode(' '));
    });
    el.dataset.geteilt = '1';
  }

  // Zahl hochzählen: data-zahl="1200"
  function zaehlen(el) {
    var ziel = parseFloat(el.getAttribute('data-zahl')); if (!isFinite(ziel)) return;
    if (reduce) { el.textContent = ziel.toLocaleString('de-DE'); return; }
    var t0 = performance.now();
    (function schritt(j) { var t = Math.min(1, (j - t0) / 1400); el.textContent = Math.round(ziel * ease.out(t)).toLocaleString('de-DE'); if (t < 1) requestAnimationFrame(schritt); })(t0);
  }

  // Weiches Scrollen mit dem Mausrad (Touch und Tastatur bleiben unverändert)
  function weich() {
    if (reduce || ('ontouchstart' in window)) return;
    var ziel = window.scrollY, jetzt = window.scrollY, laeuft = false;
    function max() { return document.documentElement.scrollHeight - window.innerHeight; }
    window.addEventListener('wheel', function (e) {
      if (e.ctrlKey || e.defaultPrevented) return;
      e.preventDefault();
      ziel = Math.max(0, Math.min(max(), ziel + e.deltaY));
      if (!laeuft) { laeuft = true; requestAnimationFrame(schritt); }
    }, { passive: false });
    function schritt() {
      jetzt += (ziel - jetzt) * 0.1;
      if (Math.abs(ziel - jetzt) < 0.5) jetzt = ziel;
      window.scrollTo(0, jetzt);
      if (jetzt !== ziel) requestAnimationFrame(schritt); else laeuft = false;
    }
    window.addEventListener('scroll', function () { if (!laeuft) ziel = jetzt = window.scrollY; }, { passive: true });
  }

  // Fortschritt je Abschnitt als CSS-Variable --p (0 beim Eintreten, 1 beim Verlassen) – für Scrubbing per CSS
  function scrub(liste) {
    function tick() {
      var h = window.innerHeight;
      liste.forEach(function (el) {
        var r = el.getBoundingClientRect();
        var p = Math.min(1, Math.max(0, (h - r.top) / (h + r.height)));
        el.style.setProperty('--p', p.toFixed(4));
      });
    }
    window.addEventListener('scroll', tick, { passive: true });
    window.addEventListener('resize', tick);
    tick();
  }

  function start() {
    var root = document.documentElement;
    root.classList.add(reduce ? 'bewegung-aus' : 'bewegung-an');
    [].forEach.call(document.querySelectorAll('[data-split]'), split);
    var beobachtet = [].slice.call(document.querySelectorAll('[data-reveal],[data-split],[data-zahl]'));
    if (reduce || !('IntersectionObserver' in window)) {
      beobachtet.forEach(function (el) { el.classList.add('sichtbar'); if (el.hasAttribute('data-zahl')) zaehlen(el); });
    } else {
      var io = new IntersectionObserver(function (eintraege) {
        eintraege.forEach(function (e) {
          if (!e.isIntersecting) return;
          e.target.classList.add('sichtbar');
          if (e.target.hasAttribute('data-zahl')) zaehlen(e.target);
          io.unobserve(e.target);
        });
      }, { threshold: 0.18 });
      beobachtet.forEach(function (el) { io.observe(el); });
    }
    scrub([].slice.call(document.querySelectorAll('[data-scrub]')));
    weich();
  }

  window.TrendoMotion = { start: start, tween: tween, timeline: timeline, ease: ease, split: split, reduziert: reduce };
})();
