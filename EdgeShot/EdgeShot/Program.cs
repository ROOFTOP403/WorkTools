namespace EdgeShot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var singleInstance = new Mutex(true, @"Local\EdgeShot.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            DialogPlacement.ShowMessage(
                "EdgeShot이 이미 실행 중입니다. 알림 영역의 아이콘을 확인해 주세요.",
                "EdgeShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) =>
            DialogPlacement.ShowMessage(
                e.Exception.Message,
                "EdgeShot 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

        var showSettingsOnStart = args.Any(arg =>
            string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase));
        using var context = new EdgeShotApplicationContext(showSettingsOnStart);
        Application.Run(context);
    }
}
