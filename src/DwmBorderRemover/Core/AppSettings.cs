using System.Text.Json.Serialization;

namespace DwmBorderRemover.Core;

internal enum RuleMode
{
    Exclude,
    Include
}

internal enum CornerStyle
{
    SystemDefault,
    Rounded,
    RoundedSmall,
    Square
}

internal enum CompatibilityMode
{
    Efficient,
    Automatic,
    Aggressive
}

internal sealed class ProgramRule
{
    public string DisplayName { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }

    [JsonIgnore]
    public string MatchText => string.IsNullOrWhiteSpace(ExecutablePath)
        ? ExecutableName
        : ExecutablePath;

    public bool Matches(WindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(ExecutablePath) &&
            !string.IsNullOrWhiteSpace(window.ExecutablePath) &&
            string.Equals(
                NormalizePath(ExecutablePath),
                NormalizePath(window.ExecutablePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ExecutableName) &&
               string.Equals(
                   ExecutableName,
                   window.ExecutableName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }
}

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoStart { get; set; } = true;
    public RuleMode Mode { get; set; } = RuleMode.Exclude;
    public CornerStyle Corners { get; set; } = CornerStyle.Rounded;
    public CompatibilityMode Compatibility { get; set; } = CompatibilityMode.Automatic;
    public List<ProgramRule> Programs { get; set; } = [];

    public bool ShouldApply(WindowInfo window)
    {
        if (!Enabled)
        {
            return false;
        }

        bool listed = Programs.Any(rule => rule.Matches(window));
        return Mode == RuleMode.Exclude ? !listed : listed;
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Enabled = Enabled,
            AutoStart = AutoStart,
            Mode = Mode,
            Corners = Corners,
            Compatibility = Compatibility,
            Programs = Programs.Select(rule => new ProgramRule
            {
                DisplayName = rule.DisplayName,
                ExecutableName = rule.ExecutableName,
                ExecutablePath = rule.ExecutablePath
            }).ToList()
        };
    }
}
