# Bericht T-0704: Übersicht „Einfach“ wieder grün

## Was war
`UebersichtEinfach_Ui` rot (2/2) nach T-0691 (3D/CAD), T-0694 (Geld & Rechnungen), T-0697 (Comics/Podcast):
- `Simple_keeps_every_function`: in „Einfach“ fehlten 36 Bedienelemente (Comic: Pinsel, Strichstärke, Farbe + 10 Farbfelder, Ebenen, Export CBZ/Bildfelder; Finanzen: DATEV, Kredit-Sonderfälle, Steuersätze; Podcast: Folgennummer, Jahr, WAV-Export).
- `Every_view_stays_clear`: Seite „3d“ hatte in „Einfach“ 43 Knöpfe (Grenze 40).

## Was ist jetzt
- **Comic (Kern des Fachs, jetzt sichtbar):** In „Einfach“ (Eigenschaften eingeklappt) steht über der Seite eine Zeichenleiste: „Pinsel: <Name> ▾“, „Farbe ▾“ (10 Farbfelder + eigene Farbe #RRGGBB), Strichstärke als Regler, „Ebenen ▾“ (zeigen/ausblenden/wählen, „Neue Ebene“). Dieselben Bedienelemente wie in den Eigenschaften (gemeinsame Bauhilfen, keine Doppel-Logik). Exporte in „Einfach“: PDF, PNG, CBZ, Bildfelder (vorher nur PDF/PNG); SVG und Storyboard-Liste ab „Erweitert“ wie bisher.
- **3D:** In „Einfach“ stecken die sechs Ansichten im Knopf „Ansicht: Schräg ▾“ (3d-view-menu) → 38 Knöpfe; Erweitert/Profi unverändert einzeln.
- **Finanzen, Podcast:** Profi-Werkzeuge bleiben in „Einfach“ verborgen, Einträge mit Begründung, Paket und Ersatzweg in `einfach-vorher-ausgeblendet.txt` (wie T-0670/T-0684). Ersatzweg Finanzen: Bedienstufe „Erweitert“ (Einstellungen → Bedienung → Sehen); Podcast: „Mehr“ → „Mehr Werkzeuge (Erweitert)“ bzw. Knopf „Eigenschaften“.
- Grenzen 40/8/400 unverändert, keine Prüfung gelockert.

## Tests (gezielt, kein Volltest)
- `UebersichtEinfach_Ui` 2/2 grün (vorher 0/2).
- Neu: `T0697ComicPodcast_Ui.Comic_Einfach_Zeichenleiste` – Stufe Einfach, Pinsel und Farbe aus der Leiste gewählt, Strichstärke gesetzt, echter Mausstrich im Bildfeld hat die gewählte Farbe, CBZ im Menü „Mehr“. Rot auf altem Code („Sequence contains no matching element“), grün danach. Echt, nichts ersetzt.
- T0697ComicPodcast_Ui, KreativStudios_Ui, ProgramHeadDesign_Ui, GeldProgramm_Ui, SimpleMode_Ui, UebersichtVorschlaege_Ui, Studio3DViewport_Ui, W4_Core05_Ui, Studio3DPro_Ui, Studio3DMeshEditUiTests, StudiosKreativ3D_Ui: grün (85 + 20 Fälle).
- Bildprüfung mit Trendo.Render (eigener TRENDO_RENDER_ROOT, Stufe einfach): Comic-Leiste und 3D „Ansicht: Schräg ▾“ angesehen.

## Abnahmekriterien
1. Leere Vorlage → Ergebnis: NICHT GETESTET in diesem Paket (Einzelauftrag Übersicht; Comic-Weg bis PDF/CBZ deckt T-0697 ab).
2. Fremddatei/Fremdprogramm: NICHT GETESTET (nicht Teil des Auftrags).
3. Abbruchsicherheit: unverändert, NICHT GETESTET.
4. KI-Fachaufgabe: unverändert, NICHT GETESTET.
5. Klick bis Datei: TEILWEISE – Klick bis Strich im Dokument geprüft, Export nicht erneut.
6. Einfach/Profi: VOLLWERTIG für die betroffenen Ansichten (Übersicht 2/2, Zeichnen in Einfach).

## Nicht geprüft / offen
- Volltest (macht die Hauptsitzung).
- Finanzen-Seite hat selbst keinen Knopf „Mehr Werkzeuge“ – die Stufe wechselt man in den Einstellungen; ein Stufen-Knopf auf der Seite wäre ein eigener Vorschlag.
