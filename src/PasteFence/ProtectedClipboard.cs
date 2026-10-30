using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PasteFence;

public static class ProtectedClipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;

    public static string? TryGetText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : null;
            }
            catch (ExternalException)
            {
                Thread.Sleep(40);
            }
        }

        return null;
    }

    public static void SetProtectedText(string text)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                SetProtectedTextOnce(text);
                return;
            }
            catch (Exception ex) when (ex is ExternalException or Win32Exception)
            {
                lastError = ex;
                Thread.Sleep(40);
            }
        }

        throw new ExternalException("クリップボードを書き換えられませんでした。", lastError);
    }

    public static void Clear()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    if (!EmptyClipboard())
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    return;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(40);
        }
        throw new Win32Exception(Marshal.GetLastWin32Error(), "クリップボードを消去できませんでした。");
    }

    private static void SetProtectedTextOnce(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        IntPtr textHandle = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard())
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var bytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
            textHandle = AllocAndCopy(bytes);
            if (SetClipboardData(CfUnicodeText, textHandle) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            textHandle = IntPtr.Zero; // Windows owns it after SetClipboardData succeeds.

            SetDwordFormat("CanIncludeInClipboardHistory", 0);
            SetDwordFormat("CanUploadToCloudClipboard", 0);
            SetDwordFormat("ExcludeClipboardContentFromMonitorProcessing", 1);
        }
        finally
        {
            if (textHandle != IntPtr.Zero)
                GlobalFree(textHandle);
            CloseClipboard();
        }
    }

    private static void SetDwordFormat(string name, int value)
    {
        var format = RegisterClipboardFormat(name);
        if (format == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var bytes = BitConverter.GetBytes(value);
        var handle = AllocAndCopy(bytes);
        if (SetClipboardData(format, handle) == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static IntPtr AllocAndCopy(byte[] bytes)
    {
        var handle = GlobalAlloc(GmemMoveable | GmemZeroInit, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
            throw new OutOfMemoryException();

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        return handle;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
