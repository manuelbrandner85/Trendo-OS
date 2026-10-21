# SYS-12 – Adaptive Interface Accessibility & Personalization: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-12-P01 | Designs | Dunkel/Hell/Kontrast | Wechsel ohne Neustart | vorhanden |
| SYS-12-P02 | UI-Skalierung | 75–200 % | Begrenzung greift | vorhanden |
| SYS-12-P03 | Schriftgröße | Separat von Skalierung | Mindestens 12 pt möglich | umgesetzt, Box getestet |
| SYS-12-P04 | Tastatursteuerung | Alle Knöpfe per Tab erreichbar | UI-Test: Fokusreihenfolge | umgesetzt, Box getestet |
| SYS-12-P05 | Tastenkürzel | Ctrl+K, F1, Übersicht | Kürzelliste vollständig | vorhanden |
| SYS-12-P06 | Bildschirmleser | AutomationProperties.Name an Bedienelementen | Narrator liest Beschriftungen | umgesetzt, Box getestet |
| SYS-12-P07 | Kontrastprüfung | WCAG-AA für Text | Automatischer Kontrasttest | umgesetzt, Box getestet |
| SYS-12-P08 | Animationen reduzieren | Bewegung aus | Keine Übergänge aktiv | umgesetzt, Box getestet |
| SYS-12-P09 | Farbblind-Modus | Statusfarben + Symbole | Status ohne Farbe erkennbar | umgesetzt, Box getestet |
| SYS-12-P10 | Leichte Sprache | Kurze Hilfetexte | Umschaltbar | umgesetzt, Box getestet |
| SYS-12-P11 | Hilfe F1 | Kontexthilfe je Seite | Jede Seite hat Hilfe | vorhanden |
| SYS-12-P12 | Startseite anpassen | Kacheln wählen/sortieren | Reihenfolge bleibt | umgesetzt, Box getestet |
| SYS-12-P13 | Hintergrund | Eigenes Bild | Lokal, ohne Upload | umgesetzt, Box getestet |
| SYS-12-P14 | Akzentfarbe | Wählbar | Kontrast bleibt AA | umgesetzt, Box getestet |
| SYS-12-P15 | Navigation 2 Ebenen | Regel automatisch prüfen | UI-Test schlägt bei 3 Ebenen fehl | umgesetzt, Box getestet |
| SYS-12-P16 | Deutsche Beschriftung | Prüfung auf englische Labels | Test findet keine | umgesetzt, Box getestet |
| SYS-12-P17 | Profil-Name | Begrüßung mit Namen | Aus Einrichtung | vorhanden |
| SYS-12-P18 | Benachrichtigungsstil | Leise/normal | Wird respektiert | umgesetzt, Box getestet |
| SYS-12-P19 | Großer Mauszeiger-Hinweis | Link Windows-Einstellungen | Nur Link | umgesetzt, Box getestet |
| SYS-12-P20 | Vorlesen | Text per Windows-SAPI vorlesen | Offline möglich | umgesetzt, Box getestet |
| SYS-12-P21 | Diktat-Hinweis | Win+H Hinweis | Nur Hinweis | umgesetzt, Box getestet |
| SYS-12-P22 | Einstellungen sichern | Export/Import settings.json | Rundweg verlustfrei | umgesetzt, Box getestet |
| SYS-12-P23 | Mehrere Bildschirme | Fenster merkt Position | Position bleibt | umgesetzt, Box getestet |
| SYS-12-P24 | Einfacher Modus | Weniger Kacheln, große Knöpfe | Umschaltbar | umgesetzt, Box getestet |
| SYS-12-P25 | Barrierefreiheits-Bericht | Selbsttest aller Seiten | Bericht in reports/ | umgesetzt, Box getestet |

Stand: 5 vorhanden (vorher), 20 umgesetzt und auf der Linux-Box getestet, 0 teilweise, 0 offen. PC-Prüfung steht für alle aus.
