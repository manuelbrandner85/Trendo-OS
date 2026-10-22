# Anschluss – Prompt-Dienste

Blockierte Aufträge hängen an vorhandenen Diensten, soweit dabei nichts nach außen läuft. Vier Vorgänge (Laden, Teilen, Installieren, Programmstart) laufen nur nach dem Knopf „Einmal freigeben“. Der Baukasten liest `seite.json` neu, `PageWriteAdapter` ist registriert. Der GET bleibt in der App, der Kern ohne `System.Net.Http`. `dotnet test Trendo.sln -c Release`: Api 15, Core 598 und 10 übersprungen, App 213, Fehler 0. Kein Studio ist `ready`.
