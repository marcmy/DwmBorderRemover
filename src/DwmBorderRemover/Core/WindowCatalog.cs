using System.Diagnostics;
using DwmBorderRemover.Interop;

namespace DwmBorderRemover.Core;

internal static class WindowCatalog
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "MultitaskingViewFrame"
    };

    internal static IReadOnlyList<WindowInfo> EnumerateCandidates()
    {
        List<WindowInfo> windows = [];

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (TryGetWindowInfo(hWnd, out WindowInfo? info) && info is not null)
            {
                windows.Add(info);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    internal static bool TryGetWindowInfo(IntPtr hWnd, out WindowInfo? info)
    {
        info = null;

        if (hWnd == IntPtr.Zero ||
            !NativeMethods.IsWindow(hWnd) ||
            !NativeMethods.IsWindowVisible(hWnd) ||
            NativeMethods.GetAncestor(hWnd, NativeMethods.GaRoot) != hWnd ||
            IsCloaked(hWnd))
        {
            return false;
        }

        string className = GetClassName(hWnd);
        if (ShellClasses.Contains(className))
        {
            return false;
        }

        long style = NativeMethods.GetWindowStyle(hWnd);
        if ((style & (NativeMethods.WsCaption | NativeMethods.WsThickFrame)) == 0)
        {
            return false;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out uint processIdValue);
        if (threadId == 0 || processIdValue == 0 || processIdValue == Environment.ProcessId)
        {
            return false;
        }

        int processId = unchecked((int)processIdValue);
        string processName = string.Empty;
        string executableName = string.Empty;
        string? executablePath = null;

        try
        {
            using Process process = Process.GetProcessById(processId);
            processName = process.ProcessName;
            executableName = process.ProcessName + ".exe";

            try
            {
                executablePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    executableName = Path.GetFileName(executablePath);
                }
            }
            catch
            {
                // Process path access can fail for elevated or protected apps.
            }
        }
        catch
        {
            return false;
        }

        info = new WindowInfo(
            hWnd,
            GetTitle(hWnd),
            className,
            processId,
            processName,
            executableName,
            executablePath);

        return true;
    }

    internal static WindowInfo? GetWindowUnderCursor()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.Point point))
        {
            return null;
        }

        IntPtr hWnd = NativeMethods.WindowFromPoint(point);
        hWnd = NativeMethods.GetAncestor(hWnd, NativeMethods.GaRoot);

        return TryGetWindowInfo(hWnd, out WindowInfo? info) ? info : null;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        int result = NativeMethods.DwmGetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaCloaked,
            out int cloaked,
            sizeof(int));

        return result == 0 && cloaked != 0;
    }

    private static string GetClassName(IntPtr hWnd)
    {
        char[] buffer = new char[256];
        int length = NativeMethods.GetClassName(hWnd, buffer, buffer.Length);
        return length > 0
            ? new string(buffer, 0, length)
            : string.Empty;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int capacity = Math.Clamp(NativeMethods.GetWindowTextLength(hWnd) + 1, 2, 4096);
        char[] buffer = new char[capacity];
        int length = NativeMethods.GetWindowText(hWnd, buffer, buffer.Length);
        return length > 0
            ? new string(buffer, 0, length)
            : string.Empty;
    }
}
