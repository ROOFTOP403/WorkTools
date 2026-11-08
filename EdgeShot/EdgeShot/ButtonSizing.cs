namespace EdgeShot;

public static class ButtonSizing
{
    public const int QhdReferenceHeight = 1440;
    public const int QhdButtonSize = 36;
    public const int MinimumSize = 28;
    public const int MaximumSize = 54;

    public static int CalculateForScreenHeight(int screenHeight)
    {
        if (screenHeight <= 0)
        {
            return QhdButtonSize;
        }

        var scaled = (int)Math.Round(QhdButtonSize * (screenHeight / (double)QhdReferenceHeight));
        return Math.Clamp(scaled, MinimumSize, MaximumSize);
    }
}
