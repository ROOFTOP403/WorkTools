using System.Drawing.Imaging;

namespace EdgeShot;

internal sealed class FloatingCaptureButton : Form
{
    private const int DragThreshold = 4;
    private Point _mouseDownScreen;
    private Point _windowDownLocation;
    private bool _dragging;
    private bool _hovered;
    private Screen? _currentScreen;
    private CaptureAccent _accent = CaptureAccent.Teal;
    private bool _suppressed;

    public event EventHandler? CaptureRequested;

    public event EventHandler? PlacementChanged;

    public FloatingCaptureButton()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        AccessibleName = "영역 캡처";

        MouseDown += OnButtonMouseDown;
        MouseMove += OnButtonMouseMove;
        MouseUp += OnButtonMouseUp;
        MouseEnter += (_, _) => SetHovered(true);
        MouseLeave += (_, _) => SetHovered(false);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExLayered = 0x00080000;
            var parameters = base.CreateParams;
            // 처음 표시할 때는 ShowWithoutActivation으로 포커스를 뺏지 않는다.
            // 사용자가 직접 클릭한 뒤에는 캡처 오버레이를 전경으로 올릴 수 있어야 한다.
            parameters.ExStyle |= wsExToolWindow | wsExLayered;
            return parameters;
        }
    }

    public void Place(AppSettings settings)
    {
        var screens = Screen.AllScreens;
        var placement = settings.FloatingButtonPlacement;
        var screen = screens.FirstOrDefault(item =>
            string.Equals(item.DeviceName, placement.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));

        var hasSavedMonitor = screen is not null;
        screen ??= screens.FirstOrDefault(item => !item.Primary) ?? Screen.PrimaryScreen ?? screens[0];
        ApplySizeForScreen(screen);
        Location = hasSavedMonitor
            ? PlacementMath.Restore(screen.WorkingArea, Size, placement)
            : PlacementMath.Default(screen.WorkingArea, Size);
    }

    public ButtonPlacement GetPlacement()
    {
        var center = new Point(Left + Width / 2, Top + Height / 2);
        var screen = Screen.FromPoint(center);
        return PlacementMath.Save(screen.WorkingArea, Size, Location, screen.DeviceName);
    }

    public void SetAccent(CaptureAccent accent)
    {
        _accent = Enum.IsDefined(accent) ? accent : CaptureAccent.Teal;
        UpdateLayeredAppearance();
    }

    public void SuppressForCapture()
    {
        _suppressed = true;
        UpdateLayeredAppearance();
    }

    public void RestoreAppearance()
    {
        _hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
        _suppressed = false;
        UpdateLayeredAppearance();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UpdateLayeredAppearance();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // UpdateLayeredWindow가 per-pixel alpha 화면을 직접 제공한다.
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateLayeredAppearance();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var scale = Math.Min(Width, Height) / 36f;
        using var path = CaptureButtonRenderer.CreateRoundedRectangle(
            new RectangleF(0, 0, Width, Height),
            9f * scale);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
        UpdateLayeredAppearance();
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        UpdateLayeredAppearance();
    }

    private void UpdateLayeredAppearance()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
        {
            return;
        }

        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            if (!_suppressed)
            {
                CaptureButtonRenderer.Draw(graphics, ClientRectangle, _accent, _hovered);
            }
        }

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        var bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
        var previousBitmap = NativeMethods.SelectObject(memoryDc, bitmapHandle);
        try
        {
            var destination = new NativeMethods.NativePoint(Left, Top);
            var source = new NativeMethods.NativePoint(0, 0);
            var size = new NativeMethods.NativeSize(Width, Height);
            var blend = new NativeMethods.BlendFunction(
                NativeMethods.AcSrcOver,
                0,
                255,
                NativeMethods.AcSrcAlpha);
            if (!NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    NativeMethods.UlwAlpha))
            {
                DiagnosticLog.Write(
                    $"UpdateLayeredWindow failed. win32={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            NativeMethods.SelectObject(memoryDc, previousBitmap);
            NativeMethods.DeleteObject(bitmapHandle);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void ApplySizeForScreen(Screen screen)
    {
        _currentScreen = screen;
        var size = ButtonSizing.CalculateForScreenHeight(screen.Bounds.Height);
        Size = new Size(size, size);
    }

    private void OnButtonMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _mouseDownScreen = Cursor.Position;
        _windowDownLocation = Location;
        _dragging = false;
        Capture = true;
    }

    private void OnButtonMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Capture)
        {
            return;
        }

        var cursor = Cursor.Position;
        var dx = cursor.X - _mouseDownScreen.X;
        var dy = cursor.Y - _mouseDownScreen.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < DragThreshold)
        {
            return;
        }

        _dragging = true;
        Location = new Point(_windowDownLocation.X + dx, _windowDownLocation.Y + dy);

        var screen = Screen.FromPoint(cursor);
        if (_currentScreen?.DeviceName != screen.DeviceName)
        {
            var oldCenter = new Point(Left + Width / 2, Top + Height / 2);
            ApplySizeForScreen(screen);
            Location = new Point(oldCenter.X - Width / 2, oldCenter.Y - Height / 2);
            _windowDownLocation = new Point(Location.X - dx, Location.Y - dy);
        }
    }

    private void OnButtonMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Capture = false;
        if (_dragging)
        {
            var screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Location = PlacementMath.Clamp(screen.WorkingArea, Size, Location);
            PlacementChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
