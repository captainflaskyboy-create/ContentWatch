using System.Diagnostics;

namespace ContentWatch.Core;

public interface IMonotonicClock { double Seconds { get; } }
public sealed class MonotonicClock : IMonotonicClock
{
    public double Seconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
public enum TimerMode { Stopwatch, Countdown }
public sealed record TimerSnapshot(TimerMode Mode, double Duration, double? Target, bool Overtime,
    double Position, bool Running, double AnimationSeconds, bool Completed, double? DeadlineAnimation, bool HasStarted = false);

/// <summary>All durations are seconds. Countdown Position is signed: negative means overtime.</summary>
public sealed class TimerEngine(IMonotonicClock clock)
{
    private readonly object gate = new();
    private TimerMode mode;
    private double duration = 300, position, animation, anchor;
    private double? target;
    private bool running, overtime, hasStarted;
    private double? deadlineAnimation;

    public TimerSnapshot Snapshot()
    {
        lock (gate)
        {
            var delta = running ? Math.Max(0, clock.Seconds - anchor) : 0;
            var value = position + (mode == TimerMode.Countdown ? -delta : delta);
            var visualTime = animation + delta;
            var completed = mode == TimerMode.Countdown ? value <= 0 : target is { } t && value >= t;
            // Record the exact crossing, even if rendering was stalled when it happened.
            if (completed && deadlineAnimation is null)
            {
                var distance = mode == TimerMode.Countdown ? position : target!.Value - position;
                deadlineAnimation = animation + Math.Max(0, distance);
            }
            // A completed countdown holds at zero unless overtime is enabled.
            if (mode == TimerMode.Countdown && !overtime && value <= 0)
            {
                value = 0;
                position = 0;
                animation = visualTime;
                anchor = clock.Seconds;
                running = false;
            }
            return new(mode, duration, target, overtime, value, running, visualTime, completed, deadlineAnimation, hasStarted);
        }
    }

    private void Capture()
    {
        var state = Snapshot();
        position = state.Position;
        animation = state.AnimationSeconds;
        anchor = clock.Seconds;
    }
    public void Start() { lock (gate) { if (running || (mode == TimerMode.Countdown && !overtime && position <= 0)) return; anchor = clock.Seconds; running = true; hasStarted = true; } }
    public void Pause() { lock (gate) { Capture(); running = false; } }
    public void Toggle() { lock (gate) { if (running) Pause(); else Start(); } }
    public void Configure(TimerMode newMode, double seconds, double? targetSeconds, bool allowOvertime)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || (targetSeconds.HasValue && (!double.IsFinite(targetSeconds.Value) || targetSeconds <= 0)))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Duration and target must be positive finite values.");
        lock (gate) { mode = newMode; duration = seconds; target = targetSeconds; overtime = allowOvertime; Reset(); }
    }
    public void Reset()
    {
        lock (gate) { running = false; hasStarted = false; position = mode == TimerMode.Countdown ? duration : 0; animation = 0; deadlineAnimation = null; anchor = clock.Seconds; }
    }
    public void SetTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        lock (gate) { Capture(); running = false; position = seconds; deadlineAnimation = null; }
    }
    public void Adjust(double seconds) { lock (gate) SetTime(Math.Max(0, Snapshot().Position + seconds)); }
    public void Restore(TimerSnapshot saved)
    {
        lock (gate)
        {
            Configure(saved.Mode, saved.Duration, saved.Target, saved.Overtime);
            if (!double.IsFinite(saved.Position) || !double.IsFinite(saved.AnimationSeconds)) throw new ArgumentException("Invalid session");
            position = saved.Mode == TimerMode.Stopwatch ? Math.Max(0, saved.Position) : saved.Position;
            animation = Math.Max(0, saved.AnimationSeconds);
            deadlineAnimation = saved.DeadlineAnimation;
            running = false;
            hasStarted = saved.HasStarted || saved.Running || saved.AnimationSeconds > 0;
        }
    }
    public void SetOvertime(bool enabled)
    {
        lock (gate) { Capture(); overtime = enabled; if (!enabled && mode == TimerMode.Countdown && position <= 0) { position = 0; running = false; } }
    }
}
