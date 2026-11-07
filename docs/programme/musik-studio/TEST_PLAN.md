# Trendo Musik – Testplan

Studio: Trendo Musik (STUDIO-MUSIK-01) · Bereich: Kreativ & Medien · Seite: `musik-studio` · Stand: 0.9.36 (Stand 5e46a94)

52 Tests sprechen dieses Programm an.

- Abbrechen: voller Grafikspeicher wird gemeldet, hängendes Lied zeigt „■ Abbrechen“, der Klick stoppt es, nichts landet in der Bibliothek, ehrlicher Hinweis
- Cover-Werkstatt UI: Karaoke ohne Original meldet freundlich; mit Original trennt es die Spuren und startet das Mitsingen
- Cover-Werkstatt UI: Lied suchen und wählen, Original untersuchen (Bauplan sichtbar), ohne Text freundlicher Hinweis, Cover mit Tempo/Tonart/Takt, rote Ampel → neuer Versuch näher am Original → grün, Klang 1956 als neue Fassung
- Einfach-Modus mit Stimme: die Text-KI schreibt den Liedtext (Sprache, Länge, Abschnitte) – an ACE-Step geht dieser Text, nicht „[Instrumental]“, die Länge bleibt
- Einfach-Modus mit „Instrumental“: kein Text, kein Gesang – ACE-Step bekommt „[Instrumental]“
- Einfach-Modus ohne Text-KI: ACE-Step schreibt und singt selbst (sample_query in der Sprache), gesungener Text und echte Länge landen beim Lied
- Erstellen: Ablaufplan baut das Textgerüst (Texte bleiben), Klang-Referenz geht als Datei an ACE-Step
- Fortschritt lesen: Wert aus dem Ergebnis, sonst aus der Protokollzeile, fertig = 100 %
- Fortschritt: Prozent aus der Musik-KI im Text und als Balken, steigt nur an; ohne Angabe läuft der Balken unbestimmt
- Grafikspeicher-Warteschlange: zweites Lied wartet („noch 1 Auftrag vor dir“), startet erst nach dem ersten; Abbrechen im Warten geht
- Im Stil von aus dem Web: MusicBrainz und Wikipedia nachschlagen, die KI bekommt die Angaben als Grundlage; ohne Text-KI die Merkmale aus dem Web, mit Quelle
- KI-Musikvideo A: „Lied verstehen“ trennt den Gesang ab, erkennt Stimmen, Zeilen und Thema und zeigt wer wann singt
- Lied-Ansicht übersichtlich: Registerkarten Lied, Bearbeiten, Spuren & Mitsingen, Musikvideo – jede zeigt nur ihre Werkzeuge, keine Funktion fehlt
- M1/M8/M6: Genre-Auswahl (Gruppe, Suche, höchstens drei), Stimme, Tonlage und Instrumente landen als ACE-Step-Begriffe im Auftrag; Tempo-Vorschlag übernehmbar
- M10 Verlängern: Abschnitt ab dem Ende (30 → 90 s) mit dem Grundmodell, Refrain kommt dazu, neues Lied „– länger“
- M10 ohne Grundmodell: Hinweis auf das Grundmodell und „Grundmodell laden“ über das Download-Werkzeug von ACE-Step
- M2 „Im Stil von“: Stilmerkmale statt Namen – der Name wird entfernt, auch wenn die KI ihn nennt; ohne Text-KI ehrlicher Hinweis
- M3/M5: eigene Aufnahme hinzufügen → analysiert (Tonart, BPM, Takt, Tonumfang), Hinweis „nur privat“, „Als Vorlage“ setzt Tempo, Tonart und Takt im Profi-Modus
- M4/M9: eingefügten Text gliedern, Refrain wiederholen, Silben zählen, Refrain eingängiger (KI)
- M6 Eigene Stimme: ohne Zustimmung keine Aufnahme, lernen erst ab 6 Aufnahmen, Cover in der eigenen Stimme (Spuren → umwandeln → mischen), Löschen entfernt alles
- M7: Tonart und Tempo eines bekannten Songs (MusicBrainz + AcousticBrainz, Quelle sichtbar), übernehmbar; Archiv-Dienst ohne Messung = ehrlicher Hinweis
- Musik-Cover ohne Bild-KI: gestaltete Farbfläche mit Schrift und ehrlicher Hinweis; „MP3 mit Cover“ bettet das Bild in die Datei ein (alter Tag ersetzt, Ton unverändert)
- Musik-Cover: „🎨 Cover-Bild erstellen“ – Motiv zum Genre (Jazz-Club), 1400 × 1400 JPEG mit Titel und Name, erscheint in Bibliothek und Lied; Layout wechseln, Neu würfeln
- Musikvideo V3: ohne Schlüssel ehrlicher Hinweis; mit Schlüssel Suche nach Bildwörtern aus dem Stil, Clips geladen, Schlüssel im Tresor statt im Feld, Quellen notiert
- Musikvideo ohne Video-KI: ehrlicher Hinweis und Weg in die KI-Bibliothek; Abbrechen behält Fertiges
- Musikvideo: Shotliste aus den Abschnitten, eigene Clips, Rendern in 16:9 und 9:16 mit Look, Balken und Liedtext; dann in den Videoschnitt
- Musikvideo: Vorschau → Prüfung findet „3 Arme“ → automatisch neu (strengere Beschreibung) → grün; dauerhaft fehlerhaft = rot; Zusammensetzen im Videoschnitt
- Profi-Reiter: Mastern mit Stil/Ziel/Referenz, Übe-Fassung, MIDI, Loop, Kurzclip – jeweils neue Fassung, Original bleibt; FFmpeg ersetzt
- Profi: Liedtext der Text-KI mit KI-Floskeln wird einmal gezielt überarbeitet, bevor gesungen wird; die Songwriter-Regeln stehen im Auftrag
- Profi: neues Lied wird gleich gemastert (messen, dann linear auf −14 LUFS), Datei ersetzt, „✓ Gemastert“; ohne FFmpeg bleibt das Lied wie es ist
- Profi: „KI-Floskeln prüfen“ unter dem Liedtext nennt gefundene Phrasen oder meldet „keine gefunden“
- S1: 20 s aufnehmen → Analyse → Profil „Erika“ mit Zusammenfassung, Aufnahme wird gelöscht; „Für mein nächstes Lied“ setzt Stimme, Tonlage, Tonart und Stimmklang
- S1: zu wenig gesungen → ehrlicher Hinweis, kein Profil
- S4-04 Trendo Musik: Noten per Mausklick in die Piano-Roll, quantisieren, transponieren, als MIDI speichern und identisch öffnen; kaputte Datei → Warnung, Stück bleibt
- S4-13 Trendo Musik: Beat-Modus – Raster klicken (an → betont → aus), Tempo 140, Arpeggio ab; als Lied gespeichert (WAV 24 Bit + Muster); 600 BPM wird abgelehnt, nichts gespeichert
- S5: Mitsingen spielt die Begleitung, zeigt live Ziel/gesungener Ton und Treffer; S6: Mischen (Versatz, Hall) und Mastern ergeben „– mit meiner Stimme“
- S5: ohne getrennte Spuren ehrlicher Hinweis „zuerst Spuren trennen“, kein Start
- STUDIO-MUSIK-01: Claude als Produzent – Briefing mit Silben und Reimen, Plan mit Rhythmus vorher/nachher, Ausführen füllt den Entwurf, Erstellen hängt die Fassung ans Original, Mastering-Ziel „laut“
- Spuren trennen ohne Demucs: „einrichten“ lädt Demucs 4.0.1 mit PyTorch in eine eigene Umgebung
- Spuren trennen: Begleitung und Gesang werden eigene Lieder (mit Herkunft), alle Spuren im Ordner „– Spuren“
- Trendo Musik DAW: Transportleiste zeigt Takt 1.1 und Tempo, Arrangement mit Abschnitten, Text und Gesang/Begleitung; Fortschritt bewegt Takt-Anzeige; Mischpult legt Mehrspur-Projekt mit den Spuren an
- Trendo Musik Profi: Liedtext läuft mit (laufende Zeile gold), Zeile anklicken springt dorthin; Raster mit Tonart, Tonleiter, Takten; Fassungen A/B an gleicher Stelle
- Trendo Musik „＋ Eigene Aufnahme“: Dialog bietet MP3, M4A, AAC, OPUS, WMA, MP4 … an; M4A und MP4 werden echt in WAV umgewandelt und landen in der Bibliothek; Datei ohne Ton wird gemeldet
- Trendo Musik: Abschnitt neu und Cover laden das Lied hoch (kein Dateipfad), neue Fassung verweist aufs Original; Liedtext mit KI; Favorit, Export, Löschen in den Papierkorb
- Trendo Musik: Aufträge an ACE-Step geprüft – Grenzen für Länge, Tempo, Varianten; Abschnitt mind. 1 s; über 8 Minuten ohne Sprachmodell; unbekannte Tonart/Takt/Aufgabe abgelehnt
- Trendo Musik: Einfach – ein Satz → zwei Varianten in „Meine Lieder“; ohne Beschreibung nur Hinweis; Profi mit Tempo, Tonart, Takt, [Strophe]/[Refrain] und Spanisch
- Trendo Musik: Treffer anklicken übernimmt Titel, Länge, Stil ohne Namen, Tonart und Tempo ins Profi-Formular; ohne Messung freundlicher Hinweis; MusicBrainz-Seite öffnet sich dabei nicht
- V1/V2/V7 Trendo Musik: hochgeladenes Lied wird gleich getrennt, Qualität gemessen (sauber), Spuren-Mischer spielt in Trendo (Gesang stumm leiser), Mischung als Lied; Modellwahl „Genau“; ohne Auto-Trennen nichts getrennt
- V3/V6: Stimme aus Gesang lernen nur mit Einwilligung (bekannte Person gesperrt, fremde Person braucht Erklärung), zu wenig Gesang abgelehnt, gelernte Stimme in der Bibliothek, Löschen entfernt alles
- V4/V5: neues Lied in der gelernten Stimme (Umwandeln mit der Stimm-Id, neues Lied „(Stimme …)“); neuer eigener Text auf die Begleitung – ACE-Step bekommt Cover-Auftrag mit dem Text
- Wie bei Suno: „Erstellen“ mit „Mehr Optionen“ (alle Einstellungen bleiben), „⋯“-Menü je Lied mit allen Aktionen
- Y1: YouTube-Link → Titel/Interpret per oEmbed (kein Download), Stil aus dem Web, Titel vorgeschlagen, Profi-Modus; falscher Link = Hinweis
