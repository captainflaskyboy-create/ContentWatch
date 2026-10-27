using ContentWatch.Core;
using ContentWatch.Rendering;
using ContentWatch.Desktop.Outputs;

namespace ContentWatch.Desktop;

public sealed class TimerApplication : IAsyncDisposable
{
    private readonly object stateGate = new();
    public TimerSessions Sessions { get; }
    public TimerEngine Engine => Sessions.Active;
    public FrameHub Frames { get; } = new();
    public SessionStore Store { get; }
    public BrowserOutput Browser { get; }
    public SavedSession? PreviousSession { get; }
    private AppSettings settings;
    public AppSettings Settings => Volatile.Read(ref settings);
    private readonly CancellationTokenSource stopping = new();
    private Task? worker;
    public string SpoutStatus { get; private set; } = "Starting";
    public string? Error { get; private set; }
    public event Action? DeadlineReached;
    public TimerApplication(string? dataDirectory = null)
    {
        Store = new(dataDirectory ?? Environment.GetEnvironmentVariable("CONTENTWATCH_DATA_DIR"));
        PreviousSession = Store.Load();
        settings = PreviousSession?.Settings ?? new();
        Sessions = new(new MonotonicClock(), PreviousSession);
        Browser = new(Frames);
    }
    public void UpdateSettings(AppSettings value)
    {
        lock (stateGate)
        {
            if (settings.CountdownOvertime != value.CountdownOvertime) Sessions.SetOvertime(value.CountdownOvertime);
            Volatile.Write(ref settings, value);
            Save();
        }
    }
    public void Command(Action<TimerEngine> action) { lock (stateGate) { action(Engine); Save(); } }
    public void SelectMode(TimerMode mode) { lock (stateGate) { Sessions.Select(mode); Save(); } }
    public void SetTime(double seconds) { lock (stateGate) { Sessions.SetTime(seconds); Save(); } }
    public void Adjust(double seconds) { lock (stateGate) { Sessions.Adjust(seconds); Save(); } }
    public Checkpoint Checkpoint()
    {
        lock (stateGate)
        {
            var checkpoint = Sessions.SaveCheckpoint(DateTimeOffset.Now);
            Save();
            if (Store.Error is { } error) throw new IOException("Checkpoint could not be saved: " + error);
            return checkpoint;
        }
    }
    public void Save() { lock (stateGate) Store.Save(Sessions.Capture(Settings)); }
    public async Task StartAsync()
    {
        try { await Browser.StartAsync(Settings.Port); }
        catch (Exception ex) { Error = "Browser output: " + ex.Message; }
        worker = Task.Factory.StartNew(RenderLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    private void RenderLoop()
    {
        using var renderer = new OverlayRenderer();
        using var spout = new SpoutOutput();
        var pacing = System.Diagnostics.Stopwatch.StartNew();
        var next = 0.0;
        var saved = 0.0;
        var previous = Engine.Snapshot();
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                TimerSnapshot timer;
                AppSettings current;
                lock (stateGate) { timer = Engine.Snapshot(); current = Settings; }
                var frame = renderer.Render(timer, current.AppearanceFor(timer.Mode));
                Frames.Publish(frame);
                spout.Send(frame, current.SpoutEnabled, current.SenderName);
                SpoutStatus = spout.Status;
                if (timer.Mode == previous.Mode && timer.Completed && !previous.Completed && previous.Running && current.EndSound) DeadlineReached?.Invoke();
                previous = timer;
                if (pacing.Elapsed.TotalSeconds - saved >= 1) { Save(); saved = pacing.Elapsed.TotalSeconds; }
            }
            catch (Exception ex) { Error = "Rendering: " + ex.Message; }
            next += 1.0 / 30;
            var delay = next - pacing.Elapsed.TotalSeconds;
            if (delay > 0) stopping.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(delay));
            else next = pacing.Elapsed.TotalSeconds;
        }
    }
    public async ValueTask DisposeAsync()
    {
        stopping.Cancel();
        if (worker is not null) await worker;
        Save();
        await Browser.DisposeAsync();
        stopping.Dispose();
    }
}
