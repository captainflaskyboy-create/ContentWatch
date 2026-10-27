using Avalonia.Controls;
using Avalonia.Layout;

namespace ContentWatch.Desktop;

/// <summary>Separate duration and set-time editors avoid confusing adjustment with reset.</summary>
public sealed class TimeEditor : StackPanel
{
    private readonly NumericUpDown hours = Number(999999, 0), minutes = Number(59, 0), seconds = Number(59, 0), milliseconds = Number(999, 0);
    public TimeEditor(double value)
    {
        Orientation = Orientation.Horizontal; Spacing = 6;
        Add("Hours", hours); Add("Min", minutes); Add("Sec", seconds); Add("ms", milliseconds);
        Value = value;
    }
    private void Add(string name, Control input) => Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = name, FontSize = 11, Opacity = .65 }, input } });
    public double Value
    {
        get => (double)((hours.Value ?? 0) * 3600 + (minutes.Value ?? 0) * 60 + (seconds.Value ?? 0) + (milliseconds.Value ?? 0) / 1000);
        set { var ms = (long)Math.Round(Math.Max(0, value) * 1000); hours.Value = ms / 3600000; minutes.Value = ms / 60000 % 60; seconds.Value = ms / 1000 % 60; milliseconds.Value = ms % 1000; }
    }
    public static NumericUpDown Number(decimal max, decimal value, decimal min = 0, decimal increment = 1) => new() { Minimum = min, Maximum = max, Value = value, Increment = increment, Width = max > 999 ? 110 : 82, ShowButtonSpinner = false, FormatString = "0.###" };
}
