# T-0213 Bericht: Studionamen und Untertitel ehrlich machen

**Stand:** main af4f3a7 · Quelle Namen/Untertitel: `src/Trendo.App/StudioCatalog.cs` (54 Einträge, **nicht geändert**) · Quelle „was wirklich geht“: `docs/audit/LOGIK_AUDIT.md` (Klick-Audit).
**Art:** reine Vorschlagsliste, kein Code. Entscheidung je Zeile durch Manuel: **A** = Name/Untertitel anpassen, **B** = Funktion ergänzen (Paket in Klammern).

Legende Bewertung: ✅ Name stimmt · ⚠️ verspricht etwas mehr · ❌ verspricht deutlich mehr.

## Zuerst entscheiden (Name verspricht deutlich mehr)
| Studio | Untertitel heute | Was wirklich geht | Vorschlag A (ehrlicher Name / Untertitel) | Vorschlag B (Funktion ergänzen) |
|---|---|---|---|---|
| Spiele bauen (CORE-06) | Eigene Spiele bauen – mit der Unreal Engine. | 3 feste Browser-Spiele ohne Einstellungen; Unreal nur Projekt anlegen | **Spiele-Vorlagen** – „Fertige kleine Browser-Spiele ausprobieren und ein Unreal-Projekt anlegen.“ | Spiel-Baukasten mit Regeln/Leveln, Unreal öffnen (T-0204) |
| Live streamen (SPEC-15) | Livestreams und Sendungen planen und durchführen. | Planungsrechner (Ablauf, Bitrate, Lautheit); kein Streaming, keine OBS-Steuerung | **Livestream planen** – „Ablauf, Bitrate und Lautstärke für deine Sendung planen.“ | OBS mit Szene öffnen (T-0203) |
| Kameras & Roboter steuern (CORE-09) | Kameras und Roboterarme steuern und auswerten. | Bildauswertung + Rechner; keine Live-Kamera, keine Steuerung | **Kamerabilder auswerten** – „Bilder auswerten und Roboter-Bewegungen berechnen.“ | Kamera-Vorschau, sichere Arm-Steuerung (T-0207) |
| Gegenstände einscannen (SPEC-09) | Gegenstände und Räume einscannen und vermessen. | Vorhandene Punktwolke per Pfad bearbeiten; keine Aufnahme | **Scans bearbeiten** – „Vorhandene 3D-Scans (Punktwolken) bearbeiten und vermessen.“ | Fotos → Modell (Fotogrammetrie), Dateidialog (T-0201) |
| Webseite bauen (CORE-07) | Webseiten und Apps programmieren. | 4 Vorlagen, Testen, Git; kein Editor | **Webseite aus Vorlage** – „Webseite aus einer Vorlage anlegen, testen und sichern.“ | Editor + Live-Vorschau + VS Code (T-0206) |
| 3D-Modelle erstellen (CORE-05) | 3D-Modelle bauen und ansehen – mit Blender. | 4 feste Formen, feste Farbe; Vorschau/Umwandeln nur mit Blender | **3D-Grundformen** – „Einfache 3D-Formen erzeugen und in Blender weiterbauen.“ | Teile-Editor mit Maßen (T-0200 – empfohlen B, da Kernstudio) |

## Alle 54 Studios
| # | Studio (ID) | Untertitel heute | Was wirklich geht (Audit) | Bew. | Vorschlag A | Vorschlag B |
|---|---|---|---|---|---|---|
| 1 | Briefe schreiben (STUDIO-LETTER-01) | Schreibt Briefe an Ämter, Firmen und Vermieter – mit Vorlagen und Fristen. | Prüfung, Akten, Export; „Freier Brief“ stumm, keine Live-Vorschau | ✅ | – | Live-Vorschau (T-0106) |
| 2 | Texte & Tabellen (SYS-10) | Texte, Tabellen und Präsentationen erstellen und bearbeiten. | PDF-Formular lesen/speichern; Druckvorschau stumm; eigener Editor nicht belegt | ⚠️ | „Dokumente & PDF“ – „Dokumente öffnen, PDF-Formulare ausfüllen und speichern.“ | Formular-UI (T-0211) |
| 3 | E-Mails & Termine (SYS-09) | E-Mails, Kontakte und Termine an einem Ort. | Entwurf, Prüfung, .eml | ⚠️ | Untertitel: „E-Mails entwerfen, prüfen und als Datei speichern.“ | – |
| 4 | Neues Projekt starten (X-11) | Ein Startpunkt für jedes Projekt – vom Dokument bis zum Video. | funktioniert; ein Rezept stumm | ✅ | – | Rückmeldung (T-0202) |
| 5 | Kleine Helfer & Musik (SYS-02) | Rechner, Notizen, Bildschirmfoto, Musik und Videos abspielen. | teils tot: 2 Abstürze, 9 stumme Knöpfe | ✅ | – | Eingaben prüfen (T-0209) |
| 6 | Online verkaufen (STUDIO-ONLINE-BUSINESS-01) | Geschäftsidee planen, gründen und online verkaufen. | Planung/Einträge; 14 Knöpfe stumm, Import stürzt ab; kein echter Shop | ⚠️ | „Geschäftsidee planen“ – „Geschäftsidee planen und den Online-Verkauf vorbereiten.“ | Rückmeldung, Pflichtfelder (T-0208) |
| 7 | Geld & Rechnungen (SPEC-11) | Haushaltsbuch, Rechnungen, Kredite und Steuern im Blick. | Geschäftsbereiche teils tot | ✅ | – | T-0208 |
| 8 | Videos & Fotos bearbeiten (CORE-08) | Videos schneiden, Musik aufnehmen und Bilder gestalten. | 7 FFmpeg-Befehle per Pfad; Schnitt nur per Sekundenzahl; Musikaufnahme nicht belegt | ⚠️ | Untertitel: „Videos kürzen, umwandeln und Bilder anpassen.“ | Zeitleiste mit Vorschau (T-0205) |
| 9 | Live streamen (SPEC-15) | siehe oben | siehe oben | ❌ | Livestream planen | T-0203 |
| 10 | Kleidung & Produkte entwerfen (SPEC-08) | Kleidergrößen, Schnitte und Produktideen. | Rechner liefern Ergebnisse (Text) | ⚠️ | „Kleidung & Produkte planen“ | Grafik/Projekte (T-0207) |
| 11 | Freie Bilder & Klänge finden (X-07) | Kostenlose Bilder, Klänge und Schriften finden. | funktioniert | ✅ | – | – |
| 12 | KI-Anweisungen schreiben (CORE-12) | Gute Anweisungen für die KI schreiben und sammeln. | funktioniert | ✅ | – | – |
| 13 | 3D-Modelle erstellen (CORE-05) | siehe oben | siehe oben | ❌ | 3D-Grundformen | T-0200 |
| 14 | Spiele bauen (CORE-06) | siehe oben | siehe oben | ❌ | Spiele-Vorlagen | T-0204 |
| 15 | VR & AR gestalten (SPEC-04) | Virtuelle Räume und 3D-Modelle für die Brille vorbereiten. | nur Prüfungen eines Modells | ⚠️ | „3D-Modelle für VR prüfen“ – „Prüft, ob ein 3D-Modell für die Brille passt.“ | Modell-Vorschau (T-0207) |
| 16 | Gegenstände einscannen (SPEC-09) | siehe oben | siehe oben | ❌ | Scans bearbeiten | T-0201 |
| 17 | Wohnung planen (SPEC-01) | Räume ausmessen, Flächen berechnen und einrichten. | Räume, Material, Kosten, SVG-Grundriss | ✅ | – | Grundriss-Vorschau (T-0207) |
| 18 | Schaltungen berechnen (SPEC-02) | Widerstände, LEDs und Schaltungen berechnen. | funktioniert (Rechner) | ✅ | – | – |
| 19 | Statik nachrechnen (SPEC-03) | Balken, Kräfte und Materialien nachrechnen. | funktioniert (Rechner) | ✅ | – | – |
| 20 | Geräte überwachen (SPEC-10) | Messwerte von Geräten und Sensoren beobachten. | Rechner/Auswertung, keine Live-Verbindung belegt | ⚠️ | „Gerätewerte auswerten“ | Live-Anzeige (T-0207) |
| 21 | Routen & Karten (SPEC-13) | Routen planen, Strecken messen und GPS-Dateien ansehen. | funktioniert (Rechner) | ✅ | – | Kartenansicht (T-0207) |
| 22 | Fräsen & Fertigen (SPEC-14) | Fräsen, Drehzahlen und Werkstücke planen. | funktioniert (Rechner) | ✅ | Name ggf. „Fräsen planen“ | – |
| 23 | Auto & Fahrten (SPEC-16) | Verbrauch, Fahrtenbuch und Kosten rund ums Auto. | funktioniert | ✅ | – | – |
| 24 | Kameras & Roboter steuern (CORE-09) | siehe oben | siehe oben | ❌ | Kamerabilder auswerten | T-0207 |
| 25 | Messwerte auswerten (SPEC-05) | Messwerte auswerten und als Diagramm zeigen. | Ergebnis nur Text, kein Diagramm | ⚠️ | Untertitel ohne „als Diagramm zeigen“ | Diagramm (T-0207) |
| 26 | Lernen mit Karteikarten (SPEC-06) | Karteikarten, Vokabeln und Prüfungen üben. | funktioniert | ✅ | – | – |
| 27 | Texte übersetzen (SPEC-07) | Texte übersetzen und Programme eindeutschen. | gesperrt (abgelehnt, nicht bauen) | ❌ | aus Katalog/Startmenü ausblenden oder „nicht verfügbar“ | – (abgelehnt) |
| 28 | Wissen merken (CORE-10) | Merkt sich, was du gelernt und gemacht hast. | funktioniert | ✅ | – | – |
| 29 | Sicher recherchieren (SYS-08) | Sicher im Netz recherchieren – nur lesen. | im Audit nicht einzeln bewertet | ✅? | – (prüfen) | – |
| 30 | KI fragen (CORE-02) | Dein Helfer, der Fragen beantwortet und Aufgaben erledigt. | funktioniert, ohne Ollama ehrlich „nicht verbunden“ | ⚠️ | Untertitel: „… beantwortet Fragen – braucht eine lokale KI (Ollama).“ | – |
| 31 | KI-Team einsetzen (CORE-03) | Mehrere KI-Helfer arbeiten gemeinsam an einer Aufgabe. | wie KI fragen | ✅ | Hinweis „braucht lokale KI“ | – |
| 32 | Kostenlose KI finden (X-05) | Kostenlose KI-Modelle finden und als Ersatz nutzen. | funktioniert | ✅ | – | – |
| 33 | Aufgaben automatisieren (SYS-05) | Wiederkehrende Aufgaben automatisch erledigen lassen. | funktioniert | ✅ | – | – |
| 34 | Großprojekte umsetzen (X-01) | Mehrere Studios arbeiten zusammen an einem Projekt. | funktioniert | ✅ | – | – |
| 35 | Webseite bauen (CORE-07) | siehe oben | siehe oben | ❌ | Webseite aus Vorlage | T-0206 |
| 36 | Apps zusammenklicken (X-03) | Apps zusammenklicken – ganz ohne Programmieren. | Seite erzeugen + Browser-Vorschau; 4 Knöpfe nur Text | ⚠️ | „Einfache Seiten zusammenklicken“ | Visueller Baukasten (T-0206) |
| 37 | Projekte von GitHub holen (X-06) | Freie Projekte von GitHub finden und nutzen. | funktioniert | ✅ | – | – |
| 38 | Trendo erweitern (X-04) | Neue Funktionen für Trendo hinzufügen. | funktioniert | ✅ | – | – |
| 39 | Programme nachbauen (X-10) | Programme rechtlich sauber neu entwickeln. | funktioniert | ✅ | – | – |
| 40 | Desktop einrichten (CORE-01) | Fenster, Startmenü und Arbeitsfläche von Trendo. | Design-Auswahl wirkt (mit Neuaufbau) | ✅ | – | Wirkung ohne Neuaufbau (T-0101, nicht hier) |
| 41 | Startmodus wählen (SYS-01) | Erkennt das Gerät und startet Trendo passend. | funktioniert | ✅ | – | – |
| 42 | Daten schützen (CORE-11) | Schützt deine Daten, Passwörter und Sicherungen. | funktioniert | ✅ | – | Wiederherstellen (T-0214) |
| 43 | System auf Gefahren prüfen (SPEC-12) | Prüft das System auf Gefahren und sichert Hinweise. | funktioniert | ✅ | – | – |
| 44 | Surfen & Herunterladen (SYS-03) | Im Internet surfen und Dateien laden. | Laden/Anhalten/Sortieren stumm (offline geprüft) | ✅ | – | Fortschritt (T-0211) |
| 45 | Programme installieren (SYS-04) | Programme installieren, aktualisieren und entfernen. | teils; Elemente nach Neuaufbau nicht auffindbar | ✅ | – | T-0203 |
| 46 | Dateien teilen (SYS-06) | Dateien im Netzwerk teilen und von unterwegs zugreifen. | funktioniert laut Klick-Audit | ✅ | – | – |
| 47 | Geräte erkennen (SYS-07) | Drucker, Kameras und andere Geräte erkennen. | funktioniert | ✅ | – | – |
| 48 | Programme starten (SYS-11) | Programme ohne Installation starten und verknüpfen. | teils, Pflege-Seite mehrere stumm | ✅ | – | T-0203 |
| 49 | Schrift & Farben anpassen (SYS-12) | Größere Schrift, Farben und einfache Bedienung. | 4 Schalter ohne sichtbare Wirkung | ✅ | – | T-0212 (läuft bei anderem Agenten, hier nicht anfassen) |
| 50 | Qualität prüfen (SYS-13) | Prüft, ob alles zusammen funktioniert. | funktioniert | ✅ | – | – |
| 51 | Einrichten & Reparieren (X-02) | Richtet Trendo automatisch ein und repariert Fehler. | 7 Reparieren-Knöpfe ohne Rückmeldung | ⚠️ | Untertitel: „Richtet Trendo ein und hilft bei Fehlern.“ | Ergebnisanzeige (T-0210) |
| 52 | Handy verbinden (X-08) | Verbindet Trendo mit deinem Handy per Kabel, WLAN oder Bluetooth. | nur nachgebildet (Box) | ⚠️ | – (am echten Gerät prüfen) | Windows-Nachweis (Integrator) |
| 53 | PC, Pi & Handy verbinden (CORE-04) | Trendo auf PC, Raspberry Pi und Android gemeinsam nutzen. | nur nachgebildet (Box) | ⚠️ | – (am echten Gerät prüfen) | Windows-/Pi-Nachweis |
| 54 | Leistung anzeigen (X-09) | Zeigt, was Speicher und Prozessor gerade tun. | funktioniert | ✅ | – | – |

**Zusammenfassung:** ✅ 33 (+1 ungeprüft: Sicher recherchieren) · ⚠️ 13 · ❌ 7 (davon 6 zur Entscheidung oben, 1 gesperrt: Texte übersetzen).
**Hinweis:** „Was wirklich geht“ stammt aus dem Klick-Audit (Box, kein Windows-Lauf); Zeilen mit „nachgebildet“ brauchen einen Nachweis am PC.

## Nächster Schritt
Projektleitung legt diese Liste Manuel vor. Erst nach Entscheidung A/B ändert ein Folgeauftrag `StudioCatalog.cs` bzw. das jeweilige Paket.
