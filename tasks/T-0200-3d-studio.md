# T-0200: 3D-Studio – eigene Objekte bauen, Blender mit einem Klick

**Ziel:** Im Studio „3D-Modelle erstellen“ kann ich ein eigenes Objekt beschreiben oder zusammenbauen, ansehen, exportieren und mit einem Klick in Blender weiterbearbeiten.
**Branch:** grok/T-0200-3d-studio
**Dateien/Bereich:** `src/Trendo.App/MainWindow.W4Studio3D.cs`, `src/Trendo.Core/Studio3D.cs`, neue Hilfsdateien unter `src/Trendo.Core/` (3D), Tests `tests/Trendo.App.Tests/W4Studio3DUiTests.cs`, `tests/Trendo.Core.Tests/`.
**Befund (Audit 22.10.):** nur 4 feste Formen (Würfel, Kugel, Affenkopf, 3D-Schrift), keine Maße, kein Material außer einer festen Farbe, kein Import per Dateiauswahl, „Blender öffnen“ startet Blender ohne das Modell, Vorschau nur mit Blender.
**Fertig, wenn (aus Sicht des Nutzers):**
- [ ] Ich wähle eine Grundform (mind. Würfel, Kugel, Zylinder, Kegel, Torus, Ebene, Text) und stelle Größe (X/Y/Z in cm), Position, Drehung und Farbe ein.
- [ ] Ich kann mehrere Teile zu einem Objekt zusammenfügen, ein Teil auswählen, ändern und löschen (Liste der Teile mit Rückgängig).
- [ ] Ich kann Werkzeuge anwenden: abrunden (Bevel), glätten (Subdivision), spiegeln, aushöhlen (Solidify), Loch bohren (Boolean).
- [ ] Ich kann in einem Satz beschreiben, was ich will („ein Tisch 120 × 80 cm mit vier Beinen“); die lokale KI erzeugt daraus die Teile-Liste, die ich vor dem Bauen sehe und ändern kann. Ohne KI ist der Knopf deaktiviert mit Grund.
- [ ] „Datei öffnen …“ mit echtem Datei-Dialog und Drag & Drop für .glb/.gltf/.obj/.stl/.fbx/.blend.
- [ ] „In Blender öffnen“ öffnet genau das aktuelle Modell (als .blend) in Blender. Fehlt Blender: ein Knopf „Blender einrichten“, danach geht es ohne Neustart weiter.
- [ ] Vorschau-Bild (gerendert) direkt im Studio, nach jeder Änderung aktualisierbar; drehbare Ansicht von mind. 4 Seiten.
- [ ] Export nach .glb, .obj, .stl (3D-Druck, mit Maßen in mm), .fbx, .blend – Datei liegt danach im 3D-Ordner und „Ordner zeigen“ markiert sie.
- [ ] Jede Aktion zeigt sichtbar Ergebnis oder verständlichen Fehler; kein Knopf ohne Wirkung (Test).
- [ ] Am PC mit echtem Blender: Tisch bauen → STL exportieren → in Blender öffnen; Screenshot und Datei-Nachweis im Bericht.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Blender nicht mitliefern oder ungefragt installieren, keine Studionamen ändern, keine anderen Studios.
**Am Ende:** Branch pushen, Bericht `tasks/T-0200-3d-studio.bericht.md`. Keinen PR öffnen (macht Grok Bot).
