namespace ContentWatch.Core;

/// <summary>Independent timers with explicit checkpoints separate from automatic recovery.</summary>
public sealed class TimerSessions
{
    private readonly TimerEngine countdown;
    private readonly TimerEngine stopwatch;
    public TimerMode ActiveMode { get; private set; } = TimerMode.Countdown;
    public TimerEngine Active => ActiveMode == TimerMode.Countdown ? countdown : stopwatch;
    public Checkpoint? CountdownCheckpoint { get; private set; }
    public Checkpoint? StopwatchCheckpoint { get; private set; }
    public Checkpoint? ActiveCheckpoint => ActiveMode == TimerMode.Countdown ? CountdownCheckpoint : StopwatchCheckpoint;

    public TimerSessions(IMonotonicClock clock, SavedSession? saved = null)
    {
        countdown = new(clock);
        stopwatch = new(clock);
        countdown.Configure(TimerMode.Countdown, 300, null, saved?.Settings.CountdownOvertime ?? false);
        if (saved is null) return;
        CountdownCheckpoint = saved.CountdownCheckpoint;
        StopwatchCheckpoint = saved.StopwatchCheckpoint;
        Restore(countdown, CountdownCheckpoint?.Timer ?? saved.Countdown ?? (saved.Timer.Mode == TimerMode.Countdown ? saved.Timer : null));
        Restore(stopwatch, StopwatchCheckpoint?.Timer ?? saved.Stopwatch ?? (saved.Timer.Mode == TimerMode.Stopwatch ? saved.Timer : null));
        ActiveMode = saved.Timer.Mode;
    }
    private static void Restore(TimerEngine engine, TimerSnapshot? saved)
    {
        // Former stopwatch targets must not affect restored sessions.
        if (saved is not null) engine.Restore(saved with { Target = null });
    }
    public void Select(TimerMode mode)
    {
        if (mode == ActiveMode) return;
        Active.Pause();
        ActiveMode = mode;
    }
    public void SetTime(double seconds)
    {
        var timer = Active.Snapshot();
        if (ActiveMode == TimerMode.Countdown && !timer.HasStarted && seconds > 0)
            Active.Configure(TimerMode.Countdown, seconds, null, timer.Overtime);
        else Active.SetTime(seconds);
    }
    public void Adjust(double seconds) => SetTime(Math.Max(0, Active.Snapshot().Position + seconds));
    public Checkpoint SaveCheckpoint(DateTimeOffset now)
    {
        Active.Pause();
        var checkpoint = new Checkpoint(Active.Snapshot(), now);
        if (ActiveMode == TimerMode.Countdown) CountdownCheckpoint = checkpoint;
        else StopwatchCheckpoint = checkpoint;
        return checkpoint;
    }
    public void SetOvertime(bool enabled) => countdown.SetOvertime(enabled);
    public SavedSession Capture(AppSettings settings) => new(settings, Active.Snapshot())
    {
        Countdown = countdown.Snapshot(), Stopwatch = stopwatch.Snapshot(),
        CountdownCheckpoint = CountdownCheckpoint, StopwatchCheckpoint = StopwatchCheckpoint
    };
}
