# Bericht T-0622: Webseite bauen, Stufe 3 (Inhalte und Web-Apps)

**Auftrag:** `tasks/T-0622-web-stufe3.md`. Vorgabe: `docs/nachbau/web-ki/anforderungen.md` (mehrere Seiten, W2, W3, K6, K3, A4, K7, K8, Rest von W4) und `anforderungen-ergaenzung.md` (Z8, Z3, Z5). Freigabe des Nutzers „Ja, Web Stufe 3“ am 14.11.2026, weitergeleitet über die Sitzung „Spiele-Studio Stufe 1“.
**Branch/Worktree:** `claude/T-0622-web-stufe3`, `C:\Users\<user>\TrendoEntwicklung\wt-T-0622-web-stufe3`, Basis `origin/main` cce5c6261.
**Status:** Alle Funktionen gebaut und mit Tests belegt. Vor dem Bau wurde der Iststand jeder Funktion im Code nachgeprüft; er ist jetzt in beiden Anforderungslisten eingetragen. QA je Teil und Security-Prüfung in eigenen Kopien, Befunde behoben (siehe unten). Kein Release.

## Was gebaut wurde

| ID | Ergebnis | Belege (Tests) |
|---|---|---|
| Seiten | Startseite `seite.trendo.json`, weitere unter `seiten/<adresse>.trendo.json`, Einstellungen in `website.trendo.json`; Stände und Varianten merken sich ihre Seite. | Anlegen, Umbenennen, Löschen in den Papierkorb, reservierte Namen und Gerätenamen abgelehnt |
| W4 | Übergang zwischen Seiten über View Transitions (einblenden, schieben); ohne Unterstützung oder bei „weniger Bewegung“ wechselt die Seite ohne Animation. | CSS im Export, Bedingung `prefers-reduced-motion` |
| W3 | Titel, Beschreibung, Adresse, Vorschaubild je Seite; canonical, og, `hreflang`, robots-Meta; `sitemap.xml`, `robots.txt`, `_redirects`. Inhalt wird im echten Browser vorgerendert und steht ohne Skript im HTML. Prüfung läuft auf dem Export. | Text ohne Skript im HTML, Sitemap listet alle Seiten, doppelte Adresse gemeldet; eigene `robots.txt`, `_redirects`, `CNAME`, `_headers` bleiben |
| W2 | Sammlungen (`cms.trendo.json`) mit Text, Bild, Datum, Verweis, Pflichtfeldern; Vorlagenseite mit `{{feld}}` ergibt eine Seite je Eintrag; Baustein „Sammlung“ zeigt die Liste. | 3 Einträge ergeben 3 Seiten und eine Liste; Pflichtfeld leer gemeldet; Platzhalter in Titel, Beschreibung und Bild |
| Z5 | Sprachen je Projekt, Übersetzungen unter `sprachen/<sprache>/`, Anzeige fehlender Übersetzungen, eigene Adressen je Sprache mit `lang` und `hreflang`; KI-Übersetzung nur als Vorschlag, erst „Übernehmen“ schreibt. Trendo selbst bleibt deutsch. | Zwei Sprachen ergeben zwei Seiten mit `hreflang`; fehlende Übersetzung angezeigt; nicht übersetzte Einträge als Original |
| K6 | Code und Design nebeneinander: Bausteine als HTML mit `data-typ`, Klick springt zur Stelle, Änderungen in beide Richtungen; was nicht abbildbar ist, bleibt als geprüfter HTML-Block (Allowlist, ohne `innerHTML`). | Änderung in jede Richtung, nicht abgleichbarer Code geht nicht verloren, Code nur auf aktuellem Stand übernommen |
| K3 | Zeichnen auf der Vorschau, Skizze und Satz gehen zusammen an die KI; ohne bildfähige KI ein Hinweis. | Chrome: Skizze im Auftrag; ohne bildfähige KI kein Fehler; Bildmaße vor dem Dekodieren geprüft |
| Z8 | HTML-Import über den GrapesJS-Kern 0.23.8 (BSD-3) in einer eigenen Seite mit CSP; Nicht-Abbildbares als HTML-Block plus Meldeliste. Figma nur mit eigenem Schlüssel, nicht gespeichert, ohne Weiterleitungen und mit Größengrenzen. Internetadressen und Netzwerkpfade als Quelle abgelehnt. | Beispielseite ergibt Bausteine plus Meldeliste; Schlüssel nirgends gespeichert; Adresse abgelehnt |
| K8 | Prüfung vor dem Hochladen (Sperrgründe, vollständige Suche nach Geheimnissen, keine Verknüpfungen und Punkt-Dateien), Zustimmung im Einzelfall mit Dateiliste, Zip genau aus dieser Liste, Hochladen zu Netlify mit eigenem Konto, ehrlicher Bericht. | Ohne Zustimmung kein Hochladen; Geheimnis sperrt; geänderter Ordner nach Zustimmung erkannt |
| A4 | Mods nur als Daten (Vorlagen, Aktionen, Skills) mit `trendo-webmod.json`, Rechten und freier Lizenz; Installation nur nach Zustimmung nach `mods/<id>`; die KI sieht Mod-Aktionen als geprüfte A1-Aktionen. | Mod ohne Beschreibung oder Lizenz abgelehnt; Mod kommt nicht aus dem Projektordner heraus; Code-Dateien abgelehnt |
| K7 | Bausteine „Datenliste“ und „Anmeldung“: SQLite im Browser (sql.js 1.15.2, MIT), lokale Anmeldung mit PBKDF2 (600 000 Durchläufe); Supabase optional über „Daten der Website“ nur mit öffentlichem Schlüssel, `service_role` und `sb_secret_` abgelehnt, SQL zum Anlegen ohne öffentliche Leseregel. | Chrome: speichert und lädt lokal; Geheimnis in Seite oder Export gemeldet und sperrt |
| Z3 | Baustein „3D-Modell“: GLB, andere Formate (.blend, .fbx, .obj) über Blender; WebGPU mit WebGL-2-Rückfall; Gaussian Splats über Spark 2.4.0 (MIT); Brennweite, Blickwinkel, Umgebung (Studio, eigenes HDR), Sonne, Schatten, Belichtung und Material einstellbar; Standbild für Geräte ohne 3D. | Chrome in Software und auf der Grafikkarte: Modell erscheint, ohne WebGPU läuft WebGL, ohne 3D erscheint das Standbild, Splat erscheint, keine Skriptfehler |

Tests neu: Core `WebDesignStufe3Tests` (18), `WebDesignStufe3Teil2Tests` (9), `WebDesignStufe3Teil3Tests` (4), `WebDesignStufe3QaTests` (11), `WebDesignStufe3Teil3QaTests` (5); App `WebDesignStufe3UiTests` (4), `WebDesignStufe3Teil2UiTests` (7), `WebDesignStufe3QaUiTests` (5), `WebDesignZ3Tests` (7). Prüfskripte in echtem Chrome: `pruefe-stufe3.mjs`, `pruefe-skizze.mjs`, `pruefe-daten.mjs`, `pruefe-3d.mjs`.

## PC-Lauf Z3

`reports/web-3d-pruefung-2026-11-14/` mit `messung.json` und vier Bildern. Grafikkarte NVIDIA GeForce RTX 5070, Chrome mit WebGPU. Testmodell: Poly Haven „vintage_video_camera“ (CC0), Umgebung `studio_small_09` (CC0), MD5 geprüft.

| Lauf | Renderer | Bildrate |
|---|---|---|
| Modell normal | WebGPU | 60 fps |
| Modell ohne WebGPU | WebGL 2 | 60 fps |
| Gaussian Splat | WebGL 2 (Spark) | 60 fps |
| Ohne 3D | Standbild | – |

60 fps ist die Bildwiederholrate des Bildschirms, also die Obergrenze.

## Reality Check und Quality Gate (Z3)

Geprüft an den Bildern des PC-Laufs. Gefunden und behoben:

- Schatten zu hart: weiche Kante über VSM-Schatten (Radius 3, 16 Proben), wie bei einer echten Sonne.
- Objekt „schwebte“: Kontaktverschattung unter der Grundfläche.
- Objekt zu klein im Bild: Abstand aus den echten Maßen statt aus der Hüllkugel, 15 % Luft.
- Linie am Rand des Schattenbodens: Boden nur so groß wie die Schattenkarte.

Fest eingestellt: AgX-Tonwertkurve, sRGB-Ausgabe, Spiegelungen aus Studio- oder HDR-Umgebung, Sonne mit etwa 5500 K, Kamera mit Brennweite auf Vollformat. Kein Bloom und kein Aufhelllicht ohne Quelle.

Quality Gate: 1. Wirkt das Ergebnis glaubwürdig real? Ja, für ein Produktbild mit Studiolicht. 2. Höchste sinnvolle Qualität? Ja, innerhalb dessen, was three.js in beiden Renderern kann.

Bewusste Grenzen:

- Die Schattenkante ist überall gleich weich. PCSS, also eine Kante, die mit dem Abstand weicher wird, gibt es in three.js nicht für beide Renderer.
- Der Splat wurde nur mit einer synthetischen Prüfkugel geprüft, nicht mit einer echten Aufnahme.

## QA-Prüfung

Unabhängig vom Implementierer, je Teil in eigener Kopie.

- **Teil 1:** 2 Muss- und 5 Soll-Befunde, alle behoben, mit Tests (Commit bde21c593).
- **Teil 2:** B1–B8 und A1–A4, alle behoben, mit Tests (Commit ed6247aff).
- **Teil 3** (A4, K7, Z3) mit Gegenprüfung von Teil 2: 23 Befunde (6 Muss, 11 Soll, 6 Kann). Die wichtigsten: Seiten mit 3D ließen sich nicht veröffentlichen (Fehlalarm der Geheimnis-Suche in drei.js), im Editor ließ sich kein Modell wählen und eine gewählte Datei kam nicht an, die Lizenzprüfung sah 3D-Dateien nicht, Neu-Installieren einer Mod konnte sie löschen, ein Export-Ordner mit `.git` war nicht mehr exportierbar. Alle Muss- und Soll-Befunde behoben (Commit 8795bc37e), dazu Kann 18, 19, 21, 23. Nachprüfung: 1–17 behoben; ein neuer Muss-Befund aus dem Fix (schreibgeschützte fremde Datei ließ ab dem dritten Export die Sicherung scheitern) und drei kleinere Befunde ebenfalls behoben, mit Test. Gegenprüfung Teil 2: alle benannten Befunde behoben; B5, B7, B8 ohne eigenen benannten Test.
  Bewusst offen: Kann 20 (Zahlen als Mod-Werte werden zu leerem Text), Kann 22 nur Meldetext (Mod-Bilder gelten in der Lizenzprüfung als „Lizenz unbekannt“), Befund 3 ohne automatischen Test im Editor.

## Security-Prüfung

- **Teil 2** (Z8 mit Figma, K8): Befunde 1–10, alle behoben (Commit ed6247aff).
- **Teil 3** (A4, K7, Z3) mit Gegenprüfung: kein kritischer oder hoher Befund; 2 mittel (Netzwerkpfad beim allgemeinen Medien-Import, verschachtelte Bausteine in Mod-Vorlagen), 6 niedrig (Mod-Texte im System-Prompt, 3D-Dateien hinter Verknüpfungen, Blender bettet fremde Texturen ein, HDR- und Splat-Größen, `CheckGlb`-Ausnahme, K7-Grenzen). Alle behoben. Nachprüfung: HDR-Kopf jetzt gelesen wie vom Lader im Browser, spitze Klammern in Mod-Texten maskiert, verknüpfte Bibliotheken in .blend-Dateien geprüft. Mit echtem Blender geprüft: Poly-Haven-Modell geht durch, Textur und verknüpfte .blend außerhalb des Modellordners werden abgelehnt.
  Grenze: Mod-Texte stehen weiter im System-Prompt, klar als fremde Daten markiert; „Nur nach Anmeldung“ ist eine Bequemlichkeit, kein Zugriffsschutz (so beschriftet).

## Volltest

VOLLTEST

## Offen und Empfehlungen

- A1-Aktion „seite_anlegen“ fehlt noch. Seiten legt bisher nur die Oberfläche an.
- Komponenten sind einsprachig. Das wird als Hinweis angezeigt.
- `Validate` begrenzt Zahlenwerte nicht auf ihren erlaubten Bereich. Editor und Laufzeit begrenzen sie.
- K7 mit Supabase: Trendo schreibt nur hinein, die Liste zeigt die Einträge auf dem Gerät. Lesen aus Supabase mit Anmeldung (Supabase Auth) wäre ein eigener Schritt.
- Splat mit einer echten Aufnahme prüfen, sobald eine vorliegt.
- `requirements.yaml` und `decisions.yaml` sind unverändert.

## Sprachwahl

C# für Core und App, JavaScript für den Editor. Rust wurde nicht eingesetzt: Es gibt keinen messbaren Engpass, und die Bündel laufen im Browser.
