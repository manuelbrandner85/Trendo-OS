# T-0750: Geräte überwachen – weitere Quellen und Alarmregeln

**Ziel:** SPEC-10 „Geräte überwachen“ (Anlagen-Monitor, Seite `zwilling`) bekommt weitere echte Messquellen ohne Admin und Alarmregeln mit Benachrichtigung und Protokoll, die im Dokument stehen, rückgängig machbar sind und sich als Datei aus- und einlesen lassen.
**Herkunft:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 27 (SPEC-10, Urteil TEILWEISE; Hülle E4 erledigt mit T-0716).
**Branch:** claude/T-0750-geraete-ueberwachen
**Dateien/Bereich:** `src/Trendo.Core/W6/DigitalTwin.cs`, neu `src/Trendo.Core/W6/TwinFeeds.cs`, `src/Trendo.App/MainWindow.Spec10.cs`, `src/Trendo.App/MainWindow.Spec10Mqtt.cs` (eine Zeile), neu `src/Trendo.App/MainWindow.Spec10Alarm.cs`, neue Tests `tests/Trendo.Core.Tests/TwinAlarmTests.cs`, `tests/Trendo.App.Tests/TwinAlarmUiTests.cs`, Belege `reports/T-0750/`, `.gitattributes` (eine Zeile).
**Fertig, wenn:**
- [ ] Echte Leistungsindikatoren dieses PCs (CPU, RAM, Datenträger, Netz) ohne Admin als Sensoren von „Dieser PC“.
- [ ] Wachsende Datei (CSV/Text/JSON-Zeilen) als Quelle; HTTP im eigenen Netz nur lesend und nur nach ausdrücklicher Freigabe.
- [ ] Alarmregeln: über/unter, Dauer, Hysterese; Benachrichtigung über den vorhandenen Weg (`Toast`), Protokoll im Modell und im Prüfprotokoll (`AuditLog`).
- [ ] Regeln im Dokument (Modell des Zwillings), rückgängig über die Hülle (T-0716), Export/Import als Datei.
- [ ] Kein Aktor wird geschaltet.
- [ ] Echtbeleg unter `reports/T-0750/`: echte Werte über eine Minute, Regel löst bei künstlicher Last und bei wachsender Testdatei aus; SHA-256 gegen die Commit-Blobs.
- [ ] Gezielte Testklassen grün (kein Volltest).
**Nicht tun:** keine Steuerung von Geräten, kein Volltest, kein Merge, kein Release, nichts installieren; serielle Schnittstelle nur, wenn ein sendendes Gerät zum Prüfen da ist.
**Am Ende:** Branch pushen, Bericht `tasks/T-0750-geraete-ueberwachen.bericht.md`.
