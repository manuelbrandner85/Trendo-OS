# Echte QA-/Release-Matrix (noch nicht ausgeführt)

| Gate | Pflichtnachweis | Status in diesem Paket |
|---|---|---|
| Code kompiliert | `dotnet build Trendo.sln -c Release` unter Zielumgebung | NOT_RUN |
| .NET Tests | `dotnet test Trendo.sln -c Release` mit Zahlen/Exitcode | NOT_RUN |
| Hook-Ereignisse | echte Claude-Code-Sitzung unter Windows | NOT_RUN |
| Host-Sicherheit | Pfad-, Secrets-, Freigabe-/Rollen-Negativtests | NOT_RUN |
| Agenten | jeder benötigte Subagent wird wirklich erkannt und korrekt delegiert | NOT_RUN |
| Job SQLite | create/transition/restart; Testbericht im Paket | PAKET_TEST_GESONDERT |
| MCP Server | initialize/tools/list/tools/call lokal, Claude /mcp noch nötig | PAKET_TEST_GESONDERT |
| Blender | installierte Version, .blend/.glb, Geometrie-/Material-Report | NOT_RUN |
| Unreal | Engine-Version, echtes .uproject, Log, Ergebnis | NOT_RUN |
| UI | Avalonia Screenshot, reale Interaktion, Barrierefreiheit | NOT_RUN |
| Installer | Windows-Installation, Deinstallation, Signatur | NOT_RUN |
| Release | Owner-Zustimmung + alle relevanten grünen Gates | BLOCKED |

GODMODE 2.0 ist ein **Claude-Code-Übergabepaket**, kein Produkt-Release und kein selbstständiges Projekt-Deployment.
