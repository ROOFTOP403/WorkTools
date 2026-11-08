namespace EdgeShot;

public static class Geometry
{
    public static Rectangle NormalizeAndClamp(Point first, Point second, Size bounds)
    {
        var firstX = Math.Clamp(first.X, 0, Math.Max(0, bounds.Width));
        var firstY = Math.Clamp(first.Y, 0, Math.Max(0, bounds.Height));
        var secondX = Math.Clamp(second.X, 0, Math.Max(0, bounds.Width));
        var secondY = Math.Clamp(second.Y, 0, Math.Max(0, bounds.Height));

        var left = Math.Min(firstX, secondX);
        var top = Math.Min(firstY, secondY);
        return Rectangle.FromLTRB(left, top, Math.Max(firstX, secondX), Math.Max(firstY, secondY));
    }
}
