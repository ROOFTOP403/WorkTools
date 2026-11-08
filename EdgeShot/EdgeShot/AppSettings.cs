namespace EdgeShot;

public enum CaptureMode
{
    Region,
    Monitor,
    AllMonitors
}

public sealed class AppSettings
{
    public bool SaveToDesktop { get; set; }

    // 비어 있으면 이전 버전과 동일하게 바탕 화면을 사용한다.
    public string? PngSaveDirectory { get; set; }

    // 기본값을 false로 유지한다. 앱이 사용자의 선택 없이 자동 시작을 켜지 않는다.
    public bool StartWithWindows { get; set; }

    public bool ShowFloatingButton { get; set; } = true;

    public CaptureAccent FloatingButtonAccent { get; set; } = CaptureAccent.Teal;

    public CaptureMode CaptureMode { get; set; } = CaptureMode.Region;

    public string? FullScreenMonitorDeviceName { get; set; }

    public ButtonPlacement FloatingButtonPlacement { get; set; } = new();
}

public sealed class ButtonPlacement
{
    public string? MonitorDeviceName { get; set; }

    public double RelativeX { get; set; } = 1.0;

    public double RelativeY { get; set; } = 0.5;
}
