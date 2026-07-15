using System.Text.Json;
using System.Text.Json.Serialization;
using DwmBorderRemover.Core;

namespace DwmBorderRemover.Services;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DwmBorderRemover");

    internal string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    internal AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(SettingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            BackupBrokenSettings();
            return new AppSettings();
        }
    }

    internal void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        string temporaryPath = SettingsPath + ".tmp";

        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, true);
    }

    private void BackupBrokenSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string backupPath = SettingsPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Move(SettingsPath, backupPath, true);
            }
        }
        catch
        {
            // Keep startup resilient even if the settings directory is inaccessible.
        }
    }
}
