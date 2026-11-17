namespace Trendo.Core;

/// <summary>T-0688: Inhalte der Projektvorlagen des Software-Studios. Jede Vorlage hat eine kleine echte Funktion und einen echten Test.
/// Alles selbst geschrieben; fremd sind nur die im Projekt genannten NuGet-Pakete (xunit: Apache-2.0, Microsoft.NET.Test.Sdk: MIT, Avalonia: MIT).
/// Platzhalter: __NAME__ (Anzeigename), __HTML__ (Name für HTML maskiert), __SAFE__ (Ordner-/Projektname), __LOWER__ (klein, für package.json).</summary>
static class DevTemplates
{
    public const string Tfm = "net10.0";
    public const string XunitVersion = "2.9.3", XunitRunnerVersion = "3.1.4", TestSdkVersion = "17.14.1", AvaloniaVersion = "12.1.4";

    public static IReadOnlyList<(string Rel, string Text)> Files(string id) => id switch
    {
        "web" => Web,
        "python" => Python,
        "dotnet-console" => DotNetConsole,
        "dotnet-desktop" => DotNetDesktop,
        "node" => Node,
        _ => throw new ArgumentException("Unbekannte Vorlage: " + id),
    };

    const string GitIgnore = "bin/\nobj/\nnode_modules/\n__pycache__/\n*.pyc\ntrendo-paket/\nTestResults/\n";

    static string TestProject(string reference) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>{Tfm}</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{TestSdkVersion}" />
            <PackageReference Include="xunit" Version="{XunitVersion}" />
            <PackageReference Include="xunit.runner.visualstudio" Version="{XunitRunnerVersion}" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\{reference}" />
          </ItemGroup>
        </Project>

        """;

    // ───────────── .NET-Konsolenprogramm ─────────────

    static readonly (string, string)[] DotNetConsole =
    [
        ("README.md", """
            # __NAME__

            Ein Konsolenprogramm in C# (.NET): rechnet Summe, Mittelwert, kleinsten und größten Wert einer Zahlenreihe.

            - Starten: `dotnet run -- 2 3 5`
            - Testen: `dotnet test tests`
            - Als fertiges Programm verpacken: im Code-Editor „Paket bauen“ (oder `dotnet publish -c Release -r win-x64 --self-contained`)

            Die Tests nutzen xunit (Apache-2.0) und Microsoft.NET.Test.Sdk (MIT); beim ersten Bauen lädt .NET diese Pakete.

            """),
        (".gitignore", GitIgnore),
        ("__SAFE__.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{Tfm}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>Projekt</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <!-- Tests und fertige Pakete gehören nicht ins Programm -->
                <Compile Remove="tests\**;trendo-paket\**" />
              </ItemGroup>
            </Project>

            """),
        ("Program.cs", """
            using System.Globalization;
            using Projekt;

            Console.WriteLine("Hallo aus Trendo OS!");
            try
            {
                // Ohne Angabe rechnet das Programm ein Beispiel: dotnet run -- 2 3 5
                var zahlen = Rechner.Lesen(args.Length > 0 ? args : ["2", "3", "5"]);
                var de = CultureInfo.GetCultureInfo("de-DE");
                Console.WriteLine("Zahlen: " + string.Join("; ", zahlen.Select(z => z.ToString(de))));
                Console.WriteLine("Summe: " + Rechner.Summe(zahlen).ToString(de));
                Console.WriteLine("Mittelwert: " + Rechner.Mittelwert(zahlen).ToString(de));
                Console.WriteLine("Minimum: " + zahlen.Min().ToString(de) + ", Maximum: " + zahlen.Max().ToString(de));
                return 0;
            }
            catch (FormatException e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

            """),
        ("Rechner.cs", """
            using System.Globalization;

            namespace Projekt;

            /// <summary>Rechnet mit einer Zahlenreihe.</summary>
            public static class Rechner
            {
                public static decimal Summe(IEnumerable<decimal> zahlen) => zahlen.Sum();

                /// <summary>Mittelwert, auf zwei Stellen gerundet.</summary>
                public static decimal Mittelwert(IReadOnlyCollection<decimal> zahlen) =>
                    zahlen.Count == 0 ? throw new ArgumentException("Ohne Zahlen gibt es keinen Mittelwert.") : Math.Round(zahlen.Sum() / zahlen.Count, 2);

                /// <summary>Liest Zahlen aus Text. Komma und Punkt gelten beide als Dezimalzeichen.</summary>
                public static IReadOnlyList<decimal> Lesen(IEnumerable<string> texte)
                {
                    var zahlen = new List<decimal>();
                    foreach (var text in texte)
                    {
                        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl))
                            throw new FormatException($"\"{text}\" ist keine Zahl.");
                        zahlen.Add(zahl);
                    }
                    return zahlen;
                }
            }

            """),
        ("tests/__SAFE__.Tests.csproj", TestProject("__SAFE__.csproj")),
        ("tests/RechnerTests.cs", """
            using Projekt;
            using Xunit;

            public class RechnerTests
            {
                [Fact]
                public void Summe_addiert_alle_Zahlen() => Assert.Equal(10m, Rechner.Summe([2m, 3m, 5m]));

                [Fact]
                public void Mittelwert_rundet_auf_zwei_Stellen() => Assert.Equal(3.33m, Rechner.Mittelwert([2m, 3m, 5m]));

                [Fact]
                public void Mittelwert_ohne_Zahlen_meldet_einen_Fehler() => Assert.Throws<ArgumentException>(() => Rechner.Mittelwert([]));

                [Fact]
                public void Lesen_versteht_Komma_und_Punkt() => Assert.Equal([1.5m, 2.25m], Rechner.Lesen(["1,5", "2.25"]));

                [Fact]
                public void Lesen_meldet_falsche_Eingabe() => Assert.Throws<FormatException>(() => Rechner.Lesen(["zwei"]));
            }

            """),
    ];

    // ───────────── .NET-Desktop-Programm (Avalonia) ─────────────

    static readonly (string, string)[] DotNetDesktop =
    [
        ("README.md", """
            # __NAME__

            Ein Desktop-Programm mit Fenster in C# (.NET, Oberfläche: Avalonia): rechnet Einheiten um (Kilometer und Meilen, Grad Celsius und Fahrenheit, Kilogramm und Pfund).

            - Starten: `dotnet run`
            - Testen: `dotnet test tests`
            - Ohne Fenster prüfen: `dotnet run -- --selbsttest`
            - Als fertiges Programm verpacken: im Code-Editor „Paket bauen“

            Fremde Bausteine: Avalonia (MIT), für die Tests xunit (Apache-2.0) und Microsoft.NET.Test.Sdk (MIT); beim ersten Bauen lädt .NET diese Pakete.

            """),
        (".gitignore", GitIgnore),
        ("__SAFE__.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>{Tfm}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>Projekt</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="{AvaloniaVersion}" />
                <PackageReference Include="Avalonia.Desktop" Version="{AvaloniaVersion}" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="{AvaloniaVersion}" />
              </ItemGroup>
              <ItemGroup>
                <!-- Tests und fertige Pakete gehören nicht ins Programm -->
                <Compile Remove="tests\**;trendo-paket\**" />
              </ItemGroup>
            </Project>

            """),
        ("Program.cs", """
            using Avalonia;

            namespace Projekt;

            static class Program
            {
                [STAThread]
                public static int Main(string[] args)
                {
                    // Prüfen ohne Fenster: rechnet ein Beispiel und beendet sich
                    if (args.Contains("--selbsttest"))
                    {
                        Console.WriteLine("Selbsttest: 10 km = " + Umrechner.Text(Umrechner.Rechne("km-mi", 10)) + " Meilen");
                        return 0;
                    }
                    return AppBuilder.Configure<Anwendung>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
                }
            }

            """),
        ("Anwendung.cs", """
            using Avalonia;
            using Avalonia.Controls.ApplicationLifetimes;
            using Avalonia.Styling;
            using Avalonia.Themes.Fluent;

            namespace Projekt;

            public sealed class Anwendung : Application
            {
                public override void Initialize()
                {
                    Styles.Add(new FluentTheme());
                    RequestedThemeVariant = ThemeVariant.Dark;
                }

                public override void OnFrameworkInitializationCompleted()
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new Hauptfenster();
                    base.OnFrameworkInitializationCompleted();
                }
            }

            """),
        ("Hauptfenster.cs", """
            using Avalonia;
            using Avalonia.Controls;
            using Avalonia.Layout;
            using Avalonia.Media;

            namespace Projekt;

            /// <summary>Das Fenster: Wert eingeben, Richtung wählen, „Umrechnen“.</summary>
            public sealed class Hauptfenster : Window
            {
                readonly TextBox _wert = new() { Text = "10", Width = 140 };
                readonly ComboBox _richtung = new() { ItemsSource = Umrechner.Richtungen.Select(r => r.Name).ToList(), SelectedIndex = 0, Width = 260 };
                readonly TextBlock _ergebnis = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };

                public Hauptfenster()
                {
                    Title = "__NAME__";
                    Width = 520; Height = 300;
                    var knopf = new Button { Content = "Umrechnen", Background = Brush.Parse("#F5B83D"), Foreground = Brushes.Black, Padding = new Thickness(18, 8) };
                    knopf.Click += (_, _) => Zeigen();
                    Content = new StackPanel
                    {
                        Margin = new Thickness(28), Spacing = 14,
                        Children =
                        {
                            new TextBlock { Text = "Einheiten umrechnen", FontSize = 20 },
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _wert, _richtung } },
                            knopf,
                            _ergebnis,
                        },
                    };
                    Zeigen();
                }

                void Zeigen()
                {
                    var richtung = Umrechner.Richtungen[Math.Max(0, _richtung.SelectedIndex)];
                    _ergebnis.Text = Umrechner.Lesen(_wert.Text) is { } wert
                        ? $"{Umrechner.Text(wert)} {richtung.Von} = {Umrechner.Text(Umrechner.Rechne(richtung.Id, wert))} {richtung.Nach}"
                        : "Bitte eine Zahl eingeben.";
                }
            }

            """),
        ("Umrechner.cs", """
            using System.Globalization;

            namespace Projekt;

            /// <summary>Die Rechenregeln – ohne Fenster, damit sie sich einzeln testen lassen.</summary>
            public static class Umrechner
            {
                public sealed record Richtung(string Id, string Name, string Von, string Nach);

                public static readonly IReadOnlyList<Richtung> Richtungen =
                [
                    new("km-mi", "Kilometer in Meilen", "km", "Meilen"),
                    new("mi-km", "Meilen in Kilometer", "Meilen", "km"),
                    new("c-f", "Grad Celsius in Fahrenheit", "°C", "°F"),
                    new("f-c", "Fahrenheit in Grad Celsius", "°F", "°C"),
                    new("kg-lb", "Kilogramm in Pfund", "kg", "Pfund"),
                    new("lb-kg", "Pfund in Kilogramm", "Pfund", "kg"),
                ];

                public static double Rechne(string richtung, double wert) => richtung switch
                {
                    "km-mi" => wert / 1.609344,
                    "mi-km" => wert * 1.609344,
                    "c-f" => wert * 9 / 5 + 32,
                    "f-c" => (wert - 32) * 5 / 9,
                    "kg-lb" => wert / 0.45359237,
                    "lb-kg" => wert * 0.45359237,
                    _ => throw new ArgumentException("Unbekannte Richtung: " + richtung),
                };

                /// <summary>Liest eine Zahl; Komma und Punkt gelten beide. Kein Wert bei falscher Eingabe.</summary>
                public static double? Lesen(string? text) =>
                    double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var wert) ? wert : null;

                /// <summary>Zahl mit höchstens zwei Nachkommastellen, deutsch geschrieben.</summary>
                public static string Text(double wert) => Math.Round(wert, 2).ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
            }

            """),
        ("tests/__SAFE__.Tests.csproj", TestProject("__SAFE__.csproj")),
        ("tests/UmrechnerTests.cs", """
            using Projekt;
            using Xunit;

            public class UmrechnerTests
            {
                [Fact]
                public void Zehn_Kilometer_sind_gut_sechs_Meilen() => Assert.Equal("6,21", Umrechner.Text(Umrechner.Rechne("km-mi", 10)));

                [Fact]
                public void Hundert_Grad_Celsius_sind_212_Fahrenheit() => Assert.Equal(212, Umrechner.Rechne("c-f", 100), 6);

                [Fact]
                public void Hin_und_zurueck_ergibt_wieder_den_Anfang()
                {
                    Assert.Equal(42, Umrechner.Rechne("mi-km", Umrechner.Rechne("km-mi", 42)), 9);
                    Assert.Equal(42, Umrechner.Rechne("f-c", Umrechner.Rechne("c-f", 42)), 9);
                    Assert.Equal(42, Umrechner.Rechne("lb-kg", Umrechner.Rechne("kg-lb", 42)), 9);
                }

                [Fact]
                public void Lesen_versteht_Komma_und_lehnt_Text_ab()
                {
                    Assert.Equal(1.5, Umrechner.Lesen("1,5"));
                    Assert.Null(Umrechner.Lesen("viel"));
                }

                [Fact]
                public void Unbekannte_Richtung_meldet_einen_Fehler() => Assert.Throws<ArgumentException>(() => Umrechner.Rechne("x-y", 1));
            }

            """),
    ];

    // ───────────── Python ─────────────

    static readonly (string, string)[] Python =
    [
        ("README.md", """
            # __NAME__

            Ein Python-Programm: wertet eine Zahlenreihe aus (Summe, Mittelwert, Median).

            - Starten: `python main.py 2 3 5`
            - Testen: `python -m unittest -v`
            - Verpacken: im Code-Editor „Paket bauen“ – es entsteht eine ZIP-Datei mit Startskript.

            Das Programm braucht nur Python, keine weiteren Pakete.

            """),
        (".gitignore", GitIgnore),
        ("main.py", """"
            """__NAME__ – wertet eine Zahlenreihe aus."""

            import sys

            from rechner import lesen, median, mittelwert, summe


            def gruss(name: str) -> str:
                return f"Hallo {name}!"


            def main(argumente: list[str]) -> int:
                print(gruss("Welt"))
                try:
                    # Ohne Angabe rechnet das Programm ein Beispiel: python main.py 2 3 5
                    zahlen = lesen(argumente or ["2", "3", "5"])
                except ValueError as fehler:
                    print(fehler, file=sys.stderr)
                    return 1
                print("Zahlen:", "; ".join(f"{z:g}" for z in zahlen))
                print(f"Summe: {summe(zahlen):g}")
                print(f"Mittelwert: {mittelwert(zahlen):g}")
                print(f"Median: {median(zahlen):g}")
                return 0


            if __name__ == "__main__":
                sys.exit(main(sys.argv[1:]))

            """"),
        ("rechner.py", """"
            """Rechnen mit einer Zahlenreihe."""


            def lesen(texte: list[str]) -> list[float]:
                """Liest Zahlen aus Text. Komma und Punkt gelten beide als Dezimalzeichen."""
                zahlen = []
                for text in texte:
                    try:
                        zahlen.append(float(text.strip().replace(",", ".")))
                    except ValueError:
                        raise ValueError(f'"{text}" ist keine Zahl.') from None
                return zahlen


            def summe(zahlen: list[float]) -> float:
                return sum(zahlen)


            def mittelwert(zahlen: list[float]) -> float:
                if not zahlen:
                    raise ValueError("Ohne Zahlen gibt es keinen Mittelwert.")
                return round(sum(zahlen) / len(zahlen), 2)


            def median(zahlen: list[float]) -> float:
                if not zahlen:
                    raise ValueError("Ohne Zahlen gibt es keinen Median.")
                sortiert = sorted(zahlen)
                mitte = len(sortiert) // 2
                return sortiert[mitte] if len(sortiert) % 2 else (sortiert[mitte - 1] + sortiert[mitte]) / 2

            """"),
        ("test_rechner.py", """
            import unittest

            from main import gruss
            from rechner import lesen, median, mittelwert, summe


            class RechnerTest(unittest.TestCase):
                def test_summe(self):
                    self.assertEqual(summe([2, 3, 5]), 10)

                def test_mittelwert_rundet(self):
                    self.assertEqual(mittelwert([2, 3, 5]), 3.33)

                def test_median_gerade_und_ungerade(self):
                    self.assertEqual(median([5, 1, 3]), 3)
                    self.assertEqual(median([4, 1, 3, 2]), 2.5)

                def test_lesen_versteht_komma(self):
                    self.assertEqual(lesen(["1,5", "2.25"]), [1.5, 2.25])

                def test_lesen_meldet_falsche_eingabe(self):
                    with self.assertRaises(ValueError):
                        lesen(["zwei"])

                def test_gruss(self):
                    self.assertEqual(gruss("Erika"), "Hallo Erika!")


            if __name__ == "__main__":
                unittest.main()

            """),
    ];

    // ───────────── Web-App (statisch + kleines Node-Skript) ─────────────

    static readonly (string, string)[] Web =
    [
        ("README.md", """
            # __NAME__

            Eine kleine Web-App: eine Einkaufsliste, die im Browser gespeichert bleibt.

            - Ansehen: `index.html` im Browser öffnen (geht ohne Internet und ohne Server).
            - Mit Node (falls eingerichtet): `node server.js` zeigt die Seite unter http://127.0.0.1:8080, `node --test` prüft die Rechenregeln.
            - Verpacken: im Code-Editor „Paket bauen“ – es entsteht ein Ordner und eine ZIP-Datei für jeden Webspeicher.

            Alles selbst geschrieben, keine fremden Pakete.

            """),
        (".gitignore", GitIgnore),
        ("index.html", """
            <!doctype html>
            <html lang="de">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>__HTML__</title><link rel="stylesheet" href="style.css"></head>
            <body>
              <main>
                <h1>__HTML__</h1>
                <form id="neu">
                  <input id="text" placeholder="Was brauchst du? z. B. Brot" autocomplete="off" aria-label="Neuer Eintrag">
                  <button type="submit">Dazu</button>
                </form>
                <ul id="liste"></ul>
                <p id="stand"></p>
              </main>
              <script src="logik.js"></script>
              <script src="app.js"></script>
            </body>
            </html>

            """),
        ("style.css", """
            body { font-family: system-ui, sans-serif; background: #12141a; color: #f1ede2; margin: 0; }
            main { max-width: 30rem; margin: 0 auto; padding: 2rem 1rem; }
            h1 { font-size: 1.6rem; }
            form { display: flex; gap: .5rem; }
            input { flex: 1; padding: .6rem .8rem; border-radius: .5rem; border: 1px solid #3a3f4b; background: #1b1e26; color: inherit; font-size: 1rem; }
            button { padding: .6rem 1rem; border-radius: .5rem; border: 0; background: #f5b83d; color: #1a1400; font-size: 1rem; cursor: pointer; }
            ul { list-style: none; padding: 0; }
            li { display: flex; align-items: center; gap: .6rem; padding: .5rem 0; border-bottom: 1px solid #2a2e38; }
            li.erledigt span { text-decoration: line-through; opacity: .55; }
            li span { flex: 1; }
            li button { background: transparent; color: #f1ede2; border: 1px solid #3a3f4b; padding: .2rem .6rem; }
            #stand { opacity: .7; }

            """),
        ("logik.js", """
            // Die Regeln der Einkaufsliste – ohne Seite, damit sie sich einzeln testen lassen.
            // Läuft im Browser (als Logik) und in Node (über require).
            (function (wurzel) {
              const Logik = {
                // Neuer Eintrag ans Ende; leere und doppelte Einträge kommen nicht dazu.
                dazu(liste, text) {
                  const sauber = String(text || '').trim();
                  if (!sauber || liste.some((e) => e.text.toLowerCase() === sauber.toLowerCase())) return liste;
                  return [...liste, { text: sauber, erledigt: false }];
                },
                umschalten(liste, nummer) {
                  return liste.map((e, i) => (i === nummer ? { ...e, erledigt: !e.erledigt } : e));
                },
                weg(liste, nummer) {
                  return liste.filter((_, i) => i !== nummer);
                },
                stand(liste) {
                  const offen = liste.filter((e) => !e.erledigt).length;
                  if (liste.length === 0) return 'Noch nichts auf der Liste.';
                  return offen === 0 ? 'Alles erledigt.' : offen + ' von ' + liste.length + ' noch offen.';
                },
              };
              if (typeof module === 'object' && module.exports) module.exports = Logik;
              else wurzel.Logik = Logik;
            })(typeof self !== 'undefined' ? self : this);

            """),
        ("app.js", """
            // Verbindet die Seite mit den Regeln aus logik.js und merkt sich die Liste im Browser.
            const SPEICHER = 'einkaufsliste';
            let liste = [];
            try { liste = JSON.parse(localStorage.getItem(SPEICHER) || '[]'); } catch { liste = []; }

            function zeigen() {
              const ul = document.getElementById('liste');
              ul.textContent = '';
              liste.forEach((eintrag, nummer) => {
                const li = document.createElement('li');
                if (eintrag.erledigt) li.className = 'erledigt';
                const haken = document.createElement('input');
                haken.type = 'checkbox'; haken.checked = eintrag.erledigt;
                haken.onchange = () => { liste = Logik.umschalten(liste, nummer); merken(); };
                const text = document.createElement('span'); text.textContent = eintrag.text;
                const weg = document.createElement('button'); weg.textContent = 'Weg';
                weg.onclick = () => { liste = Logik.weg(liste, nummer); merken(); };
                li.append(haken, text, weg); ul.append(li);
              });
              document.getElementById('stand').textContent = Logik.stand(liste);
            }

            function merken() {
              try { localStorage.setItem(SPEICHER, JSON.stringify(liste)); } catch { /* Speicher gesperrt: Liste gilt bis zum Schließen */ }
              zeigen();
            }

            document.getElementById('neu').onsubmit = (ereignis) => {
              ereignis.preventDefault();
              const feld = document.getElementById('text');
              liste = Logik.dazu(liste, feld.value); feld.value = ''; merken();
            };
            zeigen();

            """),
        ("package.json", """
            {
              "name": "__LOWER__",
              "version": "0.1.0",
              "private": true,
              "scripts": { "start": "node server.js", "test": "node --test" }
            }

            """),
        ("server.js", """
            // Kleiner Server zum Ansehen der Seite: node server.js [Ordner] [Port]
            // Liefert nur Dateien aus dem Ordner aus und ist nur von diesem Rechner erreichbar.
            const http = require('node:http');
            const fs = require('node:fs');
            const path = require('node:path');

            const ordner = path.resolve(process.argv[2] || __dirname);
            const port = Number(process.argv[3] || process.env.PORT || 8080);
            const arten = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg' };

            const server = http.createServer((anfrage, antwort) => {
              const teil = decodeURIComponent(new URL(anfrage.url, 'http://x').pathname);
              const datei = path.join(ordner, teil === '/' ? 'index.html' : teil);
              if (!datei.startsWith(ordner + path.sep) && datei !== path.join(ordner, 'index.html')) { antwort.writeHead(403).end('Gesperrt'); return; }
              fs.readFile(datei, (fehler, inhalt) => {
                if (fehler) { antwort.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }).end('Nicht gefunden'); return; }
                antwort.writeHead(200, { 'Content-Type': arten[path.extname(datei).toLowerCase()] || 'application/octet-stream' }).end(inhalt);
              });
            });
            server.listen(port, '127.0.0.1', () => console.log('Bereit: http://127.0.0.1:' + server.address().port));

            """),
        ("test/logik.test.js", """
            const test = require('node:test');
            const assert = require('node:assert');
            const Logik = require('../logik.js');

            test('dazu: neuer Eintrag kommt ans Ende', () => {
              assert.deepStrictEqual(Logik.dazu([], ' Brot '), [{ text: 'Brot', erledigt: false }]);
            });

            test('dazu: leer und doppelt kommen nicht dazu', () => {
              const liste = Logik.dazu([], 'Brot');
              assert.strictEqual(Logik.dazu(liste, '  ').length, 1);
              assert.strictEqual(Logik.dazu(liste, 'brot').length, 1);
            });

            test('umschalten und weg', () => {
              let liste = Logik.dazu(Logik.dazu([], 'Brot'), 'Milch');
              liste = Logik.umschalten(liste, 0);
              assert.strictEqual(liste[0].erledigt, true);
              assert.deepStrictEqual(Logik.weg(liste, 0), [{ text: 'Milch', erledigt: false }]);
            });

            test('stand: Texte für leer, offen und fertig', () => {
              assert.strictEqual(Logik.stand([]), 'Noch nichts auf der Liste.');
              const liste = Logik.dazu(Logik.dazu([], 'Brot'), 'Milch');
              assert.strictEqual(Logik.stand(liste), '2 von 2 noch offen.');
              assert.strictEqual(Logik.stand(Logik.umschalten(Logik.umschalten(liste, 0), 1)), 'Alles erledigt.');
            });

            """),
    ];

    // ───────────── Node ─────────────

    static readonly (string, string)[] Node =
    [
        ("README.md", """
            # __NAME__

            Ein JavaScript-Programm für Node: zählt Wörter und Zeichen eines Textes.

            - Starten: `node index.js "Ein kurzer Satz"`
            - Testen: `node --test`
            - Verpacken: im Code-Editor „Paket bauen“ – es entsteht eine ZIP-Datei mit Startskript.

            """),
        (".gitignore", GitIgnore),
        ("package.json", """
            {
              "name": "__LOWER__",
              "version": "0.1.0",
              "type": "module",
              "scripts": { "start": "node index.js", "test": "node --test" }
            }

            """),
        ("index.js", """
            import { pathToFileURL } from 'node:url';

            export const gruss = (n) => `Hallo ${n}!`;

            // Zählt Wörter und Zeichen eines Textes.
            export function zaehle(text) {
              const sauber = String(text ?? '').trim();
              const woerter = sauber ? sauber.split(/\s+/) : [];
              return { woerter: woerter.length, zeichen: sauber.length, laengstes: woerter.reduce((a, w) => (w.length > a.length ? w : a), '') };
            }

            if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
              console.log(gruss('Welt'));
              const text = process.argv.slice(2).join(' ') || 'Ein kurzer Satz zum Ausprobieren';
              const z = zaehle(text);
              console.log(`Text: ${text}`);
              console.log(`Woerter: ${z.woerter}, Zeichen: ${z.zeichen}, laengstes Wort: ${z.laengstes}`);
            }

            """),
        ("index.test.js", """
            import test from 'node:test';
            import assert from 'node:assert';
            import { gruss, zaehle } from './index.js';

            test('gruss', () => assert.equal(gruss('Erika'), 'Hallo Erika!'));

            test('zaehle: Woerter, Zeichen, laengstes Wort', () => {
              assert.deepEqual(zaehle('  Ein kurzer  Satz '), { woerter: 3, zeichen: 16, laengstes: 'kurzer' });
            });

            test('zaehle: leerer Text', () => assert.deepEqual(zaehle(''), { woerter: 0, zeichen: 0, laengstes: '' }));

            """),
    ];
}
