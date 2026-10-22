# I43-04 Moderne Web-Techniken

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 4. Wortlaut: „Moderne Web-Techniken: immersives Story-Scrolling, Scroll-Animationen, Parallax, 3D im Browser (Three.js), Partikel/Shader, Video-Hintergründe, Mikro-Animationen.“

## Ziel und Nutzen

Eine Seite kann die Bewegung nutzen, die zur Beschreibung passt. Die Technik läuft im Browser der Person, mit Dateien aus dem Projektordner. Sie wirkt ruhig und steuerbar, auch auf einem Handy.

## Nutzerablauf

1. Die Person beschreibt die Seite oder wählt die Technik.
2. Trendo nennt die gewählte Technik in einem Satz, warum sie passt.
3. Die Seite zeigt diese Bewegung, wenn die nötige Datei da ist (Modell, Video oder Bilder).
4. Fehlt die Datei, bleibt die Seite benutzbar und der Hinweis sagt, was fehlt.

## Anforderungen

Die Quelle nennt diese Techniken. Im Code (`WebsiteTechniques` in `src/Trendo.Core/Website/WebsiteModels.cs`) stehen sie so:

| Quelle | Id | Beschriftung im Code |
|---|---|---|
| immersives Story-Scrolling | `geschichte` | Story-Scrolling |
| Scroll-Animationen | Bestandteil von `geschichte` und `scroll` bei Modellen | kein eigener Eintrag |
| Parallax | `parallax` | Parallax |
| 3D im Browser (Three.js) | `drei` | 3D im Browser |
| Partikel/Shader | `partikel` | Partikel und Shader |
| Video-Hintergründe | `video` | Video-Hintergrund |
| Mikro-Animationen | `mikro` | Mikro-Animationen |

- Bibliotheken liegen lokal im Ordner. Kein Abruf von einem CDN zur Laufzeit.
- Three.js ist die Bibliothek, die die Quelle nennt. Die beigelegte Datei ist `src/Trendo.Core/WebAssets/trendo-three.js`, Lizenz `src/Trendo.Core/WebAssets/THREE-LICENSE.txt`.
- Scroll-Animationen ohne GSAP. `src/Trendo.Core/WebAssets/trendo-scroll.js` ist die vorhandene Datei. GSAP liegt nicht bei (`docs/build-log/2026-10-22-CORE-07-webseite.md`: kein MIT).
- Bewegung respektiert `prefers-reduced-motion`. Die Qualitätsprüfung kennt den Befund `motion` (`src/Trendo.Core/Website/WebsiteQuality.cs`).

## Betroffene Module

- `src/Trendo.Core/Website/WebsitePlan.cs` – `ResolveTechnique`, `Motion`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – `Css`, `NeedsThree`
- `src/Trendo.Core/Website/WebsiteFiles.cs` – Medien für den Video-Hintergrund
- `src/Trendo.Core/WebAssets/trendo-scroll.js`
- `src/Trendo.Core/WebAssets/trendo-three.js`
- `src/Trendo.Core/WebAssets/trendo-web3-entry.js`
- `src/Trendo.Core/WebAssets/seite.js`
- `src/Trendo.Core/WebAssets/LIZENZEN.txt`
- `src/Trendo.App/MainWindow.W4Web.cs` – Liste „Technik: automatisch“ und die sechs Beschriftungen

## Sicherheit und Datenschutz

- Lokal zuerst. Skripte und Medien kommen aus dem Projektordner.
- Cloud nur nach Opt-in. Ein Video-Hintergrund ist eine lokale Datei, kein Stream von einem Anbieter.
- Kein kostenpflichtiger Dienst und keine Bibliothek, die ein Abo voraussetzt.
- Keine Geheimnisse in den Skripten. Externe Skript- und Stil-Adressen wertet `WebsiteQuality` als Befund (`external-script`, `external-style`).
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` ist für diese Techniken nicht der Start. Sie schreiben nur lokale Dateien. Eine Technik startet keinen Server.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 3 liefert den Ordner und die Art.
- Punkt 5 liefert die GLB für „3D im Browser“.
- Punkt 6 liefert Video oder Bildfolge für den Hintergrund und den Scroll.
- Keine Abhängigkeit zum KI-Laden. Die Technikauswahl ist deterministisch (`ResolveTechnique`).

## Akzeptanzkriterien

- Jede der sechs Ids lässt sich wählen und steht danach in `inhalt.json` als `technique`.
- „Technik: automatisch“ setzt eine der bekannten Ids und füllt `TechniqueReason`.
- Three.js wird nur in den Ordner kopiert, wenn `WebsiteHtml.NeedsThree` wahr ist.
- Eine Seite mit Video ohne Videodatei bleibt eine gültige HTML-Seite und nennt die Lücke.
- Die Seite lädt Three.js und die Scroll-Datei nicht von einer Netzwerkadresse.

## Offene Fragen

- Die Quelle nennt Scroll-Animationen neben Story-Scrolling. Im Code sind sie kein siebter Listeneintrag. Reicht die Bewegung in `geschichte` und beim Modell-Modus `scroll`?
- Sollen Partikel und Shader getrennte Wahlmöglichkeiten werden? Heute ist beides die Id `partikel`.

## Ist-Stand

Die sechs Techniken, die lokalen Skripte und der Verzicht auf GSAP sind in `main` (Merge #246). Diese Spezifikation setzt den Punkt nicht auf `TESTED`.
