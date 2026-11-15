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

## Zweite QA-Runde (14.11.2026)

- Downloads im eingebauten Browser werden nicht abgefangen: Speicher-Adressen (Amazon S3, CloudFront), blob/data und Adressen auf .fbx/.zip laufen weiter. Weitere Anmeldewege (Microsoft-Privatkonto, Google-Länderseiten, Facebook mobil, Apple). Fremde Seiten öffnen höchstens einmal je 3 Sekunden im normalen Browser. Ein echter Mixamo-Download mit Anmeldung ist noch nicht geprüft (braucht das Adobe-Konto der Person).
- Abmelden: Ist das Profil noch belegt, bekommt der Browser ein neues, leeres Profil; das alte wird beim nächsten Öffnen gelöscht.
- Download-Beobachter: übernimmt nur in das Spiel, in dem Mixamo geöffnet wurde; wiederholt sich, solange eine Datei noch geschrieben oder gesperrt ist (höchstens 10-mal); einmal übernommene oder entfernte Downloads kommen nicht wieder; keine verwaisten Kopien; Zwischenordner beim Entpacken wird immer gelöscht.
- Wächter: erkennt auch Dateien mit UTF-8-BOM und JSON/XML ohne Kennung am Anfang; öffnet Dateien, an denen gerade geschrieben wird.
- Unreal: wiederholter Lauf überträgt in einen Zwischenordner und ersetzt gleichnamige Animationen per „Consolidate“, andere Assets im Ordner bleiben. Ob Verweise eigener Assets erhalten bleiben, ist nicht real geprüft (das Prüfskript konnte seine Testkopie nicht speichern).

## Prüfung mit echter Mixamo-Datei (15.11.2026, T-0661)

Datei der Person „Walking Left Turn.fbx“ (FBX binär, With Skin, 30 Bilder/s, 1,87 MB), selbst über das Adobe-Konto geladen. Sie liegt nur im Download-Ordner der Person; nichts davon steht im Repo oder in Testdaten.

- M2: aus dem Download-Ordner erkannt (Mixamo, Mesh und Kurven), als Animation eingestuft, Aktion „Gehen“, Quelle und Lizenz gesetzt, zweite Übernahme abgelehnt.
- M6: Asset-Paket, Browser-Paket (Datei als „.bin“ umbenannt) und ZIP-Weitergabe abgebrochen; fertiger Build erlaubt; aufbereitete FBX weiter als Mixamo erkannt.
- M4 Blender: 66 von 66 Knochen, Maßstab 0,01 → Meter, Figur 1,79 m, 37 Bilder. Auf der Stelle: 1,68 m Weg → 0 cm Abweichung über alle Bilder; Wurzelbewegung: Weg des Originals bleibt. Bilder: Pose und Linksdrehung wie im Original, Füße am Boden.
- M4 Godot 4.7: GLB importiert, Animation „gehen“ 1,23 s, 66 Knochen, Figur 1,81 m.
- M4 Unreal 5.8 – fünf Fehler gefunden und behoben, jeweils am Bild bzw. an Messwerten belegt:
  1. Kettenzuordnung FUZZY hängte Drehknochen ans Schlüsselbein und die Wurzel an den rechten Fuß → jetzt exakte Namen plus Fuß-Ziele (20 sinnvolle Ketten).
  2. Die veraltete Batch-Funktion `duplicate_and_retarget` lieferte eine leere Animation (0 Bilder), gemeldet wurde trotzdem „übertragen“ → jetzt `run_batch_retarget`, gezählt wird nur Inhalt (Bilder, Spuren).
  3. Beim Ersetzen blieb die alte, leere Animation stehen → alte wird nach „Consolidate“ gelöscht, neue verschoben und erneut geprüft.
  4. FBX in Metern: Unreal legte den Maßstab 100 in den Wurzelknochen, die Hüfte stand bei 1,0 → Unreal-FBX jetzt in Zentimetern (Wurzel 1,0, Hüfte 102 cm, Figur 179 cm).
  5. Becken der Engine-Figur am Boden (z = 0), Oberschenkel oben → Mesh zog sich wie ein Rock nach unten. Ursache: Wurzelbewegungs-Operation von UE 5.8. Auf der Stelle ist sie jetzt aus; mit Wurzelbewegung sind ihre Knochen ausdrücklich gesetzt. Ergebnis: Becken 95 cm; mit Wurzelbewegung läuft die Engine-Figur 1,51 m und dreht sich mit. Jeder Lauf prüft die Beckenhöhe und meldet „Becken am Boden“ als Fehler.
- Bilder und Protokoll liegen nur lokal (Scratchpad der Sitzung), nicht im Repo.

## Grenzen

- Kein automatischer Download, keine inoffizielle Schnittstelle, keine gespeicherten Adobe-Anmeldedaten.
- Die Blender-Übertragung setzt gleiche Knochennamen voraus (Mixamo auf Mixamo-Figur). Andere Skelette überträgt Unreal per IK Retargeter.
- Gekochte Engine-Pakete (.pak, .pck) prüft der Wächter nicht; darin stecken keine Rohdateien.
- Drehungen der ganzen Figur (Kurven, Wenden) bleiben in Blender an der Hüfte; in Unreal überträgt die Wurzelbewegung sie auf die Engine-Figur.
- Auf der Engine-Figur bleiben die Drehknochen (Twist) und Mittelhandknochen ohne eigene Quelle; sie folgen ihren Eltern.
- Geprüft ist eine echte Mixamo-Animation mit Standardfigur; eigene Figuren aus dem Auto-Rigger noch nicht.
- Ein ganzer Engine-Projektordner, der außerhalb von Trendo weitergegeben wird, ist nicht überwacht.
