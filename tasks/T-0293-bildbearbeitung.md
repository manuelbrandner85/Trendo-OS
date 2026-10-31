# T-0293 – Bildbearbeitung mit Ebenen

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Bildbearbeitung mit Ebenen“).
- Vorher: Fotos nur „in einem Schritt“ (Zuschnitt in Zahlen, Helligkeit/Kontrast, Text) als Kopie.
- Neu: Programm „Bildbearbeitung“ (Studio Videos & Fotos, Programm-Hülle: .tdoc, Rückgängig, Autosave, Bedienstufen).
  - Vorlagen: Leinwand 1920 × 1080, Beitrag 1080², Story 1080 × 1920, Plakat A4; das erste hinzugefügte Bild bestimmt die Leinwand.
  - Arbeitsfläche (`ImageCanvas`): eingepasst oder 100 %, Karomuster, Auswahlrahmen, Live-Vorschau beim Ziehen.
  - Werkzeuge: Verschieben (anklicken wählt die oberste Ebene, ziehen verschiebt), Pinsel und Radierer (auf einer Mal-Ebene, Radierer löscht nur dort), Text (Klick), Rechteck und Ellipse (aufziehen), Zuschneiden (Leinwand, Ebenen und Striche rücken mit). Farbpalette und Pinselgröße über der Fläche.
  - Ebenen (Bild, Text, Form, Malen): Liste oben→unten, ein-/ausblenden, wählen, Deckkraft, nach oben/unten, löschen; Text und Größe ändern; Filter je Bild-Ebene: Helligkeit, Kontrast, Sättigung, Unschärfe, Schwarzweiß, Sepia, Invertieren.
  - Export PNG (mit Transparenz bei transparentem Hintergrund) und JPG.
- Kern `Trendo.Core.Media.ImageDoc` (Modell, Ebenen, Striche, Treffer, Zuschnitt, Filter, Farbmatrix); Zeichnen `ImageRender` (Skia).
- Tests: `Bildbearbeitung_Ui` (echte Maus; Pixelprüfung für Pinsel, Rechteck, Schwarzweiß; Text, Verschieben, Zuschneiden, Rückgängig, PNG-Export) und Modelltests (Reihenfolge, Treffer, Zuschnitt verschiebt Striche, Filtergrenzen, Invertieren).
- Offen: Masken, Auswahlwerkzeuge (Lasso/Zauberstab), Ebenen-Mischmodi, Text mit eigener Schriftwahl.
