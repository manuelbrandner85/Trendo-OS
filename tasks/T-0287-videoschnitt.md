# T-0287 – Videoschnitt als echtes Programm (Zeitleiste, Export MP4)

- Freigabe: Manuel im Chat am 31.10.2026 („Ja, weiter umbauen“).
- Vorher („Medien bearbeiten“): eine Datei, Anfang/Ende per Schieberegler, Clips nur hintereinanderhängen. Bleibt als Reiter für Umwandeln und Bildbearbeitung erhalten.
- Neu: Programm „Videoschnitt“ (Studio Videos & Fotos, Programm-Hülle: .tdoc, Rückgängig, Autosave, Wiederherstellen, Bedienstufen).
  - Vorlagen: HD 16:9, Full HD, Hochformat 9:16.
  - Vorschau des Bildes am Abspielkopf (Standbild direkt, Video über FFmpeg, zwischengespeichert), Zeitanzeige.
  - Zeitleiste (`EditTimeline`): Lineal, Bildspur mit Vorschaubildern (lückenlos), Musikspur mit Wellenbild und Lautstärke, Abspielkopf.
  - Maus: Stück ziehen ordnet um, Kanten ziehen kürzt (innerhalb der Quelle), Musik frei verschieben, Klick ins Lineal setzt den Abspielkopf (ziehen = scrubben), Klick wählt.
  - Tasten: S teilen, Entf löschen, ←/→ Bild für Bild (Umschalt: 1 s), Strg+Z/Y.
  - Rechts: Medien hinzufügen (Datei wählen/ziehen, zuletzt benutzte), gewähltes Stück genau kürzen, Musik-Start und Lautstärke.
  - Export MP4 (H.264/AAC): Stücke zugeschnitten, auf Projektgröße/Bildrate gebracht, Fotos als Standbild, Stille für Stücke ohne Ton, Musik mit Verzögerung und Lautstärke gemischt.
- Kern `Trendo.Core.Media.VideoEdit`: Modell, Teilen, Kürzen, Umordnen, Musik, Export-Aufruf, Export mit Ergebnisprüfung, Tonspur-Erkennung.
- Katalog: Programme der Programm-Hülle sind eigene Einträge, auch wenn sie als Reiter im Fenster eines Studios stehen (jetzt 88 Programme; Videoschnitt, Punktwolke-Programm und Spiele-Programm eigenständig).
- Tests: Kern 3 (Operationen, Export-Aufruf, **echter Export mit FFmpeg**: Testbild mit Ton + Standbild + Musik → MP4 mit Bild und Ton in richtiger Länge); App `VideoSchnitt_Ui` 2 (echte Maus/Tastatur, Rückgängig, Export, Einfach-Stufe, Fehlermeldungen).
- Offen/bewusst nicht: Abspielen in Echtzeit im Programm (Vorschau ist Standbild am Abspielkopf; fertiges Video öffnet der Player), Übergänge, Texteinblendungen, Export im Hintergrund mit Fortschritt (Export läuft derzeit im Vordergrund).
