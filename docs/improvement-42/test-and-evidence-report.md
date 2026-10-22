# Verbesserung 42 – Tests und Belege (Phase A+B)

Umgebung: Linux, .NET 10, Commit-Basis `bbbfd5d7656fa73ad52b4d381afa9980bc34d30f`. Kein Blender, kein LibreOffice (`soffice`), kein Unreal im PATH. `ffmpeg`, `ffprobe`, `git`, `python3` und `dotnet` sind da. `blocked` ist nicht `passed`. Verbesserung 42 ist nicht fertig.

## Befehle und Ergebnis

| Befehl | Ergebnis |
|---|---|
| `dotnet test tests/Trendo.Core.Tests -c Release --filter PromptActionTests` | Bestanden: 23, Fehler: 0 |
| `dotnet test tests/Trendo.App.Tests -c Release --filter "FolderPick\|Preview_and_export_fit\|PromptBar"` | Bestanden: 7, Fehler: 0 |
| `dotnet test tests/Trendo.Api.Tests -c Release` | Bestanden: 15, Fehler: 0 |
| `dotnet test tests/Trendo.Core.Tests -c Release` | Bestanden: 511, Übersprungen: 10 (`WinRealTests`), Fehler: 0 |
| `dotnet test tests/Trendo.App.Tests -c Release` | Bestanden: 207, Fehler: 0 |

Die 10 übersprungenen Core-Tests sind die vorhandenen Windows-Prüfungen. Neu in diesem Stand: 23 Core-Tests und 4 App-Tests (Leiste auf der Briefseite, keine Leiste auf dem Startbildschirm, Kataloggleichstand, 3D-Leiste ohne kleinere Vorschau).

Ein früher Lauf der App-Tests mit einer hohen Leiste ist rot gewesen (Ordnerklick traf den Desktop, 3D-Vorschau 0×0 bei 1366×768). Die Leiste ist danach flach, und auf der 3D-Seite liegt sie im linken Rollbereich. Der Lauf danach ist grün. Ohne Leiste misst die Vorschau dort 55×55 px; die Prüfung verlangt mindestens 48 px.

## Pflichtfälle aus Abschnitt 10

| # | Fall | Stand | Beleg |
|---|---|---|---|
| 1 | Brief erzeugen, ändern, wieder öffnen, Export | not_run | Kein Briefadapter. Nächste Phase. |
| 2 | Tabellenformel, deterministisch | not_run | Kein Office-Adapter. |
| 3 | E-Mail: ohne Freigabe null, mit Freigabe genau eine | passed für den Testadapter `mail.senden` | `PromptActionTests.Freigabe_einmal_und_retry`, `Prompt_ersetzt_keine_Freigabe`. Produktionsversand: not_run, `MailCenter` sendet nicht, kein SMTP. |
| 4 | Cloud ohne Einwilligung, Widerruf | passed | `Cloud_ohne_Einwilligung_bleibt_stumm`, `Einwilligung_widerrufen`. Audit `ki.external.blocked`. |
| 5 | Unvollständig oder widersprüchlich | passed | `Rueckfrage_dann_Datei`, `Widerspruch_blockiert`. |
| 6 | Studio ohne Adapter | passed | `Ohne_Adapter_keine_Datei`. Alle 54 Katalogstudios sind `not_implemented`. |
| 7 | Zwei Projekte | passed | `Projekte_bleiben_getrennt`. |
| 8 | Abbruch, Retry, Resume, Rollback | passed | `Abbruch_blockiert_Wirkung`, `Kein_zweiter_Versuch_nach_Fehler`, `Resume_claimed_sendet_nicht_nochmal`, `Rollback_der_Laufdatei`, `Schema_zu_neu_bleibt_liegen`. |
| 9 | Anhang mit Prompt-Injection | passed | `Anhang_injiziert_keine_Freigabe`. |
| 10 | Technische Rechnung mit Einheiten | not_run | Kein Fachadapter. |
| 11 | Blender / Unreal | blocked | Werkzeuge fehlen, kein Adapter. |
| 12 | 23, 25, 34, Language Studio 2.0, 35, Design 41 | passed | `Entscheidungen_bleiben` liest `docs/spec/decisions.yaml`. Datei nicht geändert. |
| 13 | Forensik 120 Stunden | passed | `Forensik_120_Stunden` gegen `ForensicsLab.MaxLife` und `WarnBefore`. |
| 14 | Wirkung ohne Freigabe | passed für Nachrichten im Testadapter | `Freigabe_laeuft_ab`. Zahlung, Boot, Maschine: not_run, kein Adapter, Matrix blockiert SPEC-14 und CORE-09. |
| 15 | Matrix enthält jede StudioCatalog-ID einmal | passed | `Matrix_deckt_den_Katalog`, `Katalog_und_Abdeckung_stimmen`. 54 IDs, 0 Aktionen, Datei gleich `CapabilityCatalog.Markdown`. |

## Was die Oberfläche zeigt

Headless, kein Screenshot: Briefseite enthält „Was soll dieses Studio tun?“, „Noch nicht gestartet“ und „nicht eingebaut“. Ohne Projekt wird daraus „Rückfrage“ / „Lege zuerst ein Projekt an.“ Der Startbildschirm hat das Feld nicht.
