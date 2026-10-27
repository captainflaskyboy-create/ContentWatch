using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;

namespace ContentWatch.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
        var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ContentWatch/Assets/Branding/contentwatch.ico")));
        Styles.Add(new Style(selector => selector.Is<Window>())
        {
            Setters = { new Setter(Window.IconProperty, icon) }
        });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
