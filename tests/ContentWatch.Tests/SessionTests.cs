using System.Text.Json;
using ContentWatch.Core;
using ContentWatch.Rendering;
using Xunit;

namespace ContentWatch.Tests;

public sealed class SessionTests
{
    [Fact] public void DefaultsAreFiveMinutesAndZeroStopwatch()
    {
        var sessions = new TimerSessions(new FakeClock());
        Assert.Equal(TimerMode.Countdown, sessions.ActiveMode);
        Assert.Equal(300, sessions.Active.Snapshot().Position);
        sessions.Select(TimerMode.Stopwatch);
        Assert.Equal(0, sessions.Active.Snapshot().Position);
    }

    [Fact] public void SwitchingPausesAndPreservesBothModes()
    {
        var clock = new FakeClock(); var sessions = new TimerSessions(clock);
        sessions.Active.Start(); clock.Seconds = 12;
        sessions.Select(TimerMode.Stopwatch);
        sessions.Active.Start(); clock.Seconds = 20;
        sessions.Select(TimerMode.Countdown);
        Assert.Equal(288, sessions.Active.Snapshot().Position);
        Assert.False(sessions.Active.Snapshot().Running);
        clock.Seconds = 100;
        sessions.Select(TimerMode.Stopwatch);
        Assert.Equal(8, sessions.Active.Snapshot().Position);
        Assert.False(sessions.Active.Snapshot().Running);
    }

    [Fact] public void SelectingTheSameModeDoesNotPause()
    {
        var sessions = new TimerSessions(new FakeClock());
        sessions.Active.Start(); sessions.Select(TimerMode.Countdown);
        Assert.True(sessions.Active.Snapshot().Running);
    }

    [Fact] public void EditingBeforeStartChangesResetBaselineButEditingAfterStartDoesNot()
    {
        var clock = new FakeClock(); var sessions = new TimerSessions(clock);
        sessions.SetTime(600.123); Assert.Equal(600.123, sessions.Active.Snapshot().Duration);
        sessions.Active.Start(); clock.Seconds = 10;
        sessions.SetTime(200.456);
        Assert.False(sessions.Active.Snapshot().Running);
        sessions.Active.Reset(); Assert.Equal(600.123, sessions.Active.Snapshot().Position);
        sessions.Adjust(30); Assert.Equal(630.123, sessions.Active.Snapshot().Duration);
        sessions.Active.Start(); sessions.Adjust(-10000);
        Assert.Equal(0, sessions.Active.Snapshot().Position);
        Assert.False(sessions.Active.Snapshot().Running);
    }

    [Fact] public void CheckpointsPauseAndSurviveResetsEditsRecoveryAndClosedTime()
    {
        var clock = new FakeClock(); var sessions = new TimerSessions(clock);
        sessions.Active.Start(); clock.Seconds = 10.123;
        var checkpoint = sessions.SaveCheckpoint(DateTimeOffset.UtcNow);
        Assert.False(checkpoint.Timer.Running);
        sessions.Active.Reset(); sessions.SetTime(100);
        sessions.Select(TimerMode.Stopwatch); sessions.SetTime(77.456);
        var stopwatchCheckpoint = sessions.SaveCheckpoint(DateTimeOffset.UtcNow);
        sessions.Active.Start(); clock.Seconds += 30;
        var saved = sessions.Capture(new());
        clock.Seconds += 99999;
        var restored = new TimerSessions(clock, saved);
        Assert.Equal(TimerMode.Stopwatch, restored.ActiveMode);
        Assert.Equal(77.456, restored.Active.Snapshot().Position);
        Assert.False(restored.Active.Snapshot().Running);
        restored.Select(TimerMode.Countdown);
        Assert.Equal(checkpoint.Timer.Position, restored.Active.Snapshot().Position);
        Assert.Equal(checkpoint, restored.CountdownCheckpoint);
        Assert.Equal(stopwatchCheckpoint, restored.StopwatchCheckpoint);
        restored.Active.Start(); clock.Seconds += 1;
        Assert.Equal(checkpoint.Timer.Position - 1, restored.Active.Snapshot().Position, 6);
    }

    [Fact] public void OnlyAnotherExplicitCheckpointReplacesTheCheckpoint()
    {
        var sessions = new TimerSessions(new FakeClock());
        var first = sessions.SaveCheckpoint(DateTimeOffset.UtcNow);
        sessions.Adjust(30); sessions.Active.Reset();
        Assert.Equal(first, sessions.CountdownCheckpoint);
        sessions.SetTime(42);
        var second = sessions.SaveCheckpoint(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(42, second.Timer.Position);
        Assert.Equal(second, sessions.Capture(new()).CountdownCheckpoint);
    }

    [Fact] public void RecoveryIsUsedOnlyWhenThatModeHasNoCheckpoint()
    {
        var clock = new FakeClock(); var sessions = new TimerSessions(clock);
        sessions.SetTime(120); sessions.SaveCheckpoint(DateTimeOffset.UtcNow);
        sessions.Select(TimerMode.Stopwatch); sessions.Active.Start(); clock.Seconds = 15;
        var restored = new TimerSessions(clock, sessions.Capture(new()));
        Assert.Equal(15, restored.Active.Snapshot().Position);
        Assert.False(restored.Active.Snapshot().Running);
        restored.Select(TimerMode.Countdown); Assert.Equal(120, restored.Active.Snapshot().Position);
    }

    [Fact] public void SeparateFormatsApplyToTheSameAppearance()
    {
        var settings = new AppSettings { TimerFormat = new(true, 0), StopwatchFormat = new(false, 3) };
        var sessions = new TimerSessions(new FakeClock());
        Assert.Equal("00:05:00", TimerDisplay.Text(sessions.Active.Snapshot(), settings.AppearanceFor(TimerMode.Countdown)));
        sessions.Select(TimerMode.Stopwatch);
        Assert.Equal("00:00.000", TimerDisplay.Text(sessions.Active.Snapshot(), settings.AppearanceFor(TimerMode.Stopwatch)));
        Assert.Equal(settings.Appearance.Font, settings.AppearanceFor(TimerMode.Countdown).Font);
    }

    [Fact] public void LegacySettingsIgnoreEffectsTitlesAndOpaqueBackgrounds()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentWatch-legacy-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(path);
            var timer = new TimerEngine(new FakeClock()); timer.SetTime(42);
            var legacy = JsonSerializer.Serialize(new
            {
                Settings = new { Appearance = new { Font = "Arial", Foreground = "#FF0000", FractionDigits = 3, AlwaysHours = true, Effects = true, Title = "HIDDEN", BackgroundAlpha = 255, ShowPause = true } },
                Timer = timer.Snapshot()
            });
            File.WriteAllText(Path.Combine(path, "session.json"), legacy);
            var saved = new SessionStore(path).Load()!;
            Assert.Equal("Arial", saved.Settings.Appearance.Font);
            Assert.Equal(new TimeFormat(true, 3), saved.Settings.StopwatchFormat);
            var sessions = new TimerSessions(new FakeClock(), saved);
            using var renderer = new OverlayRenderer();
            var output = renderer.Render(sessions.Active.Snapshot(), saved.Settings.AppearanceFor(TimerMode.Stopwatch));
            // No alpha anywhere outside the central digit band: no title, label or background.
            for (var y = 0; y < 80; y++)
                for (var x = 0; x < OverlayRenderer.Width; x++) Assert.Equal(0, output.Bgra[(y * OverlayRenderer.Width + x) * 4 + 3]);
        }
        finally { Directory.Delete(path, true); }
    }
}
