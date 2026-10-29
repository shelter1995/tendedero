namespace Liangtu;

internal static class TextChecks
{
    public static void Run(App app, Photo photo, Action<string, Action> test)
    {
        test("text uses one shared properties row and B/I toggle buttons", () => {
            var editor = new EditorWindow(app, photo);
            try {
                var root = (FrameworkElement)editor.Content;
                root.Measure(new Size(760, 460)); root.Arrange(new Rect(0, 0, 760, 460)); root.UpdateLayout();
                var before = editor.Viewport.TranslatePoint(new Point(), root).Y;
                editor.SelectTool("文字"); root.UpdateLayout();
                var after = editor.Viewport.TranslatePoint(new Point(), root).Y;
                Assert(Math.Abs(before - after) < 1, "Text adds another properties row");
                var controls = Descendants(root).ToArray();
                var toggles = controls.OfType<System.Windows.Controls.Primitives.ToggleButton>().Where(b => b is not RadioButton).ToArray();
                Assert(toggles.Any(b => b.Content?.ToString() == "B" && b is not CheckBox) && toggles.Any(b => b.Content?.ToString() == "I" && b is not CheckBox), "Expected compact B/I toggles");
            } finally { Clean(editor); }
        });
        test("text tool exposes font properties and an inline input without a dialog", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字");
                var fonts = Descendants(editor.Content as DependencyObject).OfType<ComboBox>()
                    .FirstOrDefault(c => (string?)c.ToolTip == "文字字体");
                if (fonts == null) throw new Exception("Text font properties are missing; legacy modal input is still used");
                editor.BeginStroke(new Point(80, 100));
                if (!Descendants(editor.Content as DependencyObject).OfType<TextBox>().Any(c => c.AcceptsReturn))
                    throw new Exception("No inline multiline text input");
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
        test("text remains editable across styles, moving, other drawing, undo and redo", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字");
                editor.ChangeTextStyle("Segoe UI", 32, Colors.Blue, true, false);
                editor.BeginStroke(new Point(100, 120)); Input(editor).Text = "中文标注 Hello\n第二行 with spaces"; editor.CommitText();
                var original = editor.Texts.Single();
                Assert(original.FontSize == 32 && original.Bold && original.Color == Colors.Blue, "Defaults were not applied");
                editor.ChangeTextStyle("Microsoft YaHei UI", 48, Colors.Red, false, true);
                Assert(editor.Texts.Single().FontSize == 48 && editor.Texts.Single().Italic, "Post-entry style lost");
                editor.Undo(); Assert(editor.Texts.Single() == original, "Style undo lost text");
                editor.Redo(); var styled = editor.Texts.Single();
                editor.SelectText(styled.Id); editor.MoveTextTo(new Point(210, 190));
                Assert(editor.Texts.Single().X == 210, "Text did not move");
                editor.Undo(); Assert(editor.Texts.Single() == styled, "Move undo");
                editor.Redo(); var moved = editor.Texts.Single();
                editor.SelectTool("矩形"); editor.BeginStroke(new Point(30, 30)); editor.EndStroke(new Point(75, 65));
                Assert(editor.Texts.Single() == moved, "Drawing rasterized text");
                editor.SelectTool("文字"); editor.SelectText(moved.Id); Input(editor).Text = "仍可修改"; editor.CommitText();
                Assert(editor.Texts.Single().Text == "仍可修改", "Cannot edit existing text");
                editor.BeginStroke(new Point(500, 100)); Input(editor).Text = "另一段"; editor.CommitText();
                Assert(editor.Texts.Count == 2, "Multiple text objects");
                editor.DeleteText(); Assert(editor.Texts.Count == 1, "Delete selection"); editor.Undo(); Assert(editor.Texts.Count == 2, "Undo delete");
            } finally { Clean(editor); }
        });
        test("text export excludes selection chrome and follows crop rotate and view changes", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.ChangeTextStyle("Segoe UI", 30, Colors.Black, false, false);
                editor.BeginStroke(new Point(120, 160)); Input(editor).Text = "Export 中文"; editor.CommitText();
                var before = Pixels(editor.Flatten());
                editor.Viewport.SetZoom(2.5); editor.Viewport.Pan(-120, 50);
                Assert(before.SequenceEqual(Pixels(editor.Flatten())), "View changed exported text");
                editor.SelectTool("矩形"); Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Selected frame or caret leaked into export");
                editor.SelectTool("裁剪"); editor.BeginStroke(new Point(50, 60)); editor.EndStroke(new Point(600, 400));
                Assert(editor.Texts.Single().X == 70 && editor.Texts.Single().Y == 100, "Crop coordinates");
                editor.Rotate(); var text = editor.Texts.Single();
                Assert(text.X == 240 && text.Y == 70 && text.Angle == 90, "Rotate coordinates");
                var rotated = Pixels(editor.Flatten()); editor.Undo(); editor.Redo();
                Assert(rotated.SequenceEqual(Pixels(editor.Flatten())), "Rotate history pixels");
                editor.SelectTool("文字"); editor.SelectText(text.Id);
                editor.ChangeTextStyle(text.FontFamily, 36, Colors.Green, true, false);
                Assert(editor.Texts.Single().Angle == 90, "Style reset rotation");
            } finally { Clean(editor); }
        });
        test("empty text and selection do not consume redo; invalid size is bounded", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.BeginStroke(new Point(80, 100)); Input(editor).Text = "kept"; editor.CommitText();
                editor.Undo(); editor.BeginStroke(new Point(80, 100)); editor.SelectTool("矩形"); editor.Redo();
                Assert(editor.Texts.Count == 1 && editor.Texts[0].Text == "kept", "Empty text destroyed redo");
                editor.SelectTool("文字"); editor.SelectText(editor.Texts[0].Id);
                editor.ChangeTextStyle("missing-font-name", 999, Colors.Black, false, false);
                Assert(editor.Texts[0].FontSize == 512 && editor.Texts[0].FontFamily == "Microsoft YaHei UI", "Font/size validation");
                editor.ChangeTextStyle("Segoe UI", double.NaN, Colors.Red, false, false);
                Assert(editor.Texts[0].FontSize == 512, "NaN changed document");
            } finally { Clean(editor); }
        });
        test("text editor renders standard and compact layouts", () => {
            foreach (var size in new[] { new Size(1120, 740), new Size(760, 460) }) {
                var editor = new EditorWindow(app, photo);
                try {
                    editor.SelectTool("文字"); editor.ChangeTextStyle("Microsoft YaHei UI", 36, Colors.DarkSlateBlue, true, false);
                    editor.BeginStroke(new Point(100, 270)); Input(editor).Text = "让想法落在图片上"; editor.CommitText();
                    var root = (FrameworkElement)editor.Content; editor.Content = null;
                    var wrapper = new Border { Background = editor.Background, Resources = editor.Resources, Child = root };
                    wrapper.Measure(size); wrapper.Arrange(new Rect(size)); wrapper.UpdateLayout();
                    var image = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); image.Render(wrapper);
                    MediaTools.SavePng(image, Path.Combine(Settings.Root, "ui", $"text-editor-{size.Width}.png"));
                    wrapper.Child = null; editor.Content = root;
                } finally { Clean(editor); }
            }
        });
        foreach (var profile in ThemeProfiles.All)
        test("text preview and export share exactly the same multiline layout: " + profile.Id, () => {
            var previousTheme = Theme.SelectedId; Theme.Select(profile.Id);
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.ChangeTextStyle("Microsoft YaHei UI", 28, Colors.Black, false, false);
                editor.BeginStroke(new Point(80, 20)); var input = Input(editor); input.Text = "中文标注的位置与行距必须一致\n第二行也要一致"; editor.CommitText();
                var frame = (Border)input.Parent;
                var label = editor.Texts.Single().CreateLabel();
                frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); frame.Arrange(new Rect(frame.DesiredSize));
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); label.Arrange(new Rect(label.DesiredSize));
                Assert(Math.Abs(frame.DesiredSize.Width - label.DesiredSize.Width) < .01 && Math.Abs(frame.DesiredSize.Height - label.DesiredSize.Height) < .01, $"Preview/export layout differs: {frame.DesiredSize} vs {label.DesiredSize}");
                var exportInput = (TextBox)label.Child;
                Assert(input.ActualWidth == exportInput.ActualWidth && input.ActualHeight == exportInput.ActualHeight, "Native text content extents differ");
                byte[] Render(TextBox text) {
                    var image = new RenderTargetBitmap((int)Math.Ceiling(text.ActualWidth), (int)Math.Ceiling(text.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    image.Render(text); return Pixels(image);
                }
                var exported = Render(exportInput);
                Assert(Render(input).SequenceEqual(exported), "Editable native text glyph pixels differ from exported text");
                input.IsReadOnly = true; input.UpdateLayout();
                Assert(Render(input).SequenceEqual(exported), "Read-only selection text glyph pixels differ from exported text");
                Assert(InputMethod.GetIsInputMethodEnabled(input) && input.AcceptsReturn, "Native multilingual multiline input must remain enabled");
            } finally { Clean(editor); Theme.Select(previousTheme); }
        });
        test("partially cropped text retains an accessible move handle", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.BeginStroke(new Point(80, 20)); Input(editor).Text = "第一行\n第二行\n第三行"; editor.CommitText();
                editor.SelectTool("裁剪"); editor.BeginStroke(new Point(20, 70)); editor.EndStroke(new Point(600, 400));
                editor.SelectTool("文字"); editor.SelectText(editor.Texts.Single().Id);
                var handle = Descendants(editor.Content as DependencyObject).OfType<Border>().Single(b => (string?)b.ToolTip == "拖动调整文字位置");
                Assert(Canvas.GetTop(handle) >= 0 && Canvas.GetLeft(handle) >= 0, "Move handle is clipped outside image");
                var text = editor.Texts.Single(); editor.MoveTextTo(new Point(text.X + 1, text.Y + 1));
                Assert(editor.Texts.Single().Y == text.Y + 1, "Cropped text jumps on first drag delta");
            } finally { Clean(editor); }
        });
    }
    static TextBox Input(EditorWindow editor) => Descendants(editor.Content as DependencyObject).OfType<TextBox>().Single(t => t.AcceptsReturn && !t.IsReadOnly);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static byte[] Pixels(BitmapSource bitmap) { var data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0); return data; }
    static void Clean(EditorWindow editor) { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject? node)
    {
        if (node == null) yield break;
        yield return node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
}
