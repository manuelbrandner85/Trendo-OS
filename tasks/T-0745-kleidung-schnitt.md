# T-0745: Kleidung – Schnittteile frei bearbeiten

**Ziel:** Schnittteile von SPEC-08 („Kleidung & Produkte“) direkt auf der Arbeitsfläche frei bearbeiten – Punkte verschieben, einfügen, löschen, Kanten als Gerade oder Bézierkurve – mit Maßen, Nahtzugabe, Rückgängig je Schritt, Kürzeln und Rechtsklick; Export und 3D-Anprobe mit den bearbeiteten Teilen.
**Herkunft:** Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 19 (SPEC-08 TEILWEISE, „Schnitt nur aus Vorlagen“, audit-2).
**Branch:** claude/T-0745-kleidung-schnitt
**Dateien/Bereich:** `src/Trendo.Core/W6/` (Schnitt-Kern, Produktion), `src/Trendo.App/MainWindow.ProgModeGarment.cs`, neuer Editor `src/Trendo.App/GarmentPatternEditor.cs`, Tests dazu, Belege `reports/T-0745/`.
**Fertig, wenn:**
- [ ] Teil anklicken, Punkte ziehen/einfügen/löschen, Gerade ↔ Kurve mit Griffen auf der Fläche
- [ ] Nahtzugabe, Zuschnitt, Fläche und Kantenlängen folgen sofort; Nahtlängen-Vergleich (Profi)
- [ ] Rückgängig je Schritt (Strg+Z), Kürzel (Entf, Einfg, K, Pfeile, Tab, Esc), Rechtsklick-Menü
- [ ] PDF/DXF/SVG und Anprobe-Auftrag enthalten die bearbeiteten Teile; DXF und SVG mit Fremdwerkzeug gelesen (Beleg mit SHA-256)
- [ ] gezielte Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** kein zweiter Geometrie-/Editor-Kern (Punkte und Griffe kommen aus `VNode`/`VectorKit`), keine Gradierung der freien Bearbeitung, keine Fremd-Schnittformate importieren, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0745-kleidung-schnitt.bericht.md`.
