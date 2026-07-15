using Microsoft.Win32;

namespace DwmBorderRemover.Services;

internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DwmBorderRemover";

    internal static void SetEnabled(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to determine the application path.");
            key.SetValue(ValueName, $"\"{executable}\" --background", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
