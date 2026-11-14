# T-0660 Bericht: Mixamo im Spiele-Studio (M1–M6)

**Auftrag:** Mixamo-Anbindung im Programm „Spiele bauen“, M1 bis M6, Freigabe des Nutzers am 14.11.2026 („Ja alle“), weitergeleitet von der Sitzung Spiele-Studio.
**Branch:** `claude/T-0660-mixamo`, von `origin/main` 92b46dd0a, eigener Worktree; vor dem Merge mit `origin/main` 890ef994c (inkl. Stufe 3) zusammengeführt, ohne Konflikte.
**Status:** gebaut, Reality Check in Blender und Unreal mit selbst gebauter Testfigur, zwei QA-Runden, alle Befunde behoben, Volltest siehe unten.

## Was jetzt geht

- **M1:** „Mixamo öffnen“ unter „Figuren“ zeigt mixamo.com im eingebauten Browser (eigenes Profil, nicht in der Sicherung). Im Fenster bleiben Mixamo, die Anmeldung (Adobe, Google, Apple, Facebook, Microsoft) und Downloads; andere Seiten öffnet der normale Browser. „Bei Mixamo abmelden“ verwirft die Sitzung. Trendo speichert keine Anmeldedaten.
- **M2:** Neue Mixamo-FBX und Pakete (ZIP) im Download-Ordner werden von selbst übernommen – erst, wenn sie fertig geschrieben sind, im Hintergrund, nur in das Spiel, in dem Mixamo geöffnet wurde. Erkennung am Inhalt (Knochen „mixamorig“); „With Skin“-Animationen gelten als Animation, die Art ist je Datei umschaltbar. Eintrag mit Quelle Mixamo und Lizenz „Mixamo-Bedingungen, nur im fertigen Spiel“. Entfernte Einträge kommen nicht wieder.
- **M3:** „Eigene Figur für den Auto-Rigger“: Blender entfernt Skelett, wendet Transformationen an, stellt die Füße auf den Boden und schreibt eine FBX mit Texturen; das Ergebnis von Mixamo kommt über M2 zurück.
- **M4:** „Mit Blender aufbereiten“: Präfix `mixamorig1:` usw. → `mixamorig:`, Meter-Maßstab mit Objektmaßstab 1 (auch Kurven), Wurzelknochen `root`, auf der Stelle oder Wurzelbewegung, Übertragen auf die Figur (Ausgleich der Ruhehaltung, Hüfthöhe nach Größe). FBX je Aktion für Unreal, eine GLB mit allen Aktionen für Godot. „Nach Unreal“: Import, IK Rig, IK Retargeter auf die Engine-Figur (Mover-Mannequin); wiederholbar. „Nach Godot“: GLB nach `assets/mixamo` mit `.gitignore`.
- **M5:** Zuordnung Stehen, Gehen, Laufen, Springen, Fallen, Getroffen (aus dem Dateinamen, änderbar), je Aktion drei Suchbegriffe, die Mixamo öffnen.
- **M6:** Lizenzwächter am Inhalt, unabhängig von der Endung, auch in verschachtelten ZIPs; bricht Browser-Paket, ZIP-Weitergabe und Export mit Meldung ab; Unprüfbares gilt als nicht freigegeben; der fertige Build ist erlaubt. Test: keine Mixamo-Dateien im Repo.

## Reality Check und Quality Gate (M4)

Mit selbst gebauter Testfigur (`tests/fixtures/mixamo/testrig.py`, Mixamo-Knochennamen, Zentimeter-Maßstab wie Mixamo), weil echte Mixamo-Dateien nur über das Adobe-Konto der Person kommen:
- Blender 5.2: Figur 1,77 m; Weg der Vorlage 2,0 m → auf der Stelle 0 cm Abweichung über alle Bilder; 17 von 17 Knochen übertragen; gerenderte Bilder zeigen Beine und Arme im Wechsel, Füße am Boden. Zwei Aktionen heißen in der GLB genau „gehen“ und „laufen“.
- Unreal 5.8 (leeres Prüfprojekt mit Mover-Beispiel): Figur 177 cm (keine Faktor-100-Falle), Animation 1,0 s wie in Blender, IK Rig und Retargeter angelegt, Übertragung auf das Mannequin, zweimal hintereinander erfolgreich.
- Quality Gate: Mechanik und Maßstab real belegt; ob die Bewegung echter Mixamo-Animationen glaubwürdig ankommt, ist erst mit echten Dateien prüfbar → offen.

## QA

- Runde 1 (keine Freigabe): 12 Befunde, u. a. „With Skin“ als Figur, GLB-Namen „.001“, zweiter Unreal-Lauf scheitert, Navigation ungefiltert, UI-Blockade, Wächter nur nach Endung. Alle behoben.
- Runde 2 (Freigabe mit Auflagen): 9 bestätigt, 3 teilweise; 12 neue (u. a. Download könnte vom Filter abgefangen werden, Temp-Ordner, Abmelden während Benutzung, BOM, Wiederaufleben entfernter Einträge, Löschen fremder Unreal-Assets). Alle behoben.

## Testergebnis

Volltest auf dem zusammengeführten Stand 8043cfe71 (`dotnet test Trendo.sln -c Release`, `python -m unittest discover -s tests/claude_handoff`):

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 2451 | 5 | 2456 | 1 m 28 s (Wiederholung, siehe unten) |
| Trendo.App.Tests | 0 | 1243 | 5 | 1248 | 37 m 36 s |
| tests/claude_handoff | 0 | – | – | OK | – |

Im ersten Durchlauf fiel 1 Kerntest („assimp echt über TrendoOS --assimp-convert“), weil er die Release-App braucht und die in diesem frischen Arbeitsordner beim Start der Kerntests noch nicht gebaut war (nicht von T-0660). Nach dem Bau der App: Kerntests vollständig wiederholt, 0 Fehler. SHA-256 der Protokolle (nur lokal): Volltest `C403A4CC7CE6FE6725F4B57BE70E019BADBFEDA294D16A03CA743E7B61B9EEE0`, Kern-Wiederholung `511F1699EBDACC43F490F2D3EC90EC71F8CE3331DB7729CEE740D9BDC5FD4B9C`.

## Nicht geprüft / offen

- Echter Mixamo-Download mit Anmeldung im eingebauten Browser (braucht das Adobe-Konto der Person).
- Echte Mixamo-Figur und -Animationen durch Blender und Unreal (Reality Check der Bewegung).
- Ob Verweise eigener Unreal-Assets auf die Mixamo-Animationen einen zweiten Lauf überstehen (Prüfskript konnte keine Testkopie speichern).
- Drehungen der ganzen Figur bleiben an der Hüfte, nicht am Wurzelknochen.
