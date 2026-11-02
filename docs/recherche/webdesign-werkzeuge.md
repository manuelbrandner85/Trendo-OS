# Recherche: Spitzen-Werkzeuge für Webdesign (02.11.2026)

Auftrag des Projektinhabers: die besten Werkzeuge recherchieren und fest in Trendo einbauen; Claude soll damit arbeiten.
Grundsatz: Open-Source-Werkzeuge mit passender Lizenz werden direkt eingebaut. Kommerzielle Vorbilder (Webflow, Framer, Spline, Figma)
werden nur im Clean-Room-Verfahren nachgebaut – Funktionen und Arbeitsweise studieren, selbst neu bauen; kein fremder Code, keine fremden
Grafiken, kein geschütztes Aussehen. REA nur für Dateien, die analysiert werden dürfen.

## Ergebnis: Stack für immersive 3D-Webseiten mit Storyscrolling

| Aufgabe | Werkzeug | Lizenz | Einsatz in Trendo |
|---|---|---|---|
| 3D im Browser (WebGL/WebGPU) | three.js | MIT | fest: lokal im Projekt (`vendor/three.module.js`), Claude nutzt es für 3D-Szenen |
| Weiches Scrollen, gleich auf allen Geräten | Lenis | MIT | fest: lokal im Projekt |
| 3D-Modelle einfach einbinden (glTF/GLB) | @google/model-viewer | Apache-2.0 | fest: für Produkt- und Objektansichten |
| Scroll-gesteuerte Animation ohne Bibliothek | CSS Scroll-driven Animations (`animation-timeline`) | Web-Standard | bevorzugt mit Ersatz (IntersectionObserver), weil nicht jeder Browser es kann |
| Zeitleisten-Animationen | eigene kleine Zeitleiste (Clean-Room) | – | statt GSAP (siehe unten) |
| 3D-Modelle und Texturen | Poly Haven, ambientCG | CC0 | Claude lädt passende Modelle und Texturen in das Projekt |

## Nicht fest eingebaut – mit Grund

- **GSAP (inkl. ScrollTrigger):** seit 30.04.2025 kostenlos, auch kommerziell. Die Lizenz verlangt aber eine schriftliche Erlaubnis von Webflow für
  visuelle No-Code-Animationswerkzeuge, die mit Webflow konkurrieren könnten – das Webseiten-Studio von Trendo ist so ein Werkzeug. Darum nicht im
  Studio eingebaut. Ein Nutzer darf GSAP in seiner eigenen Seite selbst verwenden; Trendo schlägt es nicht von sich aus vor.
- **Spline, Webflow, Framer:** kommerzielle Dienste. Ihre Arbeitsweise (visueller 3D-Editor, Scroll-Interaktionen, Komponenten) dient als
  Vorbild für Clean-Room-Funktionen im Studio; ihr Code und ihre Oberfläche werden nicht übernommen.
- **Theatre.js:** Lizenz-Aufteilung (Kern vs. Studio) vor einer Nutzung erst prüfen.

## Was Claude damit tun soll (Regel im Webseiten-Studio)

- Hyperrealistische, moderne Seiten: 3D-Objekte (three.js oder model-viewer), Storyscrolling (Abschnitte, die beim Scrollen eine Geschichte erzählen),
  weiche Bewegung (Lenis), Licht und Material physikalisch glaubwürdig, Modelle und Texturen aus CC0-Quellen.
- Alles lokal im Projekt (läuft ohne Internet und ohne fremde Server), schnell (Bilder und Modelle komprimiert), barrierearm
  (`prefers-reduced-motion` respektieren, Inhalte auch ohne Animation lesbar).
- Schrittweise speichern – der Nutzer sieht die Seite live in der Vorschau entstehen.

## Quellen

- three.js: Lizenz MIT (u. a. Lizenztext in den Projektkopien)
- Lenis: <https://github.com/darkroomengineering/lenis> (MIT), <https://npmjs.com/package/lenis>
- @google/model-viewer: <https://npmjs.com/package/@google/model-viewer> (Apache-2.0)
- GSAP-Lizenz: <https://gsap.com/community/standard-license/>, <https://webflow.com/blog/gsap-becomes-free>, Einschränkung für No-Code-Werkzeuge: <https://forum.pinegrow.com/t/gsap-has-been-acquired-by-webflow/9906>
- Scroll-driven Animations, Browserstand uneinheitlich (Firefox umstritten): <https://www.buildmvpfast.com/blog/css-scroll-driven-animations-replace-js-2026>, <https://dev.to/chengyixu/css-scroll-driven-animations-the-end-of-javascript-scroll-listeners-4bij>
- Stack-Überblick 2026: <https://www.svilenkovic.com/3d/scrollytelling-trends-2026>
