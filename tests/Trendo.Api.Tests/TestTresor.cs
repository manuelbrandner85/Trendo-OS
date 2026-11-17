using System.Runtime.CompilerServices;
using Trendo.Core;

namespace Trendo.Api.Tests;

/// <summary>Volltest-Befund 17.11.2026: Kerntests ohne eigenen Tresor legten über <see cref="SecretStoreFactory.CreateDefault"/> echte Einträge
/// (z. B. „TrendoOS:audit-hmac“) in der Windows-Anmeldeinformationsverwaltung der Person an. Für den ganzen Testlauf gilt deshalb ein gemeinsamer
/// Speicher-Tresor – ein gemeinsamer, damit Tests, die zwei Kerne nacheinander öffnen, denselben Schlüssel sehen wie in der echten App.</summary>
static class TestTresor
{
    static readonly InMemorySecretStore Shared = new();

    [ModuleInitializer]
    internal static void Init() => SecretStoreFactory.TestOverride = () => Shared;
}
