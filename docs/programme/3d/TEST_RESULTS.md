# 3D – Testergebnisse

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `3d` · Stand: 0.9.36 (Stand 208ed9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 29 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
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
| CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen | ✓ bestanden |
| CORE-05: Eigenschaften: Quader 40×30×10 mm – Volumen 12 cm³, Oberfläche 38 cm², Masse je Werkstoff, Schwerpunkt in der Mitte | ✓ bestanden |
| CORE-05: Füllung: 100 % deckt die Fläche (Länge ≈ Fläche / Linienbreite), läuft nicht in die Lücke zwischen zwei Körpern, Winkel wechselt | ✓ bestanden |
| CORE-05: Normbohrungen: M3 Durchgang 3,4 mm, M6 Kernloch 5 mm, M8 Senkschraube 9/16,4 mm; eigenes Maß zuerst | ✓ bestanden |
| CORE-05: Querschnitt: Quader mit Bohrung – Fläche ohne das Loch; über dem Körper 0 | ✓ bestanden |
| CORE-05: Schätzung aus Bahnen: massiver Würfel wiegt etwa Volumen × Dichte, 15 % Füllung deutlich weniger, PETG schwerer und teurer als PLA | ✓ bestanden |
| CORE-05: Skalieren, Anordnen in Reihen (mittig, mit Abstand), Kopien auf der Platte; zu viele Teile oder zu groß gibt eine klare Meldung | ✓ bestanden |
| CORE-05: Stützen: Tisch-Form (Platte auf dünnem Fuß) braucht Stützen unter der Platte, Würfel nicht | ✓ bestanden |
| CORE-05: Umrisse: Würfelschnitt ergibt einen geschlossenen Umriss (Umfang 80 mm, Fläche 400 mm²); zwei Körper zwei Umrisse | ✓ bestanden |
| CORE-05: Unbekannte Formate und Formen werden abgewiesen | ✓ bestanden |
| Geräusche: MMAudio mit Hinweis „nur nicht-kommerziell“, Beschreibung + Länge → WAV zum Anhören; ohne Beschreibung kein Lauf | ✓ bestanden |
| Startbildschirm bekommt keine Auftragsleiste | ✓ bestanden |
| Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog | ✓ bestanden |
| StudioCatalog.NameOf: Katalogname, sonst der bisherige Text | ✓ bestanden |
| Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen | ✓ bestanden |
| Teams: jedes Studio (außer dem abgelehnten SPEC-07) hat ein Profi-Team; „👥 Team“ im 3D-Studio öffnet „KI einsetzen“ mit dem 3D-Team; eigene Rolle nur mit Studio | nicht im Lauf |
| Vorschlag 36: Studios zeigen „✨ Freie KI hier“ – 3D-Studio schlägt „3D aus Bild“ vor, der Klick öffnet die KI-Bibliothek (geladen wird erst dort); jede Studio-Zuordnung gibt es wirklich | ✓ bestanden |
