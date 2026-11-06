# Vorbild-Analyse Freie Medien: Openverse, Unsplash, Pexels, Pixabay, Freesound, Google Fonts (Clean-Room)

- Programme: „Freie Bilder & Klänge“
- Vorbilder: Openverse, Wikimedia Commons, Freesound, Google Fonts, Fontsource, Poly Haven (frei); Unsplash, Pexels, Pixabay (kostenlos mit eigener Lizenz); Adobe Stock, Shutterstock, Envato Elements, Epidemic Sound (kostenpflichtig) – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und den offenen Schnittstellen (Openverse-API, Commons-API, Fontsource-API); kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 04.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Startseite mit Themen-Kacheln statt leerer Seite (Unsplash, Pexels)
- ✓ Raster mit Vorschaubildern, große Vorschau rechts mit Urheber, Lizenz, Format, Größe
- ✓ Zwei Quellen: Wikimedia Commons und Openverse (über 800 Mio. Werke aus Flickr, Freesound, Jamendo …), abwechselnd gemischt, doppelte Dateien nur einmal
- ✓ Nur Lizenzen, die auch für Geld und mit Änderungen erlaubt sind (CC0, gemeinfrei, CC BY, CC BY-SA); NC und ND fallen immer heraus
- ✓ Filter Lizenz (frei / mit Namensnennung), Format, Ausrichtung quer/hoch/quadratisch (Unsplash, Pexels) – Ausrichtung filtert sofort ohne neue Suche
- ✓ Klänge vorhören (Freesound)
- ✓ Laden erst auf Klick, mit Herkunftsdatei; Namensnennung als Text, HTML mit Links oder Markdown (Openverse „Verwenden“)
- ✓ Alle Namensnennungen auf einmal kopieren (für Abspann oder Impressum)
- ✓ Geladenes Bild direkt in der Bildbearbeitung, Klang direkt im Audio-Editor öffnen (Adobe Stock in Photoshop)
- ✓ Schriften wie Google Fonts: Suche nach Name und Art, nur offene Lizenzen (OFL, Apache), Laden als TTF normal und fett mit Lizenzdatei
- ✓ 3D-Modelle, Texturen und Himmel von Poly Haven und ambientCG
- ✓ Schriftvorschau mit eigenem Probetext und Größe (06.11.2026) – eine Stärke wird zur Vorschau zwischengespeichert, ohne Installation
- ✓ Ähnliche Bilder und Klänge (06.11.2026): Openverse „related“, für Commons über die Stichworte des Titels
- ✓ Merklisten wie Unsplash Collections (06.11.2026): je Projekt, Lizenz geprüft, Namensnennungen als Text/HTML/Markdown, alles auf einmal laden
- ✓ Claude als Rechercheur (06.11.2026): sucht passend zum Projekt, prüft jede Lizenz, merkt Treffer und schreibt die Namensnennung automatisch
- ✗ Unsplash, Pexels, Pixabay direkt – brauchen einen eigenen API-Schlüssel; Trendo setzt keinen voraus

## Bedienkonzept (Profi-Bauplan A1–A3)
Reiter oben (Bilder, Klänge, Schriften, 3D & Texturen, Meine Bibliothek), Suchleiste mit Filtern, Raster links, Vorschau rechts – entspricht dem Bauplan.

## Nächste Schritte
1. Ähnliche Bilder
2. Merklisten
3. Schriftvorschau mit eigenem Text
