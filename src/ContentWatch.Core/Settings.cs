using System.Globalization;
using System.Text.Json;

namespace ContentWatch.Core;

public sealed record Appearance
{
    public string Font { get; init; } = "Segoe UI";
    public float FontSize { get; init; } = 144;
    public string Foreground { get; init; } = "#F4F7FF";
    public string Outline { get; init; } = "#101521";
    public float OutlineWidth { get; init; }
    public bool Shadow { get; init; }
    public bool AlwaysHours { get; init; }
    public int FractionDigits { get; init; }
}
public sealed record TimeFormat(bool AlwaysHours = false, int FractionDigits = 0);
public sealed record AppSettings
{
    public Appearance Appearance { get; init; } = new();
    public TimeFormat TimerFormat { get; init; } = new();
    public TimeFormat StopwatchFormat { get; init; } = new(FractionDigits: 2);
    public bool CountdownOvertime { get; init; }
    public int Port { get; init; } = 17845;
    public bool SpoutEnabled { get; init; } = true;
    public string SenderName { get; init; } = "ContentWatch";
    public string StartShortcut { get; init; } = "Ctrl+Alt+F8";
    public string PauseShortcut { get; init; } = "Ctrl+Alt+F9";
    public string ResetShortcut { get; init; } = "Ctrl+Alt+F10";
    public bool EndSound { get; init; }
    public Appearance AppearanceFor(TimerMode mode)
    {
        var format = mode == TimerMode.Countdown ? TimerFormat : StopwatchFormat;
        return Appearance with { AlwaysHours = format.AlwaysHours, FractionDigits = format.FractionDigits };
    }
}
public sealed record Checkpoint(TimerSnapshot Timer, DateTimeOffset SavedAt);
public sealed record SavedSession(AppSettings Settings, TimerSnapshot Timer)
{
    public TimerSnapshot? Countdown { get; init; }
    public TimerSnapshot? Stopwatch { get; init; }
    public Checkpoint? CountdownCheckpoint { get; init; }
    public Checkpoint? StopwatchCheckpoint { get; init; }
}

public sealed class SessionStore(string? directory = null)
{
    public string DirectoryPath { get; } = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ContentWatch");
    public string? Error { get; private set; }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public SavedSession? Load()
    {
        try
        {
            var path = Path.Combine(DirectoryPath, "session.json");
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path);
            var saved = JsonSerializer.Deserialize<SavedSession>(text, Json);
            if (saved is null) return null;
            if (saved.Settings is null || saved.Timer is null) throw new JsonException("Invalid session data.");
            using var document = JsonDocument.Parse(text);
            var legacy = document.RootElement.GetProperty("Settings");
            if (!legacy.TryGetProperty("TimerFormat", out _))
            {
                var format = new TimeFormat(saved.Settings.Appearance.AlwaysHours, Math.Clamp(saved.Settings.Appearance.FractionDigits, 0, 3));
                saved = saved with { Settings = saved.Settings with { TimerFormat = format, StopwatchFormat = format, CountdownOvertime = saved.Timer.Overtime } };
            }
            return saved;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException) { Error = ex.Message; return null; }
    }
    public void Save(SavedSession session)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "session.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(session, Json));
            // A reader or virus scanner can briefly hold the destination on Windows.
            for (var attempt = 0; ; attempt++)
            {
                try { File.Move(path + ".tmp", path, true); break; }
                catch (IOException) when (attempt < 3) { Thread.Sleep(20); }
            }
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = ex.Message; }
    }
}

public static class TimerDisplay
{
    public static string Text(TimerSnapshot timer, Appearance style)
    {
        var value = Math.Abs(timer.Position);
        // Countdown without fractions rounds up so zero is never shown before completion.
        if (timer.Mode == TimerMode.Countdown && timer.Position > 0 && style.FractionDigits == 0) value = Math.Ceiling(value);
        var units = (long)Math.Floor(value * Math.Pow(10, style.FractionDigits) + 1e-7);
        var scale = (long)Math.Pow(10, style.FractionDigits);
        var seconds = units / scale;
        var hours = seconds / 3600;
        var main = hours > 0 || style.AlwaysHours ? $"{hours:00}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
        if (style.FractionDigits > 0) main += "." + (units % scale).ToString("D" + style.FractionDigits, CultureInfo.InvariantCulture);
        return (timer.Position < 0 ? "+" : "") + main;
    }
}
