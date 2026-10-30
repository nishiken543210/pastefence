using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PasteFence;

public sealed class ClipboardMonitorWindow : NativeWindow, IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private bool _disposed;

    public event EventHandler? ClipboardChanged;

    public ClipboardMonitorWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "PasteFenceClipboardMonitor",
            Parent = new IntPtr(-3) // HWND_MESSAGE
        });

        if (!AddClipboardFormatListener(Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボード監視を開始できませんでした。");
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmClipboardUpdate)
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr window);
}
