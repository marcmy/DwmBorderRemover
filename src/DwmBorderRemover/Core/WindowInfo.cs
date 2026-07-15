namespace DwmBorderRemover.Core;

internal sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    string ClassName,
    int ProcessId,
    string ProcessName,
    string ExecutableName,
    string? ExecutablePath)
{
    public string FriendlyName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                return Title;
            }

            if (!string.IsNullOrWhiteSpace(ProcessName))
            {
                return ProcessName;
            }

            return ExecutableName;
        }
    }
}
