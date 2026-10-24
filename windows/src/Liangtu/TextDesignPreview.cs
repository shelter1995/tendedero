namespace Liangtu;

internal static class TextDesignPreview
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Root);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var settings = new Settings { Sounds = false, Welcomed = true };
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
            typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, new Store(settings));
            var path = Path.Combine(Settings.Root, "sample.png"); File.Copy(args[1], path, true);
            var photo = new Photo { Path = path, Image = MediaTools.LoadImage(path) };
            var editor = new EditorWindow(app, photo); editor.SelectTool("文字");
            editor.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => {
                File.AppendAllText(Path.Combine(Settings.Root, "focus.log"), $"{DateTime.Now:HH:mm:ss.fff} {e.NewFocus?.GetType().Name} {(e.NewFocus as FrameworkElement)?.ToolTip}\n");
            }), true);
            editor.Closed += (_, _) => app.Shutdown(); editor.Show();
            if (args.Contains("--check-focus")) {
                await Task.Delay(200); editor.Activate(); editor.BeginStroke(new Point(100, 100)); await Task.Delay(300);
                var focused = Keyboard.FocusedElement as TextBox;
                File.WriteAllText(Path.Combine(Settings.Root, "focus-result.txt"), focused?.AcceptsReturn == true ? "PASS inline text receives keyboard focus" : $"FAIL focused {focused?.Name}, multiline={focused?.AcceptsReturn}");
                editor.Close();
            }
        };
        return app.Run();
    }
}
