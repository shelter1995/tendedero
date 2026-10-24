using System.Runtime.InteropServices;
namespace Liangtu;

internal static class UiChecks
{
    public static void Run(App app, Action<string, Action> test)
    {
        var output = Path.Combine(Settings.Root, "ui"); Directory.CreateDirectory(output);
        test("native menu retains Unicode, checked and disabled commands", () => {
            var model = new ContextMenu();
            model.Items.Add(new MenuItem { Header = "系统截图", IsCheckable = true, IsChecked = true });
            model.Items.Add(new MenuItem { Header = "不可用", IsEnabled = false });
            var commands = new Dictionary<uint, MenuItem>();
            var handle = NativeMenu.Build(model.Items, commands);
            try {
                var text = new System.Text.StringBuilder(100); GetMenuString(handle, 0, text, 100, 0x400);
                if (text.ToString() != "系统截图" || (GetMenuState(handle, 0, 0x400) & 8) == 0 || (GetMenuState(handle, 1, 0x400) & 3) == 0) throw new Exception("Native menu state lost");
            } finally { NativeMenu.DestroyMenu(handle); }
        });
        var fixture = new DrawingVisual();
        using (var dc = fixture.RenderOpen()) {
            dc.DrawRectangle(Theme.Brush("#F0F2F7"), null, new Rect(0, 0, 960, 600));
            dc.DrawRectangle(Theme.Brush("#252A34"), null, new Rect(0, 0, 960, 64));
            void Text(string value, double x, double y, double size, Brush ink) => dc.DrawText(new FormattedText(value, System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface(Theme.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, ink, 1), new Point(x, y));
            Text("山野手记", 36, 18, 20, Brushes.White);
            Text("一次短暂的出发", 50, 110, 42, Theme.Brush("#293141"));
            Text("把沿途的风景，留在手边。", 52, 178, 18, Theme.Brush("#657085"));
            dc.DrawRoundedRectangle(Theme.Brush("#D8DEE9"), null, new Rect(50, 240, 860, 300), 10, 10);
            dc.DrawEllipse(Theme.Brush("#EFF3FC"), null, new Point(750, 315), 34, 34);
            dc.DrawGeometry(Theme.Brush("#A3B2CD"), null, Geometry.Parse("M50,540 L50,460 L300,280 L540,500 L690,380 L910,510 L910,540 Z"));
            dc.DrawGeometry(Theme.Brush("#667DA5"), null, Geometry.Parse("M50,540 L310,410 L470,510 L780,390 L910,480 L910,540 Z"));
            Text("界面预览用示例图片", 52, 560, 13, Theme.Brush("#657085"));
        }
        var bitmap = new RenderTargetBitmap(960, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(fixture); bitmap.Freeze();
        var path = Path.Combine(output, "sample.png"); MediaTools.SavePng(bitmap, path);
        var photo = new Photo { Path = path, Image = bitmap };
        EditorChecks.RunUi(app, photo, test);
        test("editor and dialogs render without showing windows or injecting input", () => {
            Render(new EditorWindow(app, photo), "editor", 1120, 740);
            Render(new EditorWindow(app, photo), "editor-compact", 760, 460);
            Render(app.CreateAboutDialog(), "about", 480, 440);
            Render(app.CreateShortcutDialog(true), "shortcut", 500, 360);
        });
        void Render(Window window, string name, int width, int height) {
            var content = (FrameworkElement)window.Content;
            window.Content = null;
            var wrapper = new Border { Background = window.Background, Child = content, Resources = window.Resources, UseLayoutRounding = true, SnapsToDevicePixels = true };
            System.Windows.Documents.TextElement.SetFontFamily(wrapper, window.FontFamily);
            System.Windows.Documents.TextElement.SetFontSize(wrapper, window.FontSize);
            System.Windows.Documents.TextElement.SetForeground(wrapper, window.Foreground);
            TextOptions.SetTextFormattingMode(wrapper, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(wrapper, TextRenderingMode.ClearType);
            wrapper.Measure(new Size(width, height)); wrapper.Arrange(new Rect(0, 0, width, height)); wrapper.UpdateLayout();
            var bounds = new List<string>();
            void Inspect(DependencyObject node) {
                if (node is Button button) { var rect = button.TransformToAncestor(wrapper).TransformBounds(new Rect(button.RenderSize)); bounds.Add($"{button.ToolTip ?? button.Content}: {rect}"); }
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Inspect(VisualTreeHelper.GetChild(node, i));
            }
            Inspect(wrapper); File.WriteAllLines(Path.Combine(output, name + "-layout.txt"), bounds);
            var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); result.Render(wrapper);
            MediaTools.SavePng(result, Path.Combine(output, name + ".png")); window.Close();
        }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetMenuString(IntPtr menu, uint item, System.Text.StringBuilder value, int maximum, uint flags);
    [DllImport("user32.dll")] static extern uint GetMenuState(IntPtr menu, uint item, uint flags);
}
