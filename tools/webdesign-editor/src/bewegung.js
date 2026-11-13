// W4 und Z2: Bewegung je Baustein und Zeitleiste mit Schlüsselbildern. Eigene Umsetzung über die Web Animations API des Browsers.
// Grundsatz: Ohne JavaScript, ohne IntersectionObserver oder bei „weniger Bewegung“ bleibt jeder Inhalt sichtbar und ruhig.
// Die Wertberechnung steht gleich in C# (WebDesignMotion.ValueAt); ein Test vergleicht beide.
export const EIGENSCHAFTEN = { x: 0, y: 0, skalierung: 1, drehung: 0, deckkraft: 1 };

/** Wert einer Eigenschaft zur Zeit t: vor dem ersten Schlüsselbild dessen Wert, danach der letzte, dazwischen linear. Ohne Schlüsselbild der Grundwert. */
export function wertZurZeit(punkte, eigenschaft, t) {
  const p = (punkte || []).filter((k) => k && k.eigenschaft === eigenschaft && Number.isFinite(+k.zeit) && Number.isFinite(+k.wert)).map((k) => ({ zeit: +k.zeit, wert: +k.wert })).sort((a, b) => a.zeit - b.zeit);
  if (!p.length) return EIGENSCHAFTEN[eigenschaft];
  if (t <= p[0].zeit) return p[0].wert;
  for (let i = 1; i < p.length; i++) if (t <= p[i].zeit) { const a = p[i - 1], b = p[i]; return b.zeit === a.zeit ? b.wert : a.wert + (b.wert - a.wert) * ((t - a.zeit) / (b.zeit - a.zeit)); }
  return p[p.length - 1].wert;
}

/** Schlüsselbilder für die Web Animations API: an jeder Zeit, an der eine Spur ein Schlüsselbild hat, alle Eigenschaften zusammen. */
export function bilderDerZeitleiste(z) {
  const dauer = Math.max(0.2, +z?.dauer || 1);
  const zeiten = [...new Set([0, dauer, ...(z?.punkte || []).map((k) => Math.min(dauer, Math.max(0, +k.zeit || 0)))])].sort((a, b) => a - b);
  return zeiten.map((t) => ({ offset: t / dauer,
    transform: `translate(${wertZurZeit(z.punkte, "x", t)}px, ${wertZurZeit(z.punkte, "y", t)}px) scale(${wertZurZeit(z.punkte, "skalierung", t)}) rotate(${wertZurZeit(z.punkte, "drehung", t)}deg)`,
    opacity: wertZurZeit(z.punkte, "deckkraft", t) }));
}

const ERSCHEINEN = {
  einblenden: [{ opacity: 0 }, { opacity: 1 }],
  hochgleiten: [{ opacity: 0, transform: "translateY(32px)" }, { opacity: 1, transform: "none" }],
  zoomen: [{ opacity: 0, transform: "scale(.92)" }, { opacity: 1, transform: "none" }],
};

/** Startet alle Bewegungen unter einem Element. Liefert eine Aufräumfunktion. */
export function starteBewegungen(wurzel) {
  const fenster = wurzel?.ownerDocument?.defaultView;
  if (!fenster || fenster.matchMedia?.("(prefers-reduced-motion: reduce)").matches || typeof Element.prototype.animate !== "function") return () => {};
  const aufraeumen = [];
  const io = "IntersectionObserver" in fenster ? new fenster.IntersectionObserver((eintraege) => {
    for (const e of eintraege) if (e.isIntersecting) { e.target.__tAnimation?.play(); io.unobserve(e.target); }
  }, { threshold: 0.15 }) : null;
  for (const el of wurzel.querySelectorAll("[data-erscheinen]")) {
    const bilder = ERSCHEINEN[el.dataset.erscheinen]; if (!bilder || !io) continue;
    const a = el.animate(bilder, { duration: (+el.dataset.dauer || 0.8) * 1000, delay: (+el.dataset.verzoegerung || 0) * 1000, easing: "cubic-bezier(.2,.7,.2,1)", fill: "both" });
    a.pause(); el.__tAnimation = a; io.observe(el);
    aufraeumen.push(() => a.cancel());
  }
  for (const el of wurzel.querySelectorAll("[data-zeitleiste]")) {
    let z; try { z = JSON.parse(el.dataset.zeitleiste); } catch { continue; }
    if (!z?.punkte?.length) continue;
    const dauer = Math.max(0.2, +z.dauer || 1) * 1000;
    const a = el.animate(bilderDerZeitleiste(z), { duration: dauer, iterations: z.wiederholen === "ja" && z.ausloeser !== "scroll" ? Infinity : 1, fill: "both", easing: "linear" });
    aufraeumen.push(() => a.cancel());
    if (z.ausloeser === "scroll") {
      // an den Scroll-Fortschritt gebunden: 0 wenn das Element unten hereinkommt, 1 wenn es ganz zu sehen ist (auch am Seitenende erreichbar)
      a.pause();
      const setze = () => { const r = el.getBoundingClientRect(), h = fenster.innerHeight; a.currentTime = Math.min(1, Math.max(0, (h - r.top) / Math.max(1, Math.min(r.height, h * 0.8)))) * dauer; };
      setze(); fenster.addEventListener("scroll", setze, { passive: true }); aufraeumen.push(() => fenster.removeEventListener("scroll", setze));
    }
  }
  return () => { io?.disconnect(); aufraeumen.forEach((f) => f()); };
}
