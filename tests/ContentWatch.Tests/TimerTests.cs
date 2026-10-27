using ContentWatch.Core;
using ContentWatch.Rendering;
using Xunit;

namespace ContentWatch.Tests;

public sealed class FakeClock : IMonotonicClock { public double Seconds { get; set; } }
public sealed class TimerTests
{
    [Fact] public void TimingDoesNotDependOnRenderTicksAndCanExceed24Hours()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Start(); clock.Seconds = 100000.125;
        Assert.Equal(100000.125, timer.Snapshot().Position, 6);
        Assert.Equal("27:46:40.125", TimerDisplay.Text(timer.Snapshot(), new() { FractionDigits = 3 }));
    }
    [Fact] public void RepeatedPauseResumePreservesTimeAndAnimation()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Start(); clock.Seconds = 10; timer.Pause(); clock.Seconds = 200;
        Assert.Equal(10, timer.Snapshot().Position); Assert.Equal(10, timer.Snapshot().AnimationSeconds);
        timer.Pause(); timer.Start(); timer.Start(); clock.Seconds = 205; timer.Pause();
        Assert.Equal(15, timer.Snapshot().Position); Assert.Equal(15, timer.Snapshot().AnimationSeconds);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void CountdownCompletesAndOptionallyOverruns(bool overtime)
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Configure(TimerMode.Countdown, 5, null, overtime); timer.Start(); clock.Seconds = 7;
        var state = timer.Snapshot(); Assert.True(state.Completed); Assert.Equal(overtime ? -2 : 0, state.Position);
        Assert.Equal(5, state.DeadlineAnimation);
        Assert.Equal(overtime ? "+00:02" : "00:00", TimerDisplay.Text(state, new()));
        timer.Pause(); clock.Seconds = 10; Assert.Equal(state.Position, timer.Snapshot().Position);
        timer.Reset(); Assert.Equal(5, timer.Snapshot().Position); Assert.False(timer.Snapshot().Running);
    }
    [Fact] public void StopwatchCountsIndefinitelyAndAdjustmentsPause()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Configure(TimerMode.Stopwatch, 60, 60, false); timer.Start(); clock.Seconds = 70;
        Assert.Equal(70, timer.Snapshot().Position); Assert.True(timer.Snapshot().Running);
        timer.SetTime(40); Assert.False(timer.Snapshot().Running); Assert.False(timer.Snapshot().Completed);
        timer.Adjust(-100); Assert.Equal(0, timer.Snapshot().Position);
        timer.Start(); clock.Seconds = 80; Assert.Equal(10, timer.Snapshot().Position);
    }
    [Fact] public void RestoredSessionNeverCountsClosedTime()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Configure(TimerMode.Countdown, 300, null, true); timer.Start(); clock.Seconds = 42.123;
        var saved = timer.Snapshot(); clock.Seconds = 90000;
        var restored = new TimerEngine(clock); restored.Restore(saved);
        Assert.False(restored.Snapshot().Running); Assert.Equal(saved.Position, restored.Snapshot().Position);
        restored.Start(); clock.Seconds += 5; Assert.Equal(saved.Position - 5, restored.Snapshot().Position, 6);
    }
    [Fact] public void ZeroIsNotShownBeforeCountdownActuallyCompletes()
    {
        var timer = new TimerEngine(new FakeClock()); timer.Configure(TimerMode.Countdown, .01, null, false);
        Assert.Equal("00:01", TimerDisplay.Text(timer.Snapshot(), new()));
        Assert.Equal("00:00.010", TimerDisplay.Text(timer.Snapshot(), new() { FractionDigits = 3 }));
    }
    [Fact] public void BackwardCountdownAdjustmentRearmsDeadline()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Configure(TimerMode.Countdown, 60, null, false); timer.Start(); clock.Seconds = 61;
        Assert.True(timer.Snapshot().Completed);
        timer.SetTime(50); Assert.False(timer.Snapshot().Completed);
        timer.Start(); clock.Seconds += 51; Assert.True(timer.Snapshot().Completed);
        Assert.Equal(111, timer.Snapshot().DeadlineAnimation);
    }
    [Fact] public void PausedRenderIsPixelIdenticalAndTransparent()
    {
        var clock = new FakeClock(); var timer = new TimerEngine(clock);
        timer.Configure(TimerMode.Countdown, 60, null, false); timer.Start(); clock.Seconds = 50; timer.Pause();
        using var renderer = new OverlayRenderer();
        var a = renderer.Render(timer.Snapshot(), new()); clock.Seconds += 500;
        var b = renderer.Render(timer.Snapshot(), new());
        Assert.Equal(a.Png, b.Png); Assert.Equal(a.Bgra, b.Bgra); Assert.Equal(0, a.Bgra[3]);
        Assert.Contains(a.Bgra.Where((_, i) => i % 4 == 3), alpha => alpha > 0);
        timer.Adjust(45); var adjusted = renderer.Render(timer.Snapshot(), new()); Assert.NotEqual(adjusted.Png, a.Png);
    }
    [Fact] public void RenderingIsCleanAndIndependentOfAnimationPhase()
    {
        var timer = new TimerEngine(new FakeClock()); var before = timer.Snapshot();
        using var renderer = new OverlayRenderer();
        var stages = new[] { 0.0, .3, .6, .9, 1 }.Select(v => renderer.Render(before with { AnimationSeconds = v }, new())).ToArray();
        Assert.Equal(before, timer.Snapshot());
        Assert.Single(stages.Select(f => Convert.ToBase64String(f.Png)).Distinct());
    }
    [Fact] public void SessionStoreRoundTripsAndHandlesCorruptFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentWatch-" + Guid.NewGuid());
        try
        {
            var store = new SessionStore(path); var saved = new SavedSession(new(), new TimerEngine(new FakeClock()).Snapshot());
            store.Save(saved); Assert.Equal(saved.Timer, store.Load()!.Timer);
            File.WriteAllText(Path.Combine(path, "session.json"), "broken"); Assert.Null(store.Load()); Assert.NotNull(store.Error);
        }
        finally { Directory.Delete(path, true); }
    }
}
