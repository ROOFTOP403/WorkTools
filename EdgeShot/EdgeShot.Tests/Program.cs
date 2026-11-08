using EdgeShot;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Windows.Forms;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var runDesktopIntegration = args.Any(arg =>
    string.Equals(arg, "--desktop-integration", StringComparison.OrdinalIgnoreCase));
var createVisualPreview = args.Any(arg =>
    string.Equals(arg, "--visual-preview", StringComparison.OrdinalIgnoreCase));

var failures = new List<string>();
var checksRun = 0;

Check("QHD button is 36px", ButtonSizing.CalculateForScreenHeight(1440) == 36);
Check("FHD button is kept at a clear 28px minimum", ButtonSizing.CalculateForScreenHeight(1080) == 28);
Check("4K button is 54px", ButtonSizing.CalculateForScreenHeight(2160) == 54);
Check("invalid screen height uses QHD default", ButtonSizing.CalculateForScreenHeight(0) == 36);

var workArea = new Rectangle(-1920, 0, 1920, 1040);
var original = new Point(-980, 430);
var saved = PlacementMath.Save(workArea, new Size(24, 24), original, @"\\.\DISPLAY2");
var restored = PlacementMath.Restore(workArea, new Size(24, 24), saved);
Check("button placement round-trips across a negative-coordinate monitor", restored == original);
Check("placement remembers monitor", saved.MonitorDeviceName == @"\\.\DISPLAY2");

var normalized = Geometry.NormalizeAndClamp(new Point(900, 700), new Point(-20, 10), new Size(800, 600));
Check("selection rectangle normalizes and clamps", normalized == Rectangle.FromLTRB(0, 10, 800, 600));

var clamped = PlacementMath.Clamp(new Rectangle(0, 0, 100, 100), new Size(24, 24), new Point(95, -8));
Check("floating button stays visible after drag", clamped == new Point(76, 0));
Check(
    "desktop copy operation is accepted by Graphics.CopyFromScreen",
    Enum.IsDefined(DesktopCapture.CopyOperation));
var invalidBoundsRejected = false;
try
{
    using var invalidCapture = DesktopCapture.CaptureBounds(Rectangle.Empty);
}
catch (InvalidOperationException)
{
    invalidBoundsRejected = true;
}
Check("full-screen capture rejects an empty target", invalidBoundsRejected);

var dialogLocation = DialogPositioning.Center(
    new Rectangle(2560, -4, 2560, 1400),
    new Size(420, 230));
Check("dialog centers on a secondary monitor with an offset", dialogLocation == new Point(3630, 581));

Check("new settings default to the teal accent", new AppSettings().FloatingButtonAccent == CaptureAccent.Teal);
Check("new settings default to region capture", new AppSettings().CaptureMode == CaptureMode.Region);
Check("creator name is exact", AppMetadata.CreatorName == "RoofTopStudio403");
Check("settings copyright notice is exact", AppMetadata.CopyrightNotice == "© RoofTopStudio403");

var applicationIconPath = Path.Combine(Environment.CurrentDirectory, "EdgeShot", "EdgeShot.ico");
var applicationIconBytes = File.ReadAllBytes(applicationIconPath);
var applicationIconFrameCount = BitConverter.ToUInt16(applicationIconBytes, 4);
var applicationIconSizes = new List<int>();
var applicationIconFramesValid = applicationIconFrameCount == 9;
for (var frameIndex = 0; frameIndex < applicationIconFrameCount; frameIndex++)
{
    var entryOffset = 6 + frameIndex * 16;
    var frameSize = applicationIconBytes[entryOffset] == 0
        ? 256
        : applicationIconBytes[entryOffset];
    applicationIconSizes.Add(frameSize);
    var frameLength = BitConverter.ToUInt32(applicationIconBytes, entryOffset + 8);
    var frameOffset = BitConverter.ToUInt32(applicationIconBytes, entryOffset + 12);
    if (frameOffset + frameLength > applicationIconBytes.Length)
    {
        applicationIconFramesValid = false;
        continue;
    }

    using var frameStream = new MemoryStream(
        applicationIconBytes,
        (int)frameOffset,
        (int)frameLength,
        writable: false);
    using var frame = new Bitmap(frameStream);
    applicationIconFramesValid &=
        frame.Size == new Size(frameSize, frameSize) &&
        frame.GetPixel(0, 0).A == 0 &&
        frame.GetPixel(frameSize / 2, frameSize / 2).A <= 8 &&
        frame.GetPixel(Math.Max(1, frameSize / 8), frameSize / 2).A >= 180;
}
Check(
    "application ICO contains every requested size",
    applicationIconSizes.SequenceEqual([16, 20, 24, 32, 40, 48, 64, 128, 256]));
Check(
    "application ICO frames contain a transparent-lens teal silhouette",
    applicationIconFramesValid);

var legacySettings = JsonSerializer.Deserialize<AppSettings>("{\"SaveToDesktop\":true}")!;
Check(
    "legacy settings keep existing values and receive current defaults",
    legacySettings.SaveToDesktop &&
    legacySettings.PngSaveDirectory is null &&
    legacySettings.FloatingButtonAccent == CaptureAccent.Teal &&
    legacySettings.CaptureMode == CaptureMode.Region);
var coralSettingsJson = JsonSerializer.Serialize(new AppSettings
{
    SaveToDesktop = true,
    FloatingButtonAccent = CaptureAccent.Coral
});
var restoredCoralSettings = JsonSerializer.Deserialize<AppSettings>(coralSettingsJson)!;
Check(
    "coral accent persists across settings serialization",
    restoredCoralSettings.SaveToDesktop && restoredCoralSettings.FloatingButtonAccent == CaptureAccent.Coral);

var customPngDirectory = Path.Combine(Path.GetTempPath(), "EdgeShot Custom Captures");
var pngSettingsJson = JsonSerializer.Serialize(new AppSettings
{
    SaveToDesktop = true,
    PngSaveDirectory = customPngDirectory
});
var restoredPngSettings = JsonSerializer.Deserialize<AppSettings>(pngSettingsJson)!;
Check(
    "custom PNG directory persists across settings serialization",
    restoredPngSettings.SaveToDesktop &&
    restoredPngSettings.PngSaveDirectory == customPngDirectory);
Check(
    "blank PNG directory keeps the desktop-compatible default",
    PngSaveLocation.Resolve(null, @"C:\Users\Test\Desktop") == @"C:\Users\Test\Desktop");
Check(
    "custom PNG directory overrides the default",
    PngSaveLocation.Resolve(@"D:\Captures", @"C:\Users\Test\Desktop") == @"D:\Captures");

var settingsFormType = typeof(AppSettings).Assembly.GetType("EdgeShot.SettingsForm");
using (var settingsForm = settingsFormType is null
    ? null
    : Activator.CreateInstance(
        settingsFormType,
        [new AppSettings { PngSaveDirectory = customPngDirectory }, false]) as Form)
{
    var settingsControlTexts = settingsForm?.Controls
        .Cast<Control>()
        .Select(control => control.Text)
        .ToArray() ?? [];
    Check(
        "settings UI exposes PNG folder browsing",
        settingsControlTexts.Contains("PNG 저장 폴더") &&
        settingsControlTexts.Contains("찾아보기..."));
    Check(
        "settings UI shows the final copyright notice",
        settingsControlTexts.Contains("© RoofTopStudio403"));
}

var pngWriterDirectory = Path.Combine(
    Path.GetTempPath(),
    "EdgeShot.Tests",
    Guid.NewGuid().ToString("N"));
string? firstPng = null;
string? secondPng = null;
try
{
    using var pngSample = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
    using (var pngGraphics = Graphics.FromImage(pngSample))
    {
        pngGraphics.Clear(Color.Teal);
    }

    var fixedTime = new DateTime(2026, 11, 8, 12, 34, 56, 789);
    firstPng = PngFileWriter.Save(pngSample, pngWriterDirectory, fixedTime);
    secondPng = PngFileWriter.Save(pngSample, pngWriterDirectory, fixedTime);
    Check(
        "PNG writer saves into the selected folder and avoids collisions",
        File.Exists(firstPng) &&
        File.Exists(secondPng) &&
        Path.GetDirectoryName(firstPng) == pngWriterDirectory &&
        Path.GetFileName(secondPng).EndsWith("_1.png", StringComparison.Ordinal));
}
finally
{
    if (firstPng is not null && File.Exists(firstPng))
    {
        File.Delete(firstPng);
    }
    if (secondPng is not null && File.Exists(secondPng))
    {
        File.Delete(secondPng);
    }
    if (Directory.Exists(pngWriterDirectory))
    {
        Directory.Delete(pngWriterDirectory, recursive: false);
    }
}

var monitorModeJson = JsonSerializer.Serialize(new AppSettings
{
    CaptureMode = CaptureMode.Monitor,
    FullScreenMonitorDeviceName = @"\\.\DISPLAY2"
});
var restoredMonitorMode = JsonSerializer.Deserialize<AppSettings>(monitorModeJson)!;
Check(
    "monitor capture mode and target persist across settings serialization",
    restoredMonitorMode.CaptureMode == CaptureMode.Monitor &&
    restoredMonitorMode.FullScreenMonitorDeviceName == @"\\.\DISPLAY2");
Check(
    "region mode check state is exclusive",
    CaptureModeState.IsRegionSelected(CaptureMode.Region) &&
    !CaptureModeState.IsFullScreenSelected(CaptureMode.Region));
Check(
    "full-screen mode check state covers monitor and all-monitor targets",
    CaptureModeState.IsFullScreenSelected(CaptureMode.Monitor) &&
    CaptureModeState.IsFullScreenSelected(CaptureMode.AllMonitors));
Check(
    "monitor check state matches the selected device case-insensitively",
    CaptureModeState.IsMonitorSelected(
        CaptureMode.Monitor,
        @"\\.\display2",
        @"\\.\DISPLAY2"));

var monitorMenuText = CaptureMenuText.FormatMonitor(
    2,
    @"\\.\DISPLAY2",
    new Rectangle(2560, -4, 2560, 1440),
    isPrimary: false,
    hasFloatingButton: true);
Check(
    "monitor menu identifies target, resolution, and floating-button location",
    monitorMenuText == "모니터 2 · DISPLAY2 · 2560 × 1440 · 버튼 위치");
Check(
    "all-monitor menu states the virtual desktop dimensions",
    CaptureMenuText.FormatVirtualDesktop(new Rectangle(0, -4, 5120, 1444)) ==
    "모든 모니터 · 5120 × 1444");

foreach (var accent in Enum.GetValues<CaptureAccent>())
{
    var colors = CaptureAccentPalette.Get(accent);
    var brightBackground = Color.White;
    var darkBackground = Color.FromArgb(18, 20, 24);
    var idleBackgroundOnBright = Composite(colors.IdleBackground, brightBackground);
    var idleBackgroundOnDark = Composite(colors.IdleBackground, darkBackground);

    Check(
        $"{accent} idle icon remains readable on a bright background",
        ContrastRatio(colors.IdleIcon, idleBackgroundOnBright) >= 3.0);
    Check(
        $"{accent} idle icon remains readable on a dark background",
        ContrastRatio(colors.IdleIcon, idleBackgroundOnDark) >= 3.0);
    Check(
        $"{accent} hover state keeps white camera contrast",
        ContrastRatio(Color.White, colors.HoverBackground) >= 4.0);
    Check(
        $"{accent} solid tray camera remains readable on a light taskbar",
        ContrastRatio(colors.IdleIcon, Color.FromArgb(244, 246, 248)) >= 3.0);
    Check(
        $"{accent} solid tray camera remains readable on a dark taskbar",
        ContrastRatio(colors.IdleIcon, Color.FromArgb(22, 24, 29)) >= 3.0);

    using var smallButton = new Bitmap(28, 28);
    using var graphics = Graphics.FromImage(smallButton);
    graphics.Clear(Color.Transparent);
    CaptureButtonRenderer.Draw(graphics, new Rectangle(0, 0, 28, 28), accent, hovered: false);
    Check($"{accent} button renders at the FHD 28px size", smallButton.GetPixel(14, 14).A > 0);

    using var actualSizeIdle = new Bitmap(36, 36, PixelFormat.Format32bppArgb);
    using (var actualGraphics = Graphics.FromImage(actualSizeIdle))
    {
        actualGraphics.Clear(Color.Transparent);
        CaptureButtonRenderer.Draw(actualGraphics, new Rectangle(0, 0, 36, 36), accent, hovered: false);
    }

    var idleBackgroundAlpha = actualSizeIdle.GetPixel(18, 3).A;
    Check(
        $"{accent} idle background uses real per-pixel transparency",
        idleBackgroundAlpha is >= 40 and <= 100);
    var borderBounds = FindAccentBounds(actualSizeIdle, colors.IdleBorder);
    Check(
        $"{accent} idle state has a clear one-pixel perimeter border",
        borderBounds is { Left: <= 2, Top: <= 2, Right: >= 34, Bottom: >= 34 });
    var cameraBounds = FindAccentBounds(actualSizeIdle, colors.IdleIcon);
    Check(
        $"{accent} QHD camera outline keeps the B-layout margin",
        cameraBounds is { Left: >= 6, Top: >= 6, Right: <= 30, Bottom: <= 29 });

    using var actualSizeHover = new Bitmap(36, 36, PixelFormat.Format32bppArgb);
    using (var hoverGraphics = Graphics.FromImage(actualSizeHover))
    {
        hoverGraphics.Clear(Color.Transparent);
        CaptureButtonRenderer.Draw(hoverGraphics, new Rectangle(0, 0, 36, 36), accent, hovered: true);
    }

    Check(
        $"{accent} hover background becomes opaque",
        actualSizeHover.GetPixel(18, 3).A >= 245);
    Check(
        $"{accent} hover rounded fill is mirror-symmetric with no separate ring",
        MaximumPerimeterAlphaAsymmetry(actualSizeHover) <= 4);

    foreach (var buttonSize in new[] { 28, 36, 54 })
    {
        using var perimeterSample = new Bitmap(
            buttonSize,
            buttonSize,
            PixelFormat.Format32bppArgb);
        using (var perimeterGraphics = Graphics.FromImage(perimeterSample))
        {
            perimeterGraphics.Clear(Color.Transparent);
            CaptureButtonRenderer.Draw(
                perimeterGraphics,
                new Rectangle(0, 0, buttonSize, buttonSize),
                accent,
                hovered: false);
        }

        var sideCoverage = MeasureCardinalBorderCoverage(
            perimeterSample,
            colors.IdleBackground.A);
        var alphaAsymmetry = MaximumPerimeterAlphaAsymmetry(perimeterSample);
        Console.WriteLine(
            $"Border diagnostic {accent} {buttonSize}px: " +
            $"coverage={string.Join(",", sideCoverage.Select(value => value.ToString("F3")))}, " +
            $"maxAlphaAsymmetry={alphaAsymmetry}.");
        Check(
            $"{accent} {buttonSize}px border has even cardinal thickness",
            sideCoverage.Max() - sideCoverage.Min() <= 0.08);
        Check(
            $"{accent} {buttonSize}px rounded perimeter is mirror-symmetric",
            alphaAsymmetry <= 4);
    }
}

foreach (var accent in Enum.GetValues<CaptureAccent>())
{
    using var trayBitmap16 = IconFactory.CreateTrayBitmap(16, accent);
    Check($"{accent} tray camera icon renders at 16px", trayBitmap16.Size == new Size(16, 16));
    Check($"{accent} tray camera icon keeps transparent corners", trayBitmap16.GetPixel(0, 0).A == 0);
    Check(
        $"{accent} tray icon keeps the lens hole transparent",
        trayBitmap16.GetPixel(8, 8).A <= 8);
    Check(
        $"{accent} tray icon uses a filled camera body",
        trayBitmap16.GetPixel(2, 8).A >= 220 && CountVisiblePixels(trayBitmap16) >= 90);
    var iconColor = CaptureAccentPalette.Get(accent).IdleIcon;
    Check(
        $"{accent} tray camera silhouette is visible",
        ContainsAccentPixel(trayBitmap16, iconColor));
    var trayCameraBounds = FindAccentBounds(
        trayBitmap16,
        iconColor);
    Check(
        $"{accent} 16px tray camera uses the available area without clipping",
        trayCameraBounds is { Left: <= 1, Top: <= 2, Right: >= 15, Bottom: >= 14 });
}

if (runDesktopIntegration)
{
    RunDesktopCaptureIntegration();
}

if (createVisualPreview)
{
    CreateVisualPreview();
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} verification(s) failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine($"All {checksRun} EdgeShot component verifications passed.{(runDesktopIntegration ? " Desktop integration also completed." : string.Empty)}");
return 0;

void Check(string name, bool condition)
{
    checksRun++;
    if (!condition)
    {
        failures.Add(name);
    }
}

Color Composite(Color foreground, Color background)
{
    var opacity = foreground.A / 255d;
    return Color.FromArgb(
        (int)Math.Round(foreground.R * opacity + background.R * (1 - opacity)),
        (int)Math.Round(foreground.G * opacity + background.G * (1 - opacity)),
        (int)Math.Round(foreground.B * opacity + background.B * (1 - opacity)));
}

double ContrastRatio(Color first, Color second)
{
    var firstLuminance = RelativeLuminance(first);
    var secondLuminance = RelativeLuminance(second);
    return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
        (Math.Min(firstLuminance, secondLuminance) + 0.05);
}

double RelativeLuminance(Color color)
{
    static double Channel(byte value)
    {
        var normalized = value / 255d;
        return normalized <= 0.04045
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }

    return Channel(color.R) * 0.2126 + Channel(color.G) * 0.7152 + Channel(color.B) * 0.0722;
}

bool ContainsAccentPixel(Bitmap bitmap, Color accent)
{
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (Math.Abs(pixel.R - accent.R) <= 12 &&
                Math.Abs(pixel.G - accent.G) <= 12 &&
                Math.Abs(pixel.B - accent.B) <= 12 &&
                pixel.A > 180)
            {
                return true;
            }
        }
    }

    return false;
}

Rectangle? FindAccentBounds(Bitmap bitmap, Color accent)
{
    var left = bitmap.Width;
    var top = bitmap.Height;
    var right = -1;
    var bottom = -1;
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (Math.Abs(pixel.R - accent.R) > 12 ||
                Math.Abs(pixel.G - accent.G) > 12 ||
                Math.Abs(pixel.B - accent.B) > 12 ||
                pixel.A < 160)
            {
                continue;
            }

            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
    }

    return right < left ? null : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
}

int CountVisiblePixels(Bitmap bitmap)
{
    var count = 0;
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).A >= 32)
            {
                count++;
            }
        }
    }

    return count;
}

double[] MeasureCardinalBorderCoverage(Bitmap bitmap, byte backgroundAlpha)
{
    var centerX = bitmap.Width / 2;
    var centerY = bitmap.Height / 2;
    var sampleDepth = Math.Min(4, bitmap.Width / 2);

    double Coverage(Func<int, Color> readPixel)
    {
        var total = 0d;
        for (var depth = 0; depth < sampleDepth; depth++)
        {
            var alpha = readPixel(depth).A;
            total += Math.Clamp(
                (alpha - backgroundAlpha) / (255d - backgroundAlpha),
                0,
                1);
        }

        return total;
    }

    return
    [
        Coverage(depth => bitmap.GetPixel(centerX, depth)),
        Coverage(depth => bitmap.GetPixel(centerX, bitmap.Height - 1 - depth)),
        Coverage(depth => bitmap.GetPixel(depth, centerY)),
        Coverage(depth => bitmap.GetPixel(bitmap.Width - 1 - depth, centerY))
    ];
}

int MaximumPerimeterAlphaAsymmetry(Bitmap bitmap)
{
    var maximumDifference = 0;
    const int edgeBand = 4;
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (x >= edgeBand && x < bitmap.Width - edgeBand &&
                y >= edgeBand && y < bitmap.Height - edgeBand)
            {
                continue;
            }

            var alpha = bitmap.GetPixel(x, y).A;
            var mirroredX = bitmap.GetPixel(bitmap.Width - 1 - x, y).A;
            var mirroredY = bitmap.GetPixel(x, bitmap.Height - 1 - y).A;
            maximumDifference = Math.Max(maximumDifference, Math.Abs(alpha - mirroredX));
            maximumDifference = Math.Max(maximumDifference, Math.Abs(alpha - mirroredY));
        }
    }

    return maximumDifference;
}

void RunDesktopCaptureIntegration()
{
    var virtualScreen = SystemInformation.VirtualScreen;
    using var bitmap = DesktopCapture.CaptureVirtualDesktop(virtualScreen);
    Check(
        "desktop capture matches the Windows virtual screen",
        bitmap.Width == virtualScreen.Width && bitmap.Height == virtualScreen.Height);

    Console.WriteLine(
        $"Desktop integration: {Screen.AllScreens.Length} monitor(s), virtual={virtualScreen}, " +
        $"bitmap={bitmap.Width}x{bitmap.Height}.");

    foreach (var screen in Screen.AllScreens)
    {
        var dpiPoint = new Point(
            screen.Bounds.Left + screen.Bounds.Width / 2,
            screen.Bounds.Top + screen.Bounds.Height / 2);
        var dpi = MonitorDpiDiagnostics.ReadAt(dpiPoint);
        Check($"{screen.DeviceName} effective DPI can be queried", dpi.Success);

        using var monitorCapture = DesktopCapture.CaptureBounds(screen.Bounds);
        Check(
            $"{screen.DeviceName} full-screen capture matches that monitor",
            monitorCapture.Size == screen.Bounds.Size);

        var region = new Rectangle(
            screen.Bounds.Left - virtualScreen.Left,
            screen.Bounds.Top - virtualScreen.Top,
            screen.Bounds.Width,
            screen.Bounds.Height);
        var xStep = Math.Max(1, region.Width / 64);
        var yStep = Math.Max(1, region.Height / 48);
        var colors = new HashSet<int>();
        var minimumLuma = 255;
        var maximumLuma = 0;
        var samples = 0;

        for (var y = region.Top; y < region.Bottom; y += yStep)
        {
            for (var x = region.Left; x < region.Right; x += xStep)
            {
                var color = bitmap.GetPixel(x, y);
                colors.Add(color.ToArgb());
                var luma = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
                minimumLuma = Math.Min(minimumLuma, luma);
                maximumLuma = Math.Max(maximumLuma, luma);
                samples++;
            }
        }

        Console.WriteLine(
            $"Monitor {screen.DeviceName}: bounds={screen.Bounds}, samples={samples}, " +
            $"uniqueColors={colors.Count}, lumaRange={minimumLuma}-{maximumLuma}, " +
            $"effectiveDpi={dpi.X}x{dpi.Y}, " +
            $"button={ButtonSizing.CalculateForScreenHeight(screen.Bounds.Height)}px.");
    }
}

void CreateVisualPreview()
{
    const int cellWidth = 108;
    const int cellHeight = 92;
    using var preview = new Bitmap(cellWidth * 4, cellHeight * 4);
    using var graphics = Graphics.FromImage(preview);
    graphics.Clear(Color.White);

    var states = new[]
    {
        (CaptureAccent.Teal, false, "Teal idle"),
        (CaptureAccent.Teal, true, "Teal hover"),
        (CaptureAccent.Coral, false, "Coral idle"),
        (CaptureAccent.Coral, true, "Coral hover")
    };

    for (var row = 0; row < 4; row++)
    {
        var surface = row is 1 or 3 ? Color.FromArgb(22, 24, 29) : Color.FromArgb(244, 246, 248);
        for (var column = 0; column < states.Length; column++)
        {
            var cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
            using var surfaceBrush = new SolidBrush(surface);
            graphics.FillRectangle(surfaceBrush, cell);

            var buttonSize = row switch
            {
                2 => 36,
                3 => 28,
                _ => 48
            };
            using var button = new Bitmap(buttonSize, buttonSize, PixelFormat.Format32bppArgb);
            using (var buttonGraphics = Graphics.FromImage(button))
            {
                buttonGraphics.Clear(Color.Transparent);
                CaptureButtonRenderer.Draw(
                    buttonGraphics,
                    new Rectangle(0, 0, button.Width, button.Height),
                    states[column].Item1,
                    states[column].Item2);
            }

            var buttonLeft = cell.Left + (cell.Width - buttonSize) / 2;
            graphics.DrawImage(
                button,
                new Rectangle(buttonLeft, cell.Top + 10, buttonSize, buttonSize),
                0,
                0,
                buttonSize,
                buttonSize,
                GraphicsUnit.Pixel);

            using var font = new Font("Segoe UI", 8f);
            var textColor = row == 0 ? Color.FromArgb(35, 38, 45) : Color.White;
            var sizeLabel = row switch
            {
                2 => "QHD 36",
                3 => "FHD 28",
                _ => states[column].Item3
            };
            TextRenderer.DrawText(
                graphics,
                sizeLabel,
                font,
                new Rectangle(cell.Left, cell.Top + 63, cell.Width, 20),
                textColor,
                TextFormatFlags.HorizontalCenter);
        }
    }

    var path = Path.Combine(Environment.CurrentDirectory, "artifacts", "EdgeShot-1.1.3-button-preview.png");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    preview.Save(path, ImageFormat.Png);
    Console.WriteLine($"Visual preview: {path}");

    using var trayPreview = new Bitmap(720, 320, PixelFormat.Format32bppArgb);
    using var trayGraphics = Graphics.FromImage(trayPreview);
    trayGraphics.Clear(Color.FromArgb(244, 246, 248));
    using (var darkBrush = new SolidBrush(Color.FromArgb(22, 24, 29)))
    {
        trayGraphics.FillRectangle(darkBrush, 0, 160, trayPreview.Width, 160);
    }

    var trayStates = new[] { CaptureAccent.Teal, CaptureAccent.Coral };
    for (var row = 0; row < 2; row++)
    {
        for (var column = 0; column < trayStates.Length; column++)
        {
            using var trayIcon = IconFactory.CreateTrayBitmap(16, trayStates[column]);
            var cellLeft = column * 360;
            var cellTop = row * 160;
            var actualX = cellLeft + 34;
            var actualY = cellTop + 64;
            trayGraphics.DrawImageUnscaled(trayIcon, actualX, actualY);

            trayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            trayGraphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            trayGraphics.DrawImage(
                trayIcon,
                new Rectangle(cellLeft + 120, cellTop + 8, 144, 144),
                0,
                0,
                16,
                16,
                GraphicsUnit.Pixel);

            using var trayFont = new Font("Segoe UI", 10f);
            var labelColor = row == 0 ? Color.FromArgb(35, 38, 45) : Color.White;
            TextRenderer.DrawText(
                trayGraphics,
                $"{trayStates[column]} · actual 16px",
                trayFont,
                new Rectangle(cellLeft + 4, cellTop + 100, 108, 40),
                labelColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(
                trayGraphics,
                "9x pixel inspection",
                trayFont,
                new Rectangle(cellLeft + 112, cellTop + 132, 160, 24),
                labelColor,
                TextFormatFlags.HorizontalCenter);
        }
    }

    var trayPath = Path.Combine(
        Environment.CurrentDirectory,
        "artifacts",
        "EdgeShot-1.1.4-tray-silhouette-preview.png");
    trayPreview.Save(trayPath, ImageFormat.Png);
    Console.WriteLine($"Tray preview: {trayPath}");

    const int inspectionScale = 8;
    const int inspectionButtonSize = 36;
    const int inspectionCellSize = 320;
    using var inspection = new Bitmap(inspectionCellSize * 4, inspectionCellSize * 2);
    using var inspectionGraphics = Graphics.FromImage(inspection);
    for (var row = 0; row < 2; row++)
    {
        var surface = row == 0 ? Color.FromArgb(244, 246, 248) : Color.FromArgb(22, 24, 29);
        for (var column = 0; column < states.Length; column++)
        {
            var cell = new Rectangle(
                column * inspectionCellSize,
                row * inspectionCellSize,
                inspectionCellSize,
                inspectionCellSize);
            using var surfaceBrush = new SolidBrush(surface);
            inspectionGraphics.FillRectangle(surfaceBrush, cell);

            using var actual = new Bitmap(
                inspectionButtonSize,
                inspectionButtonSize,
                PixelFormat.Format32bppArgb);
            using (var actualGraphics = Graphics.FromImage(actual))
            {
                actualGraphics.Clear(surface);
                CaptureButtonRenderer.Draw(
                    actualGraphics,
                    new Rectangle(0, 0, actual.Width, actual.Height),
                    states[column].Item1,
                    states[column].Item2);
            }

            var expandedSize = inspectionButtonSize * inspectionScale;
            var expandedBounds = new Rectangle(
                cell.Left + (cell.Width - expandedSize) / 2,
                cell.Top + 8,
                expandedSize,
                expandedSize);
            inspectionGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            inspectionGraphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            inspectionGraphics.DrawImage(
                actual,
                expandedBounds,
                0,
                0,
                actual.Width,
                actual.Height,
                GraphicsUnit.Pixel);

            using var labelFont = new Font("Segoe UI", 10f);
            var labelColor = row == 0 ? Color.FromArgb(35, 38, 45) : Color.White;
            TextRenderer.DrawText(
                inspectionGraphics,
                $"{states[column].Item3} · QHD 36px · 8x",
                labelFont,
                new Rectangle(cell.Left, cell.Bottom - 24, cell.Width, 20),
                labelColor,
                TextFormatFlags.HorizontalCenter);
        }
    }

    var inspectionPath = Path.Combine(
        Environment.CurrentDirectory,
        "artifacts",
        "EdgeShot-1.1.3-border-8x-preview.png");
    inspection.Save(inspectionPath, ImageFormat.Png);
    Console.WriteLine($"Border inspection preview: {inspectionPath}");
}
