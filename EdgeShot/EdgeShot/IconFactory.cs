namespace EdgeShot;

public static class IconFactory
{
    public static Icon CreateTrayIcon(CaptureAccent accent = CaptureAccent.Teal)
    {
        var iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
        using var bitmap = CreateTrayBitmap(iconSize, accent);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public static Bitmap CreateTrayBitmap(int size, CaptureAccent accent = CaptureAccent.Teal)
    {
        if (size < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "트레이 아이콘은 16픽셀 이상이어야 합니다.");
        }

        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            CaptureButtonRenderer.DrawTraySilhouette(
                graphics,
                new Rectangle(0, 0, size, size),
                accent);
        }

        return bitmap;
    }
}
