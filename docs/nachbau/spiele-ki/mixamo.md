# Mixamo im Spiele-Studio (T-0660)

Stand: 14.11.2026. Freigabe des Nutzers: M1 bis M6 („Ja alle“).

## Grundlage

- Mixamo (Adobe) erlaubt Figuren und Animationen in eigenen Spielen, auch verkauft. Verboten ist, die rohen Dateien weiterzugeben (z. B. als Asset-Paket).
- Es gibt keine offizielle Schnittstelle. Herunterladen braucht ein kostenloses Adobe-Konto, die Anmeldung macht die Person selbst.
- Trendo greift nichts ab: Browser plus Übernahme aus dem Download-Ordner, wie bei den freien Asset-Seiten.
- Trendo liefert keine Mixamo-Dateien mit. Tests nutzen eine selbst gebaute Testfigur mit Mixamo-Knochennamen (`tests/fixtures/mixamo/testrig.py`).

## Funktionen

| ID | Funktion | Umsetzung | Prüfung |
|---|---|---|---|
| M1 | Mixamo im eingebauten Browser | Knopf unter „Figuren“, WebView2 mit eigenem Profil `web-mixamo`; nur mixamo.com und die Adobe-Anmeldung bleiben im Fenster | Oberflächentest (Testmodus ohne Laden), Test der erlaubten Adressen |
| M2 | Downloads übernehmen | Beobachter auf dem Download-Ordner, erkennt Mixamo am Inhalt (Knochen „mixamorig“), Figur oder Animation am Mesh; Eintrag mit Quelle Mixamo und Lizenz „Mixamo-Bedingungen, nur im fertigen Spiel“; keine Doppelten | Kern- und Oberflächentest |
| M3 | Eigene Figur zum Auto-Rigger | Blender ohne Fenster: Skelett entfernen, Transformationen anwenden, Füße auf den Boden, FBX mit Texturen | Test mit echtem Blender 5.2 |
| M4 | Aufbereiten | Blender: Präfix `mixamorig1:` usw. → `mixamorig:`, Meter-Maßstab mit Objektmaßstab 1 (auch Kurven), Wurzelknochen `root`, auf der Stelle oder Wurzelbewegung, Übertragen auf die Figur (Drehungen mit Ausgleich der Ruhehaltung, Hüfthöhe nach Größe); FBX je Aktion für Unreal, eine GLB mit allen Aktionen für Godot. Unreal: Import, IK Rig, IK Retargeter auf die Engine-Figur (Mover-Mannequin). Godot: GLB nach `assets/mixamo` | Blender echt mit Testfigur, Reality Check am Bild; Unreal siehe Bericht |
| M5 | Spielaktionen | Stehen, Gehen, Laufen, Springen, Fallen, Getroffen; Zuordnung aus dem Dateinamen, änderbar; je Aktion drei Suchbegriffe, die Mixamo im eingebauten Browser öffnen | Kern- und Oberflächentest |
| M6 | Lizenzwächter | Erkennt Mixamo-Rohdateien am Inhalt, auch in ZIP und umbenannt; bricht Browser-Paket, ZIP-Weitergabe und Export mit Meldung ab; der fertige Build ist erlaubt | Kerntests, Test „keine Mixamo-Dateien im Repo“ |

## Grenzen

- Kein automatischer Download, keine inoffizielle Schnittstelle, keine gespeicherten Adobe-Anmeldedaten.
- Die Blender-Übertragung setzt gleiche Knochennamen voraus (Mixamo auf Mixamo-Figur). Andere Skelette überträgt Unreal per IK Retargeter.
- Gekochte Engine-Pakete (.pak, .pck) prüft der Wächter nicht; darin stecken keine Rohdateien.
