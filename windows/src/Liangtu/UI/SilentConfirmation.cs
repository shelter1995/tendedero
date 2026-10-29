namespace Liangtu;

// MessageBoxImage.Warning plays the Windows alert sound before the operation's
// approved cue. This dialog retains explicit confirmation without system audio.
internal sealed class SilentConfirmation : Window
{
    readonly CheckBox skip = new() { Content = "以后删除不再询问（移到回收站）", FontSize = 14, Margin = new Thickness(0, 18, 0, 0), MinHeight = 30, VerticalContentAlignment = VerticalAlignment.Center };
    internal bool DoNotAskAgain => skip.IsChecked == true;
    internal SilentConfirmation(string text, Window? owner, System.Windows.Forms.Screen? screen = null)
    {
        Title = "移到回收站"; Width = 460; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        if (owner?.IsVisible == true) Owner = owner;
        Theme.WindowStyle(this);
        // Apply after Theme.WindowStyle: the shelf owner is a transparent top
        // strip, so CenterOwner would place the prompt at the monitor's top.
        WindowStartupLocation = WindowStartupLocation.Manual;
        var display = screen ?? (owner is LineWindow line ? line.Screen : owner?.IsVisible == true
            ? System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(owner).Handle)
            : System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 23, Foreground = Theme.Ink });
        body.Children.Add(skip);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        var cancel = new Button { Content = "取消", MinWidth = 84, Padding = new Thickness(12, 7, 12, 7), IsCancel = true, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button { Content = "移到回收站", MinWidth = 112, Padding = new Thickness(12, 7, 12, 7) };
        confirm.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(confirm); body.Children.Add(actions); Content = body;
        Loaded += (_, _) => {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            void Center(double dpi) {
                var bounds = ScreenPixels.WindowBounds(display.WorkingArea, ActualWidth, ActualHeight, dpi);
                Native.SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x14);
            }
            Center(Native.Dpi(display));
            Center(Native.GetDpiForWindow(hwnd) / 96d);
            cancel.Focus();
        };
    }
    internal static DeleteDecision Decide(string text, Window? owner, System.Windows.Forms.Screen screen)
    {
        var dialog = new SilentConfirmation(text, owner, screen);
        return new(dialog.ShowDialog() == true, dialog.DoNotAskAgain);
    }
}
