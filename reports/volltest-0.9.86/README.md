# Volltest 0.9.86

Stand: Commit 3aea2ca40 (danach nur Bericht und Neuerungen).
`dotnet test Trendo.sln -c Release`, `dotnet test tests/Trendo.Core.Tests -c Release`, `python -m unittest discover -s tests/claude_handoff`, Windows.

Api 16/16, Core 2075/2080 (5 uebersprungen), App 915/919 (4 uebersprungen), Paket-Tests 9/9.

Lauf 1 endete mit Exitcode 1, weil `Trendo.Core.Tests.dll` beim Bauen gesperrt war (MSB3027, parallel laufender Test einer anderen Sitzung). Kein Testfehler. Die Core-Tests liefen danach auf demselben Commit mit Exitcode 0 nach.

`dotnet.log` ist ein Auszug der Protokolle (Ergebniszeilen, Pfade anonymisiert). SHA-256 der vollstaendigen, nur lokal vorliegenden Protokolle: C337C9A107E02CFD759C473DD98AAF3190F3EBEBFDAAAAF2CBB1BAF6281F90D6 (Lauf 1), 67E767C476DD4D757DE69D1DFE0D718B91651B854EA29E4DAEAD3F0F53DC90B3 (Lauf 2).
