using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EdgeShot;

internal sealed class SelectionOverlayForm : Form
{
    private readonly Bitmap _desktopImage;
    private Point _selectionStart;
    private Point _selectionEnd;
    private bool _selecting;

    public SelectionOverlayForm(Bitmap desktopImage, Rectangle virtualScreen)
    {
        _desktopImage = desktopImage;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Bounds = virtualScreen;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        DoubleBuffered = true;
        AccessibleName = "캡처 영역 선택";

        MouseDown += OnOverlayMouseDown;
        MouseMove += OnOverlayMouseMove;
        MouseUp += OnOverlayMouseUp;
        KeyDown += OnOverlayKeyDown;
    }

    public Rectangle SelectedRectangle =>
        Geometry.NormalizeAndClamp(_selectionStart, _selectionEnd, ClientSize);

    public Bitmap CreateSelectionBitmap()
    {
        var rectangle = SelectedRectangle;
        if (rectangle.Width < 2 || rectangle.Height < 2)
        {
            throw new InvalidOperationException("선택 영역이 너무 작습니다.");
        }

        return _desktopImage.Clone(rectangle, PixelFormat.Format32bppPArgb);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExTopMost = 0x00000008;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow | wsExTopMost;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        EnsureVisibleAboveFullscreenWindow();
        BeginInvoke(EnsureVisibleAboveFullscreenWindow);
    }

    private void EnsureVisibleAboveFullscreenWindow()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        var positioned = NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndTopMost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove |
            NativeMethods.SwpNoSize |
            NativeMethods.SwpNoOwnerZOrder |
            NativeMethods.SwpShowWindow);
        var broughtToTop = NativeMethods.BringWindowToTop(Handle);
        Activate();
        var foregroundRequested = NativeMethods.SetForegroundWindow(Handle);
        Focus();

        DiagnosticLog.Write(
            $"Overlay activation: positioned={positioned}, broughtToTop={broughtToTop}, " +
            $"foregroundRequested={foregroundRequested}, isForeground={NativeMethods.GetForegroundWindow() == Handle}.");
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_desktopImage, 0, 0);

        using var shade = new SolidBrush(Color.FromArgb(105, 0, 0, 0));
        e.Graphics.FillRectangle(shade, ClientRectangle);

        var selection = SelectedRectangle;
        if (!_selecting || selection.Width <= 0 || selection.Height <= 0)
        {
            DrawHint(e.Graphics);
            return;
        }

        e.Graphics.DrawImage(
            _desktopImage,
            selection,
            selection,
            GraphicsUnit.Pixel);

        using var border = new Pen(Color.FromArgb(78, 132, 255), 2f)
        {
            Alignment = PenAlignment.Inset
        };
        e.Graphics.DrawRectangle(border, selection);
        DrawDimensions(e.Graphics, selection);
    }

    private static void DrawHint(Graphics graphics)
    {
        const string message = "드래그하여 캡처 영역 선택  ·  Esc 취소";
        using var font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
        var size = TextRenderer.MeasureText(message, font);
        var screen = Screen.FromPoint(Cursor.Position);
        var point = new Point(
            screen.Bounds.Left - SystemInformation.VirtualScreen.Left + (screen.Bounds.Width - size.Width) / 2,
            screen.Bounds.Top - SystemInformation.VirtualScreen.Top + 24);
        var background = new Rectangle(point.X - 12, point.Y - 7, size.Width + 24, size.Height + 14);
        using var brush = new SolidBrush(Color.FromArgb(210, 28, 30, 36));
        graphics.FillRectangle(brush, background);
        TextRenderer.DrawText(graphics, message, font, point, Color.White);
    }

    private static void DrawDimensions(Graphics graphics, Rectangle selection)
    {
        var text = $"{selection.Width} × {selection.Height}";
        using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        var size = TextRenderer.MeasureText(text, font);
        var x = selection.Left;
        var y = selection.Top - size.Height - 8;
        if (y < 4)
        {
            y = Math.Min(selection.Bottom + 5, SystemInformation.VirtualScreen.Height - size.Height - 4);
        }

        using var background = new SolidBrush(Color.FromArgb(220, 28, 30, 36));
        graphics.FillRectangle(background, x, y, size.Width + 10, size.Height + 4);
        TextRenderer.DrawText(graphics, text, font, new Point(x + 5, y + 2), Color.White);
    }

    private void OnOverlayMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _selectionStart = ClampPoint(e.Location);
        _selectionEnd = _selectionStart;
        _selecting = true;
        Capture = true;
        Invalidate();
    }

    private void OnOverlayMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selectionEnd = ClampPoint(e.Location);
        Invalidate();
    }

    private void OnOverlayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_selecting)
        {
            return;
        }

        Capture = false;
        _selectionEnd = ClampPoint(e.Location);
        if (SelectedRectangle.Width >= 2 && SelectedRectangle.Height >= 2)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _selecting = false;
            Invalidate();
        }
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    private Point ClampPoint(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, ClientSize.Width),
            Math.Clamp(point.Y, 0, ClientSize.Height));
    }
}
