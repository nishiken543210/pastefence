using System.Runtime.InteropServices;

namespace PasteFence.Tests;

// 安全版に付けている抑制形式が本当にクリップボードへ登録されたかは、
// 製品コード（ProtectedClipboard）の戻り値では分からないので、試験側から Win32 を直接呼んで確かめる。
// src/ を変更しない方針のため、宣言は ProtectedClipboard と重複させている。
internal static class ClipboardInterop
{
    public const uint CfUnicodeText = 13;

    // この環境でクリップボードを扱えるかどうかを、製品コードを通さずに調べる。
    // 製品コードで調べると、製品側の不具合を「環境の都合」として飛ばしてしまい、偽の緑になる。
    public static bool IsClipboardUsable()
    {
        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    CloseClipboard();
                    return true;
                }
                Thread.Sleep(40);
            }
        }
        catch (Exception)
        {
            // user32.dll を呼べない環境も「使えない」として扱う
        }

        return false;
    }

    public static bool IsFormatAvailable(string name)
    {
        var format = RegisterClipboardFormat(name);
        return format != 0 && IsClipboardFormatAvailable(format);
    }

    // 登録済み形式の中身を 4 バイト整数として読む。形式が載っていなければ null。
    public static int? ReadDwordFormat(string name)
    {
        var bytes = ReadFormat(name);
        return bytes is { Length: >= 4 } ? BitConverter.ToInt32(bytes, 0) : null;
    }

    public static byte[]? ReadFormat(string name)
    {
        var format = RegisterClipboardFormat(name);
        return format == 0 ? null : ReadFormat(format);
    }

    public static byte[]? ReadFormat(uint format)
    {
        if (!IsClipboardFormatAvailable(format))
            return null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(40);
                continue;
            }

            try
            {
                // GetClipboardData が返すハンドルはクリップボードの持ち物なので解放しない
                var handle = GetClipboardData(format);
                if (handle == IntPtr.Zero)
                    return null;

                var size = (int)GlobalSize(handle);
                if (size <= 0)
                    return [];

                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                    return null;

                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(pointer, bytes, 0, size);
                    return bytes;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint GlobalSize(IntPtr memory);
}
