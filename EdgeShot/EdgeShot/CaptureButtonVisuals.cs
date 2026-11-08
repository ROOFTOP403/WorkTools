using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EdgeShot;

public enum CaptureAccent
{
    Teal,
    Coral
}

public readonly record struct CaptureAccentColors(
    Color IdleIcon,
    Color IdleBackground,
    Color IdleBorder,
    Color HoverBackground);

public static class CaptureAccentPalette
{
    public static CaptureAccentColors Get(CaptureAccent accent)
    {
        return accent switch
        {
            CaptureAccent.Coral => new CaptureAccentColors(
                Color.FromArgb(218, 70, 68),
                Color.FromArgb(48, 244, 118, 103),
                Color.FromArgb(245, 83, 72),
                Color.FromArgb(211, 67, 63)),
            _ => new CaptureAccentColors(
                Color.FromArgb(0, 142, 144),
                Color.FromArgb(48, 79, 218, 222),
                Color.FromArgb(0, 157, 145),
                Color.FromArgb(0, 139, 141))
        };
    }
}

public static class CaptureButtonRenderer
{
    private const int Supersampling = 4;

    public static void Draw(Graphics graphics, Rectangle bounds, CaptureAccent accent, bool hovered)
    {
        DrawSupersampled(graphics, bounds, accent, hovered, includeBackground: true);
    }

    public static void DrawCamera(Graphics graphics, Rectangle bounds, CaptureAccent accent)
    {
        DrawSupersampled(graphics, bounds, accent, hovered: false, includeBackground: false);
    }

    public static void DrawTraySilhouette(Graphics graphics, Rectangle bounds, CaptureAccent accent)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var rendered = new Bitmap(
            bounds.Width * Supersampling,
            bounds.Height * Supersampling,
            PixelFormat.Format32bppPArgb);
        using (var renderedGraphics = Graphics.FromImage(rendered))
        {
            renderedGraphics.Clear(Color.Transparent);
            DrawTraySilhouetteCore(
                renderedGraphics,
                new RectangleF(0, 0, rendered.Width, rendered.Height),
                accent);
        }

        var previousCompositing = graphics.CompositingMode;
        using var downsampled = Downsample(rendered, bounds.Size, normalizePerimeter: false);
        try
        {
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.DrawImageUnscaled(downsampled, bounds.Location);
        }
        finally
        {
            graphics.CompositingMode = previousCompositing;
        }
    }

    private static void DrawSupersampled(
        Graphics graphics,
        Rectangle bounds,
        CaptureAccent accent,
        bool hovered,
        bool includeBackground)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var rendered = new Bitmap(
            bounds.Width * Supersampling,
            bounds.Height * Supersampling,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var renderedGraphics = Graphics.FromImage(rendered))
        {
            renderedGraphics.Clear(Color.Transparent);
            DrawCore(
                renderedGraphics,
                new RectangleF(0, 0, rendered.Width, rendered.Height),
                accent,
                hovered,
                includeBackground);
        }

        var previousCompositing = graphics.CompositingMode;
        using var downsampled = Downsample(rendered, bounds.Size, includeBackground);
        try
        {
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.DrawImageUnscaled(downsampled, bounds.Location);
        }
        finally
        {
            graphics.CompositingMode = previousCompositing;
        }
    }

    private static void DrawCore(
        Graphics graphics,
        RectangleF bounds,
        CaptureAccent accent,
        bool hovered,
        bool includeBackground)
    {
        var colors = CaptureAccentPalette.Get(accent);
        const float designSize = 36f;
        const float cameraOffset = 2f;
        var scale = Math.Min(bounds.Width, bounds.Height) / designSize;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingMode = CompositingMode.SourceOver;

        if (includeBackground)
        {
            var edgeInset = 0.5f * Supersampling;
            var borderThickness = 1f * Supersampling;
            var outerRectangle = new RectangleF(
                bounds.Left + edgeInset,
                bounds.Top + edgeInset,
                bounds.Width - edgeInset * 2,
                bounds.Height - edgeInset * 2);
            var outerRadius = 9f * scale;
            using var outerPath = CreateRoundedRectangle(outerRectangle, outerRadius);
            using var background = new SolidBrush(
                hovered ? colors.HoverBackground : colors.IdleBackground);
            graphics.FillPath(background, outerPath);

            if (!hovered)
            {
                var innerRectangle = RectangleF.Inflate(
                    outerRectangle,
                    -borderThickness,
                    -borderThickness);
                using var innerPath = CreateRoundedRectangle(
                    innerRectangle,
                    Math.Max(0, outerRadius - borderThickness));
                using var borderRing = new GraphicsPath(FillMode.Alternate);
                borderRing.AddPath(outerPath, connect: false);
                borderRing.AddPath(innerPath, connect: false);
                using var border = new SolidBrush(colors.IdleBorder);
                graphics.FillPath(border, borderRing);
            }
        }

        var iconColor = hovered ? Color.White : colors.IdleIcon;
        using var iconPen = new Pen(iconColor, Math.Max(1.5f, 2.1f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        using var cameraOutline = CreateCameraOutline(bounds, scale, cameraOffset);
        graphics.DrawPath(iconPen, cameraOutline);
        graphics.DrawEllipse(
            iconPen,
            bounds.Left + (12.25f + cameraOffset) * scale,
            bounds.Top + (13.25f + cameraOffset) * scale,
            7.5f * scale,
            7.5f * scale);
    }

    private static void DrawTraySilhouetteCore(
        Graphics graphics,
        RectangleF bounds,
        CaptureAccent accent)
    {
        const float designSize = 16f;
        var scale = Math.Min(bounds.Width, bounds.Height) / designSize;
        PointF Point(float x, float y) =>
            new(bounds.Left + x * scale, bounds.Top + y * scale);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingMode = CompositingMode.SourceOver;

        using var silhouette = new GraphicsPath(FillMode.Alternate);
        silhouette.StartFigure();
        silhouette.AddLine(Point(3f, 3.75f), Point(4.6f, 3.75f));
        silhouette.AddLine(Point(4.6f, 3.75f), Point(5.5f, 2.3f));
        silhouette.AddBezier(
            Point(5.5f, 2.3f),
            Point(5.75f, 1.9f),
            Point(6.2f, 1.6f),
            Point(6.7f, 1.6f));
        silhouette.AddLine(Point(6.7f, 1.6f), Point(9.3f, 1.6f));
        silhouette.AddBezier(
            Point(9.3f, 1.6f),
            Point(9.8f, 1.6f),
            Point(10.25f, 1.9f),
            Point(10.5f, 2.3f));
        silhouette.AddLine(Point(10.5f, 2.3f), Point(11.4f, 3.75f));
        silhouette.AddLine(Point(11.4f, 3.75f), Point(13f, 3.75f));
        silhouette.AddBezier(
            Point(13f, 3.75f),
            Point(14.25f, 3.75f),
            Point(15.1f, 4.7f),
            Point(15.1f, 5.9f));
        silhouette.AddLine(Point(15.1f, 5.9f), Point(15.1f, 12f));
        silhouette.AddBezier(
            Point(15.1f, 12f),
            Point(15.1f, 13.4f),
            Point(14.1f, 14.35f),
            Point(12.8f, 14.35f));
        silhouette.AddLine(Point(12.8f, 14.35f), Point(3.2f, 14.35f));
        silhouette.AddBezier(
            Point(3.2f, 14.35f),
            Point(1.9f, 14.35f),
            Point(0.9f, 13.4f),
            Point(0.9f, 12f));
        silhouette.AddLine(Point(0.9f, 12f), Point(0.9f, 5.9f));
        silhouette.AddBezier(
            Point(0.9f, 5.9f),
            Point(0.9f, 4.7f),
            Point(1.75f, 3.75f),
            Point(3f, 3.75f));
        silhouette.CloseFigure();
        silhouette.AddEllipse(
            bounds.Left + 5.3f * scale,
            bounds.Top + 6.1f * scale,
            5.4f * scale,
            5.4f * scale);

        using var fill = new SolidBrush(CaptureAccentPalette.Get(accent).IdleIcon);
        graphics.FillPath(fill, silhouette);
    }

    internal static GraphicsPath CreateRoundedRectangle(RectangleF rectangle, float radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Bitmap Downsample(Bitmap source, Size targetSize, bool normalizePerimeter)
    {
        var target = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppPArgb);
        var sourceData = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        var targetData = target.LockBits(
            new Rectangle(0, 0, target.Width, target.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var sourceStride = Math.Abs(sourceData.Stride);
            var targetStride = Math.Abs(targetData.Stride);
            var sourceBytes = new byte[sourceStride * source.Height];
            var targetBytes = new byte[targetStride * target.Height];
            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);

            for (var targetY = 0; targetY < target.Height; targetY++)
            {
                for (var targetX = 0; targetX < target.Width; targetX++)
                {
                    var blue = 0;
                    var green = 0;
                    var red = 0;
                    var alpha = 0;
                    for (var sampleY = 0; sampleY < Supersampling; sampleY++)
                    {
                        var sourceY = targetY * Supersampling + sampleY;
                        var sourceRow = sourceData.Stride >= 0
                            ? sourceY * sourceStride
                            : (source.Height - 1 - sourceY) * sourceStride;
                        for (var sampleX = 0; sampleX < Supersampling; sampleX++)
                        {
                            var sourceX = targetX * Supersampling + sampleX;
                            var sourceIndex = sourceRow + sourceX * 4;
                            blue += sourceBytes[sourceIndex];
                            green += sourceBytes[sourceIndex + 1];
                            red += sourceBytes[sourceIndex + 2];
                            alpha += sourceBytes[sourceIndex + 3];
                        }
                    }

                    var targetRow = targetData.Stride >= 0
                        ? targetY * targetStride
                        : (target.Height - 1 - targetY) * targetStride;
                    var targetIndex = targetRow + targetX * 4;
                    targetBytes[targetIndex] = (byte)((blue + 8) / 16);
                    targetBytes[targetIndex + 1] = (byte)((green + 8) / 16);
                    targetBytes[targetIndex + 2] = (byte)((red + 8) / 16);
                    targetBytes[targetIndex + 3] = (byte)((alpha + 8) / 16);
                }
            }

            if (normalizePerimeter)
            {
                NormalizePerimeterSymmetry(
                    targetBytes,
                    targetStride,
                    target.Width,
                    target.Height,
                    targetData.Stride >= 0);
            }

            Marshal.Copy(targetBytes, 0, targetData.Scan0, targetBytes.Length);
        }
        finally
        {
            source.UnlockBits(sourceData);
            target.UnlockBits(targetData);
        }

        return target;
    }

    private static void NormalizePerimeterSymmetry(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool topDown)
    {
        const int edgeBand = 4;
        var source = (byte[])pixels.Clone();

        int PixelIndex(int x, int y)
        {
            var row = topDown ? y * stride : (height - 1 - y) * stride;
            return row + x * 4;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x >= edgeBand && x < width - edgeBand &&
                    y >= edgeBand && y < height - edgeBand)
                {
                    continue;
                }

                var mirroredX = width - 1 - x;
                var mirroredY = height - 1 - y;
                var topLeft = PixelIndex(x, y);
                var topRight = PixelIndex(mirroredX, y);
                var bottomLeft = PixelIndex(x, mirroredY);
                var bottomRight = PixelIndex(mirroredX, mirroredY);

                for (var channel = 0; channel < 4; channel++)
                {
                    var average = (
                        source[topLeft + channel] +
                        source[topRight + channel] +
                        source[bottomLeft + channel] +
                        source[bottomRight + channel] + 2) / 4;
                    pixels[topLeft + channel] = (byte)average;
                    pixels[topRight + channel] = (byte)average;
                    pixels[bottomLeft + channel] = (byte)average;
                    pixels[bottomRight + channel] = (byte)average;
                }
            }
        }
    }

    private static GraphicsPath CreateCameraOutline(RectangleF bounds, float scale, float offset)
    {
        PointF Point(float x, float y) =>
            new(bounds.Left + (x + offset) * scale, bounds.Top + (y + offset) * scale);

        var path = new GraphicsPath();
        path.StartFigure();
        path.AddLine(Point(9, 10), Point(11, 10));
        path.AddBezier(Point(11, 10), Point(11.7f, 8), Point(12.8f, 7), Point(14.5f, 7));
        path.AddLine(Point(14.5f, 7), Point(17.5f, 7));
        path.AddBezier(Point(17.5f, 7), Point(19.2f, 7), Point(20.3f, 8), Point(21, 10));
        path.AddLine(Point(21, 10), Point(23, 10));
        path.AddBezier(Point(23, 10), Point(24.7f, 10), Point(26, 11.3f), Point(26, 13));
        path.AddLine(Point(26, 13), Point(26, 21));
        path.AddBezier(Point(26, 21), Point(26, 22.7f), Point(24.7f, 24), Point(23, 24));
        path.AddLine(Point(23, 24), Point(9, 24));
        path.AddBezier(Point(9, 24), Point(7.3f, 24), Point(6, 22.7f), Point(6, 21));
        path.AddLine(Point(6, 21), Point(6, 13));
        path.AddBezier(Point(6, 13), Point(6, 11.3f), Point(7.3f, 10), Point(9, 10));
        path.CloseFigure();
        return path;
    }
}
