using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ContentWatch.Core;
using ContentWatch.Desktop;
using ContentWatch.Rendering;
using Xunit;

namespace ContentWatch.Tests;

public sealed class SpoutHardwareFactAttribute : FactAttribute
{
    public SpoutHardwareFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CONTENTWATCH_TEST_SPOUT") != "1")
            Skip = "Requires Windows GPU and built native bridge; set CONTENTWATCH_TEST_SPOUT=1 to run.";
    }
}
public sealed class SpoutHardwareTests
{
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    private static async Task WaitUntil(Func<bool> condition)
    { using var timeout = new CancellationTokenSource(10000); while (!condition()) await Task.Delay(30, timeout.Token); }

    [SpoutHardwareFact] public async Task NativePixelsMatchSharedFrameAfterPauseAdjustmentRestoreAndResume()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ContentWatch-gpu-" + Guid.NewGuid());
        var name = "ContentWatch.Test." + Guid.NewGuid().ToString("N");
        nint receiver = 0;
        try
        {
            await using var application = new TimerApplication(directory);
            application.UpdateSettings(application.Settings with { Port = FreePort(), SenderName = name, SpoutEnabled = true });
            application.Command(e => e.Configure(TimerMode.Countdown, 60, null, true));
            application.Command(e => e.SetTime(8.123));
            await application.StartAsync();
            await WaitUntil(() => application.SpoutStatus.StartsWith("Spout transmitting"));
            receiver = Create(name); Assert.NotEqual(0, receiver);
            var received = new byte[OverlayRenderer.Width * OverlayRenderer.Height * 4];
            async Task CheckPixels(double position)
            {
                await WaitUntil(() => application.Frames.Latest?.Timer.Position == position);
                var expected = application.Frames.Latest!.Bgra;
                // SDK receiver objects own a D3D context; all receive calls use a dedicated thread.
                var handle = receiver;
                var matched = await Task.Factory.StartNew(() =>
                {
                    for (var i = 0; i < 150; i++)
                    {
                        if (Receive(handle, received, OverlayRenderer.Width, OverlayRenderer.Height) != 0 && expected.AsSpan().SequenceEqual(received)) return true;
                        Thread.Sleep(30);
                    }
                    return false;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(matched, "Spout GPU readback must exactly match the shared renderer's premultiplied BGRA frame");
            }
            await CheckPixels(8.123);
            application.Command(e => e.Start()); await Task.Delay(250); application.Command(e => e.Pause());
            await CheckPixels(application.Engine.Snapshot().Position);
            application.Command(e => e.SetTime(45.678)); await CheckPixels(45.678);
            var saved = application.Engine.Snapshot();
            application.Command(e => e.Reset()); await CheckPixels(60);
            application.Command(e => e.Restore(saved)); await CheckPixels(45.678);
            application.Command(e => e.Start()); await Task.Delay(250); application.Command(e => e.Pause());
            Assert.True(application.Engine.Snapshot().Position < 45.678);
            await CheckPixels(application.Engine.Snapshot().Position);
            application.Command(e => e.Configure(TimerMode.Countdown, .05, null, false));
            application.Command(e => e.Start());
            await WaitUntil(() => application.Engine.Snapshot().Completed);
            await CheckPixels(0);
            application.Command(e => e.Configure(TimerMode.Countdown, .05, null, true));
            application.Command(e => e.Start());
            await WaitUntil(() => application.Engine.Snapshot().Position < -.1);
            application.Command(e => e.Pause());
            await CheckPixels(application.Engine.Snapshot().Position);
        }
        finally { if (receiver != 0) Destroy(receiver); Directory.Delete(directory, true); }
    }
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_receiver_create", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Create([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_receive", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Receive(nint handle, byte[] data, int width, int height);
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_receiver_destroy", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Destroy(nint handle);
}
