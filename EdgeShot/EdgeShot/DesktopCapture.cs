using System.Drawing.Imaging;

namespace EdgeShot;

public static class DesktopCapture
{
    public const CopyPixelOperation CopyOperation = CopyPixelOperation.SourceCopy;

    public static Bitmap CaptureVirtualDesktop(Rectangle virtualScreen)
    {
        return CaptureBounds(virtualScreen);
    }

    public static Bitmap CaptureBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("캡처할 화면을 찾을 수 없습니다.");
        }

        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bounds.Size,
                CopyOperation);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
