using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Trendo.App;

public partial class App : Application
{
    public override void Initialize() { StartupTrace.Mark("app.init"); AvaloniaXamlLoader.Load(this); StartupTrace.Mark("app.xaml"); }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            { StartupTrace.Mark("app.framework"); desktop.MainWindow = new MainWindow(Program.Options); }
        base.OnFrameworkInitializationCompleted();
    }
}
