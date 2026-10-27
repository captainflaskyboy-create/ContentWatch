using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ContentWatch.Core;
using ContentWatch.Desktop.Outputs;

namespace ContentWatch.Desktop;

public sealed class MainWindow : Window
{
    private readonly TimerApplication application = new();
    private readonly GlobalShortcuts shortcuts;
    private readonly ContentControl page = new();
    private readonly Dictionary<string, Button> navigation = new();
    private readonly Image timeImage = new() { Stretch = Stretch.Uniform };
    private readonly Image obsImage = new() { Stretch = Stretch.Uniform, Height = 130 };
    private readonly ProgressRing ring = new() { Width = 258, Height = 258, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock checkpointStatus = Note("");
    private readonly TextBlock outputStatus = Note("");
    private readonly TextBlock settingsStatus = Note("");
    private readonly TextBlock message = Note("");
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Button play;
    private readonly Button timeButton;
    private readonly StackPanel quickAdd;
    private readonly Grid timerPage;
    private readonly Control obsPage;
    private readonly Control settingsPage;
    private Bitmap? bitmap;
    private long displayed;
    private string selectedPage = "Timer";
    private bool closing;

    public MainWindow()
    {
        Title = "ContentWatch";
        Width = 680; Height = 510; MinWidth = 570; MinHeight = 470;
        Background = Brush.Parse("#2B2F3B");
        shortcuts = new(index => Dispatcher.UIThread.Post(() => Run(() =>
            application.Command(engine =>
            {
                if (index == 0) engine.Start();
                else if (index == 1) engine.Toggle();
                else engine.Reset();
            }))));
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(18) };
        var nav = Row();
        foreach (var name in new[] { "Timer", "Stopwatch", "OBS", "Settings" })
        {
            var button = ActionButton(name, () => Navigate(name));
            navigation.Add(name, button);
            nav.Children.Add(button);
        }
        layout.Children.Add(nav);
        Grid.SetRow(page, 1); layout.Children.Add(page);
        Grid.SetRow(message, 2); layout.Children.Add(message);
        Content = layout;

        timerPage = new Grid { RowDefinitions = new("*,Auto,Auto,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        var face = new Grid();
        face.Children.Add(ring);
        timeButton = ActionButton("", async () => await EditTime());
        timeButton.Content = timeImage;
        timeButton.Background = Brushes.Transparent;
        timeButton.Resources["ButtonBackgroundPointerOver"] = Brushes.Transparent;
        timeButton.Resources["ButtonBackgroundPressed"] = Brushes.Transparent;
        timeButton.Resources["ButtonBorderBrushPointerOver"] = Brushes.Transparent;
        timeButton.Resources["ButtonBorderBrushPressed"] = Brushes.Transparent;
        timeButton.VerticalAlignment = VerticalAlignment.Center;
        timeButton.Height = 145;
        timeButton.Padding = new Thickness(0);
        ToolTip.SetTip(timeButton, "Click to edit time (pauses the timer)");
        face.Children.Add(timeButton);
        timerPage.Children.Add(face);
        quickAdd = Row(
            ActionButton("+0:30", () => Run(() => application.Adjust(30))),
            ActionButton("+1:00", () => Run(() => application.Adjust(60))),
            ActionButton("+5:00", () => Run(() => application.Adjust(300))));
        quickAdd.HorizontalAlignment = HorizontalAlignment.Center;
        quickAdd.Margin = new Thickness(0, 8, 0, 12);
        Grid.SetRow(quickAdd, 1); timerPage.Children.Add(quickAdd);
        var controls = new Grid { ColumnDefinitions = new("2*,*,*"), ColumnSpacing = 8 };
        play = ActionButton("▶ Play", () => Run(() => application.Command(e => e.Toggle())));
        play.Background = Brush.Parse("#AEC7FC"); play.Foreground = Brush.Parse("#1C273B");
        controls.Children.Add(play);
        var reset = ActionButton("↺ Reset", () => Run(() => application.Command(e => e.Reset())));
        Grid.SetColumn(reset, 1); controls.Children.Add(reset);
        var checkpoint = ActionButton("Checkpoint", () => Run(() =>
        {
            var saved = application.Checkpoint();
            var confirmation = "Paused and saved at " + saved.SavedAt.ToLocalTime().ToString("HH:mm:ss");
            message.Text = confirmation;
            DispatcherTimer.RunOnce(() => { if (message.Text == confirmation) message.Text = ""; }, TimeSpan.FromSeconds(4));
        }));
        ToolTip.SetTip(checkpoint, "Pause and save this session for the next launch");
        Grid.SetColumn(checkpoint, 2); controls.Children.Add(checkpoint);
        Grid.SetRow(controls, 2); timerPage.Children.Add(controls);
        checkpointStatus.Margin = new Thickness(0, 10, 0, 0);
        checkpointStatus.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRow(checkpointStatus, 3); timerPage.Children.Add(checkpointStatus);
        obsPage = BuildObs();
        settingsPage = BuildSettings();
        Navigate(application.Sessions.ActiveMode == TimerMode.Countdown ? "Timer" : "Stopwatch");
        Opened += async (_, _) =>
        {
            await application.StartAsync();
            ConfigureShortcuts();
            refresh.Start();
        };
        refresh.Tick += (_, _) => Refresh();
        application.DeadlineReached += () =>
        {
            if (OperatingSystem.IsWindows()) MessageBeep(0x40);
            else Console.Write('\a');
        };
        Closing += async (_, e) =>
        {
            if (closing) return;
            e.Cancel = true; closing = true;
            refresh.Stop(); shortcuts.Dispose();
            await application.DisposeAsync();
            timeImage.Source = null; obsImage.Source = null; bitmap?.Dispose();
            Close();
        };
    }

    private void Navigate(string destination)
    {
        if (destination is "Timer" or "Stopwatch")
        {
            var mode = destination == "Timer" ? TimerMode.Countdown : TimerMode.Stopwatch;
            if (mode != application.Sessions.ActiveMode) timeImage.Source = null;
            application.SelectMode(destination == "Timer" ? TimerMode.Countdown : TimerMode.Stopwatch);
        }
        selectedPage = destination;
        page.Content = destination switch { "OBS" => obsPage, "Settings" => settingsPage, _ => timerPage };
        foreach (var (name, button) in navigation)
        {
            button.Background = Brush.Parse(name == destination ? "#365785" : "#2B2F3B");
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse(name == destination ? "#365785" : "#353C4E");
            button.Resources["ButtonBackgroundPressed"] = button.Background;
        }
        message.Text = "";
        Refresh();
    }

    private void Refresh()
    {
        var frame = application.Frames.Latest;
        if (frame is not null && frame.Timer.Mode == application.Sessions.ActiveMode && displayed != frame.Sequence)
        {
            using var stream = new MemoryStream(frame.Png);
            var next = new Bitmap(stream);
            timeImage.Source = next; obsImage.Source = next;
            bitmap?.Dispose(); bitmap = next; displayed = frame.Sequence;
        }
        var timer = application.Engine.Snapshot();
        var text = TimerDisplay.Text(timer, application.Settings.AppearanceFor(timer.Mode));
        AutomationProperties.SetName(timeButton, "Edit time: " + text);
        play.Content = timer.Running ? "Ⅱ Pause" : "▶ Play";
        AutomationProperties.SetName(play, timer.Running ? "Pause" : "Play");
        quickAdd.IsVisible = timer.Mode == TimerMode.Countdown;
        ring.IsVisible = timer.HasStarted;
        ring.Progress = timer.Mode == TimerMode.Countdown
            ? Math.Clamp(1 - timer.Position / timer.Duration, 0, 1)
            : timer.Position % 60 / 60;
        ring.InvalidateVisual();
        var checkpoint = application.Sessions.ActiveCheckpoint;
        checkpointStatus.Text = checkpoint is null ? "Click the time to edit" : "Checkpoint saved " + checkpoint.SavedAt.ToLocalTime().ToString("g");
        outputStatus.Text = $"Showing {(timer.Mode == TimerMode.Countdown ? "Timer" : "Stopwatch")} in OBS\n{application.Browser.Status}\n{application.SpoutStatus}";
        if (application.Error is { } error) outputStatus.Text += "\n" + error;
        settingsStatus.Text = shortcuts.Status;
        if (application.Store.Error is { } saveError)
        {
            settingsStatus.Text += "\nSave failed: " + saveError;
            if (selectedPage is "Timer" or "Stopwatch") message.Text = "Could not save session. See Settings.";
        }
        else if (message.Text == "Could not save session. See Settings.") message.Text = "";
    }

    private async Task EditTime()
    {
        application.Command(e => e.Pause());
        var editor = new TimeEditor(Math.Max(0, application.Engine.Snapshot().Position));
        var error = Note("");
        var dialog = new Window
        {
            Title = "Set time", Width = 470, Height = 275, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Background
        };
        void Adjust(double seconds) => editor.Value = Math.Max(0, editor.Value + seconds);
        var content = Column(); content.Margin = new Thickness(20);
        content.Children.Add(Note(application.Engine.Snapshot().HasStarted ? "Adjust time. Reset keeps its original starting point." : "Set the starting time."));
        content.Children.Add(editor);
        content.Children.Add(Row(ActionButton("−1 min", () => Adjust(-60)), ActionButton("−10 sec", () => Adjust(-10)), ActionButton("+10 sec", () => Adjust(10)), ActionButton("+1 min", () => Adjust(60))));
        content.Children.Add(error);
        content.Children.Add(Row(ActionButton("Cancel", () => dialog.Close()), ActionButton("Set time", () =>
        {
            try { application.SetTime(editor.Value); dialog.Close(); }
            catch (ArgumentException ex) { error.Text = ex.Message; }
        })));
        dialog.Content = content;
        await dialog.ShowDialog(this);
        Refresh();
    }

    private Control BuildObs()
    {
        var panel = Column();
        panel.Children.Add(Heading("OBS output"));
        panel.Children.Add(new Border { Background = Brush.Parse("#20242E"), CornerRadius = new(10), Child = obsImage });
        panel.Children.Add(outputStatus);
        panel.Children.Add(Heading("Browser source"));
        var url = Input($"http://127.0.0.1:{application.Settings.Port}/overlay"); url.IsReadOnly = true;
        panel.Children.Add(url);
        panel.Children.Add(ActionButton("Copy URL", async () =>
        {
            if (Clipboard is { } clipboard) await clipboard.SetTextAsync(url.Text);
        }));
        panel.Children.Add(Note("OBS → Browser • 1280 × 360 • 30 FPS. Output is time only, with a transparent background. Reconnects restore the current time."));
        var port = TimeEditor.Number(65535, application.Settings.Port, 1024);
        panel.Children.Add(Field("Local port (restart after changing)", port));
        panel.Children.Add(Heading("Spout2 · Windows"));
        var enabled = Check("Enable Spout2", application.Settings.SpoutEnabled);
        var sender = Input(application.Settings.SenderName);
        panel.Children.Add(enabled); panel.Children.Add(Field("Sender name", sender));
        panel.Children.Add(Note("Install the Off World Live OBS Spout2 receiver plugin. Add Spout2 Capture, select this sender and Premultiplied Alpha. Use the same GPU for OBS and ContentWatch."));
        panel.Children.Add(ActionButton("Save OBS settings", () => Run(() =>
        {
            if (string.IsNullOrWhiteSpace(sender.Text) || System.Text.Encoding.UTF8.GetByteCount(sender.Text) > 200)
                throw new ArgumentException("Sender name must contain 1–200 UTF-8 bytes.");
            application.UpdateSettings(application.Settings with { Port = (int)port.Value!, SenderName = sender.Text.Trim(), SpoutEnabled = enabled.IsChecked == true });
            message.Text = "Saved. Port changes take effect after restart.";
        })));
        return Scroll(panel);
    }

    private Control BuildSettings()
    {
        var settings = application.Settings;
        var style = settings.Appearance;
        var panel = Column();
        panel.Children.Add(Heading("Appearance"));
        var font = Input(style.Font);
        var size = TimeEditor.Number(220, (decimal)style.FontSize, 24);
        var colour = Input(style.Foreground);
        var outline = Input(style.Outline);
        var width = TimeEditor.Number(16, (decimal)style.OutlineWidth);
        var shadow = Check("Soft shadow", style.Shadow);
        panel.Children.Add(Field("Font family", font));
        panel.Children.Add(Row(Field("Size", size), Field("Text colour", colour)));
        panel.Children.Add(Row(Field("Outline width", width), Field("Outline colour", outline)));
        panel.Children.Add(shadow);
        panel.Children.Add(Note("These settings apply to both clocks and both OBS outputs. Backgrounds are always transparent."));
        var timerHours = Check("Timer: always show hours", settings.TimerFormat.AlwaysHours);
        var stopwatchHours = Check("Stopwatch: always show hours", settings.StopwatchFormat.AlwaysHours);
        var timerFractions = Fractions(settings.TimerFormat.FractionDigits);
        var stopwatchFractions = Fractions(settings.StopwatchFormat.FractionDigits);
        panel.Children.Add(Heading("Time format"));
        panel.Children.Add(Field("Timer precision", timerFractions)); panel.Children.Add(timerHours);
        panel.Children.Add(Field("Stopwatch precision", stopwatchFractions)); panel.Children.Add(stopwatchHours);
        panel.Children.Add(Heading("Behaviour"));
        var overtime = Check("Countdown continues into overtime", settings.CountdownOvertime);
        var sound = Check("Play end sound", settings.EndSound);
        panel.Children.Add(overtime); panel.Children.Add(sound);
        panel.Children.Add(Note("End sound is off by default. Capture system/application audio in OBS to record it; the browser source is silent."));
        panel.Children.Add(Heading("Global shortcuts"));
        var start = Input(settings.StartShortcut);
        var pause = Input(settings.PauseShortcut);
        var reset = Input(settings.ResetShortcut);
        panel.Children.Add(Field("Play", start)); panel.Children.Add(Field("Pause / resume", pause)); panel.Children.Add(Field("Reset", reset));
        panel.Children.Add(Note("For example Ctrl+Alt+F9. macOS/Linux may need keyboard-hook permissions. Shortcut conflicts appear below."));
        panel.Children.Add(settingsStatus);
        panel.Children.Add(ActionButton("Save settings", () => Run(() =>
        {
            foreach (var value in new[] { colour.Text, outline.Text })
                if (!SkiaSharp.SKColor.TryParse(value, out _)) throw new ArgumentException("Use a valid hex colour, for example #AEC7FC.");
            var bindings = new[] { start.Text!, pause.Text!, reset.Text! }.Select(Shortcut.Parse).ToArray();
            if (bindings.Select(b => (b.Modifiers, b.Key)).Distinct().Count() != 3) throw new ArgumentException("Choose three different shortcuts.");
            application.UpdateSettings(application.Settings with
            {
                Appearance = style with { Font = font.Text ?? style.Font, FontSize = (float)size.Value!, Foreground = colour.Text!, Outline = outline.Text!, OutlineWidth = (float)width.Value!, Shadow = shadow.IsChecked == true },
                TimerFormat = new(timerHours.IsChecked == true, timerFractions.SelectedIndex),
                StopwatchFormat = new(stopwatchHours.IsChecked == true, stopwatchFractions.SelectedIndex),
                CountdownOvertime = overtime.IsChecked == true, EndSound = sound.IsChecked == true,
                StartShortcut = start.Text!, PauseShortcut = pause.Text!, ResetShortcut = reset.Text!
            });
            ConfigureShortcuts(); message.Text = "Settings saved.";
        })));
        return Scroll(panel);
    }

    private void ConfigureShortcuts() => shortcuts.Configure(application.Settings.StartShortcut, application.Settings.PauseShortcut, application.Settings.ResetShortcut);
    private void Run(Action action)
    {
        try { action(); Refresh(); }
        catch (Exception ex) { message.Text = ex.Message; }
    }
    private static ComboBox Fractions(int selected) => new() { ItemsSource = new[] { "Whole seconds", "Tenths", "Hundredths", "Milliseconds" }, SelectedIndex = selected, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static StackPanel Column() => new() { Spacing = 12, Margin = new Thickness(0, 16, 8, 6) };
    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }
    private static StackPanel Field(string label, Control input) => new() { Spacing = 4, Children = { Note(label), input } };
    private static TextBlock Note(string text) => new() { Text = text, FontSize = 12, Opacity = .75, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 17, FontWeight = FontWeight.SemiBold };
    private static TextBox Input(string text) => new() { Text = text, MinWidth = 160 };
    private static CheckBox Check(string label, bool value) => new() { Content = label, IsChecked = value };
    private static ScrollViewer Scroll(Control child) => new() { Content = child, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private static Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label, Background = Brush.Parse("#3B4151"), Foreground = Brush.Parse("#EFF3FF"), CornerRadius = new(24), Padding = new Thickness(15, 10), HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }
    [DllImport("user32")] private static extern bool MessageBeep(uint type);
}
