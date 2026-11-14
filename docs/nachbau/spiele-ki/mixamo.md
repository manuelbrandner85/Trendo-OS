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

## Ergänzungen nach der QA (14.11.2026)

- M1: Auch Seitenwechsel im Fenster werden gefiltert. Im eingebauten Browser bleiben nur mixamo.com, Adobe und die Anmeldung über Google, Apple, Facebook oder Microsoft; alles andere öffnet der normale Browser. „Bei Mixamo abmelden“ löscht die Sitzung; sie wird nicht mitgesichert.
- M2: Animationen „With Skin“ (Mixamos Standard: Mesh und Kurven) gelten als Animation, wenn der Name eine Aktion verrät; die Art lässt sich je Datei umschalten. Pakete (ZIP) werden mit Größengrenze entpackt. Übernommen wird erst, wenn die Datei fertig geschrieben ist; Prüfen und Kopieren laufen im Hintergrund, Ereignisse werden gebündelt.
- M4: Mit eigener Figur heißen die Animationen in der GLB genau wie die Spielaktionen. Ohne eigene Figur wird das Skelett der ersten Animation die Figur, alle weiteren werden darauf übertragen. Unreal: wiederholter Lauf im selben Projekt geht (vorhandene Assets werden neu eingestellt); nach dem Import werden die Rohdateien aus dem Projektordner gelöscht. Godot: `.gitignore` in `assets/mixamo`.
- M6: Erkennung am Dateianfang, unabhängig von der Endung, auch verschachtelte ZIPs (bis Tiefe 3) als Datenstrom mit Größengrenze. Was sich nicht prüfen lässt, gilt als nicht freigegeben.

## Grenzen

- Kein automatischer Download, keine inoffizielle Schnittstelle, keine gespeicherten Adobe-Anmeldedaten.
- Die Blender-Übertragung setzt gleiche Knochennamen voraus (Mixamo auf Mixamo-Figur). Andere Skelette überträgt Unreal per IK Retargeter.
- Gekochte Engine-Pakete (.pak, .pck) prüft der Wächter nicht; darin stecken keine Rohdateien.
- Drehungen der ganzen Figur (Kurven, Wenden) bleiben an der Hüfte, nicht am Wurzelknochen.
- Die Blender-Übertragung ist mit gleicher Ruhehaltung geprüft (Testfigur); echte Mixamo-Dateien sind noch nicht durchgelaufen, weil sie nur über das Adobe-Konto der Person kommen.
- Ein ganzer Engine-Projektordner, der außerhalb von Trendo weitergegeben wird, ist nicht überwacht.
