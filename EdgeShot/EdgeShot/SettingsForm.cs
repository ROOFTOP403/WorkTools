namespace EdgeShot;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _saveToDesktop;
    private readonly Label _pngDirectoryLabel;
    private readonly TextBox _pngDirectory;
    private readonly Button _browsePngDirectory;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _showFloatingButton;
    private readonly RadioButton _tealAccent;
    private readonly RadioButton _coralAccent;

    public SettingsForm(AppSettings settings, bool actualAutoStartState)
    {
        Text = "EdgeShot 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 402);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(22, 20),
            Text = "EdgeShot 캡처 설정"
        };

        _saveToDesktop = new CheckBox
        {
            AutoSize = true,
            Location = new Point(24, 58),
            Text = "클립보드 복사와 함께 PNG 파일 저장",
            Checked = settings.SaveToDesktop
        };

        _pngDirectoryLabel = new Label
        {
            AutoSize = true,
            Location = new Point(24, 88),
            Text = "PNG 저장 폴더"
        };

        _pngDirectory = new TextBox
        {
            Location = new Point(24, 110),
            ReadOnly = true,
            Size = new Size(276, 27),
            Text = PngSaveLocation.Resolve(settings.PngSaveDirectory)
        };

        _browsePngDirectory = new Button
        {
            Location = new Point(308, 108),
            Size = new Size(94, 29),
            Text = "찾아보기..."
        };
        _browsePngDirectory.Click += (_, _) => BrowsePngDirectory();
        _saveToDesktop.CheckedChanged += (_, _) => UpdatePngDirectoryEnabled();

        _startWithWindows = new CheckBox
        {
            AutoSize = true,
            Location = new Point(24, 158),
            Text = "Windows에 로그인할 때 EdgeShot 자동 시작",
            Checked = actualAutoStartState
        };

        _showFloatingButton = new CheckBox
        {
            AutoSize = true,
            Location = new Point(24, 190),
            Text = "화면 가장자리에 플로팅 캡처 버튼 표시",
            Checked = settings.ShowFloatingButton
        };

        var accentLabel = new Label
        {
            AutoSize = true,
            Location = new Point(24, 226),
            Text = "플로팅 버튼 색상"
        };

        _tealAccent = new RadioButton
        {
            AutoSize = true,
            Location = new Point(24, 250),
            Text = "청록 (기본)",
            Checked = settings.FloatingButtonAccent != CaptureAccent.Coral
        };

        _coralAccent = new RadioButton
        {
            AutoSize = true,
            Location = new Point(144, 250),
            Text = "코랄",
            Checked = settings.FloatingButtonAccent == CaptureAccent.Coral
        };

        var hint = new Label
        {
            AutoSize = false,
            Location = new Point(24, 284),
            Size = new Size(370, 28),
            ForeColor = SystemColors.GrayText,
            Text = "플로팅 버튼은 드래그해서 원하는 모니터로 옮길 수 있습니다."
        };

        var creator = new Label
        {
            AutoSize = true,
            Location = new Point(24, 320),
            ForeColor = SystemColors.GrayText,
            Text = AppMetadata.CopyrightNotice
        };

        var okButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(238, 360),
            Size = new Size(78, 28),
            Text = "확인"
        };
        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(324, 360),
            Size = new Size(78, 28),
            Text = "취소"
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.AddRange([
            title,
            _saveToDesktop,
            _pngDirectoryLabel,
            _pngDirectory,
            _browsePngDirectory,
            _startWithWindows,
            _showFloatingButton,
            accentLabel,
            _tealAccent,
            _coralAccent,
            hint,
            creator,
            okButton,
            cancelButton
        ]);
        UpdatePngDirectoryEnabled();
    }

    public bool SaveToDesktop => _saveToDesktop.Checked;

    public string PngSaveDirectory => _pngDirectory.Text;

    public bool StartWithWindows => _startWithWindows.Checked;

    public bool ShowFloatingButton => _showFloatingButton.Checked;

    public CaptureAccent FloatingButtonAccent =>
        _coralAccent.Checked ? CaptureAccent.Coral : CaptureAccent.Teal;

    private void BrowsePngDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "EdgeShot PNG 저장 폴더를 선택하세요.",
            InitialDirectory = _pngDirectory.Text,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK &&
            !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _pngDirectory.Text = dialog.SelectedPath;
        }
    }

    private void UpdatePngDirectoryEnabled()
    {
        var enabled = _saveToDesktop.Checked;
        _pngDirectoryLabel.Enabled = enabled;
        _pngDirectory.Enabled = enabled;
        _browsePngDirectory.Enabled = enabled;
    }
}
