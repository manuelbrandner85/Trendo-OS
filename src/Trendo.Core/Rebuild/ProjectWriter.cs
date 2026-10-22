using System.Text;

namespace Trendo.Core.Rebuild;

/// <summary>Schreibt ein eigenes Avalonia-Projekt. Der Quelltext kommt von hier, nicht aus dem Vorbild.</summary>
public static class ProjectWriter
{
    public static string AppDir(string root) => Path.Combine(root, "2-Umsetzung", "app");

    public static string KernDll(string root) => Path.Combine(AppDir(root), "Kern", "bin", "Release", "net10.0", "RebuildKern.dll");

    public static IReadOnlyList<GenFile> Create(AppBlueprint bp)
    {
        var asm = OwnName.Assembly(bp.OwnName);
        var on = bp.Features.Where(f => f.On && !f.Id.StartsWith("merk-", StringComparison.Ordinal)).Select(f => f.Id).ToList();
        var notes = bp.Features.Where(f => f.On && f.Id.StartsWith("merk-", StringComparison.Ordinal)).Select(f => f.Title).ToList();
        return
        [
            new("Kern/Kern.csproj", KernProj()),
            new("Kern/AppLogic.cs", Logic(bp, on, notes)),
            new("App/" + asm + ".csproj", AppProj(asm)),
            new("App/App.axaml", Axml()),
            new("App/App.axaml.cs", AppCs()),
            new("App/Program.cs", ProgramCs()),
            new("App/MainWindow.cs", WindowCs()),
            new("App/AppView.cs", ViewCs()),
            new("Tests/Tests.csproj", TestProj()),
            new("Tests/LogicTests.cs", Tests(bp.OwnName)),
            new("README.md", Readme(bp)),
            new(".gitignore", "bin/\nobj/\n")
        ];
    }

    public static IReadOnlyList<GenFile> Merge(IReadOnlyList<GenFile> skeleton, IReadOnlyList<GenFile> overlay)
    {
        var map = skeleton.ToDictionary(f => f.RelativePath, f => f.Content);
        foreach (var file in overlay)
        {
            if (!CodeGuard.Allowed.Contains(file.RelativePath)) continue;
            map[file.RelativePath] = file.Content;
        }
        return map.Select(kv => new GenFile(kv.Key, kv.Value)).ToList();
    }

    public static void Emit(string appDir, IReadOnlyList<GenFile> files)
    {
        if (Directory.Exists(appDir)) Directory.Delete(appDir, true);
        foreach (var file in files)
        {
            var path = Path.GetFullPath(Path.Combine(appDir, file.RelativePath));
            var root = Path.GetFullPath(appDir) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("Datei liegt außerhalb der App.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }

    static string KernProj() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>RebuildApp</RootNamespace>
            <AssemblyName>RebuildKern</AssemblyName>
          </PropertyGroup>
        </Project>
        """;

    static string AppProj(string asm) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>RebuildApp</RootNamespace>
            <AssemblyName>{{asm}}</AssemblyName>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Avalonia" Version="12.1.4" />
            <PackageReference Include="Avalonia.Desktop" Version="12.1.4" />
            <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.4" />
            <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.4" />
            <PackageReference Include="Velopack" Version="1.2.161" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\Kern\Kern.csproj" />
          </ItemGroup>
        </Project>
        """;

    static string TestProj() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
            <PackageReference Include="xunit" Version="2.9.3" />
            <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\Kern\Kern.csproj" />
          </ItemGroup>
        </Project>
        """;

    static string Axml() => """
        <Application xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     x:Class="RebuildApp.App"
                     RequestedThemeVariant="Dark">
            <Application.Styles>
                <FluentTheme />
            </Application.Styles>
        </Application>
        """;

    static string AppCs() => """
        using Avalonia;
        using Avalonia.Controls.ApplicationLifetimes;
        using Avalonia.Markup.Xaml;

        namespace RebuildApp;

        public partial class App : Application
        {
            public override void Initialize() => AvaloniaXamlLoader.Load(this);

            public override void OnFrameworkInitializationCompleted()
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.MainWindow = new MainWindow();
                base.OnFrameworkInitializationCompleted();
            }
        }
        """;

    static string ProgramCs() => """
        using Avalonia;

        namespace RebuildApp;

        internal static class Program
        {
            public static int Main(string[] args)
            {
                Velopack.VelopackApp.Build().Run();
                if (args.Contains("--selftest"))
                {
                    var result = AppLogic.SelfTest();
                    Console.WriteLine(result);
                    return result == "ok" ? 0 : 1;
                }
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                return 0;
            }

            public static AppBuilder BuildAvaloniaApp() =>
                AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
        }
        """;

    static string WindowCs() => """
        using Avalonia.Controls;

        namespace RebuildApp;

        public sealed class MainWindow : Window
        {
            public MainWindow()
            {
                Title = AppLogic.Title;
                Width = 880;
                Height = 620;
                Content = AppView.Build();
            }
        }
        """;

    static string Logic(AppBlueprint bp, List<string> on, List<string> notes)
    {
        var features = on.Count == 0 ? "" : string.Join(", ", on.Select(OwnName.Cs));
        var noteItems = notes.Count == 0 ? "" : string.Join(", ", notes.Select(OwnName.Cs));
        return $$"""
            namespace RebuildApp;

            public static class AppLogic
            {
                public const string Title = {{OwnName.Cs(bp.OwnName)}};
                public static readonly string Kind = {{OwnName.Cs(bp.Kind)}};
                public const int ButtonHeight = {{bp.ButtonHeight}};
                public const int FontSize = {{bp.FontSize}};
                public const bool Dark = {{(bp.Dark ? "true" : "false")}};
                public static readonly string[] Features = [{{features}}];
                public static readonly string[] Notes = [{{noteItems}}];

                public static bool Has(string id) => Array.IndexOf(Features, id) >= 0;
                public static string NotePath(string dir) => Path.Combine(dir, "zettel.txt");

                public static string Save(string dir, string text)
                {
                    Directory.CreateDirectory(dir);
                    var path = NotePath(dir);
                    File.WriteAllText(path, text ?? "");
                    return path;
                }

                public static string Load(string dir)
                {
                    var path = NotePath(dir);
                    return File.Exists(path) ? File.ReadAllText(path) : "";
                }

                public static int Count(string? text, string? term)
                {
                    if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text)) return 0;
                    var n = 0;
                    var i = 0;
                    while ((i = text.IndexOf(term, i, StringComparison.OrdinalIgnoreCase)) >= 0) { n++; i += term.Length; }
                    return n;
                }

                public static int Font(int current, int delta) => Math.Clamp(current + delta, 12, 36);
                public static string StrokePath(string dir) => Path.Combine(dir, "striche.txt");

                public static void AddStroke(string dir, string? color, int width, string? points)
                {
                    Directory.CreateDirectory(dir);
                    var safeColor = (color ?? "blau").Replace("|", "").Replace("\n", "").Replace("\r", "");
                    var safePoints = (points ?? "").Replace("\n", " ").Replace("\r", " ");
                    File.AppendAllText(StrokePath(dir), safeColor + "|" + width + "|" + safePoints + "\n");
                }

                public static int StrokeCount(string dir)
                {
                    var path = StrokePath(dir);
                    return File.Exists(path) ? File.ReadAllLines(path).Count(l => l.Length > 0) : 0;
                }

                public static void ClearStrokes(string dir)
                {
                    var path = StrokePath(dir);
                    if (File.Exists(path)) File.WriteAllText(path, "");
                }

                public static string Greet(string? name) => string.IsNullOrWhiteSpace(name) ? "Hallo." : "Hallo " + name.Trim() + ".";

                public static string SelfTest()
                {
                    var dir = Path.Combine(Path.GetTempPath(), "rebuild-self-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        if (Save(dir, "Hallo") is null) return "speichern";
                        if (Load(dir) != "Hallo") return "laden";
                        if (Count("AbAb", "ab") != 2) return "suchen";
                        if (Font(12, -8) != 12 || Font(12, 4) != 16) return "schrift";
                        AddStroke(dir, "blau", 3, "0,0 1,1");
                        if (StrokeCount(dir) != 1) return "strich";
                        ClearStrokes(dir);
                        if (StrokeCount(dir) != 0) return "leeren";
                        if (Greet("Ada") != "Hallo Ada.") return "gruss";
                        if (string.IsNullOrWhiteSpace(Title) || ButtonHeight < 28) return "titel";
                        return "ok";
                    }
                    finally
                    {
                        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { }
                    }
                }
            }
            """;
    }

    static string ViewCs() => """
        using Avalonia;
        using Avalonia.Controls;
        using Avalonia.Controls.Shapes;
        using Avalonia.Layout;
        using Avalonia.Media;

        namespace RebuildApp;

        public static class AppView
        {
            public static Control Build()
            {
                var dark = AppLogic.Dark;
                var bg = Brush.Parse(dark ? "#101826" : "#F4F7FB");
                var fg = Brush.Parse(dark ? "#EEF3FB" : "#1B2433");
                var muted = Brush.Parse(dark ? "#9DB0CF" : "#5C6B80");
                var accent = Brush.Parse("#3BA7FF");
                var root = new StackPanel { Spacing = 10, Margin = new Thickness(16), Background = bg };
                root.Children.Add(new TextBlock { Text = AppLogic.Title, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = fg });
                root.Children.Add(new TextBlock { Text = "Eine eigene App. Kein übernommener Name und kein Logo eines Vorbilds.", Foreground = muted, TextWrapping = TextWrapping.Wrap });
                foreach (var note in AppLogic.Notes)
                    root.Children.Add(new TextBlock { Text = "Merkpunkt: " + note, Foreground = muted, TextWrapping = TextWrapping.Wrap });
                if (AppLogic.Kind == "zeichnen") root.Children.Add(Paint(fg, muted, accent));
                else if (AppLogic.Kind == "notizen") root.Children.Add(Notes(fg, muted, accent));
                else root.Children.Add(General(fg, muted, accent));
                return new Border { Background = bg, Child = root };
            }

            static string Dir()
            {
                var d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EigeneApps", AppLogic.Title);
                Directory.CreateDirectory(d);
                return d;
            }

            static Button Btn(string text, IBrush accent)
            {
                return new Button
                {
                    Content = text,
                    MinHeight = AppLogic.ButtonHeight,
                    MinWidth = 96,
                    Margin = new Thickness(0, 0, 8, 8),
                    Background = accent,
                    Foreground = Brushes.White,
                    FontSize = AppLogic.FontSize
                };
            }

            static Control Notes(IBrush fg, IBrush muted, IBrush accent)
            {
                var box = new TextBox { AcceptsReturn = true, MinHeight = 220, TextWrapping = TextWrapping.Wrap, FontSize = AppLogic.FontSize, Text = AppLogic.Has("speichern") ? AppLogic.Load(Dir()) : "" };
                var row = new WrapPanel();
                var status = new TextBlock { Foreground = fg, TextWrapping = TextWrapping.Wrap };
                if (AppLogic.Has("speichern"))
                {
                    var save = Btn("Speichern", accent);
                    save.Click += (_, _) => { AppLogic.Save(Dir(), box.Text ?? ""); status.Text = "Gespeichert."; };
                    var open = Btn("Öffnen", accent);
                    open.Click += (_, _) => { box.Text = AppLogic.Load(Dir()); status.Text = "Geladen."; };
                    row.Children.Add(save);
                    row.Children.Add(open);
                }
                if (AppLogic.Has("suchen"))
                {
                    var q = new TextBox { PlaceholderText = "Suchwort", Width = 160, MinHeight = AppLogic.ButtonHeight, FontSize = AppLogic.FontSize };
                    var find = Btn("Suchen", accent);
                    find.Click += (_, _) => status.Text = AppLogic.Count(box.Text, q.Text) + " Treffer";
                    row.Children.Add(q);
                    row.Children.Add(find);
                }
                if (AppLogic.Has("schrift"))
                {
                    var up = Btn("Größer", accent);
                    var down = Btn("Kleiner", accent);
                    up.Click += (_, _) => box.FontSize = AppLogic.Font((int)box.FontSize, 2);
                    down.Click += (_, _) => box.FontSize = AppLogic.Font((int)box.FontSize, -2);
                    row.Children.Add(up);
                    row.Children.Add(down);
                }
                var panel = new StackPanel { Spacing = 8 };
                if (AppLogic.Has("schreiben") || AppLogic.Has("speichern")) panel.Children.Add(box);
                if (AppLogic.Has("zeilen"))
                {
                    var lines = new TextBlock { Foreground = muted };
                    void Update() => lines.Text = "Zeilen: " + (box.Text ?? "").Replace("\r\n", "\n").Split('\n').Length;
                    box.TextChanged += (_, _) => Update();
                    Update();
                    panel.Children.Add(lines);
                }
                panel.Children.Add(row);
                panel.Children.Add(status);
                return panel;
            }

            static Control Paint(IBrush fg, IBrush muted, IBrush accent)
            {
                var canvas = new Canvas { Width = 640, Height = 320, Background = Brush.Parse("#0B1220") };
                var color = "blau";
                var width = 3;
                Point? last = null;
                var current = new List<Point>();
                canvas.PointerPressed += (_, e) => { last = e.GetPosition(canvas); current = [last.Value]; e.Pointer.Capture(canvas); };
                canvas.PointerMoved += (_, e) =>
                {
                    if (last is not { } from) return;
                    var to = e.GetPosition(canvas);
                    canvas.Children.Add(new Line { StartPoint = from, EndPoint = to, Stroke = Brush.Parse(color == "gruen" ? "#4ADE80" : color == "rot" ? "#FB7185" : "#7DD3FC"), StrokeThickness = width });
                    current.Add(to);
                    last = to;
                };
                canvas.PointerReleased += (_, _) =>
                {
                    if (current.Count > 1 && AppLogic.Has("speichern"))
                        AppLogic.AddStroke(Dir(), color, width, string.Join(" ", current.Select(p => (int)p.X + "," + (int)p.Y)));
                    last = null;
                    current.Clear();
                };
                var row = new WrapPanel();
                var status = new TextBlock { Foreground = fg };
                if (AppLogic.Has("farben"))
                {
                    foreach (var (label, key) in new[] { ("Blau", "blau"), ("Grün", "gruen"), ("Rot", "rot") })
                    {
                        var chosen = key;
                        var b = Btn(label, accent);
                        b.Click += (_, _) => color = chosen;
                        row.Children.Add(b);
                    }
                }
                if (AppLogic.Has("pinsel"))
                {
                    var b = Btn("Dicker", accent);
                    b.Click += (_, _) => width = Math.Min(16, width + 2);
                    row.Children.Add(b);
                }
                if (AppLogic.Has("leeren"))
                {
                    var b = Btn("Leeren", accent);
                    b.Click += (_, _) => { canvas.Children.Clear(); if (AppLogic.Has("speichern")) AppLogic.ClearStrokes(Dir()); status.Text = "Leer."; };
                    row.Children.Add(b);
                }
                return new StackPanel { Spacing = 8, Children = { canvas, row, new TextBlock { Text = "Die Fläche gehört zu dieser App, nicht zu einem Vorbild.", Foreground = muted }, status } };
            }

            static Control General(IBrush fg, IBrush muted, IBrush accent)
            {
                var box = new TextBox { PlaceholderText = "Dein Text", MinHeight = AppLogic.ButtonHeight, FontSize = AppLogic.FontSize, Text = AppLogic.Has("speichern") ? AppLogic.Load(Dir()) : "" };
                var status = new TextBlock { Foreground = fg, TextWrapping = TextWrapping.Wrap };
                var row = new WrapPanel();
                if (AppLogic.Has("gruss"))
                {
                    var b = Btn("Zeigen", accent);
                    b.Click += (_, _) => status.Text = AppLogic.Greet(box.Text);
                    row.Children.Add(b);
                }
                if (AppLogic.Has("speichern"))
                {
                    var b = Btn("Speichern", accent);
                    b.Click += (_, _) => { AppLogic.Save(Dir(), box.Text ?? ""); status.Text = "Gespeichert."; };
                    row.Children.Add(b);
                }
                return new StackPanel { Spacing = 8, Children = { box, row, status } };
            }
        }
        """;

    static string Tests(string title) => $$"""
        using RebuildApp;
        using Xunit;

        public class LogicTests
        {
            [Fact]
            public void Name_ist_der_eigene() => Assert.Equal({{OwnName.Cs(title)}}, AppLogic.Title);

            [Fact]
            public void SelfTest_ist_ok() => Assert.Equal("ok", AppLogic.SelfTest());

            [Fact]
            public void Speichern_und_laden()
            {
                var dir = Path.Combine(Path.GetTempPath(), "rebuild-t-" + Guid.NewGuid().ToString("N"));
                AppLogic.Save(dir, "Zettel");
                Assert.Equal("Zettel", AppLogic.Load(dir));
                Directory.Delete(dir, true);
            }
        }
        """;

    static string Readme(AppBlueprint bp) =>
        "# " + bp.OwnName + "\n\n" +
        "Diese App hat Trendo neu geschrieben. Sie übernimmt keinen Quelltext, keinen Namen und kein Logo eines Vorbilds.\n\n" +
        "Der Name ist neu. Übernommen werden nur die bestätigten Funktionen, kein Markenname und kein Logo.\n\n" +
        "Bauen: `dotnet test Tests/Tests.csproj -c Release`\n";
}
