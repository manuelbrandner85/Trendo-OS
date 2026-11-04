# 3D – Testplan

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `3d` · Stand: 0.9.36 (Stand 808cc8f)

23 Tests sprechen dieses Programm an.

- 3D aus Bild: TripoSR nach Klick einrichten (Stand geprüft, eigene Umgebung), Foto wählen → GLB → „Im 3D-Studio öffnen“; falscher Stand wird nicht eingerichtet
- 3D-Arbeitsfläche: Formen einsetzen (auf dem Boden), Teil anklicken, mit der Maus verschieben (Raster), Pfeiltasten, Verdoppeln, Entf, Rückgängig
- 3D-Arbeitsfläche: „Auf den Boden“ setzt die Unterkante auf 0, Text und Ebene stehen auf dem Boden
- 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern
- 3D-Studio: Kreatur-Karte – Drache in Bronze/Grün, „Vorschaubild rendern“ startet Blender im Hintergrund, „sichtbar“ ohne -b; Ergebnis mit Vorschau
- 3D: OBJ, STL und GLB speichern, Datei öffnen, Projekt wieder öffnen
- 3D: Satz wird zum Vorschlag und erst nach Übernehmen gebaut
- 3D: Teil hinzufügen, ändern, Vorschau, löschen, rückgängig
- 3D: Vorschau und Export sind bei üblichen Fenstergrößen ohne Scrollen sichtbar
- 3D: ohne Blender einmal fragen, mit Blender das aktuelle Objekt öffnen
- Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche
- Baukasten übernimmt die neue Seite ins Formular
- Briefseite zeigt den echten Brieftext in der Vorschau
- Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist
- CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen
- CORE-05: Unbekannte Formate und Formen werden abgewiesen
- Geräusche: MMAudio mit Hinweis „nur nicht-kommerziell“, Beschreibung + Länge → WAV zum Anhören; ohne Beschreibung kein Lauf
- Startbildschirm bekommt keine Auftragsleiste
- Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog
- StudioCatalog.NameOf: Katalogname, sonst der bisherige Text
- Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen
- Teams: jedes Studio (außer dem abgelehnten SPEC-07) hat ein Profi-Team; „👥 Team“ im 3D-Studio öffnet „KI einsetzen“ mit dem 3D-Team; eigene Rolle nur mit Studio
- Vorschlag 36: Studios zeigen „✨ Freie KI hier“ – 3D-Studio schlägt „3D aus Bild“ vor, der Klick öffnet die KI-Bibliothek (geladen wird erst dort); jede Studio-Zuordnung gibt es wirklich
