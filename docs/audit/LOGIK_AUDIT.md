# Logik-Audit Trendo OS – jedes Element gegen den echten Code
**Stand:** 2026-10-22 · main `55d9550` · Box (Linux, headless, offline, ohne Blender/FFmpeg/LibreOffice/Ollama) · Grundlage ist der Code und ein Klick-Lauf, **nicht** `requirements.yaml`.

## 1. Methode und Zahlen
- **Klick-Lauf (headless Avalonia):** Jede Seite aus `_showMap` (92 Seiten) geöffnet, jeder sichtbare aktive Knopf mit Standardeingaben geklickt; Wirkung gemessen: Seitenwechsel, gestartetes Programm (Fake-Launcher), geschriebene Datei, geänderter sichtbarer Text. Riskante Knöpfe (Löschen, Installieren, Schließen …) übersprungen. Rohdaten `reports/audit/klick-rohdaten.tsv`, Liste ohne Wirkung `reports/audit/ohne-wirkung.tsv`, Prüfprogramm `reports/audit/klick-audit-harness.cs.txt`.
- **Einschränkung:** Der Lauf endete durch Zeitlimit nach Seite „teilen“; technik, verbund, werkzeuge, wissenschaft, xr, zwilling wurden nur teilweise (über offene Fenster) erfasst. Schalter (CheckBox/RadioButton) reagieren auf den simulierten Klick nicht und sind **nicht bewertet**. Ohne Blender/FFmpeg/LibreOffice/Ollama zeigen viele Studios nur „fehlt – Einrichten“.
- **Code-Sichtung:** jedes Studio-View (`src/Trendo.App/MainWindow.*.cs`) und Kernlogik gelesen.

| Ergebnis (1546 eindeutige Bedienelemente) | Anzahl |
|---|---|
| **funktioniert** (sichtbare echte Wirkung) | 1198 |
| **schwach** (nur Text ändert sich ohne Ergebnis, oder „Zusatzprogramm fehlt“) | 68 (44 + 24) |
| **tot** (keine Wirkung: 36 · Absturz: 6) | 42 |
| nicht prüfbar (Schalter 23, Netz/Hintergrund 9, nach Neuaufbau nicht gefunden 24) | 56 |
| deaktiviert (z. B. braucht vorherige Eingabe) | 47 |
| absichtlich nicht geklickt (riskant) | 135 |

**Wichtig:** „funktioniert“ heißt nur „der Knopf tut etwas Echtes“. Viele Studios sind trotzdem **schwach**, weil sie nur Vorlagen oder Rechner bieten, Dateien nur per eingetipptem Pfad annehmen oder ihr Name mehr verspricht (Abschnitt 2).

## Querschnitt-Befunde
- **Keine Dateiauswahl:** 36 Dateifelder verlangen einen eingetippten Pfad; die App nutzt nirgends einen Datei-Dialog oder Drag & Drop. → T-0201
- **Externe Programme öffnen ohne Datei:** „Blender öffnen“ startet Blender leer; Unreal/VS Code/OBS/LibreOffice nicht mit der aktuellen Datei. → T-0203
- **Stille Knöpfe:** 36 Knöpfe zeigen bei leerer/Standard-Eingabe gar nichts; Fehler erscheinen teils nicht dort, wo man klickt. → T-0202
- **Abstürze:** Geld umrechnen, Pipette, Betrieb-Import (leere Eingabe), 3× Sicherung „Wiederherstellen“. → T-0209, T-0208, T-0214
- **Freitext statt Auswahl:** feste Wertelisten als Hinweistext („Standard: altbau, teilsaniert …“). → T-0207
- **Ergebnis nur als Text:** Fach-Studios rechnen echt, zeigen aber keine Grafik und speichern keinen Projektstand. → T-0207

## 2. Studios – Bewertung (priorisiert)
Legende Status: **funktioniert** = Knöpfe bewirken Echtes · **schwach** = echt, aber Vorlagen/Rechner, Ablauf umständlich oder Name verspricht mehr · **tot** = Knöpfe ohne Wirkung/Absturz. Nutzen: hoch/mittel/gering = erwarteter Nutzen nach Behebung.

| Prio | Studio (Katalogname) | Seite | Tut es Echtes? | Ablauf logisch? | Was der Nutzer erwartet | Nutzen | Konkrete Behebung | Paket |
|---|---|---|---|---|---|---|---|---|
| 1 | 3D-Modelle erstellen | 3d | **schwach** – 4 feste Formen (Würfel, Kugel, Affenkopf, Schrift), feste Farbe, Vorschau/Umwandeln nur mit Blender | nein – keine Maße, kein Zusammenbauen, „Blender öffnen“ ohne Modell | Objekt beschreiben/bauen, Grundformen + Werkzeuge, Import/Export, in Blender öffnen, Vorschau | hoch | Teile-Editor mit Maßen, Modifikatoren, KI-Beschreibung, Dateidialog, „In Blender öffnen“ mit Datei | T-0200 |
| 2 | (alle Studios) Dateifelder | – | **schwach** – 36 Felder verlangen eingetippten Pfad, nirgends Dateidialog/Drag & Drop | nein | Durchsuchen-Knopf, Ziehen & Ablegen | hoch | StorageProvider + Drop | T-0201 |
| 3 | Spiele bauen | spiele | **schwach** – 3 feste Browser-Spiele ohne Einstellungen; Unreal nur Projekt anlegen | nein – Name verspricht Unreal | eigenes Spiel gestalten, Level, Figuren, Export | hoch | Spiel-Baukasten mit Regeln/Leveln, Unreal öffnen | T-0204 |
| 4 | Videos & Fotos bearbeiten | kreativ-medien | **schwach** – 7 FFmpeg-Befehle, Datei per Pfad, „Übernehmen“ stumm | nein – Schnitt nur über Sekundenzahl | Video sehen, Zeitleiste, Bild bearbeiten | hoch | Dialog, Zeitleiste mit Vorschau, Bildwerkzeuge | T-0205 |
| 5 | Webseite bauen | dev | **schwach** – 4 Vorlagen, Testen, Git; kein Editor | nein | Inhalt/Code ändern, Vorschau | hoch | Editor + Live-Vorschau + VS Code | T-0206 |
| 6 | Apps zusammenklicken | baukasten | funktioniert (Seite erzeugen, Vorschau im Browser) – 4 Knöpfe nur Textänderung | teils | Formulare/Seiten visuell bauen | mittel | Vorschau im Studio | T-0206 |
| 7 | Online verkaufen + Geschäftsbereiche | business, biz-* | **teils tot** – 14 Knöpfe ohne Wirkung bei Standardeingaben, Import stürzt ab | teils | Einträge anlegen und sehen | hoch | Rückmeldung, Pflichtfelder, Dialog | T-0208 |
| 8 | Kleine Helfer & Musik | alltag-* | **teils tot** – 2 Abstürze (Geld umrechnen, Pipette), 9 stumme Knöpfe | teils | Helfer sofort nutzbar | hoch | Eingaben prüfen, Dialoge, Vorschau | T-0209 |
| 9 | Einrichten & Reparieren / Einrichtung | einrichtung*, heilung | **teils tot** – Design-/Paketwahl ohne sichtbaren Zustand, 7 Reparieren-Knöpfe stumm, „Ordner wählen“ ohne Dialog | nein | sehen was gewählt ist, was repariert wurde | hoch | Auswahlzustand, Ergebnisanzeige | T-0210 |
| 10 | Texte & Tabellen (PDF) | doku-pdf | **teils tot** – Formular lesen/speichern, Druckvorschau stumm | teils | PDF-Formular ausfüllen | mittel | Formular-UI | T-0211 |
| 11 | Surfen & Herunterladen | netz-laden, netz-browser | **teils tot** – Laden/Anhalten/Sortieren/Offline/PDF/Tor-holen stumm (Box offline) | teils | Downloads mit Fortschritt | mittel | Fortschritt, Hinweise | T-0211 |
| 12 | Schrift & Farben anpassen | bedien-* | **teils tot** – 4 Schalter ohne sichtbare Wirkung | nein – Zustand unsichtbar | sofort sichtbare Änderung | hoch | Schalterzustand + Wirkung | T-0212 |
| 13 | Live streamen | sendung | schwach – Planungsrechner (Ablauf, Bitrate, Lautheit), kein Streaming, keine OBS-Steuerung | Name verspricht mehr | Stream starten/steuern | mittel | OBS öffnen mit Szene; Name prüfen | T-0203, T-0213 |
| 14 | Kameras & Roboter steuern | roboter | schwach – Bildauswertung und Rechner, keine Live-Kamera, keine Steuerung | Name verspricht mehr | Kamera sehen, Arm bewegen (sicher) | mittel | Name ehrlich; Kamera-Vorschau | T-0207, T-0213 |
| 15 | Gegenstände einscannen | scan3d | schwach – Punktwolke per Pfad bearbeiten, keine Aufnahme | Name verspricht mehr | Fotos → Modell | mittel | Dateidialog, Name/Fotogrammetrie | T-0201, T-0213 |
| 16 | VR & AR gestalten | xr | schwach – nur Prüfungen eines Modells | teils | Szene ansehen/vorbereiten | gering | Vorschau des Modells | T-0207 |
| 17 | Wohnung planen | architektur | funktioniert – Räume, Material, Kosten, SVG-Grundriss; Freitext-Aufzählungen | teils | Grundriss sehen/zeichnen | mittel | Auswahllisten, Grundriss-Vorschau | T-0207 |
| 18 | Schaltungen berechnen, Statik nachrechnen, Messwerte auswerten, Geräte überwachen, Routen & Karten, Fräsen & Fertigen, Kleidung & Produkte, Auto & Fahrten, Lernen mit Karteikarten | elektronik, technik, wissenschaft, zwilling, karten, cnc, mode, fahrzeug, lernen | funktioniert – Rechner liefern Ergebnisse (Klick-Audit) | teils – Ergebnis nur Text, Stand nicht als Projekt | Grafik, Speichern/Laden | mittel | Auswahllisten, Diagramme, Projekte | T-0207 |
| 19 | Briefe schreiben | brief, akten, akte | funktioniert (Prüfung, Akten, Export) – „Freier Brief“ stumm, keine Live-Vorschau | teils – ein langes Formular | Vorschau beim Schreiben | hoch | Live-Vorschau (siehe Design 41 T-0106) | T-0106 (Design 41) |
| 20 | KI fragen, KI-Team, KI-Anweisungen, Kostenlose KI, Wissen merken | ki-* | funktioniert (ohne Ollama ehrlich „nicht verbunden“) | ja | – | mittel | – | – |
| 21 | Programme installieren / starten | appcenter, prog-* | teils – 15 Elemente nicht wiederauffindbar nach Neuaufbau, Pflege-Seite mehrere stumm | teils | Programm suchen → installieren → starten | hoch | zentrale Programmliste mit Starten | T-0203 |
| 22 | E-Mails & Termine | post | funktioniert (Entwurf, Prüfung, .eml) | ja | – | mittel | – | – |
| 23 | Daten schützen, System auf Gefahren prüfen, Dateien teilen, Geräte erkennen, Handy verbinden, PC/Pi/Handy, Startmodus, Leistung, Qualität | schutz, forensik, teilen, geraete, handy, verbund, modus, ressourcen, systemcheck | funktioniert laut Klick-Audit (Pi/Handy nur nachgebildet) | ja | – | mittel | – | – |
| 24 | Neues Projekt starten, Großprojekte, Projekte von GitHub, Trendo erweitern, Programme nachbauen, Freie Bilder & Klänge, Aufgaben automatisieren | suite, produktion, oss, erweiterungen, nachbau, assets, ablaeufe | funktioniert; „Geschäftsidee → Webseite“-Rezept stumm | teils | – | mittel | Rückmeldung | T-0202 |
| – | Texte übersetzen (SPEC-07) | – | gesperrt (abgelehnt) | – | – | – | nicht bauen | – |

## 3. Einstellungen
| Element | Befund | Behebung | Paket |
|---|---|---|---|
| Design/Akzent/Hintergrund (settings) | Auswahl wirkt (Neuaufbau der Seite) | Wirkung ohne Neuaufbau | Design 41 T-0101 |
| Schalter „Farbe passend zum Hintergrund“, „Hintergrund nach Tageszeit“, „Bewegung reduzieren“, Widget-Schalter | im Klick-Lauf nicht prüfbar; Zustand nach Klick nicht erkennbar | sichtbarer Zustand + Test | T-0212 |
| Einrichtungsassistent Design/Pakete | Auswahl ohne sichtbare Markierung im Lauf | Markierung + Zusammenfassung | T-0210 |
| Reparieren (6×), „Jetzt prüfen und reparieren“ | keine Rückmeldung im Lauf | Fortschritt + Ergebnis | T-0210 |
| Schlüssel (keys) | 4 Knöpfe deaktiviert ohne Grund-Hinweis | Tooltip mit Grund | T-0202 |

## 4. Ohne sichtbare Wirkung / Absturz (Auszug, vollständig in `reports/audit/ohne-wirkung.tsv`)
| Art | Seite | Knopf | Beschriftung |
|---|---|---|---|
| ABSTURZ | alltag-helfer | color-pick | Pipette |
| ABSTURZ | alltag-helfer | cur-run | Geld umrechnen |
| ABSTURZ | biz-betrieb | op-s-import | Übernehmen |
| ABSTURZ | sicherung | sicherung#5 | Wiederherstellen |
| ABSTURZ | spiele | spiele#14 | Wiederherstellen |
| ABSTURZ | spiele | spiele#15 | Wiederherstellen |
| STUMM | akte | src-scan | Jetzt suchen |
| STUMM | alltag-bilder | img-left | Nach links drehen |
| STUMM | alltag-bilder | img-resize | Verkleinern |
| STUMM | alltag-dateien | du-run | Anzeigen |
| STUMM | alltag-dateien | photo-apply | Ordnen |
| STUMM | alltag-dateien | ren-apply | Umbenennen |
| STUMM | alltag-dateien | trash-empty | Papierkorb leeren … |
| STUMM | alltag-dateien | trash-show | Papierkorb anzeigen |
| STUMM | alltag-helfer | clip-add | Merken |
| STUMM | alltag-helfer | color-copy | Kopieren |
| STUMM | biz-betrieb | op-c-add | Speichern |
| STUMM | biz-betrieb | op-k-add | Aufnehmen |
| STUMM | biz-betrieb | op-m-add | Mitglied anlegen |
| STUMM | biz-betrieb | op-p-add | Hinzufügen |
| STUMM | biz-betrieb | op-t-add | Tarif anlegen |
| STUMM | biz-marke | br-f-calc | Berechnen |
| STUMM | biz-marke | br-p-add | Schritt anhängen |
| STUMM | biz-marke | br-palette | Farben berechnen |
| STUMM | biz-marke | br-seo-check | Prüfen |
| STUMM | biz-markt | mk-c-add | Hinzufügen |
| STUMM | biz-markt | mk-e-add | Test anlegen |
| STUMM | biz-markt | mk-p-add | Merken |
| STUMM | brief | letter-cat-frei | Freier Brief |
| STUMM | doku-pdf | form-read | Felder lesen |
| STUMM | doku-pdf | form-save | Ausgefüllt speichern |
| STUMM | doku-pdf | print-preview | Vorschau |
| STUMM | einrichtung | fix-7zip | Reparieren |
| STUMM | einrichtung | fix-libreoffice | Reparieren |
| STUMM | einrichtung | fix-model | Reparieren |
| STUMM | einrichtung | fix-mpv | Reparieren |
| STUMM | einrichtung | fix-ollama | Reparieren |
| STUMM | einrichtung | fix-sumatra | Reparieren |
| STUMM | einrichtung-1 | einrichtung-1#3 | Ordner wählen … |
| STUMM | kreativ-medien | media-use | Übernehmen |
| STUMM | netz-laden | dlsort-apply | Sortieren |
| STUMM | produktion | fusion-recipe-idea-site | Geschäftsidee → Bauplan → Webseite Aus deiner Idee entstehen Bauplan u |

## 5. Arbeitspakete (Box-Agenten, priorisiert)
T-0200 3D-Studio · T-0201 Dateiauswahl · T-0202 Jeder Knopf wirkt · T-0203 Programme direkt öffnen · T-0204 Spiele · T-0205 Medien · T-0206 Webseite/Software · T-0207 Fach-Studios · T-0208 Business · T-0209 Kleine Helfer · T-0210 Einrichtung · T-0211 PDF/Downloads · T-0212 Barrierefreiheit · T-0213 Namen ehrlich (Entscheidung Manuel) · T-0214 Sicherung wiederherstellen.
