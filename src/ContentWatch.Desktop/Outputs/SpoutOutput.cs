using System.Runtime.InteropServices;
using ContentWatch.Rendering;

namespace ContentWatch.Desktop.Outputs;

/// <summary>Used only by the rendering thread: creation, sends and release share a D3D context.</summary>
public sealed class SpoutOutput : IDisposable
{
    private nint sender;
    private string? activeName;
    private bool failed;
    public string Status { get; private set; } = OperatingSystem.IsWindows() ? "Spout starting" : "Spout2 requires Windows; use browser source";
    public void Send(OverlayFrame frame, bool enabled, string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!enabled) { Dispose(); activeName = null; failed = false; Status = "Spout disabled"; return; }
        if (activeName != name) { Dispose(); activeName = name; failed = false; }
        if (failed) return;
        try
        {
            if (sender == 0) sender = Create(name);
            if (sender == 0) throw new InvalidOperationException("DirectX 11 sender creation failed");
            if (SendImage(sender, frame.Bgra, OverlayRenderer.Width, OverlayRenderer.Height, OverlayRenderer.Width * 4) == 0)
                throw new InvalidOperationException("Spout SendImage failed; check GPU/driver");
            Status = $"Spout transmitting: {name}";
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or InvalidOperationException)
        { Dispose(); failed = true; Status = "Spout unavailable: " + ex.Message; }
    }
    public void Dispose() { if (sender != 0) { Destroy(sender); sender = 0; } }
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_create", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Create([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_send", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SendImage(nint sender, byte[] pixels, int width, int height, int pitch);
    [DllImport("ContentWatch.Spout", EntryPoint = "cw_destroy", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Destroy(nint sender);
}
