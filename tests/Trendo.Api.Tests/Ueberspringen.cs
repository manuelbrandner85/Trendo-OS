using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("Trendo.Api.Tests.UeberspringenFramework", "Trendo.Api.Tests")]

namespace Trendo.Api.Tests;

/// <summary>T-0681: ehrliches Überspringen mitten im Test. Fehlt ein Programm (FFmpeg, LibreOffice, Node …) oder passt das Betriebssystem nicht,
/// meldet xUnit den Test als „übersprungen“ – statt ihn mit einem stillen <c>return</c> als bestanden zu zählen.
/// Was der Test bis zu dieser Stelle geprüft hat, ist trotzdem gelaufen. Für ganze Tests gibt es weiterhin NeedsLanFact.
/// Dieses Projekt nutzt xUnit 2, das kein <c>Assert.Skip</c> kennt; <see cref="UeberspringenFramework"/> meldet die Ausnahme als „übersprungen“.</summary>
public static class Ueberspringen
{
    [DoesNotReturn]
    public static void Weil(string grund) => throw new UebersprungenException("Übersprungen – " + grund);
}

public sealed class UebersprungenException(string message) : Exception(message);

/// <summary>xUnit-2-Erweiterung: ein Test, der mit <see cref="UebersprungenException"/> endet, wird als „übersprungen“ gemeldet (nicht als Fehler, nicht als bestanden).
/// Alles andere läuft unverändert über die normalen xUnit-Klassen.</summary>
public sealed class UeberspringenFramework(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    sealed class Executor(AssemblyName name, ISourceInformationProvider source, IMessageSink diagnostics) : XunitTestFrameworkExecutor(name, source, diagnostics)
    {
        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using var runner = new Runner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
            await runner.RunAsync();
        }
    }

    sealed class Runner(ITestAssembly assembly, IEnumerable<IXunitTestCase> cases, IMessageSink diagnostics, IMessageSink execution, ITestFrameworkExecutionOptions options)
        : XunitTestAssemblyRunner(assembly, cases, diagnostics, execution, options)
    {
        protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            var bus = new Bus(messageBus);
            var summary = await base.RunTestCollectionAsync(bus, testCollection, testCases, cancellationTokenSource);
            summary.Failed -= bus.Skipped; summary.Skipped += bus.Skipped;
            return summary;
        }
    }

    sealed class Bus(IMessageBus inner) : IMessageBus
    {
        int _skipped;
        public int Skipped => _skipped;
        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (message is ITestFailed failed && failed.ExceptionTypes.Contains(typeof(UebersprungenException).FullName))
            {
                Interlocked.Increment(ref _skipped);
                return inner.QueueMessage(new TestSkipped(failed.Test, failed.Messages.FirstOrDefault(m => m?.StartsWith("Übersprungen", StringComparison.Ordinal) == true) ?? "Übersprungen"));
            }
            return inner.QueueMessage(message);
        }
        public void Dispose() { } // der eigentliche Bus gehört dem Aufrufer
    }
}
