# 3D – Testergebnisse

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `3d` · Stand: 0.9.36 (Stand 66211cf)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 49 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| 3D Für Engine: ohne Teil Hinweis; vier Ziele; Unity-Export mit Bericht-Datei; Verlust (Material) wird als Fehler gemeldet | ✓ bestanden |
| 3D aus Bild: TripoSR nach Klick einrichten (Stand geprüft, eigene Umgebung), Foto wählen → GLB → „Im 3D-Studio öffnen“; falscher Stand wird nicht eingerichtet | ✓ bestanden |
| 3D-Arbeitsfläche: Formen einsetzen (auf dem Boden), Teil anklicken, mit der Maus verschieben (Raster), Pfeiltasten, Verdoppeln, Entf, Rückgängig | ✓ bestanden |
| 3D-Arbeitsfläche: „Auf den Boden“ setzt die Unterkante auf 0, Text und Ebene stehen auf dem Boden | ✓ bestanden |
| 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern | ✓ bestanden |
| 3D-Studio: Kreatur-Karte – Drache in Bronze/Grün, „Vorschaubild rendern“ startet Blender im Hintergrund, „sichtbar“ ohne -b; Ergebnis mit Vorschau | ✓ bestanden |
| 3D: OBJ, STL und GLB speichern, Datei öffnen, Projekt wieder öffnen | ✓ bestanden |
| 3D: Satz wird zum Vorschlag und erst nach Übernehmen gebaut | ✓ bestanden |
| 3D: Teil hinzufügen, ändern, Vorschau, löschen, rückgängig | ✓ bestanden |
| 3D: Vorschau und Export sind bei üblichen Fenstergrößen ohne Scrollen sichtbar | nicht im Lauf |
| 3D: ohne Blender einmal fragen, mit Blender das aktuelle Objekt öffnen | ✓ bestanden |
| Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche | ✓ bestanden |
| Baukasten übernimmt die neue Seite ins Formular | ✓ bestanden |
| Briefseite zeigt den echten Brieftext in der Vorschau | ✓ bestanden |
| Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist | ✓ bestanden |
| CORE-05 B1: Drucker im Netz finden – Bambu-Meldung ergibt IP, Seriennummer, Modell (P1S), Name, LAN-Modus; fremde Geräte und kaputte Meldungen fallen weg; gleiches Netz wie der PC | ✓ bestanden |
| CORE-05 B1–B5: Drucker finden und übernehmen, Einrichtung mit WLAN-Schritten, verbinden, Licht/Tempo/Düse steuern, AMS und Meldungen sehen, Fertig-Meldung, mehrere Drucker, vorbereiten und mit Zeitraffer drucken | ✓ bestanden |
| CORE-05 B2/WLAN: Einrichtung in vier Schritten – ins WLAN (2,4 GHz), Nur LAN, Entwicklermodus (sonst nur Zusehen), Zugangscode; Bedienweg passend zum Modell | ✓ bestanden |
| CORE-05 B4: Fertig-Meldung – Wechsel von „Druckt“ zu „Fertig“ meldet einmal Erfolg, zu „Fehlgeschlagen“ einen Fehler; Leerlauf meldet nichts | ✓ bestanden |
| CORE-05 B4: Statuswerte – WLAN-Signal in Klartext, Tempo-Stufe, Kammerlicht, Lüfter, AMS-Fächer mit Typ, Farbe, Rest und aktivem Fach, Fehlermeldungen mit Bereich und Link | ✓ bestanden |
| CORE-05 B4: Steuerbefehle wie Bambu Studio – Licht, Tempo 1–4, Düse bis 300 °C, Bett bis 110 °C, Lüfter in Prozent; außerhalb der Grenzen abgelehnt; Zeitraffer beim Druckstart | ✓ bestanden |
| CORE-05 B5/B3: Mehrere Drucker in einer Liste (gleiche Seriennummer ersetzt, ohne Zugangscode in der Datei); Bambu-Studio-Befehlszeile zum Slicen mit Profilen | ✓ bestanden |
| CORE-05 Q1: Smithsonian (nur CC0 mit 3D-Datei, GLB bevorzugt), NASA über GitHub (Suche im Namen, LFS-Dateien über den Medien-Server), Sketchfab (Suche frei, Laden nur mit eigenem Schlüssel im Kopf der Anfrage) | ✓ bestanden |
| CORE-05 Q1: Thingiverse über die offizielle Schnittstelle – ohne Schlüssel klare Meldung, Suche mit Schlüssel, Laden holt Lizenz und die STL, Lizenzdatei mit Namensnennung | ✓ bestanden |
| CORE-05 Q1–Q4: Quellen-Leiste, Schlüssel geschützt speichern, NASA suchen und laden, in den 3D-Druck und ins 3D-Studio übernehmen, andere Seiten über den Download-Ordner, Filter „nur kommerziell“ | ✓ bestanden |
| CORE-05 Q2: Download-Übernahme – nur neue, fertig geladene Modelldateien; ZIP wird entpackt (ohne Pfad-Ausbruch), Lizenz und Seite kommen in die Bibliothek | ✓ bestanden |
| CORE-05 Q3: Lizenzen – CC0, CC BY, CC BY-SA und NASA sind kommerziell nutzbar, NC nicht; Namensnennung bei allem außer CC0/gemeinfrei | ✓ bestanden |
| CORE-05: Anfasser – Verschieben, Drehen, Skalieren entlang einer Achse (Raster), Tasten G/R/S wechseln die Art, ein Schritt im Rückgängig | ✓ bestanden |
| CORE-05: Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render – Stand aus der Szene, ausgeblendete Teile zählen nicht | ✓ bestanden |
| CORE-05: Arbeitsschritte – Licht und Kamera wählen, Render (nachgebildet) landet im Verlauf, zwei Renders zeigen Vorher/Nachher mit Regler; alle Schritte erledigt | ✓ bestanden |
| CORE-05: Claude baut ein 3D-Modell – sieht Szene, Formen und Materialien, Plan zeigt das Modell mit Gesamtmaß, erst „Ausführen“ baut Teile, Licht und Kamera; ungültiges Material fällt weg; Rückgängig stellt zurück | ✓ bestanden |
| CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen | ✓ bestanden |
| CORE-05: Eigenschaften: Quader 40×30×10 mm – Volumen 12 cm³, Oberfläche 38 cm², Masse je Werkstoff, Schwerpunkt in der Mitte | ✓ bestanden |
| CORE-05: Füllung: 100 % deckt die Fläche (Länge ≈ Fläche / Linienbreite), läuft nicht in die Lücke zwischen zwei Körpern, Winkel wechselt | ✓ bestanden |
| CORE-05: Normbohrungen: M3 Durchgang 3,4 mm, M6 Kernloch 5 mm, M8 Senkschraube 9/16,4 mm; eigenes Maß zuerst | ✓ bestanden |
| CORE-05: PBR-Materialien (Metall binär, Rauheit 0–1, Glas mit Durchlässigkeit) und Gruppen; Blender-Szene trägt Material, Licht, Kamera – ausgeblendete Teile fehlen; JSON übersteht Speichern | ✓ bestanden |
| CORE-05: Querschnitt: Quader mit Bohrung – Fläche ohne das Loch; über dem Körper 0 | ✓ bestanden |
| CORE-05: Render-Verlauf – neue Bilder bekommen eindeutige Namen, neuestes zuerst; Blender-Skript (Bauen + Cycles-Render) ist gültiges Python | ✓ bestanden |
| CORE-05: Schätzung aus Bahnen: massiver Würfel wiegt etwa Volumen × Dichte, 15 % Füllung deutlich weniger, PETG schwerer und teurer als PLA | ✓ bestanden |
| CORE-05: Skalieren, Anordnen in Reihen (mittig, mit Abstand), Kopien auf der Platte; zu viele Teile oder zu groß gibt eine klare Meldung | ✓ bestanden |
| CORE-05: Stützen: Tisch-Form (Platte auf dünnem Fuß) braucht Stützen unter der Platte, Würfel nicht | ✓ bestanden |
| CORE-05: Szenenbaum – Teile in Gruppen ordnen, Gruppe auswählen bewegt alle ihre Teile, Ausblenden nimmt Teile aus Ansicht und Render, Material für die ganze Gruppe | ✓ bestanden |
| CORE-05: Umrisse: Würfelschnitt ergibt einen geschlossenen Umriss (Umfang 80 mm, Fläche 400 mm²); zwei Körper zwei Umrisse | ✓ bestanden |
| CORE-05: Unbekannte Formate und Formen werden abgewiesen | ✓ bestanden |
| Geräusche: MMAudio mit Hinweis „nur nicht-kommerziell“, Beschreibung + Länge → WAV zum Anhören; ohne Beschreibung kein Lauf | ✓ bestanden |
| Startbildschirm bekommt keine Auftragsleiste | ✓ bestanden |
| Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog | ✓ bestanden |
| StudioCatalog.NameOf: Katalogname, sonst der bisherige Text | ✓ bestanden |
| Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen | ✓ bestanden |
| Teams: jedes Studio (außer dem abgelehnten SPEC-07) hat ein Profi-Team; „👥 Team“ im 3D-Studio öffnet „KI einsetzen“ mit dem 3D-Team; eigene Rolle nur mit Studio | nicht im Lauf |
| Vorschlag 36: Studios zeigen „✨ Freie KI hier“ – 3D-Studio schlägt „3D aus Bild“ vor, der Klick öffnet die KI-Bibliothek (geladen wird erst dort); jede Studio-Zuordnung gibt es wirklich | ✓ bestanden |
