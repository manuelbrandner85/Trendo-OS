using System.Runtime.CompilerServices;
using Trendo.Core;

namespace Trendo.App.Tests;

/// <summary>T-0729: Befund 17.11.2026 – Testläufe ließen über 100 GB in %TEMP% liegen (150 000 leere trendo-ui-*, 585 Chrome-Profile …).
/// Dieser Testprozess schreibt deshalb in einen eigenen Laufordner %TEMP%\trendo-testlauf-&lt;pid&gt;-…, der am Ende als Ganzes verschwindet;
/// verwaiste Reste früherer Läufe räumt der Start weg (nur eigene Präfixe, älter als 24 h; abschaltbar mit TRENDO_TEMP_AUFRAEUMEN=0).
/// Feste Prüfordner wie trendo-tools-b erreichen Tests über <see cref="TempDir.SystemTemp"/>.</summary>
static class TestTemp
{
    [ModuleInitializer]
    internal static void Init() => TempDir.BeginTestRun();
}
