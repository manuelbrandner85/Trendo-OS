# T-0284 – „3D-Modelle erstellen“: echte 3D-Arbeitsfläche statt Vorschaubild

- Freigabe: Manuel im Chat am 31.10.2026 („Erst Programme umbauen“) – erster Umbau nach dem Programmkatalog (T-0283), der hier „keine eigene Arbeitsfläche“ fand.
- Vorher: Eingabefelder links, ein 480 × 360-Bild der Szene rechts (260 px breit), Teile nur über Zahlen bewegbar.
- Jetzt: drei Spalten – Teile/Objekt links, **3D-Arbeitsfläche** in der Mitte (CadView mit Licht und Schatten aus T-0281), Werte des gewählten Teils rechts.
  - Formen mit einem Klick einsetzen (neben die vorhandenen Teile, auf den Boden gestellt, gleich ausgewählt).
  - Teil anklicken wählt es (Auswahl farbig, auch an runden Teilen), Klick ins Leere hebt die Auswahl auf.
  - Ausgewähltes Teil mit der Maus ziehen: in der Bodenebene, bei waagrechtem Blick hoch/runter; 0,5-cm-Raster; ein Schritt im Rückgängig.
  - Daneben ziehen dreht die Ansicht, Mausrad zoomt, Doppelklick ins Leere setzt die Ansicht zurück; Ansicht bleibt beim Bearbeiten erhalten.
  - Tasten: Pfeile 1 cm (Umschalt 10 cm), Bild↑/↓ Höhe, Strg+D verdoppeln, Entf löschen, Esc Auswahl aufheben.
  - Werkzeuge: Verdoppeln, Auf den Boden, Löschen, Ansichten (Schräg, Vorne, Oben, Rechts, Hinten, Links).
- `CadView`: Bearbeiten-Schnittstelle (`PartClicked`, `PartMoved`, `PickAt`, `ScreenOf`, `ScreenToWorld`, `Frame`, `Snap`) – CAD und Grundriss nutzen sie nicht und verhalten sich wie bisher.
- Tests: neu `Studio3DViewport_Ui` (2, echte Maus und Tastatur); angepasst `W4_Core05_Ui`, `PromptBarTests` (Arbeitsfläche statt Bild), `StudioHeadingTests` (Programmkatalog ist Systemseite).
