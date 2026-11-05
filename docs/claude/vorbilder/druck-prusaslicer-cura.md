# Vorbild-Analyse Trendo 3D-Druck: PrusaSlicer, Cura, Bambu Studio (Clean-Room)

- Programme: „Trendo 3D-Druck“
- Vorbilder: PrusaSlicer, UltiMaker Cura, OrcaSlicer, Bambu Studio (frei), Simplify3D (kostenpflichtig) – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 04.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Modell laden (STL, 3MF, OBJ, GLB, aus Trendo CAD), Druckplatte in 3D
- ✓ Überhänge zeigen, automatisch aufstellen, um X/Y drehen
- ✓ Eigenes Slicen für die Vorschau: Umrisse (Wände) und Füllung je Schicht, Winkel wechselt, Ober-/Unterseite massiv
- ✓ Druckeinstellungen: Material (PLA, PETG, ABS, TPU, ASA mit Temperaturen), Schichthöhe 0,12–0,28 mm, Wände, Füllung 0–100 %
- ✓ Zeit, Filament (m und g) und Kosten aus den eigenen Bahnen; Stützen-Schätzung bei Überhang
- ✓ Skalieren in Prozent, Kopien automatisch auf der Platte anordnen (PrusaSlicer „Instanzen“ + „Anordnen“)
- ✓ Prüfung: passt auf die Platte, Überhang
- ✓ Bambu Studio einrichten und übergeben, Drucker im Heimnetz (Status, Kamera, Pause, Abbrechen), Druck nur nach Bestätigung
- ✓ Bambu-Drucker im Netz finden (ihre Meldungen auf Port 2021), Übernehmen trägt IP, Seriennummer und Modell ein; Prüfung „gleiches Netz wie der PC“
- ✓ Einrichtungshilfe je Modell: ins WLAN (2,4 GHz), „Nur LAN“, Entwicklermodus (ehrlich: ohne ihn kann Trendo nur zusehen), Zugangscode; WLAN-Signal des Druckers in Klartext
- ✓ Steuerung wie in Bambu Studio: Kammerlicht, Tempo (leise bis maximal), Düse, Bett, Teile-Lüfter – mit Grenzen; Filamente im AMS mit Farbe, Rest und aktivem Fach; Druckermeldungen (HMS) mit Bereich und Link zum Nachschlagen
- ✓ Live-Kamera (alle 2 s), Meldung „Druck fertig/fehlgeschlagen“, Zeitraffer beim Start aufnehmen und das neueste Video holen
- ✓ Direkt aus Trendo drucken: Bambu Studio bereitet im Hintergrund vor (aufstellen, anordnen, slicen), danach Hochladen und Drucken nach Bestätigung
- ✓ Mehrere Drucker mit Zustand, Auswählen und Entfernen (Zugangscodes nur im Geheimnisspeicher)
- ◐ Stützen werden geschätzt, aber nicht als Bahnen erzeugt
- ✗ Eigener G-Code – bewusst nicht: die Bahnen für den Drucker rechnet der Fachslicer (Sicherheit für Drucker und Düse)
- ✗ Mehrere verschiedene Modelle auf einer Platte (nur Kopien desselben)

## Bedienkonzept (Profi-Bauplan A1–A3)
Wie ein Slicer: Werkzeugleiste oben, links Datei und Drucker, Mitte Druckplatte, rechts Schichten, Einstellungen und Prüfungen.

## Nächste Schritte
1. Mehrere Modelle auf einer Platte
2. Stützen als Vorschau-Bahnen
