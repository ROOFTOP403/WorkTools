namespace EdgeShot;

public static class DialogPositioning
{
    public static Point Center(Rectangle workingArea, Size dialogSize)
    {
        return new Point(
            workingArea.Left + Math.Max(0, (workingArea.Width - dialogSize.Width) / 2),
            workingArea.Top + Math.Max(0, (workingArea.Height - dialogSize.Height) / 2));
    }
}
