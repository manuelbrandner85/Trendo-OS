# Verbesserung 42 – Tests und Belege

Umgebung: Linux, .NET 10. Kein Blender, kein LibreOffice (`soffice`), kein Unreal im PATH. `ffmpeg`, `ffprobe`, `git`, `python3` und `dotnet` sind da. `blocked` ist nicht `passed`. Verbesserung 42 ist nicht vollständig implementiert.

Die erste Fassung dieses Berichts gehörte zu Phase A+B auf `bbbfd5d7656fa73ad52b4d381afa9980bc34d30f`. Die Tabellen unten nennen den Stand nach den Fachadaptern. Die Gesamtsuite steht in `tasks/T-0042-uebergabe.bericht.md`.

## Befehle und Ergebnis

| Befehl | Ergebnis |
|---|---|
| `dotnet test Trendo.sln -c Release` | Api 15, Core 609 bestanden und 10 übersprungen, App 224, Fehler 0 |
| `dotnet test tests/Trendo.Core.Tests -c Release --filter PromptActionTests\|ConnectedPromptTests` | Bestanden: 103, Fehler: 0 |

Die 10 übersprungenen Core-Tests sind die vorhandenen Windows-Prüfungen. Der Kern hat 103 Prompt-Tests, darunter Plan-vor-Los, die Fachadapter, die angeschlossenen Dienste und `Uebergabe_behauptet_keine_fertigstellung`. Die App-Prüfung der Leiste umfasst Vorschau, den Profi-Schalter und das Baukasten-Formular.

Ein früher Lauf der App-Tests mit einer hohen Leiste ist rot gewesen (Ordnerklick traf den Desktop, 3D-Vorschau 0×0 bei 1366×768). Die Leiste ist danach flach, und auf der 3D-Seite liegt sie im linken Rollbereich. Der Lauf danach ist grün. Ohne Leiste misst die Vorschau dort 55×55 px; die Prüfung verlangt mindestens 48 px.

## Pflichtfälle aus Abschnitt 10

| # | Fall | Stand | Beleg |
|---|---|---|---|
| 1 | Brief erzeugen, ändern, wieder öffnen, Export | passed für DOCX, PDF blocked | `Brief_wird_geschrieben_und_wieder_gelesen`. LibreOffice fehlt. |
| 2 | Tabellenformel, deterministisch | passed für SUMME | `Tabelle_rechnet_formel`. LibreOffice rechnet nicht nach. |
| 3 | E-Mail: ohne Freigabe null, mit Freigabe genau eine | blocked für den Versand | `Mail_senden_schickt_nichts`, `Mail_entwurf_wird_gelesen`. Kein SMTP. Der Freigabetext ändert das nicht. |
| 4 | Cloud ohne Einwilligung, Widerruf | passed | `Cloud_ohne_Einwilligung_bleibt_stumm`, `Einwilligung_widerrufen`. Audit `ki.external.blocked`. |
| 5 | Unvollständig oder widersprüchlich | passed | `Rueckfrage_dann_Datei`, `Widerspruch_blockiert`. |
| 6 | Studio ohne Adapter | passed für eine leere Liste | `Ohne_Adapter_keine_Datei`. Im Katalog ist kein Studio mehr `not_implemented`. |
| 7 | Zwei Projekte | passed | `Projekte_bleiben_getrennt`. |
| 8 | Abbruch, Retry, Resume, Rollback | passed | `Abbruch_blockiert_Wirkung`, `Kein_zweiter_Versuch_nach_Fehler`, `Resume_claimed_sendet_nicht_nochmal`, `Rollback_der_Laufdatei`, `Schema_zu_neu_bleibt_liegen`. |
| 9 | Anhang mit Prompt-Injection | passed | `Anhang_injiziert_keine_Freigabe`. |
| 10 | Technische Rechnung mit Einheiten | passed für die registrierten Rechner | `Rechnung_rechnet_und_bucht_nicht`, `Messung_rechnet_mittelwert`. |
| 11 | Blender / Unreal | blocked | `Modell_startet_blender_nicht`, `Spiel_startet_nichts`. |
| 12 | 23, 25, 34, Language Studio 2.0, 35, Design 41 | passed | `Entscheidungen_bleiben` liest `docs/spec/decisions.yaml`. Datei nicht geändert. Übersetzung, Desktop und Darstellung ändern diese Entscheidungen nicht. |
| 13 | Forensik 120 Stunden | passed | `Forensik_120_Stunden` gegen `ForensicsLab.MaxLife` und `WarnBefore`. |
| 14 | Wirkung ohne Freigabe | blocked | `Geld_zahlt_nichts`, `Gesperrte_Studios_schreiben_nichts`, `Freigabe_laeuft_ab`. |
| 15 | Matrix enthält jede StudioCatalog-ID einmal | passed | `Matrix_deckt_den_Katalog`, `Katalog_und_Abdeckung_stimmen`. 54 IDs, 82 Aktionen, 52 `partial`, 2 `blocked`, kein `not_implemented`, kein `ready`. |

## Verbesserung 43, Punkte 1, 2, 9, 10, 11

| Punkt | Stand | Beleg |
|---|---|---|
| 1 Live-Vorschau | passed für den Kern | Plan: „Noch keine Datei…“. Nach `Los` die gelesene Datei oder der Satz, dass es keine Textvorschau gibt. Leiste: `auftrag-vorschau`. |
| 2 Plan, dann Los, Folgeeingabe | passed | `Rueckfrage_dann_Datei`, `Revision_behaelt_Verlauf`, `Lokale_KI_gibt_nicht_frei`. Datei entsteht erst in `Los`. |
| 9 Versionen und Rückgängig | passed | `Rollback_der_Laufdatei` über `Undo`. Fassung zählt nur hoch, wenn schon eine Datei da war. |
| 10 Echte Datei am Ende | passed | `Ohne_Datei_kein_Erfolg`. `QA bestanden` ohne Datei gibt es nicht. |
| 11 Einfach und Profi | passed in der Leiste | Knopf `auftrag-modus`. Profi zeigt Fassung und Schritte, schaltet keine Rechte frei. |
| 3–8 Webseite | partial | `IWebsitePageAdapter` ist als `PageWriteAdapter` registriert. `Webseiten_Adapter_ist_begrenzt_registriert`. 3D, Scroll-Geschichte, Partikel, Videohintergrund und Pixel-Streaming bleiben aus. |

## Was die Oberfläche zeigt

Headless-Bilder, nicht im Repository: `briefe-vorschau.png`, `tabelle-vorschau.png`, `mail-vorschau.png`, `rechnung-vorschau.png`, `geschaeft-vorschau.png`, `software-vorschau.png`, `modell-vorschau.png`, `spiel-vorschau.png`, `medien-vorschau.png`, `wohnung-vorschau.png`, `lernen-vorschau.png`, `baukasten-vorschau.png`. Die Leiste steht auf „QA bestanden“. Beim Baukasten steht im Formular danach „Hofseite“. Ohne Projekt wird daraus „Rückfrage“ / „Lege zuerst ein Projekt an.“ Der Startbildschirm hat das Feld nicht.
