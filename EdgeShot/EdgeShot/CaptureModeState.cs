namespace EdgeShot;

public static class CaptureModeState
{
    public static bool IsRegionSelected(CaptureMode mode) =>
        mode == CaptureMode.Region;

    public static bool IsFullScreenSelected(CaptureMode mode) =>
        mode is CaptureMode.Monitor or CaptureMode.AllMonitors;

    public static bool IsMonitorSelected(
        CaptureMode mode,
        string? selectedDeviceName,
        string candidateDeviceName) =>
        mode == CaptureMode.Monitor &&
        string.Equals(
            selectedDeviceName,
            candidateDeviceName,
            StringComparison.OrdinalIgnoreCase);
}
