# T-0622: Webseite bauen, Stufe 3 (Inhalte und Web-Apps)

**Freigabe:** Nutzer am 14.11.2026 „Ja, Web Stufe 3“ (weitergeleitet über die Sitzung „Spiele-Studio Stufe 1“).
**Vorgabe:** `docs/nachbau/web-ki/anforderungen.md` (Stufe 3: mehrere Seiten, W2, W3, K6, K3, A4, K7, K8; Rest von W4: Übergänge zwischen Seiten) und `anforderungen-ergaenzung.md` (Z8, Z3, Z5).
**Zweig:** `claude/T-0622-web-stufe3` von `origin/main` cce5c6261.

## Grenzen

- Clean-Room; AGPL nur als Vorbild; kein GSAP.
- Bezahl-KI nie voraussetzen; Claude nur über das Abo.
- Z8 mit Figma, A4 und K7 vor dem Merge durch den Security-Agenten prüfen.
- Oberfläche von Trendo bleibt deutsch; Z5 betrifft nur die erstellten Websites.
- Nichts hochladen oder veröffentlichen ohne Zustimmung im Einzelfall (K8).
- Commits nur mit `-- <pfade>`; `requirements.yaml` und `decisions.yaml` unverändert.
- Bei grünem Volltest selbst nach `main` mergen und pushen; Release nur mit OK des Nutzers.

## Teile

1. **Teil 1, Seiten und Inhalte:** mehrere Seiten, Übergänge zwischen Seiten (W4), W3 SEO mit Inhalt im HTML, W2 CMS, Z5 Mehrsprachigkeit.
2. **Teil 2, Code und Austausch:** K6 Code und Design, K3 Skizze, Z8 Import, K8 Veröffentlichen.
3. **Teil 3, Erweiterungen und 3D:** A4 Mods, K7 Web-Apps mit Daten, Z3 3D-Ausbau mit PC-Lauf.

Je Teil: gezielte Tests, QA-Prüfung in eigener Kopie, Befunde beheben. Am Ende Security-Prüfung, Volltest, Bericht, Merge.
