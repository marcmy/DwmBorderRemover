using DwmBorderRemover.Core;
using DwmBorderRemover.Services;

namespace DwmBorderRemover;

internal static class Program
{
    private const string MutexName = @"Local\DwmBorderRemover-58E7C65D-4DF5-41CF-9BC3-4EC77F352A61";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string command = GetCommand(args);
        bool background = args.Any(argument =>
            string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));

        using Mutex mutex = new(initiallyOwned: true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            if (background && string.Equals(command, "show-options", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool sent = IpcServer.TrySend(command);
            if (sent && IsExitCommand(command))
            {
                try
                {
                    if (mutex.WaitOne(5000))
                    {
                        mutex.ReleaseMutex();
                    }
                }
                catch (AbandonedMutexException)
                {
                    // The primary process exited before releasing the mutex normally.
                }
            }

            return;
        }

        if (IsExitCommand(command))
        {
            BorderEngine.RestoreCurrentWindows();
            try
            {
                AutoStartManager.SetEnabled(false);
            }
            catch
            {
                // Uninstall restoration should still succeed if the Run key is inaccessible.
            }
            return;
        }

        WindowsFormsSynchronizationContext synchronizationContext = new();
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        SettingsStore settingsStore = new();
        AppSettings settings = settingsStore.Load();

        try
        {
            using TrayApplicationContext context = new(
                settingsStore,
                settings,
                synchronizationContext,
                showOptionsOnStart: !background);
            Application.Run(context);
        }
        catch (Exception exception)
        {
            AppLog.Write("Fatal error: " + exception);
            MessageBox.Show(
                "DWM Border Remover encountered an unexpected error.\n\n" +
                exception.Message + "\n\nA log was written to:\n" + AppLog.LogPath,
                "DWM Border Remover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string GetCommand(string[] args)
    {
        foreach (string argument in args)
        {
            if (string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return "show-options";
    }

    private static bool IsExitCommand(string command)
    {
        return string.Equals(command, "restore-and-exit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "--restore-and-exit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "--exit", StringComparison.OrdinalIgnoreCase);
    }
}
