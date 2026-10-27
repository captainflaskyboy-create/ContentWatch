using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using ContentWatch.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ContentWatch.Desktop.Outputs;

public sealed class FrameHub
{
    private OverlayFrame? latest;
    public OverlayFrame? Latest => Volatile.Read(ref latest);
    public void Publish(OverlayFrame frame) => Volatile.Write(ref latest, frame);
}

public sealed class BrowserOutput(FrameHub frames) : IAsyncDisposable
{
    private WebApplication? app;
    public string Status { get; private set; } = "Browser server stopped";
    public string Url { get; private set; } = "";
    public async Task StartAsync(int port)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, port));
        app = builder.Build();
        app.UseWebSockets();
        var assembly = Assembly.GetExecutingAssembly();
        using var reader = new StreamReader(assembly.GetManifestResourceStream("ContentWatch.Desktop.Overlay.html")!);
        var html = await reader.ReadToEndAsync();
        app.MapGet("/overlay", () => Results.Content(html, "text/html"));
        app.MapGet("/health", () => Results.Json(new { frame = frames.Latest?.Sequence ?? 0, running = frames.Latest?.Timer.Running ?? false }));
        app.Map("/frames", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            // This output is read-only. No timer controls are exposed through HTTP.
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var ack = new byte[32];
            long sent = -1;
            try
            {
                while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var latest = frames.Latest;
                    if (latest is null || latest.Sequence == sent) { await Task.Delay(10, context.RequestAborted); continue; }
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await socket.SendAsync(latest.Png.AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
                    sent = latest.Sequence;
                    var response = await socket.ReceiveAsync(ack.AsMemory(), timeout.Token);
                    if (response.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException) { }
        });
        try { await app.StartAsync(); Url = $"http://127.0.0.1:{port}/overlay"; Status = "Browser source ready"; }
        catch (IOException ex) { Status = $"Port {port} unavailable: {ex.Message}"; await app.DisposeAsync(); app = null; throw; }
    }
    public async ValueTask DisposeAsync() { if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); app = null; } }
}
