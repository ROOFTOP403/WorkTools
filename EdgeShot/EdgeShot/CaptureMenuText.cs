namespace EdgeShot;

public static class CaptureMenuText
{
    public static string FormatMonitor(
        int position,
        string deviceName,
        Rectangle bounds,
        bool isPrimary,
        bool hasFloatingButton)
    {
        var role = isPrimary ? "주 모니터" : $"모니터 {position}";
        var shortName = deviceName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
        var buttonMarker = hasFloatingButton ? " · 버튼 위치" : string.Empty;
        return $"{role} · {shortName} · {bounds.Width} × {bounds.Height}{buttonMarker}";
    }

    public static string FormatVirtualDesktop(Rectangle bounds)
    {
        return $"모든 모니터 · {bounds.Width} × {bounds.Height}";
    }
}
