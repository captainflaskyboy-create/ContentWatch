using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using ContentWatch.Core;
using ContentWatch.Desktop.Outputs;
using ContentWatch.Rendering;
using ContentWatch.Desktop;
using Xunit;

namespace ContentWatch.Tests;

public sealed class OutputTests
{
    private static async Task WaitUntil(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(20, timeout.Token);
    }
    private static int FreePort() { var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop(); return port; }
    private static async Task<byte[]> Receive(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(5000);
        using var data = new MemoryStream(); var buffer = new byte[65536];
        WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer, timeout.Token); data.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
        return data.ToArray();
    }
    [Fact] public async Task ReconnectionRestoresCurrentFrameAndSlowClientDoesNotBlockPublishing()
    {
        var hub = new FrameHub(); using var renderer = new OverlayRenderer(); var engine = new TimerEngine(new FakeClock());
        var first = renderer.Render(engine.Snapshot(), new()); hub.Publish(first);
        await using var browser = new BrowserOutput(hub); var port = FreePort(); await browser.StartAsync(port);
        using var socket = new ClientWebSocket(); await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/frames"), default);
        Assert.Equal(first.Png, await Receive(socket));
        // Leave this client awaiting ACK while replacing the published frame repeatedly.
        engine.SetTime(42); var last = renderer.Render(engine.Snapshot(), new());
        for (var i = 0; i < 100; i++) hub.Publish(last);
        using var reconnect = new ClientWebSocket(); await reconnect.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/frames"), default);
        Assert.Equal(last.Png, await Receive(reconnect));
        Assert.Equal(42, engine.Snapshot().Position);
        socket.Abort(); reconnect.Abort();
    }
    [Fact] public async Task PortConflictIsExplicit()
    {
        await using var first = new BrowserOutput(new()); await using var second = new BrowserOutput(new());
        var port = FreePort(); await first.StartAsync(port);
        await Assert.ThrowsAnyAsync<IOException>(() => second.StartAsync(port)); Assert.Contains("unavailable", second.Status);
    }
    [Theory] [InlineData("Ctrl+Alt+F8", 3, 119)] [InlineData("Shift+A", 4, 65)]
    public void ShortcutParserUsesNativeKeyCodes(string binding, uint mods, uint key)
    { var shortcut = Shortcut.Parse(binding); Assert.Equal(mods, shortcut.Modifiers); Assert.Equal(key, shortcut.Key); }

    [Fact] public async Task OutputsAnimateWithoutAnyControlWindowAndFreezeOnPauseAndRestore()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentWatch-output-" + Guid.NewGuid());
        try
        {
            TimerSnapshot saved;
            await using (var application = new TimerApplication(path))
            {
                application.UpdateSettings(application.Settings with { Port = FreePort(), SpoutEnabled = false, TimerFormat = new(FractionDigits: 3) });
                application.Command(e => e.Configure(TimerMode.Countdown, 10, null, true));
                application.Command(e => e.Start());
                await application.StartAsync();
                await WaitUntil(() => application.Frames.Latest is not null);
                var a = application.Frames.Latest!;
                await WaitUntil(() => application.Frames.Latest!.Timer.Position < a.Timer.Position - .3);
                var b = application.Frames.Latest!;
                Assert.True(b.Sequence > a.Sequence); Assert.True(b.Timer.Position < a.Timer.Position);
                Assert.NotEqual(a.Png, b.Png);
                application.Command(e => e.Pause());
                await WaitUntil(() => !application.Frames.Latest!.Timer.Running);
                var paused = application.Frames.Latest!; await Task.Delay(160);
                Assert.Equal(paused.Png, application.Frames.Latest!.Png);
                application.Command(e => e.SetTime(7.123));
                await WaitUntil(() => application.Frames.Latest!.Timer.Position == 7.123);
                Assert.Equal(7.123, application.Frames.Latest!.Timer.Position);
                saved = application.Engine.Snapshot();
            }
            await using var restored = new TimerApplication(path);
            Assert.Equal(saved.Position, restored.Engine.Snapshot().Position);
            Assert.False(restored.Engine.Snapshot().Running);
            restored.Command(e => e.Start()); await Task.Delay(100);
            Assert.True(restored.Engine.Snapshot().Position < saved.Position);
        }
        finally { Directory.Delete(path, true); }
    }

    [Fact] public async Task BrowserRestoresCheckpointAfterResetRestartAndModeSwitch()
    {
        var path = Path.Combine(Path.GetTempPath(), "ContentWatch-checkpoint-output-" + Guid.NewGuid());
        try
        {
            await using (var original = new TimerApplication(path))
            {
                original.UpdateSettings(original.Settings with { Port = FreePort(), SpoutEnabled = false });
                original.SetTime(123.456);
                original.Checkpoint();
                original.Command(e => e.Reset());
                original.SetTime(999);
            }
            await using var restored = new TimerApplication(path);
            await restored.StartAsync();
            await WaitUntil(() => restored.Frames.Latest is not null);
            Assert.Equal(123.456, restored.Engine.Snapshot().Position);
            using var client = new ClientWebSocket();
            var address = new Uri($"ws://127.0.0.1:{restored.Settings.Port}/frames");
            await client.ConnectAsync(address, default);
            Assert.Equal(restored.Frames.Latest!.Png, await Receive(client));
            client.Abort();
            restored.SelectMode(TimerMode.Stopwatch);
            await WaitUntil(() => restored.Frames.Latest!.Timer.Mode == TimerMode.Stopwatch);
            using var reconnected = new ClientWebSocket();
            await reconnected.ConnectAsync(address, default);
            Assert.Equal(restored.Frames.Latest!.Png, await Receive(reconnected));
            Assert.Equal(0, restored.Frames.Latest!.Timer.Position);
            reconnected.Abort();
            restored.SelectMode(TimerMode.Countdown);
            Assert.Equal(123.456, restored.Engine.Snapshot().Position);
        }
        finally { Directory.Delete(path, true); }
    }
}
