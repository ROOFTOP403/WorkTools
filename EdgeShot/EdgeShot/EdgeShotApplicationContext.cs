using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EdgeShot;

internal sealed class EdgeShotApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly FloatingCaptureButton _floatingButton;
    private readonly System.Windows.Forms.Timer _captureDelayTimer;
    private readonly System.Windows.Forms.Timer? _startupActionTimer;
    private readonly ContextMenuStrip _floatingButtonMenu;
    private CaptureRequest? _pendingCaptureRequest;
    private bool _capturePending;
    private bool _isCapturing;
    private bool _disposed;

    public EdgeShotApplicationContext(bool showSettingsOnStart = false)
    {
        _settings = _settingsStore.Load();

        _floatingButton = new FloatingCaptureButton();
        _floatingButton.CaptureRequested += (_, _) =>
            RequestCapture(CreateCurrentCaptureRequest("floating-button"));
        _floatingButton.PlacementChanged += (_, _) => SaveFloatingButtonPlacement();
        _floatingButton.Place(_settings);
        _floatingButton.SetAccent(_settings.FloatingButtonAccent);
        NormalizeCaptureModeTarget();

        var trayMenu = CreateContextMenu("tray-menu");
        _floatingButtonMenu = CreateContextMenu("floating-button-menu");
        _floatingButton.ContextMenuStrip = _floatingButtonMenu;

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(_settings.FloatingButtonAccent),
            Text = CreateNotifyIconText(),
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                RequestCapture(CreateCurrentCaptureRequest("tray-icon"));
            }
        };

        _captureDelayTimer = new System.Windows.Forms.Timer { Interval = 180 };
        _captureDelayTimer.Tick += (_, _) => CaptureDelayElapsed();

        ApplyFloatingButtonVisibility();

        if (showSettingsOnStart)
        {
            _startupActionTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _startupActionTimer.Tick += (_, _) =>
            {
                _startupActionTimer.Stop();
                ShowSettings();
            };
            _startupActionTimer.Start();
        }
    }

    private ContextMenuStrip CreateContextMenu(string origin)
    {
        var menu = new ContextMenuStrip();
        var regionMenu = new ToolStripMenuItem("영역 캡처");
        regionMenu.Click += (_, _) => SetCaptureMode(CaptureMode.Region, null, origin);
        menu.Items.Add(regionMenu);

        var fullScreenMenu = new ToolStripMenuItem("전체 화면 캡처");
        fullScreenMenu.DropDownOpening += (_, _) =>
            PopulateFullScreenMenu(fullScreenMenu, origin);
        menu.Items.Add(fullScreenMenu);
        menu.Opening += (_, _) =>
        {
            NormalizeCaptureModeTarget();
            regionMenu.Checked = CaptureModeState.IsRegionSelected(_settings.CaptureMode);
            fullScreenMenu.Checked = CaptureModeState.IsFullScreenSelected(_settings.CaptureMode);
            PopulateFullScreenMenu(fullScreenMenu, origin);
        };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정...", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Exit());
        return menu;
    }

    private void PopulateFullScreenMenu(ToolStripMenuItem menu, string origin)
    {
        menu.DropDownItems.Clear();
        var buttonCenter = new Point(
            _floatingButton.Left + _floatingButton.Width / 2,
            _floatingButton.Top + _floatingButton.Height / 2);
        var buttonScreen = Screen.FromPoint(buttonCenter);
        var screens = Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.Bounds.Left)
            .ThenBy(screen => screen.Bounds.Top)
            .ToArray();

        for (var index = 0; index < screens.Length; index++)
        {
            var screen = screens[index];
            var hasFloatingButton = string.Equals(
                screen.DeviceName,
                buttonScreen.DeviceName,
                StringComparison.OrdinalIgnoreCase);
            var label = CaptureMenuText.FormatMonitor(
                index + 1,
                screen.DeviceName,
                screen.Bounds,
                screen.Primary,
                hasFloatingButton);
            var monitorItem = new ToolStripMenuItem(label)
            {
                Checked = CaptureModeState.IsMonitorSelected(
                    _settings.CaptureMode,
                    _settings.FullScreenMonitorDeviceName,
                    screen.DeviceName)
            };
            monitorItem.Click += (_, _) =>
                SetCaptureMode(CaptureMode.Monitor, screen.DeviceName, origin);
            menu.DropDownItems.Add(monitorItem);
        }

        if (screens.Length > 1)
        {
            menu.DropDownItems.Add(new ToolStripSeparator());
            var allMonitorsItem = new ToolStripMenuItem(
                CaptureMenuText.FormatVirtualDesktop(SystemInformation.VirtualScreen))
            {
                Checked = _settings.CaptureMode == CaptureMode.AllMonitors
            };
            allMonitorsItem.Click += (_, _) =>
                SetCaptureMode(CaptureMode.AllMonitors, null, origin);
            menu.DropDownItems.Add(allMonitorsItem);
        }
    }

    private void SetCaptureMode(CaptureMode mode, string? monitorDeviceName, string origin)
    {
        _settings.CaptureMode = mode;
        _settings.FullScreenMonitorDeviceName = mode == CaptureMode.Monitor
            ? monitorDeviceName
            : null;
        NormalizeCaptureModeTarget();
        DiagnosticLog.Write(
            $"Capture mode changed. origin={origin}, mode={_settings.CaptureMode}, " +
            $"monitor={_settings.FullScreenMonitorDeviceName ?? "-"}.");
        _notifyIcon.Text = CreateNotifyIconText();
        SaveSettings();
    }

    private void NormalizeCaptureModeTarget()
    {
        if (_settings.CaptureMode != CaptureMode.Monitor)
        {
            return;
        }

        var selectedScreenExists = Screen.AllScreens.Any(screen =>
            string.Equals(
                screen.DeviceName,
                _settings.FullScreenMonitorDeviceName,
                StringComparison.OrdinalIgnoreCase));
        if (selectedScreenExists)
        {
            return;
        }

        var fallback = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (fallback is null)
        {
            _settings.CaptureMode = CaptureMode.Region;
            _settings.FullScreenMonitorDeviceName = null;
            return;
        }

        _settings.FullScreenMonitorDeviceName = fallback.DeviceName;
    }

    private CaptureRequest CreateCurrentCaptureRequest(string origin)
    {
        NormalizeCaptureModeTarget();
        return _settings.CaptureMode switch
        {
            CaptureMode.Monitor => CaptureRequest.Monitor(
                origin,
                _settings.FullScreenMonitorDeviceName!),
            CaptureMode.AllMonitors => CaptureRequest.AllMonitors(origin),
            _ => CaptureRequest.Region(origin)
        };
    }

    private string CreateNotifyIconText()
    {
        return _settings.CaptureMode switch
        {
            CaptureMode.Monitor =>
                $"EdgeShot - 왼쪽 클릭: {ShortDeviceName(_settings.FullScreenMonitorDeviceName)} 전체 화면",
            CaptureMode.AllMonitors => "EdgeShot - 왼쪽 클릭: 모든 모니터",
            _ => "EdgeShot - 왼쪽 클릭: 영역 캡처"
        };
    }

    private static string ShortDeviceName(string? deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName)
            ? "모니터"
            : deviceName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
    }

    private void RequestCapture(CaptureRequest request)
    {
        if (_capturePending || _isCapturing)
        {
            DiagnosticLog.Write(
                $"Capture request ignored because another capture is active. origin={request.Origin}.");
            return;
        }

        DiagnosticLog.Write(
            $"Capture requested. origin={request.Origin}, kind={request.Kind}, " +
            $"monitor={request.MonitorDeviceName ?? "-"}, " +
            $"foreground=0x{NativeMethods.GetForegroundWindow():X}, " +
            $"screens={Screen.AllScreens.Length}, virtualScreen={SystemInformation.VirtualScreen}.");
        _pendingCaptureRequest = request;
        _capturePending = true;
        // 창을 즉시 숨기면 Windows가 포커스를 전체화면 게임으로 돌릴 수 있다.
        // 투명하게 유지해 캡처 이미지에서는 제외하면서 오버레이 전환 시점을 안정화한다.
        if (_floatingButton.Visible)
        {
            _floatingButton.SuppressForCapture();
        }

        _captureDelayTimer.Start();
    }

    private void CaptureDelayElapsed()
    {
        _captureDelayTimer.Stop();
        var request = _pendingCaptureRequest;
        _pendingCaptureRequest = null;
        _capturePending = false;
        if (request is null)
        {
            ApplyFloatingButtonVisibility();
            return;
        }

        DiagnosticLog.Write($"Capture delay elapsed; starting {request.Kind} capture.");
        Capture(request);
    }

    private void Capture(CaptureRequest request)
    {
        _isCapturing = true;
        var captureTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            try
            {
                NativeMethods.DwmFlush();
            }
            catch (DllNotFoundException)
            {
                // DWM이 없는 특수 환경에서도 일반 캡처는 계속한다.
            }

            if (request.Kind == CaptureMode.Region)
            {
                CaptureRegion(captureTimer);
            }
            else
            {
                CaptureFullScreen(request, captureTimer);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Capture failed: {ex}");
            _notifyIcon.ShowBalloonTip(3000, "EdgeShot 캡처 실패", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _isCapturing = false;
            ApplyFloatingButtonVisibility();
        }
    }

    private void CaptureRegion(System.Diagnostics.Stopwatch captureTimer)
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        using var desktopImage = DesktopCapture.CaptureVirtualDesktop(virtualScreen);
        DiagnosticLog.Write(
            $"Desktop copy completed in {captureTimer.ElapsedMilliseconds} ms. " +
            $"size={desktopImage.Width}x{desktopImage.Height}.");
        using var overlay = new SelectionOverlayForm(desktopImage, virtualScreen);
        var overlayResult = overlay.ShowDialog();
        DiagnosticLog.Write($"Overlay closed. result={overlayResult}.");
        if (overlayResult != DialogResult.OK)
        {
            return;
        }

        using var selection = overlay.CreateSelectionBitmap();
        DeliverCapture(selection, "영역 캡처");
    }

    private void CaptureFullScreen(
        CaptureRequest request,
        System.Diagnostics.Stopwatch captureTimer)
    {
        Rectangle bounds;
        string description;
        if (request.Kind == CaptureMode.AllMonitors)
        {
            bounds = SystemInformation.VirtualScreen;
            description = "모든 모니터 전체 화면";
        }
        else
        {
            var screen = Screen.AllScreens.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.DeviceName,
                    request.MonitorDeviceName,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    "선택한 모니터를 찾을 수 없습니다. 디스플레이 연결 상태를 확인해 주세요.");
            bounds = screen.Bounds;
            var shortName = screen.DeviceName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
            description = $"{shortName} 전체 화면";
        }

        using var image = DesktopCapture.CaptureBounds(bounds);
        DiagnosticLog.Write(
            $"Full-screen copy completed in {captureTimer.ElapsedMilliseconds} ms. " +
            $"bounds={bounds}, size={image.Width}x{image.Height}.");
        DeliverCapture(image, description);
    }

    private void DeliverCapture(Image image, string description)
    {
        CopyToClipboardWithRetry(image);
        if (!_settings.SaveToDesktop)
        {
            return;
        }

        var savedPath = PngFileWriter.Save(
            image,
            PngSaveLocation.Resolve(_settings.PngSaveDirectory));
        _notifyIcon.ShowBalloonTip(
            1200,
            "EdgeShot",
            $"{description}를 클립보드에 복사하고 {Path.GetFileName(savedPath)}로 저장했습니다.",
            ToolTipIcon.Info);
    }

    private static void CopyToClipboardWithRetry(Image image)
    {
        ExternalException? lastException = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (ExternalException ex)
            {
                lastException = ex;
                Thread.Sleep(35);
            }
        }

        throw new ExternalException("클립보드가 다른 프로그램에서 사용 중입니다. 잠시 후 다시 시도해 주세요.", lastException);
    }

    private void ShowSettings()
    {
        bool actualAutoStart;
        try
        {
            actualAutoStart = AutoStartManager.IsEnabled();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            actualAutoStart = _settings.StartWithWindows;
        }

        using var form = new SettingsForm(_settings, actualAutoStart);
        if (DialogPlacement.ShowDialog(form, _floatingButton) != DialogResult.OK)
        {
            return;
        }

        if (form.StartWithWindows != actualAutoStart)
        {
            try
            {
                AutoStartManager.SetEnabled(form.StartWithWindows);
                _settings.StartWithWindows = form.StartWithWindows;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
            {
                DialogPlacement.ShowMessage(
                    $"자동 시작 설정을 변경하지 못했습니다.\n\n{ex.Message}",
                    "EdgeShot",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    _floatingButton);
            }
        }
        else
        {
            _settings.StartWithWindows = actualAutoStart;
        }

        _settings.SaveToDesktop = form.SaveToDesktop;
        _settings.PngSaveDirectory = form.PngSaveDirectory;
        _settings.ShowFloatingButton = form.ShowFloatingButton;
        var accentChanged = _settings.FloatingButtonAccent != form.FloatingButtonAccent;
        _settings.FloatingButtonAccent = form.FloatingButtonAccent;
        _floatingButton.SetAccent(_settings.FloatingButtonAccent);
        if (accentChanged)
        {
            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = IconFactory.CreateTrayIcon(_settings.FloatingButtonAccent);
            oldIcon?.Dispose();
        }

        SaveSettings();
        ApplyFloatingButtonVisibility();
    }

    private void ApplyFloatingButtonVisibility()
    {
        _floatingButton.RestoreAppearance();
        if (_settings.ShowFloatingButton && !_isCapturing && !_capturePending)
        {
            _floatingButton.Show();
        }
        else
        {
            _floatingButton.Hide();
        }
    }

    private void SaveFloatingButtonPlacement()
    {
        _settings.FloatingButtonPlacement = _floatingButton.GetPlacement();
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _notifyIcon.ShowBalloonTip(2500, "EdgeShot 설정 저장 실패", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void Exit()
    {
        SaveFloatingButtonPlacement();
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _startupActionTimer?.Dispose();
            _captureDelayTimer.Dispose();
            _floatingButtonMenu.Dispose();
            _floatingButton.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record CaptureRequest(
        CaptureMode Kind,
        string Origin,
        string? MonitorDeviceName)
    {
        public static CaptureRequest Region(string origin) =>
            new(CaptureMode.Region, origin, null);

        public static CaptureRequest Monitor(string origin, string monitorDeviceName) =>
            new(CaptureMode.Monitor, origin, monitorDeviceName);

        public static CaptureRequest AllMonitors(string origin) =>
            new(CaptureMode.AllMonitors, origin, null);
    }
}
