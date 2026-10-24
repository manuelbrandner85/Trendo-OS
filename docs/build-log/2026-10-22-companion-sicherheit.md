# Begleiter: Loopback-Standard, Kopplungs-Sperre, Token-Ablauf

QA-Rückgabe zur Bindung in `CompanionServer`. Der genannte Commit `af4f3a7` ist auf `main` der Merge von #246, nicht diese Bindung. Bisher band `serve` ohne Schalter auf `http://+:{port}/`. `--local` war die Ausnahme.

- Standard ist jetzt `http://127.0.0.1:{port}/`. Alle Schnittstellen nur mit `serve --lan` oder gespeichertem `trendo-companion netz an`. `--local` gewinnt gegen beides.
- Kopplung: höchstens 5 Versuche pro Minute und Client (429, `Retry-After`). Nach 5 falschen Codes 15 Minuten Sperre nur für diese Adresse (403). Der Einmal-Code verbrennt weiterhin nach 5 Fehlversuchen. Ein anderer Client kann in der Sperre weiter koppeln. `forget` löscht Kopplungen und Sperren. Werte außerhalb der erlaubten Spanne in der Zustandsdatei fallen auf diese Standards zurück.
- Zugriffstokens bekommen ein Ablaufdatum (Standard 30 Tage, `--token-tage` oder `--token-stunden`, 1 Stunde bis 365 Tage). Abgelaufene Tokens liefern „Token abgelaufen“. Ältere Zustandsdateien ohne Datum gelten ab `At` plus der eingestellten Dauer. Kopplungscode bleibt 10 Minuten, einstellbar mit `--code-minuten` (1–60).
- Dienstdatei und Installer sagen das ausdrücklich. Die Unit startet weiter ohne `--lan`, also nur lokal. Hinweis im Geräteverbund: einmalig `netz an`, dann `pair`.
- Der Sicherheitscommit hat StudioWorkflow und die Issues #246, #236, #104 nicht bearbeitet. Der anschließende Merge holt `main` einschließlich #246 herein, ohne diese Dateien danach zu ändern.

Prüfung auf diesem Linux, nach dem Merge von `main`: `dotnet build Trendo.sln -c Release` ohne Fehler (0 Error). `dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 538 bestanden / 10 übersprungen (WinRealTests, gesamt 548), App 222 bestanden, keine Fehler. Davor curl: Port nur auf 127.0.0.1, 127.0.0.2 erst mit `--lan`, `--local` bleibt Loopback obwohl `netz an` gespeichert ist. Keine Pi-Hardware.

QA-Folge zu PR #272, nach Merge von `main` (kein Rebase): Entkoppeln vergleicht den Token-Hash mit `FixedTimeEquals`. Abgelaufene PCs werden aus `Pcs` entfernt und die Datei neu geschrieben. `Trim` streicht alte Versuche und Fehlerserien, die älter als die Sperrfrist sind; eine laufende Sperre und eine frische Fehlerserie bleiben. Eine beschädigte Zustandsdatei wird gemeldet, nicht still geschluckt. StudioWorkflow aus `main` ist nur durch den Merge hereingekommen, nicht nachbearbeitet.
