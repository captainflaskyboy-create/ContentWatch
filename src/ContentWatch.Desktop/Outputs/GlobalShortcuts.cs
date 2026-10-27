using System.ComponentModel;
using System.Runtime.InteropServices;
using SharpHook;
using SharpHook.Data;

namespace ContentWatch.Desktop.Outputs;

public sealed record Shortcut(uint Modifiers, uint Key, KeyCode HookKey)
{
    public static Shortcut Parse(string text)
    {
        uint modifiers = 0;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("A shortcut needs a key");
        foreach (var part in parts[..^1]) modifiers |= part.ToLowerInvariant() switch
        { "ctrl" or "control" => 2u, "alt" => 1u, "shift" => 4u, "super" or "win" or "cmd" => 8u, _ => throw new ArgumentException("Unknown modifier: " + part) };
        var key = parts[^1].ToUpperInvariant();
        var code = key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]) ? key[0] :
            key.StartsWith('F') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24 ? (uint)(111 + f) : throw new ArgumentException("Use A–Z, 0–9 or F1–F24");
        if (!Enum.TryParse<KeyCode>("Vc" + key, true, out var hook)) throw new ArgumentException("Unsupported key: " + key);
        return new(modifiers, code, hook);
    }
}

public sealed class GlobalShortcuts : IDisposable
{
    private Thread? thread;
    private uint threadId;
    private SimpleGlobalHook? hook;
    private readonly Action<int> command;
    public string Status { get; private set; } = "Shortcuts stopped";
    public GlobalShortcuts(Action<int> command) => this.command = command;
    public void Configure(params string[] bindings)
    {
        Dispose();
        Shortcut[] parsed;
        try
        {
            parsed = bindings.Select(Shortcut.Parse).ToArray();
            if (parsed.Select(x => (x.Modifiers, x.Key)).Distinct().Count() != parsed.Length) throw new ArgumentException("Shortcuts must be distinct");
        }
        catch (ArgumentException ex) { Status = ex.Message; return; }
        if (OperatingSystem.IsWindows())
        {
            using var ready = new ManualResetEventSlim();
            thread = new Thread(() =>
            {
                threadId = GetCurrentThreadId();
                // Ensure the message queue exists before exposing the thread ID to disposal.
                PeekMessage(out _, 0, 0, 0, 0);
                var registered = new List<int>();
                var errors = new List<string>();
                for (var i = 0; i < parsed.Length; i++)
                {
                    if (RegisterHotKey(0, i + 1, parsed[i].Modifiers | 0x4000, parsed[i].Key)) registered.Add(i + 1);
                    else errors.Add(bindings[i] + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                }
                Status = errors.Count == 0 ? "Global shortcuts active" : string.Join("; ", errors);
                ready.Set();
                try { while (GetMessage(out var message, 0, 0, 0) > 0) if (message.Message == 0x0312) command((int)message.WParam - 1); }
                finally { foreach (var id in registered) UnregisterHotKey(0, id); threadId = 0; }
            }) { IsBackground = true, Name = "ContentWatch global shortcuts" };
            thread.Start(); ready.Wait();
        }
        else
        {
            // SharpHook uses the native macOS, X11 or Wayland backend. Never request root.
            hook = new SimpleGlobalHook();
            var pressed = new HashSet<KeyCode>();
            hook.KeyReleased += (_, e) => pressed.Remove(e.Data.KeyCode);
            hook.KeyPressed += (_, e) =>
            {
                if (!pressed.Add(e.Data.KeyCode)) return;
                var mask = e.RawEvent.Mask;
                uint mods = 0;
                if ((mask & EventMask.Ctrl) != 0) mods |= 2;
                if ((mask & EventMask.Alt) != 0) mods |= 1;
                if ((mask & EventMask.Shift) != 0) mods |= 4;
                if ((mask & EventMask.Meta) != 0) mods |= 8;
                for (var i = 0; i < parsed.Length; i++) if (parsed[i].HookKey == e.Data.KeyCode && parsed[i].Modifiers == mods) command(i);
            };
            hook.HookEnabled += (_, _) => Status = "Global shortcuts active (native keyboard hook)";
            _ = RunHookAsync(hook);
        }
    }
    private async Task RunHookAsync(SimpleGlobalHook value)
    {
        try { await value.RunAsync(GlobalHookType.Keyboard); }
        catch (Exception ex) { Status = "Global shortcuts unavailable: " + ex.Message + ". Check macOS Accessibility/Input Monitoring or Linux input permissions."; }
    }
    public void Dispose()
    {
        hook?.Dispose(); hook = null;
        if (threadId != 0) PostThreadMessage(threadId, 0x0012, 0, 0);
        if (thread is not null && thread != Thread.CurrentThread) thread.Join(2000);
        thread = null;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MessageData { public nint Window; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32")] private static extern int GetMessage(out MessageData message, nint window, uint min, uint max);
    [DllImport("user32")] private static extern bool PeekMessage(out MessageData message, nint window, uint min, uint max, uint remove);
    [DllImport("user32")] private static extern bool PostThreadMessage(uint id, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32")] private static extern uint GetCurrentThreadId();
}
