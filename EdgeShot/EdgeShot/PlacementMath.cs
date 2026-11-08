namespace EdgeShot;

public static class PlacementMath
{
    private const int EdgeMargin = 12;

    public static Point Restore(Rectangle workingArea, Size windowSize, ButtonPlacement placement)
    {
        var usableWidth = Math.Max(0, workingArea.Width - windowSize.Width);
        var usableHeight = Math.Max(0, workingArea.Height - windowSize.Height);

        var relativeX = double.IsFinite(placement.RelativeX) ? Math.Clamp(placement.RelativeX, 0, 1) : 1;
        var relativeY = double.IsFinite(placement.RelativeY) ? Math.Clamp(placement.RelativeY, 0, 1) : 0.5;

        var x = workingArea.Left + (int)Math.Round(usableWidth * relativeX);
        var y = workingArea.Top + (int)Math.Round(usableHeight * relativeY);
        return Clamp(workingArea, windowSize, new Point(x, y));
    }

    public static Point Default(Rectangle workingArea, Size windowSize)
    {
        return new Point(
            Math.Max(workingArea.Left, workingArea.Right - windowSize.Width - EdgeMargin),
            workingArea.Top + Math.Max(0, (workingArea.Height - windowSize.Height) / 2));
    }

    public static ButtonPlacement Save(Rectangle workingArea, Size windowSize, Point location, string deviceName)
    {
        var clamped = Clamp(workingArea, windowSize, location);
        var usableWidth = Math.Max(0, workingArea.Width - windowSize.Width);
        var usableHeight = Math.Max(0, workingArea.Height - windowSize.Height);

        return new ButtonPlacement
        {
            MonitorDeviceName = deviceName,
            RelativeX = usableWidth == 0 ? 0 : (clamped.X - workingArea.Left) / (double)usableWidth,
            RelativeY = usableHeight == 0 ? 0 : (clamped.Y - workingArea.Top) / (double)usableHeight
        };
    }

    public static Point Clamp(Rectangle workingArea, Size windowSize, Point location)
    {
        var maxX = Math.Max(workingArea.Left, workingArea.Right - windowSize.Width);
        var maxY = Math.Max(workingArea.Top, workingArea.Bottom - windowSize.Height);
        return new Point(
            Math.Clamp(location.X, workingArea.Left, maxX),
            Math.Clamp(location.Y, workingArea.Top, maxY));
    }
}
