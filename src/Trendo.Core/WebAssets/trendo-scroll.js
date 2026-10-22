/* trendo-scroll.js – Story-Scrolling und Parallax, lokal, ohne CDN.
   Eigene Datei von Trendo OS. Ersetzt GSAP ScrollTrigger. */
(function () {
  function fortschritt() {
    var max = document.documentElement.scrollHeight - window.innerHeight;
    if (max <= 0) return 0;
    return Math.max(0, Math.min(1, window.scrollY / max));
  }

  function start() {
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var bar = document.querySelector('[data-fortschritt]');
    var kapitel = [].slice.call(document.querySelectorAll('[data-kapitel]'));
    var schichten = [].slice.call(document.querySelectorAll('[data-parallax]'));
    var folge = document.getElementById('bildfolge');

    function tick() {
      var p = fortschritt();
      document.documentElement.style.setProperty('--scroll', p.toFixed(4));
      if (bar) bar.style.transform = 'scaleY(' + p.toFixed(4) + ')';
      kapitel.forEach(function (el) {
        var r = el.getBoundingClientRect();
        var sicht = 1 - Math.min(1, Math.abs(r.top) / Math.max(1, window.innerHeight));
        el.style.setProperty('--sicht', sicht.toFixed(3));
        var aktiv = r.top < window.innerHeight * 0.62 && r.bottom > window.innerHeight * 0.38;
        el.classList.toggle('aktiv', aktiv);
      });
      if (!reduce) {
        schichten.forEach(function (el) {
          var speed = parseFloat(el.getAttribute('data-parallax') || '0.2');
          if (!isFinite(speed)) speed = 0.2;
          var r = el.getBoundingClientRect();
          var shift = (r.top - window.innerHeight * 0.45) * speed * 0.28;
          el.style.transform = 'translate3d(0,' + shift.toFixed(1) + 'px,0)';
        });
      }
      if (folge && folge._bilder && folge._bilder.length) {
        var i = Math.min(folge._bilder.length - 1, Math.floor(p * (folge._bilder.length - 1)));
        var img = folge._bilder[i];
        if (img && img.complete && img.naturalWidth) {
          var ctx = folge.getContext('2d');
          ctx.drawImage(img, 0, 0, folge.width, folge.height);
          folge._stand = i;
        }
      }
    }

    var nr = parseInt(new URLSearchParams(location.search).get('kapitel') || '1', 10);
    if (nr > 1 && kapitel[nr - 1]) kapitel[nr - 1].scrollIntoView();
    window.addEventListener('scroll', tick, { passive: true });
    window.addEventListener('resize', tick);
    tick();
  }

  window.TrendoScroll = { start: start };
})();
