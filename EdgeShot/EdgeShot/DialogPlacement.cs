namespace EdgeShot;

internal static class DialogPlacement
{
    public static Screen GetPreferredScreen(Control? anchor = null)
    {
        if (anchor is not null && !anchor.Bounds.IsEmpty)
        {
            return Screen.FromRectangle(anchor.Bounds);
        }

        return Screen.AllScreens.FirstOrDefault(screen => !screen.Primary)
            ?? Screen.PrimaryScreen
            ?? Screen.AllScreens[0];
    }

    public static void CenterOnScreen(Form form, Screen screen)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = DialogPositioning.Center(screen.WorkingArea, form.Size);
    }

    public static DialogResult ShowDialog(Form form, Control? anchor = null)
    {
        var screen = GetPreferredScreen(anchor);
        CenterOnScreen(form, screen);
        form.Shown += (_, _) =>
        {
            // Per-Monitor DPI가 적용된 최종 크기를 기준으로 다시 중앙 정렬한다.
            CenterOnScreen(form, screen);
            NativeMethods.SetWindowPos(
                form.Handle,
                NativeMethods.HwndTopMost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove |
                NativeMethods.SwpNoSize |
                NativeMethods.SwpNoOwnerZOrder |
                NativeMethods.SwpShowWindow);
            NativeMethods.SetForegroundWindow(form.Handle);
        };

        return anchor is { Visible: true, IsHandleCreated: true }
            ? form.ShowDialog(anchor)
            : form.ShowDialog();
    }

    public static DialogResult ShowMessage(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        Control? anchor = null)
    {
        var screen = GetPreferredScreen(anchor);
        if (anchor is { Visible: true, IsHandleCreated: true })
        {
            return MessageBox.Show(anchor, text, caption, buttons, icon);
        }

        var center = DialogPositioning.Center(screen.WorkingArea, new Size(1, 1));
        using var owner = new Form
        {
            AutoScaleMode = AutoScaleMode.None,
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            TopMost = true,
            Opacity = 0,
            Bounds = new Rectangle(center, new Size(1, 1))
        };
        owner.Show();
        try
        {
            return MessageBox.Show(owner, text, caption, buttons, icon);
        }
        finally
        {
            owner.Close();
        }
    }
}
