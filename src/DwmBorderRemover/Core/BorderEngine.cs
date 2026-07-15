using DwmBorderRemover.Interop;
using DwmBorderRemover.Services;

namespace DwmBorderRemover.Core;

internal sealed class BorderEngine : IDisposable
{
    private static readonly HashSet<string> AutomaticReapplyExecutables = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Discord.exe",
        "DiscordCanary.exe",
        "DiscordPTB.exe",
        "Vesktop.exe"
    };

    private readonly HashSet<IntPtr> _modifiedWindows = [];
    private readonly Dictionary<IntPtr, WindowInfo> _windowInfo = [];
    private readonly List<IntPtr> _hooks = [];
    private readonly System.Windows.Forms.Timer _safetyTimer;
    private readonly System.Windows.Forms.Timer _compatibilityTimer;
    private readonly NativeMethods.WinEventDelegate _winEventCallback;

    private AppSettings _settings;
    private IntPtr _previousForeground;
    private bool _disposed;

    internal BorderEngine(AppSettings settings)
    {
        _settings = settings.Clone();
        _winEventCallback = OnWinEvent;

        _safetyTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _safetyTimer.Tick += (_, _) => RefreshAllWindows();

        _compatibilityTimer = new System.Windows.Forms.Timer();
        _compatibilityTimer.Tick += (_, _) => ReapplyManagedWindows();
    }

    internal void Start()
    {
        InstallHooks();
        ConfigureTimers();
        RefreshAllWindows();
        _safetyTimer.Start();
    }

    internal void UpdateSettings(AppSettings settings)
    {
        _settings = settings.Clone();
        ConfigureTimers();
        RefreshAllWindows();
    }

    internal void RefreshAllWindows()
    {
        IReadOnlyList<WindowInfo> candidates = WindowCatalog.EnumerateCandidates();
        HashSet<IntPtr> currentHandles = candidates.Select(window => window.Handle).ToHashSet();

        foreach (WindowInfo window in candidates)
        {
            ApplyOrRestore(window);
        }

        foreach (IntPtr staleHandle in _modifiedWindows.Where(handle => !currentHandles.Contains(handle)).ToArray())
        {
            _modifiedWindows.Remove(staleHandle);
            _windowInfo.Remove(staleHandle);
        }
    }

    internal void ApplyOrRestore(WindowInfo window)
    {
        if (_settings.ShouldApply(window))
        {
            Apply(window);
        }
        else if (_modifiedWindows.Contains(window.Handle))
        {
            Restore(window.Handle);
        }
    }

    internal void RestoreAll()
    {
        foreach (IntPtr hWnd in _modifiedWindows.ToArray())
        {
            Restore(hWnd);
        }

        _modifiedWindows.Clear();
        _windowInfo.Clear();
    }

    internal static void RestoreCurrentWindows()
    {
        foreach (WindowInfo window in WindowCatalog.EnumerateCandidates())
        {
            RestoreDefaults(window.Handle);
        }
    }

    private void Apply(WindowInfo window)
    {
        if (!NativeMethods.IsWindow(window.Handle))
        {
            return;
        }

        uint borderColor = NativeMethods.DwmColorNone;
        int borderResult = NativeMethods.DwmSetWindowAttribute(
            window.Handle,
            NativeMethods.DwmwaBorderColor,
            ref borderColor,
            sizeof(uint));

        int cornerPreference = _settings.Corners switch
        {
            CornerStyle.Rounded => NativeMethods.DwmwcpRound,
            CornerStyle.RoundedSmall => NativeMethods.DwmwcpRoundSmall,
            CornerStyle.Square => NativeMethods.DwmwcpDoNotRound,
            _ => NativeMethods.DwmwcpDefault
        };

        _ = NativeMethods.DwmSetWindowAttribute(
            window.Handle,
            NativeMethods.DwmwaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));

        if (borderResult == 0)
        {
            _modifiedWindows.Add(window.Handle);
            _windowInfo[window.Handle] = window;
        }
    }

    private void Restore(IntPtr hWnd)
    {
        if (NativeMethods.IsWindow(hWnd))
        {
            RestoreDefaults(hWnd);
        }

        _modifiedWindows.Remove(hWnd);
        _windowInfo.Remove(hWnd);
    }

    private static void RestoreDefaults(IntPtr hWnd)
    {
        uint borderColor = NativeMethods.DwmColorDefault;
        _ = NativeMethods.DwmSetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaBorderColor,
            ref borderColor,
            sizeof(uint));

        int cornerPreference = NativeMethods.DwmwcpDefault;
        _ = NativeMethods.DwmSetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));
    }

    private void ReapplyManagedWindows()
    {
        foreach (IntPtr hWnd in _modifiedWindows.ToArray())
        {
            if (!NativeMethods.IsWindow(hWnd))
            {
                _modifiedWindows.Remove(hWnd);
                _windowInfo.Remove(hWnd);
                continue;
            }

            if (!_windowInfo.TryGetValue(hWnd, out WindowInfo? window) ||
                !WindowCatalog.TryGetWindowInfo(hWnd, out WindowInfo? refreshed) ||
                refreshed is null)
            {
                continue;
            }

            window = refreshed;
            _windowInfo[hWnd] = window;

            bool shouldReapply = _settings.Compatibility == CompatibilityMode.Aggressive ||
                                 (_settings.Compatibility == CompatibilityMode.Automatic &&
                                  AutomaticReapplyExecutables.Contains(window.ExecutableName));

            if (shouldReapply)
            {
                ApplyOrRestore(window);
            }
        }
    }

    private void ConfigureTimers()
    {
        _compatibilityTimer.Stop();

        switch (_settings.Compatibility)
        {
            case CompatibilityMode.Automatic:
                _compatibilityTimer.Interval = 1_500;
                _compatibilityTimer.Start();
                break;
            case CompatibilityMode.Aggressive:
                _compatibilityTimer.Interval = 750;
                _compatibilityTimer.Start();
                break;
        }
    }

    private void InstallHooks()
    {
        AddHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground);
        AddHook(NativeMethods.EventSystemMinimizeEnd, NativeMethods.EventSystemMinimizeEnd);
        AddHook(NativeMethods.EventSystemDesktopSwitch, NativeMethods.EventSystemDesktopSwitch);
        AddHook(NativeMethods.EventObjectCreate, NativeMethods.EventObjectShow);
        AddHook(NativeMethods.EventObjectUncloaked, NativeMethods.EventObjectUncloaked);
    }

    private void AddHook(uint eventMin, uint eventMax)
    {
        IntPtr hook = NativeMethods.SetWinEventHook(
            eventMin,
            eventMax,
            IntPtr.Zero,
            _winEventCallback,
            0,
            0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);

        if (hook != IntPtr.Zero)
        {
            _hooks.Add(hook);
        }
        else
        {
            AppLog.Write($"Failed to install WinEvent hook {eventMin:X}-{eventMax:X}.");
        }
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        if (_disposed)
        {
            return;
        }

        if (eventType == NativeMethods.EventSystemDesktopSwitch)
        {
            RefreshAllWindows();
            return;
        }

        if (eventType == NativeMethods.EventSystemForeground)
        {
            IntPtr previous = _previousForeground;
            _previousForeground = hWnd;
            ProcessHandle(previous);
            ProcessHandle(hWnd);
            return;
        }

        if ((eventType == NativeMethods.EventObjectCreate ||
             eventType == NativeMethods.EventObjectShow ||
             eventType == NativeMethods.EventObjectUncloaked ||
             eventType == NativeMethods.EventSystemMinimizeEnd) &&
            (idObject == NativeMethods.ObjidWindow || idObject == 0))
        {
            ProcessHandle(hWnd);
        }
    }

    private void ProcessHandle(IntPtr hWnd)
    {
        if (WindowCatalog.TryGetWindowInfo(hWnd, out WindowInfo? window) && window is not null)
        {
            ApplyOrRestore(window);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _safetyTimer.Stop();
        _compatibilityTimer.Stop();

        foreach (IntPtr hook in _hooks)
        {
            _ = NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _safetyTimer.Dispose();
        _compatibilityTimer.Dispose();
    }
}
