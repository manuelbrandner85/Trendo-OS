# Echter Lauf: IFC-Dateien in 3D ansehen (Wohnung planen) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“ und „mach einfach alles automatisch“.

## Aufbau
- Seite `src/Trendo.App/Assets/ifc` (Quellen `tools/ifc-ansicht`, Vite, ein klassisches Skript). Sie nutzt:
  - **web-ifc 0.0.78** (ThatOpen, MPL-2.0, unverändert) zum Lesen der IFC-Datei,
  - **three.js 0.180** (MIT) zum Zeichnen.
- Die WASM-Datei von web-ifc ist im Bündel eingebettet, es gibt kein Netz und keinen Worker.
  - `@thatopen/components` wurde bewusst nicht genommen: Seine Worker- und WASM-Dateien bräuchten unter `file://` eigene Wege. web-ifc ist der Kern von ThatOpen und reicht zum Ansehen.
- Wohnungsplaner, Menüband „BIM“:
  - **IFC ansehen:** speichert den aktuellen Grundriss als IFC und zeigt ihn in 3D. Ein zweiter Klick schließt die Ansicht.
  - **IFC öffnen …:** zeigt eine beliebige IFC-Datei, z. B. vom Architekten.
  - Die Ansicht füllt die Arbeitsfläche. Trendo schickt die Datei über die Brücke (`window.trendoIfc`). Räume (IfcSpace) werden ausgeblendet, weil sie die Wände verdecken würden.

## Läufe
- **Browser** (Testkopie mit nachgebildeter Brücke): `ifc-musterwohnung.ifc` aus dem IFC-Export (Vorlage „2-Zimmer-Wohnung“) wird gelesen. Ergebnis: 14 Wände mit ausgeschnittenen Tür- und Fensteröffnungen, Anzahl 14 Wände, 6 Türen, 3 Fenster, 5 Räume.
- **Echtes Trendo-Fenster** (eigene Testfassung und Datenordner, `file://`):
  - „IFC öffnen …“ über die Windows-Dateiauswahl.
  - web-ifc startet mit eingebetteter WASM. Gemeldet werden **14 Wände, 6 Türen, 3 Fenster, 5 Räume**.
  - Bild aus dem WebView per CDP: `ifc-ansicht-trendo.png` (SHA-256 649f58e15eb5d8a68355b652a1dc1e797389d9f60e04799ec5f877c1425d4b93).
  - Bildschirmfoto: `ifc-ansicht-bildschirm.jpg` (SHA-256 561ef4c2321beb42ed812a18ff325cad35b378fef8f773eedaad62e958781e56).

## Befund (behoben)
- Auf diesem Bildschirm stellt der WebView Farben deutlich heller dar als im Seitenbild (Test: #2a6 erschien hell-mint). Vermutlich hebt HDR oder das Farbprofil an.
- Folge: Hellgraue Wände vor hellgrauem Grund wurden zu reinem Weiß, die Fläche wirkte leer.
- Behoben mit dunklem Hintergrund im Trendo-Look und gedämpfter Belichtung (`toneMappingExposure` 0,75, schwächeres Licht). Auf dem Bildschirm sind danach Wände, Schattierung und Öffnungen gut erkennbar.
- Die Vektorkarte wirkt auf diesem Bildschirm aus demselben Grund etwas blasser. Lesbar ist sie, ein Umbau ist nicht nötig.

## Tests
- `IfcViewUi` (Oberfläche): Menüband exportiert und öffnet die Ansicht; „bereit“ schickt die Datei als Base64; „geladen“ zeigt die Bauteile; kaputte oder fremde Nachrichten werden ignoriert; eine fehlende Datei wird abgelehnt; „IFC öffnen …“ öffnet die gewählte Datei; zweiter Klick schließt. Grün.

## Grenzen
- Türen und Fenster aus Trendos eigenem Export haben noch keine eigene Form, sichtbar sind nur die Öffnungen.
- Messen und Schnitte gibt es nicht. Das ist eine reine Ansicht.

## Nachtrag (Volltest 08.11.2026)
- Die drei BIM-Knöpfe ließen das Menüband des Wohnungsplaners in eine zweite Zeile umbrechen. Dadurch rutschte der Foto-Knopf aus dem Bild, und drei bestehende Oberflächentests schlugen fehl (T-0257, T-0280).
- Behoben: Die BIM-Knöpfe erscheinen erst ab der Stufe „Professionell“. Der Export „IFC (BIM)“ bleibt unter Exportieren und nennt im Hinweis die Stufe.
- Kartenbündel: Schriften und Symbole sind jetzt hexadezimal eingebettet statt Base64. Base64 enthielt zufällig den Namen des früheren Entwicklungswerkzeugs, den der Paket-Wächter sucht. Nach dem Umbau gibt es 0 Treffer, die Karte ist im Browser unverändert (53 Symbole, 46 Beschriftungen, keine Fehler).
