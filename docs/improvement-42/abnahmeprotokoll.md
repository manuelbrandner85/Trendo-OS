# Verbesserung 42 – Abnahmeprotokoll

Umgebung: Linux, .NET 10. `ffmpeg`, `ffprobe`, `git`, `python3` und `dotnet` sind im PATH. `blender`, `soffice` und Unreal sind es nicht. Verbesserung 42 ist nicht vollständig implementiert. `blocked` ist nicht `passed`. QA-017, QA-019 und die Codesignatur sind `not_run`.

Die Zahlen der Gesamtsuite stehen in `tasks/T-0042-anschluss.bericht.md`.

## Pflichtfälle aus Abschnitt 10

| # | Fall | Stand | Beleg |
|---|---|---|---|
| 1 | Brief erzeugen, ändern, wieder öffnen | passed für DOCX | `Brief_wird_geschrieben_und_wieder_gelesen`. PDF: blocked, LibreOffice fehlt. |
| 2 | Tabellenformel | passed für SUMME im Adapter | `Tabelle_rechnet_formel`, `Tabelle_teilt_nicht_durch_null`. LibreOffice rechnet die Datei nicht nach: blocked. |
| 3 | E-Mail ohne Versand null, mit Freigabe genau eine | blocked für den Versand | `Mail_senden_schickt_nichts`. Entwurf: `Mail_entwurf_wird_gelesen`. Der Freigabetext schickt keine Mail. |
| 4 | Cloud ohne Einwilligung | passed | `Cloud_ohne_Einwilligung_bleibt_stumm`, `Einwilligung_widerrufen`. |
| 5 | Unvollständig oder widersprüchlich | passed | `Rueckfrage_dann_Datei`, `Widerspruch_blockiert`, `Brief_unvollstaendig_fragt`. |
| 6 | Studio ohne Adapter | passed für eine leere Adapterliste | `Ohne_Adapter_keine_Datei`. Im Katalog ist kein Studio mehr `not_implemented`. |
| 7 | Zwei Projekte | passed | `Projekte_bleiben_getrennt`. |
| 8 | Abbruch, Retry, Resume, Rollback | passed | `Abbruch_blockiert_Wirkung`, `Resume_claimed_sendet_nicht_nochmal`, `Rollback_der_Laufdatei`, `Schema_zu_neu_bleibt_liegen`. |
| 9 | Anhang mit Prompt-Injection | passed | `Anhang_injiziert_keine_Freigabe`. |
| 10 | Technische Rechnung | passed für die registrierten Rechner | `Rechnung_rechnet_und_bucht_nicht`, `Messung_rechnet_mittelwert`, `Fraese_rechnet_drehzahl`, `Seite_schreibt_html`. Keine Fachberatung. |
| 11 | Blender und Unreal | blocked | `Modell_startet_blender_nicht`, `Spiel_startet_nichts`. OBJ und Projektdatei entstehen ohne den Editor. |
| 12 | 23, 25, 34, Language Studio 2.0, 35, Design 41 | passed | `Entscheidungen_bleiben`. SPEC-07 übersetzt nur Deutsch oder Englisch. CORE-01 und SYS-12 zeigen nur. Ändern bleibt aus. |
| 13 | Forensik 120 Stunden | passed | `Forensik_120_Stunden`. `Forensics.cs` ist unverändert. SPEC-12 ist `blocked`. |
| 14 | Boot, Maschine, Sicherheit, Zahlung, Veröffentlichung, Versand | blocked für diese Wirkungen | `Geld_zahlt_nichts`, `Software_veroeffentlicht_nichts`, `Seite_wird_nicht_veroeffentlicht`, `Github_klont_nichts`, `Ablauf_startet_nichts`, `Gesperrte_Studios_schreiben_nichts`. Laden, Teilen, Installieren und Programmstart sind davon getrennt und brauchen den Knopf. |
| 15 | Matrix enthält jede StudioCatalog-ID einmal | passed | `Matrix_deckt_den_Katalog`, `Katalog_und_Abdeckung_stimmen`. 54 IDs, 82 Aktionen, 52 `partial`, 2 `blocked`, kein `not_implemented`, kein `ready`. |

## Verbesserung 43, Punkte 1, 2, 9, 10, 11

| Punkt | Stand | Beleg |
|---|---|---|
| 1 Live-Vorschau | passed | Leiste `auftrag-vorschau`. Nach `Los` der gelesene Text. |
| 2 Plan, dann Los | passed | `Rueckfrage_dann_Datei`. Datei entsteht erst in `Los`. |
| 9 Versionen und Rückgängig | passed | `Rollback_der_Laufdatei`. Fassung zählt nur hoch, wenn schon eine Datei da war. |
| 10 Echte Datei | passed | `Ohne_Datei_kein_Erfolg`. |
| 11 Einfach und Profi | passed | Knopf `auftrag-modus`. Keine Rechteänderung. |
| 3–8 Webseite | partial | `PageWriteAdapter` ist registriert. `Webseiten_Adapter_ist_begrenzt_registriert` schreibt HTML und `seite.json`. 3D, Scroll-Geschichte, Partikel, Videohintergrund und Pixel-Streaming bleiben aus. |

## Bilder

Headless erzeugt, nicht im Repository. Die Leiste zeigt „QA bestanden“. Der Baukasten zeigt danach den Titel „Hofseite“ im Formular.

`briefe-vorschau.png`, `tabelle-vorschau.png`, `mail-vorschau.png`, `rechnung-vorschau.png`, `geschaeft-vorschau.png`, `software-vorschau.png`, `modell-vorschau.png`, `spiel-vorschau.png`, `medien-vorschau.png`, `wohnung-vorschau.png`, `lernen-vorschau.png`, `baukasten-vorschau.png`.

## Offen

- Kein Studio ist `ready`. Die Fertigdefinition aus Abschnitt 12 ist nicht erfüllt.
- Kamera, Mikrofon, Roboter, Handy-Verbindung, Kopplung, Explorer, Reparatur, Theme, Desktop und Geheimniswerte bleiben ohne Wirkung. Lesen schreibt nur eine Textdatei.
- Laden, Teilen, Installieren und Programmstart laufen nur nach dem Knopf „Einmal freigeben“. Installieren zusätzlich nur mit Lizenzdatei und nur für den Katalog.
- Verbesserung 35 bleibt offen und ist nicht aktiviert.
- Die Webseite hat keinen Shop, kein 3D und kein Pixel-Streaming.
