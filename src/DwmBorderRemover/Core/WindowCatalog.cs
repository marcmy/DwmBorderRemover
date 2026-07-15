using System.Diagnostics;
using System.Text;
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
            if (TryGetWindowInfo(hWnd, out WindowInfo? info))
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

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processIdValue);
        if (processIdValue == 0 || processIdValue == Environment.ProcessId)
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
        StringBuilder buffer = new(256);
        return NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int length = Math.Clamp(NativeMethods.GetWindowTextLength(hWnd) + 1, 2, 4096);
        StringBuilder buffer = new(length);
        return NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }
}
